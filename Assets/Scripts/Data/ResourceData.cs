using System;

namespace NuclearReMind
{
    /// <summary>
    /// ทรัพยากรหลักของเมือง (V4 §4) — ติดตามผ่าน ResourceManager
    /// 6 ชนิด: Energy/Water/Food/Iron/Deuterium/Tritium + Knowledge (สะสม 0–100, V4 §9)
    /// หมายเหตุ: Hope อยู่ที่ PopulationManager / workers อยู่ที่ PopulationData.total
    /// </summary>
    [Serializable]
    public struct ResourceData
    {
        public float energy;
        public float water;
        public float food;
        public float iron;
        public float deuterium;
        public float tritium;
        public float knowledge;   // 0–100 · สะสมจากควิซ/อ่าน Codex (V4 §9) — ไม่ถูกใช้จ่าย
        public float labMat;      // v6.3: วัสดุแล็บ (+5/วัน) — วิจัย + คราฟต์ Rad Suit (30/ชุด) · default 0 = save เก่าโหลดได้
    }

    /// <summary>
    /// ประเภทของทรัพยากรที่ใช้อ้างอิงใน event/UI (V4 §16)
    /// ★ เพิ่มท้าย enum เท่านั้น — ห้าม reorder (serialized เป็น int ใน asset/save)
    /// </summary>
    public enum ResourceType
    {
        Energy,
        Water,
        Food,
        Iron,
        Deuterium,
        Tritium,
        Knowledge,
        LabMat    // v6.3 (GDD §4)
    }
}
