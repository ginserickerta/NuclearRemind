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

        // ── ฟิลด์การ์ดบันทึก (mockup v2) — มี default กันเซฟ/asset เก่าที่ยังไม่ตั้ง ───────────
        // คำมุมขวาบนหัวการ์ด (mockup = "กู้คืนสำเร็จ") · ว่าง → UI ใช้ default "กู้คืนสำเร็จ"
        public string statusLabel = "กู้คืนสำเร็จ";
        // ชื่อผู้บันทึกล้วน ๆ (mockup "- ผู้บันทึก: {recorderName}") · ว่าง → UI fallback ไป authorLabel
        public string recorderName;

        // ── v6.3 Data Recovery (GDD §24 / STORY.md) — added, default "" keeps old assets valid ──
        // ★ Lead pre-unlocked when this record is recovered (record_01→water_analysis, ...).
        // "" = story-only record (no gameplay unlock).
        public string unlocksLead;
    }
}
