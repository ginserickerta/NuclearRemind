using UnityEngine;
using UnityEngine.EventSystems;

namespace NuclearReMind
{
    /// <summary>
    /// ทำให้ช่องอาคารใน hotbar "ลากไปวาง" บน grid ได้ (drag & drop)
    ///   • ลากช่อง (เกิน drag threshold) → RaiseBuildingDragStarted → PlacementController เข้าโหมดวางแบบ drag (ghost ตามเมาส์)
    ///   • ปล่อยเมาส์ → RaiseBuildingDragDropped → วางถ้าตำแหน่งใช้ได้ ไม่งั้นยกเลิก
    ///   • แตะสั้น ๆ (ไม่ลาก) = Button.onClick ทำงานเหมือนเดิม (คลิกเลือกแล้วคลิกวาง) — EventSystem แยกให้อัตโนมัติ
    /// ผูก data ต่อช่องโดย BuildingSelectionUI.PopulateSlot · สื่อสารผ่าน EventManager เท่านั้น (ตามกติกาโปรเจกต์)
    /// </summary>
    public class HotbarSlotDrag : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("อาคารของช่องนี้ (เซ็ตโดย BuildingSelectionUI)")]
        public BuildingData data;

        private bool _dragging;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (data == null || EventManager.Instance == null) return;
            _dragging = true;
            EventManager.Instance.RaiseBuildingDragStarted(data);
        }

        // ต้องมี IDragHandler เพื่อให้ EventSystem ส่ง begin/end drag — ghost ตามเมาส์ทำใน PlacementController.Update เอง
        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging || EventManager.Instance == null) return;
            _dragging = false;
            EventManager.Instance.RaiseBuildingDragDropped();
        }
    }
}
