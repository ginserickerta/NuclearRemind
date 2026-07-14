using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ตัวช่วยปรับ "กล่องฐาน (base collider)" ของอาคารด้วยการลาก handle ใน Scene view (edit-time เท่านั้น)
    ///   • assign BuildingData → โชว์สไปรต์อาคารจาง ๆ + custom editor วาดกล่องเขียวให้ลากปรับ
    ///   • ค่าเขียนลง BuildingData.baseColliderSize/Offset + overrideBaseCollider=true โดยตรง (กด Ctrl+S เซฟ asset)
    ///   • ไม่มีผลตอน Play (renderer ปิด) · ลบ GameObject นี้ทิ้งได้เมื่อปรับเสร็จ
    /// เปิดใช้ผ่านเมนู NuclearReMind → Tools → Base Collider Editor
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class BaseColliderTuner : MonoBehaviour
    {
        public BuildingData data;

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
