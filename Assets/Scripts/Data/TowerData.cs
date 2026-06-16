using System;

namespace NuclearReMind
{
    /// <summary>
    /// สถานะการก่อสร้าง CORE TOWER (3 phases)
    /// </summary>
    [Serializable]
    public struct TowerData
    {
        public int currentPhase;
        public float phaseProgress;
        public float durability;        // 0–100, default 100. Overdrive ทำให้ลดลง
        public bool isOverdriveActive;  // default false
    }
}
