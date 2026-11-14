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
        private readonly List<RoadLineData> _roads = new();
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
                if (!GameLogicManager.hasInstance || !UIManager.hasInstance ||
                    UIManager.inst.currentPanel is BattleSettlementPanel) { Clear(); return; }
                BattleLogic battle = GameLogicManager.inst.battle;
                BattlePlayerData? actor = battle?.GetCurrentPlayer();
                if (battle == null || actor == null || actor.characterType != CharacterType.Monster ||
                    actor.CharacterInst == null || actor.CharacterInst.canStep <= 0 ||
                    actor.player == null || battle.battleInfo?.UIObject == null ||
                    battle.battleInfo.UIObject.isDisposed || !battle.battleInfo.UIObject.onStage) {
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
                ShowCaption(battle.battleInfo.UIObject, initialSteps, character.canStep);
                if (origin < 0 || _planned || DateTime.UtcNow < _nextPlanAttempt) return;
                // Keep the first route set for the full monster action. Rebuilding
                // after each movement tile destroys asynchronous Addressables
                // instances before they have time to become visible.
                _origin = origin;
                if (BuildRoads(origin, character.fromLandId, initialSteps)) _planned = true;
                else _nextPlanAttempt = DateTime.UtcNow.AddSeconds(2);
            } catch (Exception ex) {
                Clear();
                if (DateTime.UtcNow >= _warnAt) {
                    _warnAt = DateTime.UtcNow.AddSeconds(30);
                    SdkLog.Warn("怪物路线显示", "怪物路线预览暂不可用：" + ex.Message);
                }
            }
        }

        internal void Clear() {
            RetireRoads();
            HideCaption();
            _battle = null;
            _character = null;
            _actor = 0;
            _origin = 0;
            _planned = false;
            _nextPlanAttempt = default;
        }

        public void Dispose() => Clear();
    }
}
