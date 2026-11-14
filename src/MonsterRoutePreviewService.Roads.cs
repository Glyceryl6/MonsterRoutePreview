using System;
using System.Collections.Generic;
using CesiumLoader.SDK.Logging;
using Core;
using Core.Scene;
using Core.Unit;
using UnityEngine;

namespace MonsterRoutePreview {
    internal sealed partial class MonsterRoutePreviewService {
        private readonly List<StopEffectData> _stops = new List<StopEffectData>();
        // A cold scene may need to load effect 2001 for the very first monster.
        // Start one route at a time, rather than racing 24 Addressables loads.
        private readonly Queue<RoadLineData> _queuedRoads = new Queue<RoadLineData>();
        private const int MaxStopEffects = 16;
        private const int MaxLoadRetries = 6;
        private int _pendingRoads;
        private int _readyRoads;
        private int _failedRoads;
        private int _retryCount;
        private DateTime _nextPlanAttempt;

        private bool BuildRoads(int origin, int previous, int totalSteps) {
            if (!LandManager.hasInstance || BattleSceneController.inst == null ||
                BattleSceneController.inst.SceneUI == null || totalSteps <= 0) return false;

            // Read-only path generation: do not call RoadLineManager.GeneratePath,
            // as it also owns the player's selectable roads and landing controls.
            MonsterRoutePlanner.Preview plan = MonsterRoutePlanner.Build(origin, previous, totalSteps,
                (id, from) => {
                    UnitLand land = LandManager.inst.GetLandById(id);
                    return land == null ? Array.Empty<int>() : land.CanSelectedLandId(from);
                });
            if (plan.Paths.Count == 0) {
                WarnRoad("怪物当前位置没有可预览的前进道路");
                return false;
            }

            int generation = _generation;
            int started = 0;
            for (int i = 0; i < plan.Paths.Count; i++) {
                List<int> path = plan.Paths[i];
                if (path.Count < 2) continue;
                var road = new RoadLineData(path, null, Math.Max(0, totalSteps - (path.Count - 1)));
                started++;
                _queuedRoads.Enqueue(road);
            }
            if (started == 0) return false;
            StartNextRoad(generation);
            int stops = 0;
            foreach (int terminal in plan.Terminals) {
                if (stops >= MaxStopEffects) break;
                StartStop(new StopEffectData(terminal), totalSteps, generation);
                stops++;
            }
            SdkLog.Info("怪物路线显示", "怪物路线已规划：" + plan.Paths.Count +
                " 条，落点 " + stops + " 个，预览步数 " + plan.StepsShown +
                (plan.Truncated ? "（剩余步数超过预览上限）" : ""));
            return true;
        }

        private void StartNextRoad(int generation) {
            if (generation != _generation || _queuedRoads.Count == 0) return;
            RoadLineData next = _queuedRoads.Dequeue();
            _pendingRoads++;
            StartRoad(next, generation);
        }

        // Use the game's UniTask awaiter directly; no UniTaskVoid async state
        // machine is emitted by the mod and no native pointer hook is needed.
        private void StartRoad(RoadLineData road, int generation) {
            try {
                var awaiter = road.InstantiateRoad(new Color(1.73f, 0f, .027f, 1f)).GetAwaiter();
                if (awaiter.IsCompleted) {
                    CompleteRoad(road, generation, () => awaiter.GetResult());
                } else {
                    awaiter.OnCompleted(() => CompleteRoad(road, generation, () => awaiter.GetResult()));
                }
            } catch (Exception ex) {
                if (generation == _generation) {
                    _pendingRoads = Math.Max(0, _pendingRoads - 1);
                    _failedRoads++;
                }
                WarnRoad(ex.Message);
                SafeDestroy(road);
                StartNextRoad(generation);
            }
        }

        private void CompleteRoad(RoadLineData road, int generation, Func<bool> complete) {
            try {
                bool ready = complete();
                if (generation == _generation) {
                    _pendingRoads = Math.Max(0, _pendingRoads - 1);
                    if (ready) {
                        _readyRoads++;
                        _roads.Add(road);
                        StartNextRoad(generation);
                        return;
                    }
                    _failedRoads++;
                    WarnRoad("原版道路资源尚未创建（特效 2001），本回合将按需重试");
                }
            } catch (Exception ex) {
                if (generation == _generation) {
                    _pendingRoads = Math.Max(0, _pendingRoads - 1);
                    _failedRoads++;
                }
                WarnRoad(ex.Message);
            }
            // Generation guards protect late Addressables completions after
            // turning the feature off or unloading the scene.
            SafeDestroy(road);
            StartNextRoad(generation);
        }

        private void StartStop(StopEffectData stop, int totalSteps, int generation) {
            try {
                // Slot 0 is a valid effect palette even when a monster has an
                // out-of-range slot. The native stop effect has no selection
                // callback for a monster turn (the current player is not self).
                var awaiter = stop.CreateEffect(0, totalSteps).GetAwaiter();
                if (awaiter.IsCompleted) {
                    CompleteStop(stop, generation, () => awaiter.GetResult());
                } else {
                    awaiter.OnCompleted(() => CompleteStop(stop, generation, () => awaiter.GetResult()));
                }
            } catch (Exception ex) {
                WarnRoad("落点特效：" + ex.Message);
                SafeDestroy(stop);
            }
        }

        private void CompleteStop(StopEffectData stop, int generation, Action complete) {
            try {
                complete();
                if (generation == _generation) {
                    _stops.Add(stop);
                    return;
                }
            } catch (Exception ex) {
                WarnRoad("落点特效：" + ex.Message);
            }
            SafeDestroy(stop);
        }

        private void WarnRoad(string message) {
            if (DateTime.UtcNow < _warnAt) return;
            _warnAt = DateTime.UtcNow.AddSeconds(30);
            SdkLog.Warn("AstralProbe", "怪物地图路线渲染失败：" + message);
        }

        private static void SafeDestroy(RoadLineData road) {
            try { road.DestroyRoadLine(); } catch (Exception) { }
        }

        private static void SafeDestroy(StopEffectData stop) {
            try { stop.DestroyEffect(); } catch (Exception) { }
        }

        private void RetireRoads() {
            _generation++;
            for (int i = 0; i < _roads.Count; i++) SafeDestroy(_roads[i]);
            for (int i = 0; i < _stops.Count; i++) SafeDestroy(_stops[i]);
            _roads.Clear();
            _stops.Clear();
            _queuedRoads.Clear();
            _pendingRoads = 0;
            _readyRoads = 0;
            _failedRoads = 0;
        }

    }
}
