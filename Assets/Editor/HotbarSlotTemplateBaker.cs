using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NuclearReMind;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// สร้าง "slot template" ของ hotbar (BuildingSelectionUI) เป็น object ในซีน (inactive) ที่แก้ layout ด้วยตาได้
    ///   • BuildingSelectionUI.CreateSlot จะ Instantiate template นี้แทนการสร้างสด แล้วเติมข้อมูล (icon/ชื่อ/ราคา/คีย์/lock)
    ///   • เปิด object HotbarSlotTemplate → ลาก/ปรับตำแหน่ง·ขนาด·ฟอนต์·สี ของ Icon/NameLabel/CostLabel/KeyLabel/LockLabel ได้เลย
    ///   • ลบ template ออก / เคลียร์ field slotTemplate = กลับไปสร้างสดตามฟิลด์ Inspector
    ///
    /// ⚠ วางไว้ใต้ Canvas (ไม่ใช่ buttonContainer — จะถูก Destroy ตอน Start) · inactive (runtime clone เอง)
    /// รันซ้ำ = สร้าง template ใหม่แทนของเก่า (งานที่แก้มือใน template หาย)
    /// </summary>
    public static class HotbarSlotTemplateBaker
    {
        [MenuItem("NuclearReMind/UI/Bake Hotbar Slot Template")]
        public static void Bake()
        {
            var sel = Object.FindFirstObjectByType<BuildingSelectionUI>();
            if (sel == null)
            {
                EditorUtility.DisplayDialog("Bake Hotbar Slot Template",
                    "ไม่พบ BuildingSelectionUI ในซีน — เปิด Gamescene.unity ก่อน", "OK");
                return;
            }

            // parent ใต้ Canvas (ให้เห็น/แก้ใน Scene ได้ + ไม่โดนลบตอน Start)
            var canvas = sel.buttonContainer != null
                ? sel.buttonContainer.GetComponentInParent<Canvas>()
                : sel.GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : sel.transform;

            var old = parent.Find("HotbarSlotTemplate");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var slot = sel.BuildSlotStructure(parent, "HotbarSlotTemplate");
            Undo.RegisterCreatedObjectUndo(slot, "Bake Hotbar Slot Template");

            // วางเหนือ hotbar ให้เห็นตอนเปิด active แก้ · เก็บ inactive (runtime clone)
            var rt = slot.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0f, 180f);
            slot.SetActive(false);

            Undo.RecordObject(sel, "Assign slot template");
            sel.slotTemplate = slot;
            EditorUtility.SetDirty(sel);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = slot;
            Debug.Log("[HotbarSlotTemplateBaker] ✅ สร้าง HotbarSlotTemplate + wire slotTemplate แล้ว — " +
                      "ติ๊ก active ชั่วคราวเพื่อดู/แก้ layout (Icon/NameLabel/CostLabel/KeyLabel/LockLabel) · กด Ctrl+S เซฟ · Play เห็นผล");
        }
    }
}
