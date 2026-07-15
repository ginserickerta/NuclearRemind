using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// แนบกับ visual ของอาคาร (โดย BuildingVisualSpawner) — เก็บชนิด/ช่องอาคารไว้ให้แผงคลิก
    /// (CoreTowerPanelUI/LabPanelUI/MemorialPanelController) หา "ตัวอาคารใต้เมาส์" ได้
    ///
    /// ★ ใช้ ContainsWorldPoint/PickAt (กรอบสไปรต์ · เรขาคณิตล้วน) แทน Physics2D.OverlapPoint —
    ///   เพราะบน WebGL ตอนเกม pause ช่วง intro physics ไม่ step → broadphase ไม่พร้อม → OverlapPoint
    ///   คืนค่าว่าง (Editor เดินฟิสิกส์ปกติจึงไม่เจอบั๊ก) · bounds จาก sprite+transform ไม่พึ่งฟิสิกส์เลย
    /// </summary>
    public class BuildingClickTarget : MonoBehaviour
    {
        public Vector2Int originCell;   // มุมเริ่ม footprint (แผงบางตัวต้องรู้ช่องอาคาร)
        public BuildingData data;       // ข้อมูลอาคาร (ตรวจชนิดตอนคลิก)

        private SpriteRenderer _sr;

        /// <summary>จุด world อยู่ในกรอบสไปรต์อาคารนี้ไหม (world AABB จาก sprite.bounds × transform · ไม่พึ่ง Physics2D)</summary>
        public bool ContainsWorldPoint(Vector2 world)
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr == null || _sr.sprite == null) return false;

            // เดิม collider = size sprite.bounds.size, offset sprite.bounds.center บน transform เดียวกัน
            // → แปลงเป็น world เอง (รองรับกรณี renderer ถูกปิด/แยกส่วนโดย BuildingDepthSort · อาคารไม่หมุน)
            Bounds lb = _sr.sprite.bounds;
            Vector2 c = transform.TransformPoint(lb.center);
            Vector2 ext = Vector2.Scale(lb.extents, (Vector2)transform.lossyScale);
            return world.x >= c.x - ext.x && world.x <= c.x + ext.x
                && world.y >= c.y - ext.y && world.y <= c.y + ext.y;
        }

        /// <summary>หา target ที่จุด world โดน + ผ่านเงื่อนไข match (ซ้อนกัน → เลือกกรอบเล็กสุด = เจาะจงสุด)</summary>
        public static BuildingClickTarget PickAt(Vector2 world, System.Predicate<BuildingClickTarget> match)
        {
            BuildingClickTarget best = null;
            float bestArea = float.MaxValue;
            foreach (var t in FindObjectsByType<BuildingClickTarget>(FindObjectsSortMode.None))
            {
                if (t == null || t.data == null || !match(t)) continue;
                if (!t.ContainsWorldPoint(world)) continue;
                var sr = t.GetComponent<SpriteRenderer>();
                float area = sr != null && sr.sprite != null
                    ? sr.sprite.bounds.size.x * sr.sprite.bounds.size.y : 0f;
                if (area < bestArea) { best = t; bestArea = area; }
            }
            return best;
        }
    }
}
