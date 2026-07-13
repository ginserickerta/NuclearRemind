using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// สร้างหน้า "คำอธิบายหลังตอบควิส" (QuizExplanationPopupController) บน canvas แยกของตัวเอง
    /// sortingOrder 70 (เหนือ quiz popup=0 · ใต้ pause=100) — โผล่หลังผู้เล่นตอบ ทั้งถูกและผิด
    ///
    /// data-driven จาก QuizQuestionSO (explanationTitle/scoreDelta/explainText/codexUnlockId) —
    /// setup แค่วาง UI + wire field เข้า controller · การปลดล็อก Codex ทำที่ QuizManager (ผ่าน event)
    ///
    /// idempotent — ลบ "QuizExplanationCanvas" เดิมก่อนสร้างใหม่ (รันเมนูซ้ำไม่ทับซ้อน)
    /// สกิน: frame_metal (Codex) + plate_inset/btn_metal (Story) — fallback สีถ้าไม่มีไฟล์
    /// helper self-contained แนวเดียวกับ TooltipDilemmaSetup (ไม่พึ่งไฟล์อื่น)
    /// </summary>
    public static class QuizExplanationSetup
    {
        private const string ScenePath  = "Assets/Scenes/Gamescene.unity";
        private const string CanvasName = "QuizExplanationCanvas";

        [MenuItem("NuclearReMind/Setup Quiz Explanation UI")]
        public static void SetupAll()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem", typeof(RectTransform));
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }

            // idempotent — ลบ canvas เดิมทั้งก้อนก่อนสร้างใหม่
            var existing = GameObject.Find(CanvasName);
            if (existing != null) Object.DestroyImmediate(existing);

            // ===== canvas แยก (sortingOrder 70) — ไม่ยุ่งกับ HUDCanvas =====
            var canvasGO = new GameObject(CanvasName, typeof(RectTransform));
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1536f, 864f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            canvasGO.AddComponent<GraphicRaycaster>();

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildPopup(canvasGO.transform, font);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[QuizExplanationSetup] สร้าง Quiz Explanation UI (canvas แยก order 70) + wire controller + save scene สำเร็จ");
        }

        private static void BuildPopup(Transform canvasTransform, Font font)
        {
            var frameMetal = LoadUISkin("Assets/Resources/CodexUI/frame_metal.png");
            var plateInset = LoadUISkin("Assets/Resources/StoryUI/plate_inset.png");
            var btnMetal   = LoadUISkin("Assets/Resources/StoryUI/btn_metal.png");

            Color gold  = new Color(0.94f, 0.77f, 0.29f);
            Color light = new Color(0.90f, 0.90f, 0.86f);
            Color reward = new Color(0.59f, 0.80f, 1f);

            // ===== overlay เต็มจอ (บังคลิกทะลุ) =====
            var overlay = CreatePanel("QuizExplanationPanel", canvasTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.72f);

            // ===== Dialog กรอบโลหะ (กลางจอ) =====
            const float DW = 820f, DH = 560f, P = 28f;
            var dialog = CreatePanel("DialogBox", overlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(DW, DH));
            var dialogImage = dialog.AddComponent<Image>();
            if (frameMetal != null) { dialogImage.sprite = frameMetal; dialogImage.type = Image.Type.Sliced; dialogImage.color = Color.white; }
            else dialogImage.color = new Color(0.10f, 0.11f, 0.14f, 0.98f);
            var dlgT = dialog.transform;

            // --- หัวข้อ (กลางบน · ทอง) ---
            var titleText = CreateText("TitleText", dlgT, font, "คำอธิบาย", 28, new Vector2(DW - 2f * P, 48f), TextAnchor.MiddleCenter);
            PlaceTL(titleText.rectTransform, P, P, DW - 2f * P, 48f);
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = gold;
            titleText.resizeTextForBestFit = true; titleText.resizeTextMinSize = 20; titleText.resizeTextMaxSize = 34;

            // --- badge มุมขวาบน (สร้างหลัง title = render ทับ) — controller ตั้งข้อความ/สีตามถูก-ผิด ---
            var scoreBadgeText = CreateText("ScoreBadgeText", dlgT, font, "+8", 30, new Vector2(140f, 44f), TextAnchor.MiddleRight);
            PlaceTL(scoreBadgeText.rectTransform, DW - P - 140f, 26f, 140f, 44f);
            scoreBadgeText.fontStyle = FontStyle.Bold;

            // --- เส้นคั่น ---
            var divider = new GameObject("Divider", typeof(RectTransform));
            divider.transform.SetParent(dlgT, false);
            var divImg = divider.AddComponent<Image>();
            divImg.color = new Color(gold.r, gold.g, gold.b, 0.55f);
            PlaceTL(divImg.rectTransform, P, 84f, DW - 2f * P, 3f);

            // --- กล่องเนื้อหา (explainText · word-wrap + best-fit) ---
            var bodyPlate = MetalPlate("BodyPlate", dlgT, plateInset, P, 94f, DW - 2f * P, 286f);
            var bodyText = CreateText("BodyText", bodyPlate.transform, font, "คำอธิบาย …", 20, new Vector2(DW - 2f * P, 286f), TextAnchor.UpperLeft);
            FillParentText(bodyText, 18f);
            bodyText.color = light;
            bodyText.resizeTextForBestFit = true; bodyText.resizeTextMinSize = 14; bodyText.resizeTextMaxSize = 22;

            // --- แถบรางวัล Codex (plate) — controller เปิด/ปิด SetActive เอง ---
            var rewardPlate = MetalPlate("RewardBar", dlgT, plateInset, P, 392f, DW - 2f * P, 76f);
            var rewardBar = rewardPlate.gameObject;

            var rewardIconGO = new GameObject("RewardIcon", typeof(RectTransform));
            rewardIconGO.transform.SetParent(rewardBar.transform, false);
            var rewardIcon = rewardIconGO.AddComponent<Image>();
            rewardIcon.preserveAspect = true;
            rewardIcon.enabled = false; // controller เปิดตอนมีภาพ entry
            PlaceTL(rewardIcon.rectTransform, 12f, 8f, 60f, 60f);

            var rewardText = CreateText("RewardText", rewardBar.transform, font, "ปลดล็อก Codex: —", 20, new Vector2(DW - 2f * P, 76f), TextAnchor.MiddleLeft);
            var rrt = rewardText.rectTransform;
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.offsetMin = new Vector2(86f, 8f);
            rrt.offsetMax = new Vector2(-12f, -8f);
            rewardText.color = reward;
            rewardText.resizeTextForBestFit = true; rewardText.resizeTextMinSize = 12; rewardText.resizeTextMaxSize = 22;

            // --- ปุ่มปิด (ล่างกลาง) — controller ผูก onClick เองที่ Start() (ไม่ต้อง persistent listener) ---
            var closeGO = MakeMetalButton("CloseButton", dlgT, font, btnMetal != null ? btnMetal : plateInset, "ปิด", gold, 290f, 480f, 240f, 52f);
            var closeBtn = closeGO.GetComponent<Button>();

            overlay.SetActive(false);

            // ===== QuizExplanationPopupController (sibling ของ overlay · ต้อง active เพื่อรับ event) =====
            var controllerGO = new GameObject("QuizExplanationController", typeof(RectTransform));
            controllerGO.transform.SetParent(canvasTransform, false);
            var controller = controllerGO.AddComponent<QuizExplanationPopupController>();

            controller.popupPanel = overlay;
            controller.titleText = titleText;
            controller.scoreBadgeText = scoreBadgeText;
            controller.bodyText = bodyText;
            controller.rewardBar = rewardBar;
            controller.rewardIcon = rewardIcon;
            controller.rewardText = rewardText;
            controller.closeButton = closeBtn;

            rewardBar.SetActive(false);
            EditorUtility.SetDirty(controller);
        }

        // ── helpers (self-contained · แนวเดียวกับ TooltipDilemmaSetup) ──
        private static Sprite LoadUISkin(string assetPath) => AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

        // วางลูกโดยยึดมุมซ้าย-บนของ parent (x ไปขวา · y ลงล่าง)
        private static RectTransform PlaceTL(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        // แผ่นโลหะ (plate_inset 9-slice · tint ขาว) — fallback สีเข้มถ้าไม่มีสไปรต์
        private static Image MetalPlate(string name, Transform parent, Sprite plate, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            if (plate != null) { img.sprite = plate; img.type = Image.Type.Sliced; img.color = Color.white; }
            else img.color = new Color(0.12f, 0.13f, 0.17f, 0.98f);
            PlaceTL(img.rectTransform, x, y, w, h);
            return img;
        }

        // ยืด Text ให้เต็ม parent + padding (word-wrap ในกล่อง)
        private static void FillParentText(Text t, float padding)
        {
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        // ปุ่มโลหะป้ายกลาง (ปุ่มปิด)
        private static GameObject MakeMetalButton(string name, Transform parent, Font font, Sprite plate, string label, Color labelColor, float x, float y, float w, float h)
        {
            var img = MetalPlate(name, parent, plate, x, y, w, h);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
            cb.pressedColor = new Color(0.70f, 0.70f, 0.70f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            var t = CreateText(name + "Text", img.transform, font, label, 26, new Vector2(w, h), TextAnchor.MiddleCenter);
            FillParentText(t, 6f);
            t.fontStyle = FontStyle.Bold;
            t.color = labelColor;
            return img.gameObject;
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return go;
        }

        private static Text CreateText(string name, Transform parent, Font font, string content, int fontSize, Vector2 size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = content;
            return text;
        }
    }
}
