using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// สร้าง Canvas + UI elements (Slider/Text/Panel) สำหรับ HUD แบบ placeholder
    /// และผูก reference เข้า UIManagerHUD ให้อัตโนมัติ — รันครั้งเดียวจาก Editor
    /// </summary>
    public static class HUDCanvasSetup
    {
        [MenuItem("NuclearReMind/Setup HUD Canvas")]
        public static void SetupHUD()
        {
            var hudGO = GameObject.Find("UIManagerHUD");
            if (hudGO == null)
            {
                Debug.LogError("[HUDCanvasSetup] ไม่พบ GameObject 'UIManagerHUD' ใน scene");
                return;
            }
            var hud = hudGO.GetComponent<UIManagerHUD>();
            if (hud == null)
            {
                Debug.LogError("[HUDCanvasSetup] GameObject 'UIManagerHUD' ไม่มี component UIManagerHUD");
                return;
            }

            // ลบ HUDCanvas เก่าออกก่อน (กัน duplicate เมื่อรัน setup ซ้ำ) — ใช้ Undo เพื่อให้ Ctrl+Z คืนได้
            var existing = GameObject.Find("HUDCanvas");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                Debug.Log("[HUDCanvasSetup] ลบ HUDCanvas เก่าออก (Ctrl+Z เพื่อคืน)");
            }

            var font = LoadFont();

            // ===== Canvas =====
            var canvasGO = new GameObject("HUDCanvas", typeof(RectTransform));
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem", typeof(RectTransform));
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }

            // ===== Resource panel (top-left) =====
            var resourcePanel = CreatePanel("ResourcePanel", canvasGO.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20, -20), new Vector2(260, 180));
            var resourceLayout = resourcePanel.AddComponent<VerticalLayoutGroup>();
            resourceLayout.spacing = 6f;
            resourceLayout.childControlHeight = false;
            resourceLayout.childForceExpandHeight = false;

            // Food/Water ใช้ sprite icon — 🌿💧 เป็น emoji นอก BMP (surrogate pair) legacy Text วาดไม่ได้
            // ⛏⚡ อยู่ใน BMP เรนเดอร์ผ่าน OS font fallback ได้ จึงคงเป็น text
            hud.foodBar = CreateResourceBar("FoodBar", resourcePanel.transform, font, new Color(0.4f, 0.8f, 0.2f), "F",
                PlaceholderSpriteGenerator.EnsureIconSprite("IconFood"));
            hud.waterBar = CreateResourceBar("WaterBar", resourcePanel.transform, font, new Color(0.2f, 0.6f, 1f), "W",
                PlaceholderSpriteGenerator.EnsureIconSprite("IconWater"));
            hud.ironBar = CreateResourceBar("IronBar", resourcePanel.transform, font, new Color(0.6f, 0.55f, 0.5f), "⛏");
            hud.energyBar = CreateResourceBar("EnergyBar", resourcePanel.transform, font, new Color(1f, 0.8f, 0.2f), "⚡");

            // ===== Day panel (top-center, above tower) =====
            // กว้าง 320/สูง 64 เผื่อ line height ของ Kanit (สูงกว่า Arial ~1.5×) — ข้อความไม่โดน truncate
            var dayPanel = CreatePanel("DayPanel", canvasGO.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(320, 64));
            // พื้น panel ทึบ + ข้อความเข้ม — กัน text ขาวจมหายบน light theme (Palette.CameraBackground = #E9EDF3)
            var dayBg = dayPanel.AddComponent<Image>();
            dayBg.color = Palette.PanelBg;
            hud.dayText = CreateText("DayText", dayPanel.transform, font, "DAY 1 / 30", 22, new Vector2(0, -4), new Vector2(320, 30), TextAnchor.UpperCenter);
            hud.dayText.color = Palette.TextPrimary;
            hud.timerText = CreateText("TimerText", dayPanel.transform, font, "—", 20, new Vector2(0, -32), new Vector2(320, 26), TextAnchor.UpperCenter);
            hud.timerText.color = Palette.TextMuted;

            // ===== Tower panel (top-center, below day panel) =====
            var towerPanel = CreatePanel("TowerPanel", canvasGO.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(320, 60));
            hud.towerPhaseText = CreateText("TowerPhaseText", towerPanel.transform, font, "CORE TOWER — Phase 1/3", 18, new Vector2(0, -2), new Vector2(320, 24), TextAnchor.UpperCenter);
            hud.towerProgressBar = CreateSlider("TowerProgressBar", towerPanel.transform, new Color(1f, 0.4f, 0.2f), new Vector2(0, -30), new Vector2(320, 20));

            // ===== Population panel (top-right) =====
            var popPanel = CreatePanel("PopulationPanel", canvasGO.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20, -20), new Vector2(260, 230));
            var popLayout = popPanel.AddComponent<VerticalLayoutGroup>();
            popLayout.spacing = 6f;
            popLayout.childControlHeight = false;
            popLayout.childForceExpandHeight = false;

            hud.populationText = CreateTextRow("PopulationText", popPanel.transform, font, "ประชากร 10/10  ·  W10 E0 M0");
            hud.hopeText = CreateTextRow("HopeText", popPanel.transform, font, "Hope: 100");
            hud.hopeBar = CreateSliderRow("HopeBar", popPanel.transform, new Color(0.3f, 0.85f, 1f));

            // Knowledge (V4 §16) — ป้าย tier เริ่มที่ "Novice" (Gap G8: ไม่มี initial broadcast จึง bake ค่าเริ่มต้นไว้)
            hud.knowledgeText = CreateTextRow("KnowledgeText", popPanel.transform, font, "Knowledge: 0 / 100 · Novice");
            hud.knowledgeBar = CreateSliderRow("KnowledgeBar", popPanel.transform, new Color(0.62f, 0.5f, 1f));

            // ===== Train class buttons (ใต้ Population panel — V4 §5) =====
            var trainPanel = CreatePanel("TrainPanel", canvasGO.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20, -258), new Vector2(260, 44));
            hud.trainEngineerButton = CreateButton("TrainEngineerBtn", trainPanel.transform, font, "ฝึกวิศวกร", new Vector2(-64, 0), new Vector2(122, 36));
            hud.trainMedicButton    = CreateButton("TrainMedicBtn",    trainPanel.transform, font, "ฝึกแพทย์",  new Vector2(64, 0),  new Vector2(122, 36));

            // ===== Decree buttons (ประกาศฉุกเฉิน — V4 §11) =====
            var decreePanel = CreatePanel("DecreePanel", canvasGO.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20, -306), new Vector2(260, 44));
            hud.decree1Button = CreateButton("Decree1Btn", decreePanel.transform, font, "ประกาศ①", new Vector2(-64, 0), new Vector2(122, 36));
            hud.decree2Button = CreateButton("Decree2Btn", decreePanel.transform, font, "ประกาศ②", new Vector2(64, 0),  new Vector2(122, 36));

            // ===== Speed controls (top-center ขวาของ DayPanel) =====
            // เดิมอยู่ bottom-center (0,20) ทับ BuildingSelectionPanel hotbar — ย้ายไปคู่กับนาฬิกาวัน
            var speedPanel = CreatePanel("SpeedPanel", canvasGO.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(280, -20), new Vector2(210, 50));
            hud.pauseButton  = CreateButton("PauseButton",  speedPanel.transform, font, "II", new Vector2(-70, 0), new Vector2(60, 40));
            hud.normalButton = CreateButton("NormalButton", speedPanel.transform, font, "1x", new Vector2(0, 0),   new Vector2(60, 40));
            hud.fastButton   = CreateButton("FastButton",   speedPanel.transform, font, "2x", new Vector2(70, 0),  new Vector2(60, 40));

            // ===== Alert container (bottom-right, ซ้อนขึ้นบน) + AlertController =====
            var alertGO = new GameObject("AlertContainer", typeof(RectTransform));
            alertGO.transform.SetParent(canvasGO.transform, false);
            var alertRect = alertGO.GetComponent<RectTransform>();
            alertRect.anchorMin = new Vector2(1f, 0f);
            alertRect.anchorMax = new Vector2(1f, 0f);
            alertRect.pivot = new Vector2(1f, 0f);
            alertRect.anchoredPosition = new Vector2(-20, 90);
            alertRect.sizeDelta = new Vector2(320, 0);
            var alertLayout = alertGO.AddComponent<VerticalLayoutGroup>();
            alertLayout.spacing = 6f;
            alertLayout.childAlignment = TextAnchor.LowerRight;
            alertLayout.childControlHeight = false;
            alertLayout.childControlWidth = true;
            alertLayout.childForceExpandHeight = false;
            alertLayout.childForceExpandWidth = true;
            var alertFitter = alertGO.AddComponent<ContentSizeFitter>();
            alertFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var controllerGO = GameObject.Find("AlertController") ?? new GameObject("AlertController");
            var alertController = controllerGO.GetComponent<AlertController>() ?? controllerGO.AddComponent<AlertController>();
            alertController.alertContainer = alertRect;
            alertController.font = font;
            EditorUtility.SetDirty(alertController);

            // ===== CORE TOWER overclock panel (bottom-center, เหนือ hotbar) =====
            // hotbar (BuildingSelectionPanel) กิน y 4–122 — เริ่มที่ 130 กันทับ
            var corePanel = CreatePanel("CoreTowerPanel", canvasGO.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 130), new Vector2(380, 150));

            var coreStatus = CreateText("CoreStatusText", corePanel.transform, font, "CORE TOWER — ล็อก (Day 11)", 16, new Vector2(0, 58), new Vector2(370, 22), TextAnchor.MiddleCenter);
            var coreBar = CreateSlider("CoreBar", corePanel.transform, new Color(0.3f, 0.8f, 1f), new Vector2(0, 34), new Vector2(360, 16));
            var heatBar = CreateSlider("HeatBar", corePanel.transform, new Color(1f, 0.6f, 0.2f), new Vector2(0, 12), new Vector2(360, 16));
            var heatFill = heatBar.transform.Find("Fill Area/Fill").GetComponent<Image>();

            var idleBtn = CreateButton("ModeIdle",      corePanel.transform, font, "0x",    new Vector2(-152, -20), new Vector2(70, 36));
            var normalBtn = CreateButton("ModeNormal",  corePanel.transform, font, "1x",    new Vector2(-76, -20),  new Vector2(70, 36));
            var boostBtn = CreateButton("ModeBoost",    corePanel.transform, font, "2x",    new Vector2(0, -20),    new Vector2(70, 36));
            var odBtn = CreateButton("ModeOverdrive",   corePanel.transform, font, "3x",    new Vector2(76, -20),   new Vector2(70, 36));
            var scramBtn = CreateButton("ScramButton",  corePanel.transform, font, "SCRAM", new Vector2(152, -20),  new Vector2(70, 36));

            var coreUIGo = GameObject.Find("CoreTowerUI") ?? new GameObject("CoreTowerUI");
            var coreUI = coreUIGo.GetComponent<CoreTowerUI>() ?? coreUIGo.AddComponent<CoreTowerUI>();
            coreUI.statusText = coreStatus;
            coreUI.coreBar = coreBar;
            coreUI.heatBar = heatBar;
            coreUI.heatFill = heatFill;
            coreUI.idleButton = idleBtn;
            coreUI.normalButton = normalBtn;
            coreUI.boostButton = boostBtn;
            coreUI.overdriveButton = odBtn;
            coreUI.scramButton = scramBtn;
            EditorUtility.SetDirty(coreUI);

            // ปุ่ม Coils (V4 §6) — wire onClick ตอน runtime ใน UIManagerHUD.Start
            hud.toroidalButton = CreateButton("ToroidalBtn", corePanel.transform, font, "+Toroidal", new Vector2(-95, -56), new Vector2(160, 30));
            hud.poloidalButton = CreateButton("PoloidalBtn", corePanel.transform, font, "+Poloidal", new Vector2(95, -56), new Vector2(160, 30));

            // ===== Game Over panel (full screen) =====
            var goPanel = CreatePanel("GameOverPanel", canvasGO.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var goRect = goPanel.GetComponent<RectTransform>();
            goRect.anchorMin = Vector2.zero;
            goRect.anchorMax = Vector2.one;
            goRect.offsetMin = Vector2.zero;
            goRect.offsetMax = Vector2.zero;
            var goImage = goPanel.AddComponent<Image>();
            goImage.color = new Color(0f, 0f, 0f, 0.75f);
            hud.gameOverText = CreateText("GameOverText", goPanel.transform, font, "", 28, new Vector2(0, 40), new Vector2(1200, 240), TextAnchor.MiddleCenter);
            var goTextRect = hud.gameOverText.GetComponent<RectTransform>();
            goTextRect.anchorMin = new Vector2(0.5f, 0.5f);
            goTextRect.anchorMax = new Vector2(0.5f, 0.5f);

            // ปุ่มเริ่มใหม่ (V4 §14) — UIManagerHUD wire onClick + ซ่อน/โชว์ตอนจบเกม
            hud.restartButton = CreateButton("RestartButton", goPanel.transform, font, "เริ่มใหม่", new Vector2(0, -150), new Vector2(220, 54));
            hud.restartButton.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.5f);
            hud.restartButton.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);

            hud.gameOverPanel = goPanel;
            goPanel.SetActive(false);

            // ===== Quiz popup (V4 §16) — full-screen overlay + dialog, wire QuizPopupController =====
            SetupQuizPopup(canvasGO.transform, font);

            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("[HUDCanvasSetup] สร้าง HUD Canvas และผูก reference เข้า UIManagerHUD สำเร็จ — กด Save Scene (Ctrl+S)");
        }

        // ─────────────────────────────────────────────────────────────
        //  Quiz popup — Decision Quiz (V4 §16 / Gap G9)
        //  QuizPanel เป็นลูกของ HUDCanvas (สร้างใหม่ทุกครั้งที่รัน setup)
        //  ส่วน QuizPopupController เป็น GameObject เดี่ยว (found-or-create → รอด re-run)
        //  แล้ว re-wire reference ชี้ panel children ที่เพิ่งสร้างทุกครั้ง
        //  ปุ่มตัวเลือก 3 ปุ่ม: onClick ผูกเองตอน runtime ใน QuizPopupController.Start (ส่ง index)
        //  ที่นี่จึงผูกเฉพาะ Confirm/Close แบบ persistent
        // ─────────────────────────────────────────────────────────────
        private static void SetupQuizPopup(Transform canvasTransform, Font font)
        {
            // ===== Full-screen overlay (มืด) =====
            var overlay = CreatePanel("QuizPopupPanel", canvasTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.72f);

            // ===== Dialog box (center) =====
            var dialog = CreatePanel("QuizDialog", overlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 540));
            var dialogImage = dialog.AddComponent<Image>();
            dialogImage.color = new Color(0.10f, 0.11f, 0.14f, 0.97f);

            // แถบสีหมวด (บนสุด) — QuizPopupController จะเปลี่ยนสีตาม category ตอน runtime
            var categoryGO = new GameObject("QuizCategoryBar", typeof(RectTransform));
            categoryGO.transform.SetParent(dialog.transform, false);
            var catRect = categoryGO.GetComponent<RectTransform>();
            catRect.anchorMin = new Vector2(0f, 1f);
            catRect.anchorMax = new Vector2(1f, 1f);
            catRect.pivot = new Vector2(0.5f, 1f);
            catRect.anchoredPosition = Vector2.zero;
            catRect.sizeDelta = new Vector2(0, 10);
            var categoryBar = categoryGO.AddComponent<Image>();
            categoryBar.color = new Color(0.20f, 0.50f, 0.85f);

            var speakerText = CreateText("QuizSpeakerText", dialog.transform, font, "VESTA", 18, Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
            speakerText.fontStyle = FontStyle.Bold;
            speakerText.color = new Color(0.6f, 0.85f, 1f);
            AnchorTop(speakerText, 18, new Vector2(720, 24));

            var questionText = CreateText("QuizQuestionText", dialog.transform, font, "คำถาม", 20, Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
            questionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            questionText.verticalOverflow = VerticalWrapMode.Overflow;
            AnchorTop(questionText, 48, new Vector2(720, 110));

            // ปุ่มตัวเลือก 3 ปุ่ม
            var optionButtons = new Button[3];
            var optionTexts = new Text[3];
            float[] optionY = { 168f, 226f, 284f };
            for (int i = 0; i < 3; i++)
            {
                var btn = CreateButton($"QuizOption{i}", dialog.transform, font, $"ตัวเลือก {i + 1}", Vector2.zero, new Vector2(700, 52));
                AnchorTop(btn, optionY[i], new Vector2(700, 52));
                optionButtons[i] = btn;
                optionTexts[i] = btn.GetComponentInChildren<Text>();
            }

            var confirmButton = CreateButton("QuizConfirmButton", dialog.transform, font, "ยืนยัน", Vector2.zero, new Vector2(240, 48));
            AnchorTop(confirmButton, 348, new Vector2(240, 48));

            var explainText = CreateText("QuizExplainText", dialog.transform, font, "", 16, Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
            explainText.horizontalOverflow = HorizontalWrapMode.Wrap;
            explainText.verticalOverflow = VerticalWrapMode.Overflow;
            explainText.color = new Color(0.9f, 0.92f, 0.8f);
            AnchorTop(explainText, 348, new Vector2(720, 120));
            explainText.gameObject.SetActive(false);

            var closeButton = CreateButton("QuizCloseButton", dialog.transform, font, "ปิด", Vector2.zero, new Vector2(200, 46));
            AnchorTop(closeButton, 478, new Vector2(200, 46));
            closeButton.gameObject.SetActive(false);

            overlay.SetActive(false);

            // ===== QuizPopupController (GameObject เดี่ยว — รอด re-run) =====
            var quizGO = GameObject.Find("QuizPopupController") ?? new GameObject("QuizPopupController");
            var quiz = quizGO.GetComponent<QuizPopupController>() ?? quizGO.AddComponent<QuizPopupController>();
            quiz.popupPanel = overlay;
            quiz.categoryBar = categoryBar;
            quiz.speakerText = speakerText;
            quiz.questionText = questionText;
            quiz.explainText = explainText;
            quiz.optionButtons = optionButtons;
            quiz.optionTexts = optionTexts;
            quiz.confirmButton = confirmButton;
            quiz.closeButton = closeButton;

            // Confirm/Close ผูกแบบ persistent (ปุ่มตัวเลือกผูกเองตอน runtime ใน controller.Start)
            UnityEventTools.AddPersistentListener(confirmButton.onClick, new UnityAction(quiz.Confirm));
            UnityEventTools.AddPersistentListener(closeButton.onClick, new UnityAction(quiz.Close));

            EditorUtility.SetDirty(quiz);

            // ===== QuizManager (GameObject เดี่ยว — allQuizzes wire ทีหลังโดย Setup Quiz System) =====
            // ต้องมี QuizManager ในซีน ไม่งั้นควิซไม่ทำงาน + Setup Quiz System จะ wire allQuizzes ไม่ได้
            var quizMgrGO = GameObject.Find("QuizManager") ?? new GameObject("QuizManager");
            if (quizMgrGO.GetComponent<QuizManager>() == null)
                quizMgrGO.AddComponent<QuizManager>();
            EditorUtility.SetDirty(quizMgrGO);

            // ===== TimeManager (pause-reason stack — V4 §15/§16) =====
            var timeMgrGO = GameObject.Find("TimeManager") ?? new GameObject("TimeManager");
            if (timeMgrGO.GetComponent<TimeManager>() == null)
                timeMgrGO.AddComponent<TimeManager>();
            EditorUtility.SetDirty(timeMgrGO);
        }

        // จัด RectTransform ให้ยึดขอบบนของ dialog แล้วเลื่อนลงตาม yFromTop (px)
        private static void AnchorTop(Component target, float yFromTop, Vector2 size)
        {
            var rect = target.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -yFromTop);
            rect.sizeDelta = size;
        }

        private static Font LoadFont()
        {
            // ลอง Kanit ก่อน — ถ้าไม่มีค่อย fallback เป็น built-in
            var kanit = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Kanit-Regular.ttf");
            if (kanit != null) return kanit;

            var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtin != null) return builtin;

            // กรณี Unity 6 ไม่พบ LegacyRuntime ให้หา .ttf ใดก็ได้ใน project
            foreach (var guid in AssetDatabase.FindAssets("t:Font", new[] { "Assets" }))
            {
                var f = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));
                if (f != null) return f;
            }
            Debug.LogWarning("[HUDCanvasSetup] ไม่พบ font ใดเลย — ข้อความอาจไม่แสดง");
            return null;
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

        private static UIManagerHUD.ResourceBarUI CreateResourceBar(string name, Transform parent, Font font, Color fillColor, string icon, Sprite iconSprite = null)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(260, 26);

            // icon ด้านซ้ายสุด — sprite ถ้ามี (emoji นอก BMP วาดไม่ได้) ไม่งั้นใช้ text
            RectTransform iconRect;
            if (iconSprite != null)
            {
                var iconGO = new GameObject(name + "Icon", typeof(RectTransform));
                iconGO.transform.SetParent(row.transform, false);
                var img = iconGO.AddComponent<Image>();
                img.sprite = iconSprite;
                img.preserveAspect = true;
                iconRect = iconGO.GetComponent<RectTransform>();
                iconRect.sizeDelta = new Vector2(22, 22);
            }
            else
            {
                var iconText = CreateText(name + "Icon", row.transform, font, icon, 18, new Vector2(2, 0), new Vector2(24, 24), TextAnchor.MiddleCenter);
                iconRect = iconText.GetComponent<RectTransform>();
            }
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(2, 0);

            var slider = CreateSlider(name + "Slider", row.transform, fillColor, new Vector2(28, 0), new Vector2(150, 20));
            var sliderRect = slider.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 0.5f);
            sliderRect.anchorMax = new Vector2(0f, 0.5f);
            sliderRect.pivot = new Vector2(0f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(28, 0);

            var valueText = CreateText(name + "ValueText", row.transform, font, "0 / 0", 16, new Vector2(0, 0), new Vector2(80, 24), TextAnchor.MiddleRight);
            var valueRect = valueText.GetComponent<RectTransform>();
            valueRect.anchorMin = new Vector2(1f, 0.5f);
            valueRect.anchorMax = new Vector2(1f, 0.5f);
            valueRect.pivot = new Vector2(1f, 0.5f);
            valueRect.anchoredPosition = new Vector2(0, 0);

            var fillImage = slider.transform.Find("Fill Area/Fill").GetComponent<Image>();

            return new UIManagerHUD.ResourceBarUI
            {
                bar = slider,
                valueText = valueText,
                fillImage = fillImage
            };
        }

        private static Text CreateTextRow(string name, Transform parent, Font font, string content)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260, 24);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleRight;
            text.text = content;
            text.verticalOverflow = VerticalWrapMode.Overflow; // กัน Kanit โดน truncate ทั้งบรรทัด
            return text;
        }

        private static Slider CreateSliderRow(string name, Transform parent, Color fillColor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260, 20);
            return BuildSlider(go, fillColor);
        }

        private static GameObject CreateWarningText(string name, Transform parent, Font font, string content, Color color)
        {
            var text = CreateTextRow(name, parent, font, content);
            text.color = color;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.gameObject.SetActive(false);
            return text.gameObject;
        }

        private static GameObject CreateCenterWarning(string name, Transform parent, Font font, string content, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -100);
            rect.sizeDelta = new Vector2(600, 40);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = content;
            go.SetActive(false);
            return go;
        }

        private static Text CreateText(string name, Transform parent, Font font, string content, int fontSize, Vector2 anchoredPos, Vector2 size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = anchor;
            text.text = content;
            // Kanit line height สูงกว่ากล่องที่วางไว้ — default Truncate จะตัดทั้งบรรทัดจนมองไม่เห็น
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, Font font, string label, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = Color.white;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None; // ให้ UIManagerHUD คุมสี (highlight) เองล้วน

            var textGO = new GameObject("Label", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var trect = textGO.GetComponent<RectTransform>();
            trect.anchorMin = Vector2.zero;
            trect.anchorMax = Vector2.one;
            trect.offsetMin = Vector2.zero;
            trect.offsetMax = Vector2.zero;
            var t = textGO.AddComponent<Text>();
            t.font = font;
            t.fontSize = 20;
            t.fontStyle = FontStyle.Bold;
            t.color = Color.black;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = label;

            return btn;
        }

        private static Slider CreateSlider(string name, Transform parent, Color fillColor, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return BuildSlider(go, fillColor);
        }

        private static Slider BuildSlider(GameObject go, Color fillColor)
        {
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            var fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGO.transform.SetParent(go.transform, false);
            var fillAreaRect = fillAreaGO.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(2, 2);
            fillAreaRect.offsetMax = new Vector2(-2, -2);

            var fillGO = new GameObject("Fill", typeof(RectTransform));
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fillRect = fillGO.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fillGO.AddComponent<Image>();
            fillImage.color = fillColor;

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.targetGraphic = fillImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.interactable = false;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            return slider;
        }
    }
}
