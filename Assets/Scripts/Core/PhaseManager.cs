namespace NuclearReMind
{
    /// <summary>
    /// Phase (GDD §7) — bound to core%, NEVER the day (v6.3 rule #1). Phase does exactly two things:
    /// a UI signpost, and a building-unlock gate (Zone B can't be built at Phase 1). It must never be
    /// a trigger for cards/quizzes/crises — those read raw state (§19/§25).
    ///
    /// Pure static (the §7 formula is a function of core); nothing to instantiate. CurrentPhase reads
    /// the live ReactorController.
    /// </summary>
    public static class PhaseManager
    {
        /// <summary>1 survive (&lt;40) · 2 recover (≥40) · 3 nuclear (≥60) · 4 ignition (≥80).</summary>
        public static int PhaseForCore(float core) =>
            core >= 80f ? 4 :
            core >= 60f ? 3 :
            core >= 40f ? 2 : 1;

        public static int CurrentPhase =>
            ReactorController.Instance != null ? PhaseForCore(ReactorController.Instance.Core) : 1;

        public static string PhaseName(int phase)
        {
            switch (phase)
            {
                case 4: return "จุดติดเตา";
                case 3: return "นิวเคลียร์";
                case 2: return "ฟื้นฟู";
                default: return "เอาตัวรอด";
            }
        }

        /// <summary>Gate: a building tagged with unlockPhase is buildable only at ≥ that phase.</summary>
        public static bool IsPhaseUnlocked(int requiredPhase) => CurrentPhase >= requiredPhase;
    }
}
