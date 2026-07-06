using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// การ์ดข้อมูลความรู้ (Story Guide §2) — เด้งกลางจอ "ก่อน" ควิซ/การตัดสินใจเสมอ
    /// กฎเหล็กของระบบเล่าเรื่อง: ความรู้ต้องมาก่อนควิซ — ควิซเป็นการทบทวน ไม่ใช่การเดา
    /// ลำดับบังคับ: InfoCard → EventCard → Choice(A/B/C) → Outcome → QuizCard
    /// </summary>
    [CreateAssetMenu(fileName = "NewInfoCard", menuName = "NuclearReMind/Story/Info Card")]
    public class InfoCardSO : ScriptableObject
    {
        public string title;             // เช่น "เชื้อเพลิงที่ซ่อนอยู่ในน้ำ"
        public CardCategory category;    // กำหนดสีแถบหัวการ์ด

        [TextArea(3, 10)]
        public string bodyTH;            // เนื้อหา 3-4 บรรทัด (ไทย)

        public string buttonLabel = "รับทราบ"; // เช่น "เข้าใจแล้ว" / "รับทราบ"
    }
}
