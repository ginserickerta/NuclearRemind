namespace NuclearReMind
{
    /// <summary>
    /// Snapshot of "has the player actually USED this knowledge yet?" — the requiresApplied gate for
    /// each quiz (QUIZZES.md). Read by <see cref="QuizAvailability"/>; produced by the game loop
    /// (Sprint 5/6 wires ZoneB/Reactor into it) or set directly by tests / the F9 dev panel.
    ///
    /// ★ bug #3: <see cref="tritiumEverProduced"/> is a LATCH, never current stock. Tritium is burned
    /// to 0 every turn, so a `tritium > 0` check would pass ~1 turn in 40 and the game-deciding quiz
    /// (q_tritium_breeding) would be all but unreachable. Once Zone B produces even once, this stays
    /// true for the rest of the run.
    /// </summary>
    public struct MasteryAppliedState
    {
        public int extractorRanDays;      // q_deuterium: Extractor ran ≥ 1 day
        public int coilTypesInstalled;    // q_plasma ≥ 1 · q_magnetic_pair = 2
        public int medBayHealedCount;     // q_nuclear_medicine: healed ≥ 1 patient
        public bool riskZoneEntered;      // q_alara: sent someone into Zone B / Mine
        public bool mutationLabRan;       // q_mutation
        public bool co60Ran;              // q_food_irradiation
        public bool tritiumFed;           // q_dt_fuel: tritium fed into the core ≥ once
        public int zoneBProducedDays;     // q_tritium_breeding: Zone B produced ≥ 2 days
        public bool tritiumEverProduced;  // ★ bug #3 latch — set once Zone B produces, never cleared
        public float coreProgress;        // q_fusion ≥ 95
        public bool reachedEnding;        // q_clean_energy
    }

    /// <summary>
    /// Decides whether a quiz is answerable right now (requiresApplied — QUIZZES.md). Every v6.3 quiz
    /// carries requiresApplied = true, so this switch is the single gate for all of them. Keeping the
    /// per-quiz conditions together (rather than scattered across systems) is what keeps bug #3 from
    /// being re-introduced by a stray `tritium > 0` check somewhere else.
    /// </summary>
    public static class QuizAvailability
    {
        public static bool IsApplied(string quizId, in MasteryAppliedState s)
        {
            switch (quizId)
            {
                case QuizIds.Deuterium:       return s.extractorRanDays >= 1;
                case QuizIds.Plasma:          return s.coilTypesInstalled >= 1;
                case QuizIds.MagneticPair:    return s.coilTypesInstalled >= 2;
                case QuizIds.NuclearMedicine: return s.medBayHealedCount >= 1;
                case QuizIds.Alara:           return s.riskZoneEntered;
                case QuizIds.Mutation:        return s.mutationLabRan;
                case QuizIds.FoodIrradiation: return s.co60Ran;
                case QuizIds.DtFuel:          return s.tritiumFed;
                // ★ bug #3: latch + days produced, NEVER current tritium stock
                case QuizIds.TritiumBreeding: return s.tritiumEverProduced && s.zoneBProducedDays >= 2;
                case QuizIds.Fusion:          return s.coreProgress >= 95f;
                case QuizIds.CleanEnergy:     return s.reachedEnding;
                default:                      return false;
            }
        }
    }
}
