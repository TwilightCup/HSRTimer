using System.Collections.Generic;
using HumanAPI;

namespace HSRTimer
{
    /// <summary>
    /// Checkpoint validation data (R4): the built-in skip-detection exceptions
    /// and the per-level required final checkpoint. Used by
    /// <see cref="CheckpointTagRule"/>. Tables transcribed verbatim from
    /// REQUIREMENTS.md R4.1.2 and R4.2.2/R4.2.3.
    /// </summary>
    public static class CheckpointRules
    {
        // ── R4.1.2: allowed checkpoint jumps that would otherwise look like skips ──
        // Keyed by (level source, level number, from-checkpoint) → allowed to-checkpoints.
        // BuiltIn level 9 = Dark / Halloween.
        private static readonly Dictionary<long, HashSet<int>> _skipExceptions = BuildSkipExceptions();

        // ── R4.2.2: required final checkpoint per BuiltIn level (index = level number) ──
        // 0=Mansion(Intro) 1=Train 2=Carry 3=Mountain(Climb) 4=Demolition(Break)
        // 5=Castle(Siege) 6=Water 7=Power Plant 8=Aztec 9=Dark(Halloween) 10=Steam 11=Ice
        private static readonly int[] _builtInFinalCp = { 3, 4, 3, 3, 7, 12, 10, 10, 13, 24, 11, 13 };

        // R4.2.3: EditorPick level 5 special-cased to final cp 4.
        private const int EditorPickLevel5Final = 4;

        /// <summary>
        /// R4.1.1: a checkpoint advance of more than 1 is a skip — unless the
        /// (source, level, fromCp)→toCp pair is in the exception table.
        /// </summary>
        public static bool IsSkipViolation(WorkshopItemSource source, int level, int fromCp, int toCp)
        {
            if (toCp <= fromCp + 1)
                return false; // normal sequential advance

            HashSet<int> allowed;
            if (_skipExceptions.TryGetValue(KeyOf(source, level, fromCp), out allowed))
                return !allowed.Contains(toCp);
            return true;
        }

        /// <summary>
        /// R4.2: the checkpoint the player must be on when leaving the level.
        /// Returns null when there is no fixed requirement (non-linear EditorPick
        /// levels defer to the max observed checkpoint).
        /// </summary>
        public static int? ExpectedFinalCheckpoint(WorkshopItemSource source, int level, int maxObservedCp)
        {
            switch (source)
            {
                case WorkshopItemSource.BuiltIn:
                    if (level < 0 || level >= _builtInFinalCp.Length) return null;
                    return _builtInFinalCp[level];

                case WorkshopItemSource.EditorPick:
                    // R4.2.3: level 5 special-cased; others use the highest cp in the level.
                    return level == 5 ? (int?)EditorPickLevel5Final : maxObservedCp;

                default:
                    // Workshop / unknown: no enforced final checkpoint.
                    return null;
            }
        }

        // ── key packing: source(byte) | level(int) | fromCp(int) into a long ──
        private static long KeyOf(WorkshopItemSource source, int level, int fromCp)
            => ((long)(byte)source << 48) | ((long)(uint)level << 16) | (uint)fromCp;

        private static void Add(Dictionary<long, HashSet<int>> d, WorkshopItemSource src, int level, int fromCp, params int[] tos)
        {
            var set = new HashSet<int>(tos);
            d[KeyOf(src, level, fromCp)] = set;
        }

        private static Dictionary<long, HashSet<int>> BuildSkipExceptions()
        {
            var d = new Dictionary<long, HashSet<int>>();
            var dark = WorkshopItemSource.BuiltIn;
            var ep = WorkshopItemSource.EditorPick;

            // BuiltIn level 9 (Dark / Halloween)
            Add(d, dark, 9, 6, 11);
            Add(d, dark, 9, 17, 19, 20, 21, 22, 23);
            Add(d, dark, 9, 18, 21, 22, 23, 24);
            Add(d, dark, 9, 19, 21, 22, 23, 24);
            Add(d, dark, 9, 20, 21, 22, 23, 24);
            Add(d, dark, 9, 21, 24);
            Add(d, dark, 9, 22, 24);

            // EditorPick level 9
            Add(d, ep, 9, 7, 9);
            Add(d, ep, 9, 8, 10);

            return d;
        }
    }
}
