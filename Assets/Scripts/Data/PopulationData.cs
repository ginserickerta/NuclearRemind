using System;

namespace NuclearReMind
{
    /// <summary>
    /// สถานะประชากรของเมือง Veltara: จำนวนคน, ความเชื่อมั่น, สถานะ strike
    /// </summary>
    [Serializable]
    public struct PopulationData
    {
        public int total;
        public float trust;
        public bool isOnStrike;
    }
}
