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
    }
}
