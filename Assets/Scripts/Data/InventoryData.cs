using System;
using System.Collections.Generic;

namespace NuclearReMind
{
    /// <summary>
    /// สถานะ Inventory สำหรับ save/load (GDD §13) — JsonUtility ไม่รองรับ Dictionary
    /// จึงเก็บเป็นคู่ list ขนาน (แพทเทิร์นเดียวกับ SaveData.workerAssignmentCells/Counts)
    /// ทุก field มี default = ว่าง → เซฟรุ่นเก่าที่ไม่มี inventory ยัง deserialize ได้ (คลังว่าง)
    /// </summary>
    [Serializable]
    public class InventoryData
    {
        // ไอเทมที่ถืออยู่: itemIds[i] มีจำนวน itemCounts[i]
        public List<string> itemIds = new List<string>();
        public List<int> itemCounts = new List<int>();

        // คิวคราฟต์ที่ค้างอยู่: craftQueueIds[i] คืบหน้า craftQueueProgress[i] tick
        public List<string> craftQueueIds = new List<string>();
        public List<int> craftQueueProgress = new List<int>();
    }
}
