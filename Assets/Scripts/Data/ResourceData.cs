using System;

namespace NuclearReMind
{
    /// <summary>
    /// ทรัพยากรหลักของเมือง Veltara ที่ติดตามผ่าน ResourceManager
    /// </summary>
    [Serializable]
    public struct ResourceData
    {
        public float food;
        public float water;
        public float radiationProtection;
        public float energy;
        public int workers;
        public float researchPoints;   // สะสมจาก Laboratory ทุก tick ใช้ unlock Codex entries
    }

    /// <summary>
    /// ประเภทของทรัพยากรที่ใช้อ้างอิงใน event/UI
    /// </summary>
    public enum ResourceType
    {
        Food,
        Water,
        RadiationProtection,
        Energy,
        Workers,
        ResearchPoints
    }
}
