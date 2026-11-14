#nullable enable
using System;
using System.Collections.Generic;
using CesiumLoader.SDK.Logging;
using Core;
using Core.Scene;
using Core.Unit;
using FairyGUI;
using GameLogic;
using UI;

namespace MonsterRoutePreview {
    /// <summary>
    /// A read-only monster-turn route snapshot rendered with the game's own
    /// road and terminal effects. No player input or shared route manager state
    /// is changed.
    /// </summary>
    internal sealed partial class MonsterRoutePreviewService : IDisposable {
        private readonly List<RoadLineData> _roads = new List<RoadLineData>();
        private BattleLogic? _battle;
        private Character? _character;
        private long _actor;
        private int _origin;
        private int _generation;
        private GTextField? _caption;
        private UIBattleInfoPanel? _captionParent;
        private DateTime _next;
        private DateTime _warnAt;
        private bool _planned;

        internal void Tick() {
            if (DateTime.UtcNow < _next) return;
            // Check quickly while the first actor and map resources are becoming ready.
            _next = DateTime.UtcNow.AddMilliseconds(_planned ? 65 : 20);
            try {
                if (!GameLogicManager.hasInstance || !SceneManager.hasInstance ||
                    SceneManager.inst.currentType.Value != SceneType.Battle ||
                    (UIManager.hasInstance &&
                     (UIManager.inst.currentPanel is BattleSettlementPanel ||
                      UIManager.inst.Fight.isShowing))) { Clear(); return; }
                BattleLogic battle = GameLogicManager.inst.battle;
                if (battle != null) WarmCaptionAnchor(battle.battleInfo);
                BattlePlayerData? actor = battle?.GetCurrentPlayer();
                if (battle == null || actor is not { characterType: CharacterType.Monster } ||
                    actor.CharacterInst == null || actor.player == null) {
                    Clear();
                    return;
                }
                Character character = actor.CharacterInst;
                if (!ReferenceEquals(_battle, battle) || !ReferenceEquals(_character, character) ||
                    _actor != actor.player.Id) {
                    Clear();
                    _battle = battle;
                    _character = character;
                    _actor = actor.player.Id;
                }
                // The first monster frequently has no movement points during
                // its dice animation. Do not destroy an already-started async
                // road just because canStep reaches zero while it is moving.
                int remaining = Math.Max(0, character.canStep);
                int initialSteps = MonsterInitialSteps.Capture(battle, actor.player.Id,
                    character, remaining);
                if (initialSteps <= 0) { HideCaption(); return; }
                int origin = character.standLand == null ? -1 : character.standLand.Id;
                // The first monster may begin moving before BattleInfo UI is on stage.
                // Map roads must not depend on a ready HUD or dice animation.
                UIBattleInfoPanel? panel = battle.battleInfo?.UIObject;
                if (panel is { isDisposed: false, onStage: true })
                    ShowCaption(panel, initialSteps);
                else HideCaption();

                // The first Addressables load can fail while the battle scene is
                // warming up. Retry a failed batch only while still at its origin.
                if (_planned && _readyRoads == 0 && _retryCount < MaxLoadRetries &&
                    ((_pendingRoads == 0 && _failedRoads > 0) ||
                     (_pendingRoads > 0 && remaining > 0 && DateTime.UtcNow - _roadStartedAt >
                      TimeSpan.FromSeconds(4)))) {
                    _retryCount++;
                    RetireRoads();
                    _planned = false;
                    _nextPlanAttempt = DateTime.UtcNow.AddMilliseconds(350);
                }
                if (origin < 0 || _planned || DateTime.UtcNow < _nextPlanAttempt) return;
                // Prefer the first full movement budget, but a cold first-load
                // retry after the monster has already stepped must start from
                // its CURRENT tile and remaining movement points.
                // The initial budget must survive a cold Addressables load.
                // When the monster has already advanced, use the remaining steps
                // and current tile rather than drawing the old starting path.
                int stepsToPreview = _retryCount > 0 ? remaining : initialSteps;
                if (stepsToPreview <= 0) return;
                if (BuildRoads(origin, character.fromLandId, stepsToPreview)) {
                    _origin = origin;
                    _planned = true;
                } else {
                    // SceneUI and LandManager often become ready *after* the
                    // first actor is already active. Check again promptly.
                    _nextPlanAttempt = DateTime.UtcNow.AddMilliseconds(120);
                }
            } catch (Exception ex) {
                Clear();
                if (DateTime.UtcNow >= _warnAt) {
                    _warnAt = DateTime.UtcNow.AddSeconds(30);
                    SdkLog.Warn("怪物路线显示", "怪物路线预览暂不可用：" + ex.Message);
                }
            }
        }

        internal void Clear() {
            if (_battle != null && _character != null && _actor != 0)
                MonsterInitialSteps.Complete(_battle, _actor, _character);
            RetireRoads();
            ResetCaptionAnchor();
            _battle = null;
            _character = null;
            _actor = 0;
            _origin = 0;
            _planned = false;
            _retryCount = 0;
            _nextPlanAttempt = default;
            _roadStartedAt = default;
        }

        public void Dispose() => Clear();
    }
}
