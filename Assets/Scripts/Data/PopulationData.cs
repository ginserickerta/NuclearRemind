using System;

namespace NuclearReMind
{
    /// <summary>
    /// สถานะประชากร (V4 §5) — 4 คลาส Worker/Engineer/Medic/Farmer + ขวัญกำลังใจ Hope เดี่ยว (V4 §9)
    /// total = ผลรวมทุกคลาส (computed) · เพดานรวมตาม shelterCap (Shelter L1=10 … L4=80)
    /// </summary>
    [Serializable]
    public struct PopulationData
    {
        public int workers;     // ผลิต/ขุด — กำลังหลัก (เริ่ม 10)
        public int engineers;   // วิจัย/CORE/หล่อเย็น — ฝึกจาก Worker ที่ Research Lab
        public int medics;      // รักษา/กันรังสี — ฝึกจาก Worker ที่ Research Lab
        public int farmers;     // เพาะปลูก (Farm/Agri) — ฝึกจาก Worker ที่ Research Lab · default 0 = เซฟเก่าโหลดได้
        public float hope;      // เริ่ม 100 — ถึง 0 = แพ้
        public int shelterCap;  // เพดานประชากรรวม (Shelter L1–L4)

        // ป่วยจากรังสี (Story Guide §4 วิกฤตโรครังสี) — ป้ายกำกับ "จำนวนคนป่วย" เหนือประชากรเดิม
        // (ไม่นับซ้ำใน total/ไม่กระทบการบริโภค — กำลังผลิตที่หายชั่วคราวใช้ CrisisEffectManager.BusyWorkers แทน)
        // ใช้เป็นกลุ่มเสี่ยงของ patientDeathRisk + ฟื้นด้วย Medic ต่อวัน · default 0 = เซฟเก่าโหลดได้
        public int sick;

        /// <summary>จำนวนประชากรรวมทุกคลาส (คำนวณ ไม่ serialize)</summary>
        public int total => workers + engineers + medics + farmers;
    }
}
