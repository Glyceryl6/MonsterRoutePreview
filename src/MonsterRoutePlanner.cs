using System;
using System.Collections.Generic;

namespace MonsterRoutePreview {
    /// <summary>
    /// Read-only equivalent of the game's forward-only branch search. A previous
    /// tile is excluded, but a tile visited earlier in the walk is NOT excluded:
    /// a legal board walk may return to it after following a loop.
    /// </summary>
    internal static class MonsterRoutePlanner {
        internal const int MaxDepth = 12;
        internal const int MaxRoutes = 24;
        internal const int MaxBranches = 6;

        internal sealed class Preview {
            internal readonly List<List<int>> Paths = new();
            internal readonly HashSet<int> Terminals = new();
            internal int StepsShown;
            internal bool Truncated;
        }

        internal static Preview Build(int origin, int enteredFrom, int totalSteps,
            Func<int, int, IReadOnlyList<int>> neighbors) {
            var result = new Preview();
            if (origin < 0 || totalSteps <= 0) return result;
            if (neighbors == null) throw new ArgumentNullException(nameof(neighbors));
            result.StepsShown = Math.Min(MaxDepth, totalSteps);
            result.Truncated = totalSteps > MaxDepth;
            var path = new List<int> { origin };
            Explore(origin, enteredFrom, result.StepsShown, totalSteps, neighbors, path, result);
            return result;
        }

        private static void Explore(int current, int previous, int remaining, int totalSteps,
            Func<int, int, IReadOnlyList<int>> neighbors, List<int> path, Preview result) {
            if (result.Paths.Count >= MaxRoutes) return;
            if (remaining == 0) {
                result.Paths.Add(new List<int>(path));
                // Only mark an actual last step. Depth-limited paths are not terminals.
                if (path.Count - 1 == totalSteps) result.Terminals.Add(current);
                return;
            }
            IReadOnlyList<int> next = neighbors(current, previous);
            int followed = 0;
            for (int i = 0; i < next.Count && followed < MaxBranches && result.Paths.Count < MaxRoutes; i++) {
                int id = next[i];
                if (id < 0 || id == current || id == previous) continue;
                followed++;
                path.Add(id);
                Explore(id, current, remaining - 1, totalSteps, neighbors, path, result);
                path.RemoveAt(path.Count - 1);
            }
            if (followed == 0 && path.Count > 1 && result.Paths.Count < MaxRoutes) {
                // A dead end can still be drawn, but it is not a full-step landing tile.
                result.Paths.Add(new List<int>(path));
            }
        }
    }
}
