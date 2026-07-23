namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: ค่าคงที่รหัสควิซทั้ง 11 ข้อ — ใช้แทน string ลอย ๆ เพื่อให้ compiler ช่วยจับพิมพ์ผิด
    ///
    /// The 11 quiz ids (GDD §21 / QUIZZES.md), as constants so the registry's bonus wiring and the
    /// availability switch reference compile-checked names instead of loose strings. Identifiers
    /// only — every numeric magnitude still comes from GameConfigSO (CLAUDE.md rule #2).
    /// Mirrors the WorkerJobs constant pattern.
    /// </summary>
    public static class QuizIds
    {
        public const string Deuterium       = "q_deuterium";
        public const string Plasma          = "q_plasma";
        public const string MagneticPair    = "q_magnetic_pair";
        public const string NuclearMedicine = "q_nuclear_medicine";
        public const string Alara           = "q_alara";
        public const string Mutation        = "q_mutation";
        public const string FoodIrradiation = "q_food_irradiation";
        public const string DtFuel          = "q_dt_fuel";
        public const string TritiumBreeding = "q_tritium_breeding";
        public const string Fusion          = "q_fusion";
        public const string CleanEnergy     = "q_clean_energy";

        /// <summary>All 11, in QUIZZES.md order — used by tests / setup for full-coverage checks.</summary>
        public static readonly string[] All =
        {
            Deuterium, Plasma, MagneticPair, NuclearMedicine, Alara, Mutation,
            FoodIrradiation, DtFuel, TritiumBreeding, Fusion, CleanEnergy,
        };
    }
}
