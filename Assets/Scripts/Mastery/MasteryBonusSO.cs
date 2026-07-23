using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: ระบุว่าโบนัส Mastery ตัวนี้ไหลเข้าระบบไหน (เตา/คนงาน/หมอ/ฟาร์ม/คลังอาหาร/Zone B)
    /// ใช้ enum แทน string เพื่อกันพิมพ์ผิดแล้วโบนัสหายเงียบ ๆ
    ///
    /// Which live system a Mastery bonus flows into. MasteryRegistry exposes one typed query per
    /// target; the six systems named in GDD §21/§31 read those (ReactorController · WorkerManager ·
    /// MedBay · Farm · FoodStorage · ZoneB). An enum (not a loose string) so a typo can't silently
    /// wire a bonus to nothing.
    /// </summary>
    public enum MasteryTarget
    {
        None = 0,
        FuelEfficiency,   // ReactorController — fuel = min(1, fuel/6); +0.08 per mastery
        Cooling,          // ReactorController — cooling + 5
        PoloidalDamp,     // ReactorController — poloidalDamp × 1.15
        BoostHeat,        // ReactorController — boost heat cost − 10%
        RadiationMult,    // WorkerManager — radiation × 0.8 (ALARA)
        MedBayHeal,       // MedBay — heal 25 → 35
        FarmYield,        // Farm — yield + 15%
        SpoilRate,        // FoodStorage — spoil − 50%
        ZoneBTritium,     // ZoneB — tritium 3.0 → 8.0 / day (the game-deciding one)
    }

    /// <summary>
    /// [TH] หน้าที่: asset ข้อมูลโบนัสถาวรที่ได้จากตอบควิซ Codex ถูก 1 ข้อ
    /// เก็บเฉพาะ "ตัวตน + ข้อความโชว์ผู้เล่น" — ตัวเลขที่มีผลจริงอยู่ใน GameConfigSO ที่เดียว (กติกาข้อ 2)
    ///
    /// Mastery Bonus (GDD §16/§21) — the permanent reward for answering a Codex quiz correctly.
    /// One asset per quiz that grants a mechanical effect; q_alara / q_clean_energy carry
    /// targetSystem = None (Codex-unlock / ending-stat only).
    ///
    /// This asset holds identity + display metadata ONLY. The applied magnitude lives in
    /// GameConfigSO (CLAUDE.md rule #2 — every gameplay number has ONE home, docs/CONFIG.md), and
    /// MasteryRegistry reads it there. `value` here is the number SHOWN to the player in the Codex;
    /// nothing mechanical keys on it, so the asset and the config can never silently disagree.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMasteryBonus", menuName = "NRM/Mastery Bonus")]
    public class MasteryBonusSO : ScriptableObject
    {
        public string bonusId;                 // == the quizId that grants it (q_deuterium, ...)
        public MasteryTarget targetSystem;
        public float value;                    // display-only magnitude (mechanical value = GameConfigSO)
        [TextArea(1, 2)] public string label;  // e.g. "fuelEfficiency +0.08" — shown in Codex / quiz UI
    }
}
