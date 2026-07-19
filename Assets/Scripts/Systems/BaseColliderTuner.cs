using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ตัวช่วยปรับ "กล่อง depth-sort" ของอาคารด้วยการลาก handle ใน Scene view (edit-time เท่านั้น)
    ///   • assign BuildingData → โชว์สไปรต์อาคารจาง ๆ + custom editor วาดกล่องให้ลากปรับ
    ///   • ค่าเขียนลง BuildingData โดยตรง (กด Ctrl+S เซฟ asset)
    ///   • ไม่มีผลตอน Play (renderer ปิด) · ลบ GameObject นี้ทิ้งได้เมื่อปรับเสร็จ
    /// เปิดใช้ผ่านเมนู NuclearReMind → Tools → Base Collider Editor
    ///
    /// ★ แก้ได้ 3 กล่อง สลับด้วยช่อง Box — คนละหน้าที่กันคนละเรื่อง:
    ///   BuildingBase = อาคารทับอาคาร (ของเดิม จูนไว้แล้ว อย่าไปแตะถ้าไม่จำเป็น)
    ///   WorkerBase   = แถบพื้นเตี้ย ๆ · เท้า worker ต่ำกว่ากึ่งกลางแถบนี้ = ยืนหน้าอาคาร → วาดทับอาคาร
    ///   WorkerTop    = โซน "ยืนตรงนี้ = อยู่หลังอาคาร" → ไม่ยกขึ้นมาไม่ว่าเท้าจะต่ำแค่ไหน
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class BaseColliderTuner : MonoBehaviour
    {
        public enum TunerBox { BuildingBase, WorkerBase, WorkerTop }

        public BuildingData data;

        [Tooltip("กล่องที่กำลังแก้อยู่ — กล่องอื่นจะวาดเป็นเส้นจาง ๆ ให้เห็นความสัมพันธ์")]
        public TunerBox box = TunerBox.WorkerBase;

        private void OnEnable() => Sync();
        private void Update() { if (!Application.isPlaying) Sync(); }

        private void Sync()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) return;
            sr.enabled = !Application.isPlaying;               // ให้เห็นเฉพาะตอน edit
            if (data != null && data.sprite != null) sr.sprite = data.sprite;
            sr.color = new Color(1f, 1f, 1f, 0.55f);           // จางให้เห็นกล่องชัด
            sr.sortingOrder = 32000;                            // เหนือของอื่นในซีน
        }
    }
}
