using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using NuclearReMind;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// ระบบบันทึกใหม่ (แทนการ์ดเด้งกลางจอ): สร้าง "ไอคอนบันทึกมุมขวาล่าง + notification badge" บน HUDCanvas
    /// แล้ว wire RecordNotificationHUD — มีบันทึกใหม่เข้ามาเด้ง badge · กดเปิดแผง Records (เรียงล่าสุดบนสุด)
    ///
    /// idempotent (รันซ้ำได้ — find-or-create ตามชื่อ) · วางมุมขวาล่างสุด (เหนือ AlertContainer ที่ y=90 ไม่ทับ)
    /// ปุ่ม "บันทึก" ซ้ายล่างเดิม (side menu) ยังใช้เปิดแผงเดียวกันได้ตามที่ผู้ใช้เลือก
    /// รัน: NuclearReMind/Setup Record Notification (HUD Icon) — หรือรวมใน Run All Setups
    /// </summary>
    public static class RecordNotificationSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const string IconSpritePath = "Assets/Sprites/UI/side_save.png"; // reuse ไอคอนปุ่ม Records เดิม

        [MenuItem("NuclearReMind/Setup Record Notification (HUD Icon)")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var hud = GameObject.Find("HUDCanvas");
            if (hud == null)
            {
                Debug.LogWarning("[RecordNotificationSetup] ไม่พบ HUDCanvas — รัน Setup HUD Canvas ก่อน");
                return;
            }

            var font = LoadKanit();
            var iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconSpritePath);

            // ── ไอคอนมุมขวาล่าง ──
            var iconGO = FindOrCreate(hud.transform, "RecordNotifyIcon");
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(1f, 0f);
            iconRect.anchorMax = new Vector2(1f, 0f);
            iconRect.pivot     = new Vector2(1f, 0f);
            iconRect.anchoredPosition = new Vector2(-20f, 20f); // มุมขวาล่างสุด (Alert อยู่ y=90 ขึ้นไป → ไม่ทับ)
            iconRect.sizeDelta = new Vector2(58f, 58f);

            var iconImg = iconGO.GetComponent<Image>() ?? iconGO.AddComponent<Image>();
            iconImg.sprite = iconSprite;
            iconImg.preserveAspect = true;
            iconImg.color = iconSprite != null ? Color.white : new Color(0.15f, 0.14f, 0.11f, 1f);
            if (iconSprite == null)
                Debug.LogWarning("[RecordNotificationSetup] ไม่พบ side_save.png — ไอคอนจะเป็นสี่เหลี่ยมทึบ (import แล้วรันซ้ำ)");

            var iconBtn = iconGO.GetComponent<Button>() ?? iconGO.AddComponent<Button>();
            iconBtn.targetGraphic = iconImg;
            var cb = iconBtn.colors;
            cb.highlightedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            cb.pressedColor     = new Color(0.70f, 0.70f, 0.70f, 1f);
            cb.fadeDuration = 0.08f;
            iconBtn.colors = cb;

            // ── badge วงกลมแดง (มุมขวาบนของไอคอน) ──
            var badgeGO = FindOrCreate(iconGO.transform, "Badge");
            var badgeRect = badgeGO.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot     = new Vector2(0.5f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(-2f, -2f);
            badgeRect.sizeDelta = new Vector2(24f, 24f);

            var badgeImg = badgeGO.GetComponent<Image>() ?? badgeGO.AddComponent<Image>();
            badgeImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); // วงกลมเต็ม (built-in)
            badgeImg.type = Image.Type.Simple;
            badgeImg.color = new Color(0.85f, 0.15f, 0.15f, 1f);
            badgeImg.raycastTarget = false;

            // ── เลขจำนวนบันทึกใหม่ ──
            var countGO = FindOrCreate(badgeGO.transform, "Count");
            var countRect = countGO.GetComponent<RectTransform>();
            countRect.anchorMin = Vector2.zero; countRect.anchorMax = Vector2.one;
            countRect.offsetMin = Vector2.zero; countRect.offsetMax = Vector2.zero;
            var countTxt = countGO.GetComponent<Text>() ?? countGO.AddComponent<Text>();
            countTxt.font = font;
            countTxt.fontSize = 14;
            countTxt.fontStyle = FontStyle.Bold;
            countTxt.alignment = TextAnchor.MiddleCenter;
            countTxt.color = Color.white;
            countTxt.text = "0";
            countTxt.raycastTarget = false;
            countTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            countTxt.verticalOverflow   = VerticalWrapMode.Overflow;

            badgeGO.SetActive(false); // ซ่อนไว้ก่อน — RecordNotificationHUD เปิดเมื่อมีบันทึกใหม่

            // ── wire RecordNotificationHUD (บนตัวไอคอนเอง) ──
            var hudComp = iconGO.GetComponent<RecordNotificationHUD>() ?? iconGO.AddComponent<RecordNotificationHUD>();
            hudComp.iconButton = iconBtn;
            hudComp.badge = badgeGO;
            hudComp.badgeText = countTxt;
            EditorUtility.SetDirty(hudComp);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[RecordNotificationSetup] ✅ ไอคอนบันทึกมุมขวาล่าง + badge พร้อม — บันทึกใหม่เด้ง badge · กดเปิดแผง Records");
        }

        private static GameObject FindOrCreate(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t.gameObject;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Font LoadKanit()
        {
            var f = Resources.Load<Font>("HUD/Fonts/Kanit-Regular");
            return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
