using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// แนบกับ visual ของอาคาร (โดย BuildingVisualSpawner) คู่กับ Collider2D ที่ครอบรูปสไปรต์จริง
    /// ให้แผงต่าง ๆ (CoreTowerPanelUI/LabPanelUI/MemorialPanelController) ใช้ Physics2D.OverlapPoint
    /// raycast โดน "ตัวอาคาร" ได้ตรง ๆ → คลิกตัวอาคารสูง ๆ ในมุม iso ก็เปิดแผงได้ ไม่ต้องเล็งช่องฐาน footprint
    /// (เดิมเช็ก grid cell ใต้เมาส์ → สไปรต์สูงแมพไปช่องว่างด้านบน footprint → คลิกไม่โดน)
    /// </summary>
    public class BuildingClickTarget : MonoBehaviour
    {
        public Vector2Int originCell;   // มุมเริ่ม footprint (แผงบางตัวต้องรู้ช่องอาคาร)
        public BuildingData data;       // ข้อมูลอาคาร (ตรวจชนิดตอนคลิก)
    }
}
