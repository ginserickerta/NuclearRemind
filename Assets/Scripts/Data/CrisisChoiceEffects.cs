using System;

namespace NuclearReMind
{
    /// <summary>
    /// ผลกระทบเพิ่มเติมของทางเลือกวิกฤต (Story Guide §4 — effects ที่เดิมเกมยังไม่รองรับ จึง "ข้ามไว้")
    /// CrisisEffectManager อ่านชุดนี้ตอน OnDilemmaResolved แล้วแปลงเป็นกลไกจริง เพื่อให้ afterText เป็นความจริง
    ///
    /// nested [Serializable] หนึ่งชุดต่อทางเลือก (choiceA/B/C_Effects ใน DilemmaData) —
    /// ค่า default 0/false = "ไม่มีผล" (asset เดิมที่ไม่ได้ author จะไม่เปลี่ยนพฤติกรรม)
    /// ★ ตั้งใจให้ทุก default เป็น 0/false เพื่อเลี่ยง Unity value-init gotcha (ไม่ใช้ sentinel ที่ไม่ใช่ 0)
    /// </summary>
    [Serializable]
    public class CrisisChoiceEffects
    {
        // ── เศรษฐกิจ (ResourceManager ดึงไปคูณผลผลิต / อัตราเน่า) ──
        public float foodYieldPct;        // Food A: +1.0 → ผลผลิตอาหาร ×2 ถาวร (บวกสะสมเข้า FoodYieldMultiplier)
        public bool stopSpoilage;         // Food B: true → หยุดเน่า (FoodSpoilRatePerDay = 0)
        public float workerEfficiencyPct; // Food C: -0.5 → งานช้าลง 50% (มัลติพลายเออร์ผลิตชั่วคราว)
        public int efficiencyDays;        // Food C: มีผลกี่วัน (tunable)

        // ── แรงงานถูกดึงชั่วคราว (รวม workersReassigned / workers+days / quarantine / highSkillWorkers / researchers) ──
        public int busyWorkers;           // จำนวนคนที่หายไปจากกำลังผลิต
        public int busyDays;              // นานกี่วันแล้วกลับมา

        // ── ความปลอดภัยเตา (ทำให้ afterText "HEAT กลับสู่ปลอดภัย" จริง + กันบั๊ก แก้พลาสมาเสร็จแล้วหลอมทันที) ──
        public float coreHeatReduction;   // Plasma A/B/C: HEAT −X ตอน resolve
        public float coreReduction;       // Plasma C: CORE% −20 (guide q:-0.2)
        public float waterReductionPct;   // Plasma C: -0.5 → น้ำ −50% ของคลัง (guide cleanWaterPct)

        // ── รังสี / สุขภาพ ──
        public float radExposureInjected; // Plasma B (radSickRisk) → +exposure สะสม (RadiationManager)
        public int sickInjected;          // Plasma B: วิศวกร 2 คนได้รับรังสีเกิน (sick += n)
        public bool setsSick;             // Outbreak A/B: กำหนดจำนวนป่วยแบบสัมบูรณ์ (แทน sentinel — default false = ไม่เปลี่ยน)
        public int sickValue;             //   A→5 (เหลือหนัก 5), B→0 (หายหมด) — ใช้เมื่อ setsSick
        public float patientDeathRisk;    // Decree B: 0.2 → คนตายจากกลุ่มเสี่ยง (deterministic)

        // ── Hope ต่อวัน (แบบเดียวกับ DecreeSO.hopePerDay) ──
        public float hopePerDay;          // Decree B: -3
        public int hopePerDayDays;        // Decree B: มีผลกี่วัน (tunable)

        // ── จลาจล ──
        public bool riotRisk;             // Food C: จลาจลถ้า Hope หลังเลือกต่ำกว่าเกณฑ์ (deterministic)
    }
}
