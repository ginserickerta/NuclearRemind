using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using NuclearReMind;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// สร้าง "demolish template" ของปุ่มทุบอาคาร (BuildingSelectionUI) เป็น object ในซีน (inactive) ที่แก้ด้วยตาได้
    ///   • BuildingSelectionUI.BuildDemolishButton จะ Instantiate template นี้แทนการสร้างสด แล้ว wire ปุ่ม/ไอคอน
    ///   • เปิด object DemolishButtonTemplate → ลาก sprite ลง Icon / ปรับ layout·สี·ฟอนต์ ของ Icon/IconEmoji/Label/SubLabel ได้เลย
    ///   • ลบ template ออก / เคลียร์ field demolishTemplate = กลับไปสร้างสด (ใช้ hammerIcon ถ้ามี ไม่งั้น emoji 🔨)
    ///
    /// ⚠ วางไว้ใต้ Canvas (ไม่ใช่ buttonContainer — จะถูก Destroy ตอน Start) · inactive (runtime clone เอง)
    /// รันซ้ำ = สร้าง template ใหม่แทนของเก่า (งานที่แก้มือใน template หาย)
    /// </summary>
    public static class DemolishButtonTemplateBaker
    {
        [MenuItem("NuclearReMind/UI/Bake Demolish Button Template")]
        public static void Bake()
        {
            var sel = Object.FindFirstObjectByType<BuildingSelectionUI>();
            if (sel == null)
            {
                EditorUtility.DisplayDialog("Bake Demolish Button Template",
                    "ไม่พบ BuildingSelectionUI ในซีน — เปิด Gamescene.unity ก่อน", "OK");
                return;
            }

            // parent ใต้ Canvas (ให้เห็น/แก้ใน Scene ได้ + ไม่โดนลบตอน Start)
            var canvas = sel.buttonContainer != null
                ? sel.buttonContainer.GetComponentInParent<Canvas>()
                : sel.GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : sel.transform;

            var old = parent.Find("DemolishButtonTemplate");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var slot = sel.BuildDemolishStructure(parent, "DemolishButtonTemplate");
            Undo.RegisterCreatedObjectUndo(slot, "Bake Demolish Button Template");

            // วางเหนือ hotbar ให้เห็นตอนเปิด active แก้ · เก็บ inactive (runtime clone)
            var rt = slot.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(140f, 180f);
            slot.SetActive(false);

            Undo.RecordObject(sel, "Assign demolish template");
            sel.demolishTemplate = slot;
            EditorUtility.SetDirty(sel);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = slot;
            Debug.Log("[DemolishButtonTemplateBaker] ✅ สร้าง DemolishButtonTemplate + wire demolishTemplate แล้ว — " +
                      "ติ๊ก active ชั่วคราวเพื่อดู/แก้ (ลาก sprite ลง Icon, ปรับ layout/สี) · กด Ctrl+S เซฟ · Play เห็นผล");
        }

        const string DemolishIconPath = "Assets/Sprites/UI/demolish_icon.png";

        /// <summary>
        /// ตั้งไอคอนปุ่มทุบเป็น demolish_icon.png (ค้อนทุบหิน มีกรอบ) — เซ็ต hammerIcon
        /// และอัปเดต Icon ใน demolishTemplate ถ้ามี ให้เห็นผลใน editor ทันที
        /// </summary>
        [MenuItem("NuclearReMind/UI/Set Demolish Icon (hammer)")]
        public static void SetDemolishIcon()
        {
            var sel = Object.FindFirstObjectByType<BuildingSelectionUI>();
            if (sel == null)
            {
                EditorUtility.DisplayDialog("Set Demolish Icon",
                    "ไม่พบ BuildingSelectionUI ในซีน — เปิด Gamescene.unity ก่อน", "OK");
                return;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DemolishIconPath);
            if (sprite == null)
            {
                EditorUtility.DisplayDialog("Set Demolish Icon",
                    "โหลด sprite ไม่ได้:\n" + DemolishIconPath +
                    "\n(ถ้าเพิ่งก๊อปไฟล์เข้ามา ให้ Unity import ก่อน — คลิกที่ Project window สักครั้ง)", "OK");
                return;
            }

            Undo.RecordObject(sel, "Set demolish icon");
            sel.hammerIcon = sprite;
            EditorUtility.SetDirty(sel);

            // อัปเดต template (ถ้ามี) ให้เห็นผลใน editor ทันที
            if (sel.demolishTemplate != null)
            {
                var iconTf = FindDeep(sel.demolishTemplate.transform, "Icon");
                var img = iconTf != null ? iconTf.GetComponent<Image>() : null;
                if (img != null)
                {
                    Undo.RecordObject(img, "Set demolish icon");
                    img.sprite = sprite;
                    img.enabled = true;
                    img.preserveAspect = true;
                    EditorUtility.SetDirty(img);
                }
                var emojiTf = FindDeep(sel.demolishTemplate.transform, "IconEmoji");
                if (emojiTf != null) emojiTf.gameObject.SetActive(false);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[DemolishButtonTemplateBaker] ✅ ตั้ง hammerIcon = demolish_icon.png แล้ว — กด Ctrl+S เซฟ · Play เห็นผล");
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
