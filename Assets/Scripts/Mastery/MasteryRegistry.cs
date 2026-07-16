using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Mastery singleton (GDD §21/§31) — the permanent set of quizzes answered correctly, and the
    /// typed bonus each grants. Six systems query it read-only: ReactorController · WorkerManager ·
    /// MedBay · Farm · FoodStorage · ZoneB. Direct calls are allowed by the CLAUDE.md v6.3 exception
    /// (same as KnowledgeDB / GameConfigSO); mutations (Grant) still originate from one place.
    ///
    /// Plain class (mirrors KnowledgeDB) so headless tests and sims never need a scene object.
    /// Earned mastery is META-persistent — seeded from and written back to MetaProgress.MasteryBank —
    /// so a player who loses and restarts is genuinely stronger (GDD §16 Persistence).
    ///
    /// Every magnitude comes from GameConfigSO (CLAUDE.md rule #2). This class only decides, per
    /// earned quiz, WHICH config number applies; it never invents one.
    /// </summary>
    public class MasteryRegistry
    {
        private static MasteryRegistry _instance;
        public static MasteryRegistry Instance => _instance ?? (_instance = new MasteryRegistry());

        /// <summary>Fresh registry for EditMode tests / restart. Does NOT touch PlayerPrefs.</summary>
        public static void ResetForTest() => _instance = new MasteryRegistry();

        private readonly HashSet<string> _earned = new HashSet<string>();

        private MasteryRegistry()
        {
            // Seed from meta so persistent masteries apply from the first query. In EditMode this is
            // empty (MetaProgress.Load() is never called), which keeps tests hermetic.
            foreach (var id in MetaProgress.MasteryBank) _earned.Add(id);
        }

        private static GameConfigSO Cfg => GameConfigSO.Instance;

        // ─────────────────────────────────────────
        //  Earn / query
        // ─────────────────────────────────────────

        public bool Has(string quizId) => !string.IsNullOrEmpty(quizId) && _earned.Contains(quizId);
        public int EarnedCount => _earned.Count;

        /// <summary>
        /// Record a mastery as earned (CodexQuizManager calls this on a correct answer). Returns true
        /// the first time only. Persists to MetaProgress in play mode; stays in-memory in tests.
        /// </summary>
        public bool Grant(string quizId)
        {
            if (string.IsNullOrEmpty(quizId) || !_earned.Add(quizId)) return false;
            if (Application.isPlaying) MetaProgress.AddMastery(quizId); // meta-persistent (guarded like CodexManager)
            EventManager.Instance?.RaiseMasteryEarned(quizId);
            return true;
        }

        // ─────────────────────────────────────────
        //  Typed bonuses — the six systems (GDD §21)
        //  ★ answer vs no-answer differs ENTIRELY through these
        // ─────────────────────────────────────────

        // ReactorController --------------------------------------------------
        /// <summary>fuelEfficiency += 0.08 per mastery (deuterium via q_deuterium, tritium via q_dt_fuel).</summary>
        public float FuelEfficiencyBonus()
        {
            float b = 0f;
            if (Has(QuizIds.Deuterium)) b += Cfg.fuelEffBonus;
            if (Has(QuizIds.DtFuel))    b += Cfg.fuelEffBonus;
            return b;
        }
        public float CoolingBonus() => Has(QuizIds.Plasma) ? Cfg.coolingMasteryBonus : 0f;
        public float PoloidalDampMult() => Has(QuizIds.MagneticPair) ? Cfg.poloidalMasteryMult : 1f;
        /// <summary>Boost heat cost multiplier — 0.9 (−10%) once q_fusion is mastered, else 1.0.</summary>
        public float BoostHeatMult() => Has(QuizIds.Fusion) ? Cfg.boostHeatMasteryMult : 1f;

        // WorkerManager ------------------------------------------------------
        /// <summary>Radiation multiplier — 0.8 (ALARA) once q_nuclear_medicine is mastered, else 1.0.</summary>
        public float RadiationMult() => Has(QuizIds.NuclearMedicine) ? Cfg.radAlaraMult : 1f;

        // MedBay -------------------------------------------------------------
        /// <summary>Med Bay heal per day — 35 with mastery, otherwise the base 25.</summary>
        public float MedBayHeal() => Has(QuizIds.NuclearMedicine) ? Cfg.medBayHealMastery : Cfg.medBayHeal;

        // Farm ---------------------------------------------------------------
        public float FarmYieldMult() => Has(QuizIds.Mutation) ? Cfg.farmYieldMasteryMult : 1f;

        // FoodStorage --------------------------------------------------------
        public float SpoilMult() => Has(QuizIds.FoodIrradiation) ? Cfg.spoilMasteryMult : 1f;

        // ZoneB --------------------------------------------------------------
        /// <summary>
        /// ★ THE game-deciding query. Zone B tritium/day: base 3.0 (lose 100%) → mastery 8.0 (win).
        /// This single 3→8 flip is the crispest proof that "answer vs no-answer differs" (§32 test).
        /// </summary>
        public float ZoneBTritiumPerDay() =>
            Has(QuizIds.TritiumBreeding) ? Cfg.zoneBTritiumMastery : Cfg.zoneBTritiumBase;
    }
}
