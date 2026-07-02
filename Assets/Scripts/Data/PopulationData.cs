using System;

namespace NuclearReMind
{
    /// <summary>
    /// สถานะประชากร (V4 §5) — 3 คลาส Worker/Engineer/Medic + ขวัญกำลังใจ Hope เดี่ยว (V4 §9)
    /// total = ผลรวมทุกคลาส (computed) · เพดานรวมตาม shelterCap (Shelter L1=10 … L4=80)
    /// </summary>
    [Serializable]
    public struct PopulationData
    {
        public int workers;     // ผลิต/ขุด — กำลังหลัก (เริ่ม 10)
        public int engineers;   // วิจัย/CORE/หล่อเย็น — ฝึกจาก Worker ที่ Research Lab
        public int medics;      // รักษา/กันรังสี — ฝึกจาก Worker ที่ Hospital
        public float hope;      // เริ่ม 100 — ถึง 0 = แพ้
        public int shelterCap;  // เพดานประชากรรวม (Shelter L1–L4)

        /// <summary>จำนวนประชากรรวมทุกคลาส (คำนวณ ไม่ serialize)</summary>
        public int total => workers + engineers + medics;
    }
}
