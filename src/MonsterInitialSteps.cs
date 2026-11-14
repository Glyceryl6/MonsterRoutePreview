#nullable enable
using System;
using Core.Unit;
using GameLogic;

namespace MonsterRoutePreview {
    /// <summary>
    /// Keep the first non-zero movement budget of a monster's turn. This also
    /// covers consecutive turns of the same actor without replacing the first
    /// snapshot as canStep decreases.
    /// </summary>
    internal static class MonsterInitialSteps {
        private static BattleLogic? _battle;
        private static Character? _character;
        private static long _actorId;
        private static int _initial;

        internal static int Capture(BattleLogic battle, long actorId, Character character, int remaining) {
            if (!ReferenceEquals(_battle, battle) || _actorId != actorId ||
                !ReferenceEquals(_character, character)) {
                _battle = battle;
                _actorId = actorId;
                _character = character;
                _initial = 0;
            }
            if (_initial == 0 && remaining > 0) _initial = remaining;
            return _initial > 0 ? _initial : Math.Max(0, remaining);
        }

        internal static void Complete(BattleLogic battle, long actorId, Character? character) {
            if (!ReferenceEquals(_battle, battle) || _actorId != actorId ||
                !ReferenceEquals(_character, character)) return;
            Reset();
        }

        internal static void Reset() {
            _battle = null;
            _character = null;
            _actorId = 0;
            _initial = 0;
        }
    }
}
