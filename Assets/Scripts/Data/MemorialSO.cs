using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ข้อมูลอาคารอนุสรณ์ (Story Guide §4 — memorial_veltara): รายชื่อทีมสร้างหอคอย 6 คน
    /// ตัวอาคารมีในฐานตั้งแต่เริ่มเกม (pre-placed) คลิกเปิดแผงรายชื่อ — interaction ปกติชิ้นเดียวของระบบเนื้อเรื่อง
    /// ปมเรื่อง: ชื่อ ELARA VANE อยู่ในรายชื่อ — เกมไม่ชี้ ให้ผู้เล่นเชื่อมกับบันทึกที่กู้คืนเอง
    /// </summary>
    [CreateAssetMenu(fileName = "NewMemorial", menuName = "NuclearReMind/Story/Memorial")]
    public class MemorialSO : ScriptableObject
    {
        public string headerTH;   // เช่น "เพื่อจดจำทีมสร้างหอคอย — Veltara Core Project"

        // หนึ่งบรรทัดต่อคน เช่น "ELARA VANE — Lead Reactor Physicist — 2151–2157"
        [TextArea(1, 3)]
        public string[] names;

        // เสียงในใจตอนเปิดแผงครั้งแรก (โชว์ครั้งเดียว)
        [TextArea(2, 4)]
        public string innerVoiceOnFirstOpen;
    }
}
