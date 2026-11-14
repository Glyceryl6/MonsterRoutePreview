#nullable enable
using System;
using System.Collections.Generic;
using CesiumLoader.SDK.Logging;
using Core;
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
            _next = DateTime.UtcNow.AddMilliseconds(60);
            try {
                if (!GameLogicManager.hasInstance ||
                    (UIManager.hasInstance && UIManager.inst.currentPanel is BattleSettlementPanel)) { Clear(); return; }
                BattleLogic battle = GameLogicManager.inst.battle;
                BattlePlayerData? actor = battle?.GetCurrentPlayer();
                if (battle == null || actor == null || actor.characterType != CharacterType.Monster ||
                    actor.CharacterInst == null || actor.CharacterInst.canStep <= 0 || actor.player == null) {
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
                int origin = character.standLand == null ? -1 : character.standLand.Id;
                int initialSteps = MonsterInitialSteps.Capture(battle, actor.player.Id,
                    character, character.canStep);
                // The first monster may begin moving before BattleInfo UI is on stage.
                // Map roads must not depend on a ready HUD or dice animation.
                UIBattleInfoPanel? panel = battle.battleInfo?.UIObject;
                if (panel != null && !panel.isDisposed && panel.onStage)
                    ShowCaption(panel, initialSteps);
                else HideCaption();

                // The first Addressables load can fail while the battle scene is
                // warming up. Retry a failed batch only while still at its origin.
                if (_planned && _pendingRoads == 0 && _readyRoads == 0 && _failedRoads > 0 &&
                    _retryCount < MonsterRoutePreviewService.MaxLoadRetries && origin == _origin) {
                    _retryCount++;
                    RetireRoads();
                    _planned = false;
                    _nextPlanAttempt = DateTime.UtcNow.AddMilliseconds(350);
                }
                if (origin < 0 || _planned || DateTime.UtcNow < _nextPlanAttempt) return;
                // Keep the first route set for the full monster action. Rebuilding
                // after each movement tile destroys asynchronous Addressables
                // instances before they have time to become visible.
                if (BuildRoads(origin, character.fromLandId, initialSteps)) {
                    _origin = origin;
                    _planned = true;
                } else {
                    // SceneUI and LandManager often become ready *after* the
                    // first actor is already active. Check again promptly.
                    _nextPlanAttempt = DateTime.UtcNow.AddMilliseconds(180);
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
            HideCaption();
            _battle = null;
            _character = null;
            _actor = 0;
            _origin = 0;
            _planned = false;
            _retryCount = 0;
            _nextPlanAttempt = default;
        }

        public void Dispose() => Clear();
    }
}
