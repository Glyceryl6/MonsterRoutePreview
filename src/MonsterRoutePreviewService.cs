#nullable enable
using System;
using System.Collections.Generic;
using CesiumLoader.SDK.Logging;
using Core.Unit;
using FairyGUI;
using GameLogic;
using UI;

namespace MonsterRoutePreview {
    /// <summary>
    /// Monster turn movement snapshot. The caption uses the game's battle HUD;
    /// optional route lines use the game's own RoadLineData implementation.
    /// Original map selection controls are never modified.
    /// </summary>
    internal sealed partial class MonsterRoutePreviewService : IDisposable {
        private readonly List<Core.RoadLineData> _roads = new();
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
            _next = DateTime.UtcNow.AddMilliseconds(120);
            try {
                if (!GameLogicManager.hasInstance || !UIManager.hasInstance) { Clear(); return; }
                BattleLogic battle = GameLogicManager.inst.battle;
                BattlePlayerData? actor = battle?.GetCurrentPlayer();
                if (battle == null || actor is not { characterType: CharacterType.Monster } ||
                    actor.CharacterInst == null || actor.CharacterInst.canStep <= 0 ||
                    battle.battleInfo?.UIObject == null || battle.battleInfo.UIObject.isDisposed ||
                    battle.battleInfo.UIObject.parent == null) {
                    MonsterInitialSteps.Reset();
                    Clear();
                    return;
                }
                Character character = actor.CharacterInst;
                int origin = character.standLand == null ? 0 : character.standLand.Id;
                if (!ReferenceEquals(_battle, battle) || !ReferenceEquals(_character, character) ||
                    _actor != actor.player.Id) {
                    Clear();
                    _battle = battle;
                    _character = character;
                    _actor = actor.player.Id;
                }
                if (origin <= 0) return;
                int originalSteps = MonsterInitialSteps.Capture(battle, actor.player.Id,
                    character, character.canStep);
                ShowCaption(battle.battleInfo.UIObject, originalSteps, character.canStep);
                if (_planned && _origin == origin) return;
                // One map snapshot per movement, bounded by route count.
                RetireRoads();
                _origin = origin;
                _planned = true;
                BuildRoads(origin, character.fromLandId, originalSteps);
            } catch (Exception e) {
                Clear();
                if (DateTime.UtcNow >= _warnAt) {
                    _warnAt = DateTime.UtcNow.AddSeconds(30);
                    SdkLog.Warn("MonsterRoutePreview", "怪物路线预览暂不可用：" + e.Message);
                }
            }
        }

        internal void Clear() {
            RetireRoads();
            if (_caption != null && !_caption.isDisposed) {
                _caption.RemoveFromParent();
                _caption.Dispose();
            }
            _caption = null;
            _captionParent = null;
            _battle = null;
            _character = null;
            _actor = 0;
            _origin = 0;
            _planned = false;
        }

        public void Dispose() => Clear();
    }
}
