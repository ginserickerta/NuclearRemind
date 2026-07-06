using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// การ์ดบันทึกกู้คืนของ Dr. Elara Vane (Story Guide §2 — ระบบ "กู้คืนบันทึก" Data Recovery)
    /// เด้งกลางจอเองเมื่อถึงหมุดหมาย (ไม่ต้องคลิกตามหาในอาคาร) แล้วเก็บเข้าแผง Records ให้ย้อนอ่าน
    /// </summary>
    [CreateAssetMenu(fileName = "NewRecordCard", menuName = "NuclearReMind/Story/Record Card")]
    public class RecordCardSO : ScriptableObject
    {
        public string recordId;      // "elara_01" ...

        // เช่น "ระบบกู้คืนข้อมูลจากเครือข่ายเก่า · ผู้บันทึก: Dr. Elara Vane"
        public string authorLabel;

        [TextArea(3, 10)]
        public string bodyTH;        // เนื้อความบันทึก (ไทย)

        public string buttonLabel = "รับทราบ · เก็บเข้าแผง Records";

        // ชื่อที่โชว์ในแผง Records เช่น "บันทึก #01 — เชื้อเพลิงในน้ำ"
        public string archiveTitle;
    }
}
