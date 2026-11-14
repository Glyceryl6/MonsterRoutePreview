using System;
using System.Collections.Generic;
using System.Linq;
using CesiumLoader.SDK.Logging;
using Core;
using Core.Scene;
using Core.Unit;
using UnityEngine;

namespace MonsterRoutePreview {
    internal sealed partial class MonsterRoutePreviewService {
        private readonly List<StopEffectData> _stops = new();
        private const int MaxStopEffects = 16;
        private DateTime _nextPlanAttempt;

        private bool BuildRoads(int origin, int previous, int totalSteps) {
            if (!LandManager.hasInstance || BattleSceneController.inst.SceneUI == null || totalSteps <= 0) return false;
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
            foreach (var road in from path in plan.Paths where path.Count >= 2 select new RoadLineData(path, null, Math.Max(0, totalSteps - (path.Count - 1)))) {
                StartRoad(road, generation);
            }
            int stops = 0;
            foreach (int terminal in plan.Terminals) {
                if (stops >= MaxStopEffects) break;
                StartStop(new StopEffectData(terminal), totalSteps, generation);
                stops++;
            }
            SdkLog.Info("MonsterRoutePreview", "怪物路线已规划：" + plan.Paths.Count +
                " 条，落点 " + stops + " 个，预览步数 " + plan.StepsShown +
                (plan.Truncated ? "（剩余步数超过预览上限）" : ""));
            return true;
        }

        // Use the game's UniTask awaiter directly; no UniTaskVoid async state
        // machine is emitted by the mod and no native pointer hook is needed.
        private void StartRoad(RoadLineData road, int generation) {
            try {
                var awaiter = road.InstantiateRoad(new Color(1f, .12f, .08f, 1f)).GetAwaiter();
                if (awaiter.IsCompleted) {
                    CompleteRoad(road, generation, awaiter.GetResult);
                } else {
                    awaiter.OnCompleted(() => CompleteRoad(road, generation, () => awaiter.GetResult()));
                }
            } catch (Exception ex) {
                WarnRoad(ex.Message);
                SafeDestroy(road);
            }
        }

        private void CompleteRoad(RoadLineData road, int generation, Func<bool> complete) {
            try {
                bool ready = complete();
                switch (ready) {
                    case true when generation == _generation && Enabled && ShowRouteLines:
                        _roads.Add(road);
                        return;
                    case false when generation == _generation:
                        WarnRoad("原版道路资源未能创建，请检查特效资源 2001");
                        break;
                }
            } catch (Exception ex) {
                WarnRoad(ex.Message);
            }
            // Generation guards protect late Addressables completions after
            // turning the feature off or unloading the scene.
            SafeDestroy(road);
        }

        private void StartStop(StopEffectData stop, int totalSteps, int generation) {
            try {
                // Slot 0 is a valid effect palette even when a monster has an
                // out-of-range slot. The native stop effect has no selection
                // callback for a monster turn (the current player is not self).
                var awaiter = stop.CreateEffect(0, totalSteps).GetAwaiter();
                if (awaiter.IsCompleted) {
                    CompleteStop(stop, generation, awaiter.GetResult);
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
                if (generation == _generation && Enabled && ShowRouteLines) {
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
            SdkLog.Warn("MonsterRoutePreview", "怪物地图路线渲染失败：" + message);
        }

        private static void SafeDestroy(RoadLineData road) {
            try { road.DestroyRoadLine(); } catch (Exception) {
                // ignored
            }
        }

        private static void SafeDestroy(StopEffectData stop) {
            try { stop.DestroyEffect(); } catch (Exception) {
                // ignored
            }
        }

        private void RetireRoads() {
            _generation++;
            foreach (var t in _roads)
                SafeDestroy(t);

            foreach (var t in _stops)
                SafeDestroy(t);

            _roads.Clear();
            _stops.Clear();
        }

        internal void HideRoutes() {
            RetireRoads();
            _planned = false;
            _origin = 0;
        }
    }
}
