using System;

namespace NuclearReMind
{
    /// <summary>
    /// สถานะ CORE TOWER (GDD v2.1 §07★) — วัดด้วย CORE% + ระบบความร้อน HEAT
    /// 3 เฟส: 1 Cold Assembly (30–50%) / 2 Plasma Ramp (50–80%) / 3 Ignition (80–100%)
    /// </summary>
    [Serializable]
    public struct TowerData
    {
        public float corePercent;   // 0–100 (CORE%) — 30 เมื่อปลดล็อก, 100 = ชนะ
        public float coreHeat;      // ความร้อนสะสม — meltdown เมื่อ >= heatCap
        public int currentPhase;    // 0=ล็อก, 1=Cold, 2=Plasma, 3=Ignition (ชื่อเดิม — PowerGridManager อ่าน)
        public int overclockMode;   // 0=Idle, 1=Normal, 2=Boost, 3=Overdrive
        public float heatCap;       // เกณฑ์ meltdown (เริ่ม 100, ลดลงจาก RedZone micro-damage)
        public bool isUnlocked;     // CORE เริ่มทำงานแล้วหรือยัง (Day 11+)
        public int scramCooldown;   // เทิร์นที่เหลือก่อนกด SCRAM ได้อีก (0 = พร้อม)
    }
}
