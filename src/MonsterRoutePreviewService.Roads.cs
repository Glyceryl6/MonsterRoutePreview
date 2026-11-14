using System;
using System.Collections.Generic;
using System.Linq;
using CesiumLoader.SDK.Logging;
using Core;
using Core.Unit;
using UnityEngine;

namespace MonsterRoutePreview {
    internal sealed partial class MonsterRoutePreviewService {
        private const int MaxRoutes = 24;
        private const int MaxDepth = 10;

        private void BuildRoads(int origin, int previous, int moves) {
            if (!LandManager.hasInstance || moves <= 0) return;
            int depth = Math.Min(MaxDepth, moves);
            var path = new List<int> { origin };
            var visited = new HashSet<int> { origin };
            var result = new List<List<int>>();
            BuildReachable(origin, previous, depth, path, visited, result);
            int generation = _generation;
            foreach (RoadLineData owner in result.Select(t => new RoadLineData(t, null, depth))) {
                // Async owners remain alive in their continuation until the
                // original Addressables creation finishes (or errors out).
                StartRoad(owner, generation);
            }
        }

        private static void BuildReachable(int at, int previous, int steps,
            List<int> current, HashSet<int> visited, List<List<int>> result) {
            if (result.Count >= MaxRoutes) return;
            if (steps <= 0) {
                if (current.Count > 1) result.Add(new List<int>(current));
                return;
            }
            UnitLand land = LandManager.inst.GetLandById(at);
            if (land == null) return;
            List<int> neighbors = land.CanSelectedLandId(previous);
            bool hasNext = false;
            for (int i = 0, count = Math.Min(16, neighbors.Count); i < count; i++) {
                int next = neighbors[i];
                if (next <= 0 || !visited.Add(next)) continue;
                hasNext = true;
                current.Add(next);
                BuildReachable(next, at, steps - 1, current, visited, result);
                current.RemoveAt(current.Count - 1);
                visited.Remove(next);
                if (result.Count >= MaxRoutes) break;
            }
            if (!hasNext && current.Count > 1 && result.Count < MaxRoutes)
                result.Add(new List<int>(current));
        }

        // Do not emit an async UniTaskVoid state machine from the mod: some game
        // reference sets expose a UniTask awaiter but omit its async builder.
        // The game owns its Addressables async operation; only observe its result.
        private void StartRoad(RoadLineData road, int generation) {
            try {
                var awaiter = road.InstantiateRoad(new Color(1f, .12f, .08f, 1f)).GetAwaiter();
                if (awaiter.IsCompleted) {
                    CompleteRoad(road, generation, awaiter.GetResult);
                } else {
                    awaiter.OnCompleted(() => CompleteRoad(road, generation, awaiter.GetResult));
                }
            } catch (Exception ex) {
                WarnRoad(ex);
                SafeDestroy(road);
            }
        }

        private void CompleteRoad(RoadLineData road, int generation, Func<bool> result) {
            try {
                bool ready = result();
                if (ready && generation == _generation) {
                    _roads.Add(road);
                    return;
                }
            } catch (Exception ex) {
                WarnRoad(ex);
            }
            // A stale load must never leak its RoadLine after the scene changes.
            SafeDestroy(road);
        }

        private void WarnRoad(Exception ex) {
            if (DateTime.UtcNow < _warnAt) return;
            _warnAt = DateTime.UtcNow.AddSeconds(30);
            SdkLog.Warn("MonsterRoutePreview", "怪物地图路线加载失败：" + ex.Message);
        }

        private static void SafeDestroy(RoadLineData road) {
            try { road.DestroyRoadLine(); } catch (Exception) {
                // ignored
            }
        }

        private void RetireRoads() {
            _generation++;
            foreach (RoadLineData t in _roads) {
                try { t.DestroyRoadLine(); } catch (Exception) {
                    // ignored
                }
            }
            _roads.Clear();
        }
    }
}
