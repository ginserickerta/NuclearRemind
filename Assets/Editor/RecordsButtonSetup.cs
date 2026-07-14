using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using NuclearReMind;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// เมนูแยกเฉพาะ "ปุ่มบันทึก (Records)" — สร้าง/แก้ + wire onClick → HudMenuButtons.ToggleRecords
    /// (เปิดแผง Records "บันทึกที่กู้คืน") โดยไม่แตะปุ่มอื่นในแถบข้าง
    ///
    /// ต่างจาก "Setup Side Menu Buttons" ที่ rebuild ทั้ง 4 ปุ่ม — อันนี้จัดเฉพาะปุ่มบันทึกอย่างเดียว
    /// idempotent: รันซ้ำได้ (ลบปุ่มบันทึกเก่า/ปุ่ม Save เดิมแล้วสร้างใหม่ + re-bake persistent listener)
    /// รัน: NuclearReMind/UI/Setup Records Button
    /// </summary>
    public static class RecordsButtonSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const string UIFolder = "Assets/Sprites/UI";
        private const string SpriteName = "side_save"; // สไปรต์ป้าย "บันทึก" (icon+ข้อความ baked)

        [MenuItem("NuclearReMind/UI/Setup Records Button")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var hud = GameObject.Find("HUDCanvas");
            if (hud == null)
            {
                EditorUtility.DisplayDialog("Setup Records Button",
                    "ไม่พบ HUDCanvas — รัน Setup HUD Canvas ก่อน", "OK");
                return;
            }

            // แถบข้าง + ตัวกลาง HudMenuButtons (ใช้ร่วมกับปุ่มอื่น — ไม่มีก็สร้างให้)
            var barTf = hud.transform.Find("SideMenuBar");
            GameObject bar;
            if (barTf != null) bar = barTf.gameObject;
            else
            {
                bar = new GameObject("SideMenuBar", typeof(RectTransform));
                bar.transform.SetParent(hud.transform, false);
                var br = bar.GetComponent<RectTransform>();
                br.anchorMin = br.anchorMax = Vector2.zero;
                br.pivot = Vector2.zero;
                br.anchoredPosition = Vector2.zero;
                br.sizeDelta = Vector2.zero;
            }
            var hudMenu = bar.GetComponent<HudMenuButtons>() ?? bar.AddComponent<HudMenuButtons>();

            // ลบปุ่มบันทึกเก่า (ทั้งชื่อใหม่ SideBtn_Records และชื่อเดิม SideBtn_Save) กัน duplicate
            DestroyChild(bar.transform, "SideBtn_Records");
            DestroyChild(bar.transform, "SideBtn_Save");

            var btn = MakeSpriteButton(bar.transform, "SideBtn_Records", SpriteName,
                new Vector2(19f, 100f), new Vector2(210f, 54f));

            // wire persistent (ติดถาวรในซีน — bake ตอน edit-time)
            UnityEventTools.AddPersistentListener(btn.onClick, new UnityAction(hudMenu.ToggleRecords));

            // เตือนถ้าไม่มี RecordsPanelController ในซีน (ปุ่มจะกดแล้วเงียบ)
            if (Object.FindFirstObjectByType<RecordsPanelController>() == null)
                Debug.LogWarning("[RecordsButtonSetup] ไม่พบ RecordsPanelController ในซีน — รัน 'Setup Story UI' ก่อน ไม่งั้นปุ่มจะไม่เปิดแผง");

            EditorUtility.SetDirty(bar);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = btn.gameObject;
            Debug.Log("[RecordsButtonSetup] ✅ ปุ่มบันทึก (Records) พร้อม + wire ToggleRecords — กด Ctrl+S เซฟซีน");
        }

        private static Button MakeSpriteButton(Transform parent, string name, string spriteName,
            Vector2 anchoredPos, Vector2 size)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UIFolder}/{spriteName}.png");

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.color = Color.white;
            if (sprite == null)
            {
                Debug.LogWarning($"[RecordsButtonSetup] ไม่พบสไปรต์ {spriteName} — ปุ่มจะว่าง (ให้ Unity import ก่อน)");
                img.color = new Color(0.15f, 0.14f, 0.11f, 1f);
            }

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            cb.pressedColor = new Color(0.70f, 0.70f, 0.70f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            return btn;
        }

        private static void DestroyChild(Transform parent, string childName)
        {
            var t = parent.Find(childName);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
    }
}
