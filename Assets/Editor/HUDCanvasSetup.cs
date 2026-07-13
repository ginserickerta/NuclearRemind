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

            // ลบแผง CORE TOWER ล่างจอเดิม (CoreTowerUI) — แผงเตาย้ายไป CoreTowerPanelUI (คลิกจากตัวหอบนแมพ) แล้ว
            var oldCoreUI = GameObject.Find("CoreTowerUI");
            if (oldCoreUI != null)
            {
                Undo.DestroyObjectImmediate(oldCoreUI);
                Debug.Log("[HUDCanvasSetup] ลบ CoreTowerUI เดิมออก (แผงเตาใช้ CoreTowerPanelUI แทน)");
            }

            var font = LoadFont();

            // ===== Canvas =====
            var canvasGO = new GameObject("HUDCanvas", typeof(RectTransform));
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UIScaleSetup.ReferenceResolution; // ขนาด UI ทั้งเกมคุมที่ UIScaleSetup.UiScale
            canvasGO.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem", typeof(RectTransform));
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }

            // ===== Resource panel (top-left) — 6 แถว: อาหารมีแถบ (cap 500) ที่เหลือตัวเลขล้วน (cap 9999 V4 §4) =====
            var resourcePanel = CreatePanel("ResourcePanel", canvasGO.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20, -20), new Vector2(260, 200));
            var resourceLayout = resourcePanel.AddComponent<VerticalLayoutGroup>();
            resourceLayout.spacing = 6f;
            resourceLayout.childControlHeight = false;
            resourceLayout.childForceExpandHeight = false;

            // Food/Water ใช้ sprite icon — 🌿💧 เป็น emoji นอก BMP (surrogate pair) legacy Text วาดไม่ได้
            // ⛏⚡⚛ อยู่ใน BMP เรนเดอร์ผ่าน OS font fallback ได้ จึงคงเป็น text
            // หลอดตันที่ display* ของ UIManagerHUD (2000/500) — ตัวเลขวิ่งต่อได้ถึง cap 9999
            // icon อาร์ตจริงจาก Assets/Sprites/Icons — ถ้าไม่พบ fallback เป็น placeholder/emoji text เดิม
            hud.foodBar = CreateResourceBar("FoodBar", resourcePanel.transform, font, new Color(0.4f, 0.8f, 0.2f), "F",
                LoadIcon("Food", "IconFood"));
            hud.waterBar = CreateResourceBar("WaterBar", resourcePanel.transform, font, new Color(0.2f, 0.6f, 1f), "W",
                LoadIcon("Water", "IconWater"));
            hud.ironBar = CreateResourceBar("IronBar", resourcePanel.transform, font, new Color(0.6f, 0.55f, 0.5f), "⛏",
                LoadIcon("Iron"));
            hud.energyBar = CreateResourceBar("EnergyBar", resourcePanel.transform, font, new Color(1f, 0.8f, 0.2f), "⚡",
                LoadIcon("Energy"));
            // เชื้อเพลิงฟิวชัน (V4 §4) — Deuterium สกัดจากน้ำ · Tritium ขุดจากแหล่งแร่โซน B
            hud.deuteriumBar = CreateResourceBar("DeuteriumBar", resourcePanel.transform, font, new Color(0.35f, 0.7f, 0.95f), "D",
                LoadIcon("Deuterium"));
            hud.tritiumBar = CreateResourceBar("TritiumBar", resourcePanel.transform, font, new Color(0.85f, 0.45f, 0.2f), "⚛",
                LoadIcon("Tritium"));

            // ===== Day panel (top-center, above tower) — แผ่นโลหะ "DAY __ /30" (baked ในสไปรต์) โชว์เลขวันในช่องกลาง + timer/แถบเวลาใต้แผ่น (V4 §3) =====
            var planningCol = new Color(0.28f, 0.55f, 0.92f); // ฟ้า = วางแผน
            var liveCol = new Color(0.93f, 0.52f, 0.18f);     // ส้ม = เดินเครื่อง
            var dayPanel = CreatePanel("DayPanel", canvasGO.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(320, 100));

            // แผ่นโลหะวัน (สไปรต์ day_plate: คำว่า "DAY" อยู่ซ้าย · "/30" อยู่ขวา · เว้นช่องกลางไว้ใส่เลข) วางบนสุดของแผง
            // Simple + preserveAspect — ห้ามยืด (ข้อความ baked จะเพี้ยน) · fallback สีทึบถ้ายังไม่ import สไปรต์
            var dayPlateSprite = LoadUISkin("Assets/Resources/HUD/day_plate.png");
            var plateGO = new GameObject("DayPlate", typeof(RectTransform));
            plateGO.transform.SetParent(dayPanel.transform, false);
            var plateRect = plateGO.GetComponent<RectTransform>();
            plateRect.anchoredPosition = new Vector2(0, 19); // ชิดบนสุดของแผง (ครึ่งความสูงแผ่น 63/2 ≈ 31.5)
            plateRect.sizeDelta = new Vector2(320, 63);       // อัตราส่วนแผ่น ~5.08:1 (สไปรต์ 1430×280)
            var plateImg = plateGO.AddComponent<Image>();
            if (dayPlateSprite != null) { plateImg.sprite = dayPlateSprite; plateImg.type = Image.Type.Simple; plateImg.preserveAspect = true; plateImg.color = Color.white; }
            else plateImg.color = Palette.PanelBg;

            // เลขวันเท่านั้น (ไม่เขียน "DAY"/"/30" — มากับสไปรต์แล้ว) วางในช่องกลางแผ่น (frac 0.511 ของกว้าง)
            hud.dayText = CreateText("DayText", plateGO.transform, font, "1", 30, new Vector2(4, 2), new Vector2(56, 44), TextAnchor.MiddleCenter);
            hud.dayText.color = new Color(0.92f, 0.94f, 0.97f); // ขาวนวลให้เข้ากับตัวอักษร baked บนแผ่น
            hud.dayText.fontStyle = FontStyle.Bold;

            // timer + แถบเวลา ย้ายมาอยู่ "ใต้แผ่น" (ผู้ใช้เลือก) — ไม่มีพื้น panel ทึบแล้ว จึงใส่ Outline กัน text จมพื้น
            hud.timerText = CreateText("TimerText", dayPanel.transform, font, "—", 17, new Vector2(0, -25), new Vector2(320, 22), TextAnchor.MiddleCenter);
            hud.timerText.color = Palette.TextMuted;
            hud.timerText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            // แถบเวลาแบ่งเฟส 30|60 — ช่วงซ้าย (ฟ้า) = Planning 30s, ช่วงขวา (ส้ม) = Live 60s
            hud.planningSegBar = CreateSlider("PlanningSeg", dayPanel.transform, planningCol, new Vector2(-94, -41), new Vector2(92, 9));
            hud.planningSegBar.value = 0f;
            hud.liveSegBar = CreateSlider("LiveSeg", dayPanel.transform, liveCol, new Vector2(48, -41), new Vector2(184, 9));
            hud.liveSegBar.value = 0f;

            // ===== Live-phase banner (กลางจอ, เด้ง ~1.6s ตอนเข้า Live) =====
            var liveBanner = new GameObject("LivePhaseBanner", typeof(RectTransform));
            liveBanner.transform.SetParent(canvasGO.transform, false);
            var lbRect = liveBanner.GetComponent<RectTransform>();
            lbRect.anchorMin = new Vector2(0.5f, 0.5f);
            lbRect.anchorMax = new Vector2(0.5f, 0.5f);
            lbRect.pivot = new Vector2(0.5f, 0.5f);
            lbRect.anchoredPosition = new Vector2(0, 130);
            lbRect.sizeDelta = new Vector2(380, 60);
            var lbBg = liveBanner.AddComponent<Image>();
            lbBg.color = new Color(liveCol.r, liveCol.g, liveCol.b, 0.92f);
            var lbText = CreateText("BannerText", liveBanner.transform, font, "⚡ เริ่มเดินเครื่อง! (Live 60s)", 22, Vector2.zero, new Vector2(370, 56), TextAnchor.MiddleCenter);
            lbText.color = Color.white;
            lbText.fontStyle = FontStyle.Bold;
            hud.livePhaseBanner = liveBanner;
            liveBanner.SetActive(false);

            // ===== Tower panel (top-center, below day panel) — เลื่อนลงหลบแผงวันที่สูงขึ้น (plate + timer/แถบ) =====
            var towerPanel = CreatePanel("TowerPanel", canvasGO.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -118), new Vector2(320, 60));
            hud.towerPhaseText = CreateText("TowerPhaseText", towerPanel.transform, font, "CORE TOWER — Phase 1/3", 18, new Vector2(0, -2), new Vector2(320, 24), TextAnchor.UpperCenter);
            hud.towerProgressBar = CreateSlider("TowerProgressBar", towerPanel.transform, new Color(1f, 0.4f, 0.2f), new Vector2(0, -30), new Vector2(320, 20));

            // ===== Population panel (top-right) =====
            var popPanel = CreatePanel("PopulationPanel", canvasGO.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20, -20), new Vector2(260, 230));
            var popLayout = popPanel.AddComponent<VerticalLayoutGroup>();
            popLayout.spacing = 6f;
            popLayout.childControlHeight = false;
            popLayout.childForceExpandHeight = false;

            hud.populationText = CreateTextRow("PopulationText", popPanel.transform, font, "ประชากร 10/10  ·  W6 E2 M0 F2");
            hud.hopeText = CreateTextRow("HopeText", popPanel.transform, font, "Hope: 100");
            hud.hopeBar = CreateSliderRow("HopeBar", popPanel.transform, new Color(0.3f, 0.85f, 1f));

            // Knowledge (V4 §16) — ป้าย tier เริ่มที่ "Novice" (Gap G8: ไม่มี initial broadcast จึง bake ค่าเริ่มต้นไว้)
            hud.knowledgeText = CreateTextRow("KnowledgeText", popPanel.transform, font, "Knowledge: 0 / 100 · Novice", LoadIcon("Knowledge"));
            hud.knowledgeBar = CreateSliderRow("KnowledgeBar", popPanel.transform, new Color(0.62f, 0.5f, 1f));

            // ===== (ลบแล้ว) Train class buttons + Decree buttons =====
            // แผงฝึกคลาส (ฝึกวิศวกร/แพทย์/เกษตรกร) + แผงประกาศฉุกเฉิน ถูกลบออกจาก HUD ตามคำขอผู้ใช้
            // การฝึก Engineer ย้ายไปแผงห้องวิจัย (LabPanelUI · คลิกอาคาร Lab) แล้ว
            // hud.trainEngineerButton/trainMedicButton/trainFarmerButton/decree1Button/decree2Button = null
            // → UIManagerHUD.Start null-guard ไว้แล้ว ปล่อยไม่ผูกได้ปลอดภัย

            // ===== Speed controls (top-center ขวาของ DayPanel) =====
            // เดิมอยู่ bottom-center (0,20) ทับ BuildingSelectionPanel hotbar — ย้ายไปคู่กับนาฬิกาวัน
            var speedPanel = CreatePanel("SpeedPanel", canvasGO.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(280, -20), new Vector2(210, 50));
            hud.pauseButton  = CreateButton("PauseButton",  speedPanel.transform, font, "II", new Vector2(-70, 0), new Vector2(60, 40));
            hud.normalButton = CreateButton("NormalButton", speedPanel.transform, font, "1x", new Vector2(0, 0),   new Vector2(60, 40));
            hud.fastButton   = CreateButton("FastButton",   speedPanel.transform, font, "2x", new Vector2(70, 0),  new Vector2(60, 40));
            // ไอคอนกรอบ pause/play/ff จาก atlas (ถ้าไม่พบ คงตัวอักษร II/1x/2x เดิม)
            SetSpeedIcon(hud.pauseButton,  LoadIcon("SpeedPause"));
            SetSpeedIcon(hud.normalButton, LoadIcon("SpeedNormal"));
            SetSpeedIcon(hud.fastButton,   LoadIcon("SpeedFast"));

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

            // ===== Hotkey Help (ปุ่ม "คีย์ลัด" ซ้ายล่าง เหนือปุ่ม Codex + แผงสรุปปุ่ม เปิด/ปิดด้วย F1) =====
            SetupHotkeyHelp(canvasGO, font);

            // ===== (ลบแล้ว) CORE TOWER overclock panel ล่างจอ =====
            // แผงเตาล่างจอ (0x/1x/2x/3x/SCRAM + Toroidal/Poloidal) ถูกลบตามคำขอผู้ใช้
            // ระบบเตาย้ายไป CoreTowerPanelUI (runtime-built · เปิดด้วยคลิกตัวหอ CORE TOWER บนแมพ) แล้ว
            // CoreTowerUI เดิมถูกลบทิ้งตอนต้นเมธอด (ดู oldCoreUI cleanup)

            // ===== Building upgrade hover panel (ลอยเหนืออาคารที่ชี้) =====
            var upPanel = new GameObject("BuildingUpgradePanel", typeof(RectTransform));
            upPanel.transform.SetParent(canvasGO.transform, false);
            var upRect = upPanel.GetComponent<RectTransform>();
            upRect.anchorMin = Vector2.zero;
            upRect.anchorMax = Vector2.zero;
            upRect.pivot = new Vector2(0.5f, 0f); // ยึดขอบล่างกลาง → .position = จุดเหนือหัวอาคาร
            upRect.sizeDelta = new Vector2(264, 200);
            var upBg = upPanel.AddComponent<Image>();
            upBg.color = Palette.PanelBg;

            var upName = CreateText("BU_Name", upPanel.transform, font, "อาคาร", 16, new Vector2(0, 80), new Vector2(252, 24), TextAnchor.MiddleCenter);
            upName.color = Palette.TextPrimary; upName.fontStyle = FontStyle.Bold;
            var upLevel = CreateText("BU_Level", upPanel.transform, font, "Lv.1  ●○○", 15, new Vector2(0, 58), new Vector2(252, 22), TextAnchor.MiddleCenter);
            upLevel.color = Palette.TextPrimary;
            var upProd = CreateText("BU_Prod", upPanel.transform, font, "⚡0", 13, new Vector2(0, 38), new Vector2(252, 20), TextAnchor.MiddleCenter);
            upProd.color = Palette.TextMuted;
            var upHint = CreateText("BU_Hint", upPanel.transform, font, "", 11, new Vector2(0, 20), new Vector2(252, 18), TextAnchor.MiddleCenter);
            upHint.color = Palette.Accent;
            var upCost = CreateText("BU_Cost", upPanel.transform, font, "อัปเกรด: ⛏40", 14, new Vector2(0, 0), new Vector2(252, 20), TextAnchor.MiddleCenter);
            upCost.color = Palette.TextPrimary;

            var upBtn = CreateButton("BU_UpgradeButton", upPanel.transform, font, "⬆ อัปเกรด", new Vector2(0, -26), new Vector2(190, 32));
            upBtn.image.color = Palette.Accent;
            upBtn.transition = Selectable.Transition.ColorTint; // ให้ disabled หรี่เอง (ตอนแร่ไม่พอ)
            var upCb = upBtn.colors;
            upCb.normalColor = Color.white;
            upCb.highlightedColor = new Color(0.9f, 0.95f, 1f);
            upCb.pressedColor = new Color(0.8f, 0.85f, 0.9f);
            upCb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            upBtn.colors = upCb;
            var upBtnLabel = upBtn.GetComponentInChildren<Text>();
            if (upBtnLabel != null) { upBtnLabel.color = Color.white; upBtnLabel.fontSize = 16; }

            // ── แถวจัดสรรคนงาน (V4 §5): [−] คนงาน x/y ว่าง z [+] ──
            var upWorker = CreateText("BU_Worker", upPanel.transform, font, "👷 คนงาน 0/0   ว่าง 0", 13, new Vector2(0, -70), new Vector2(180, 22), TextAnchor.MiddleCenter);
            upWorker.color = Palette.TextPrimary;
            var upMinus = CreateButton("BU_WorkerMinus", upPanel.transform, font, "−", new Vector2(-106, -70), new Vector2(34, 30));
            StyleWorkerButton(upMinus, new Color(0.75f, 0.30f, 0.28f));
            var upPlus = CreateButton("BU_WorkerPlus", upPanel.transform, font, "+", new Vector2(106, -70), new Vector2(34, 30));
            StyleWorkerButton(upPlus, new Color(0.24f, 0.55f, 0.34f));

            // ── แถบความคืบหน้าก่อสร้าง (GDD §6): โชว์แทนปุ่มอัปเกรดตอนอาคารกำลังสร้าง ──
            var upConstruct = CreateSlider("BU_ConstructBar", upPanel.transform, new Color(0.3f, 0.75f, 0.35f), new Vector2(0, -26), new Vector2(190, 18));

            var buUIgo = GameObject.Find("BuildingUpgradeUI") ?? new GameObject("BuildingUpgradeUI");
            var buUI = buUIgo.GetComponent<BuildingUpgradeUI>() ?? buUIgo.AddComponent<BuildingUpgradeUI>();
            buUI.panel = upPanel;
            buUI.panelRect = upRect;
            buUI.nameText = upName;
            buUI.levelText = upLevel;
            buUI.productionText = upProd;
            buUI.costText = upCost;
            buUI.hintText = upHint;
            buUI.upgradeButton = upBtn;
            buUI.upgradeButtonLabel = upBtnLabel;
            buUI.workerText = upWorker;
            buUI.minusButton = upMinus;
            buUI.plusButton = upPlus;
            buUI.constructionBar = upConstruct;
            EditorUtility.SetDirty(buUI);
            upConstruct.gameObject.SetActive(false);
            upPanel.SetActive(false);

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

            // ===== Day 1 Tutorial checklist (GDD §3) — พาเนลมุมซ้าย ไม่บล็อกการเล่น =====
            // ตำแหน่ง y=107 = ที่ผู้ใช้จัดเอง (อ่านจากซีน 13 ก.ค.) · เดิม y=0 (กึ่งกลางซ้าย)
            var tutPanel = CreatePanel("TutorialChecklistPanel", canvasGO.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20, 107), new Vector2(340, 190));
            var tutBg = tutPanel.AddComponent<Image>();
            tutBg.color = Palette.PanelBg;
            var tutTitle = CreateText("TutTitle", tutPanel.transform, font, "ภารกิจ Day 1 — สอนเล่น", 16, new Vector2(0, 78), new Vector2(320, 24), TextAnchor.MiddleCenter);
            tutTitle.color = Palette.Accent; tutTitle.fontStyle = FontStyle.Bold;
            var tTask1 = CreateText("TutTask1", tutPanel.transform, font, "⬜ เดินโรงงานพื้นฐาน 3 โรง  0/3", 13, new Vector2(8, 46), new Vector2(320, 22), TextAnchor.MiddleLeft);
            var tTask2 = CreateText("TutTask2", tutPanel.transform, font, "⬜ ขยาย Shelter เพิ่มเพดานประชากร", 13, new Vector2(8, 20), new Vector2(320, 22), TextAnchor.MiddleLeft);
            var tTask3 = CreateText("TutTask3", tutPanel.transform, font, "⬜ จัดคนงานเข้าประจำอาคาร", 13, new Vector2(8, -6), new Vector2(320, 22), TextAnchor.MiddleLeft);

            var tStartBtn = CreateButton("TutStartButton", tutPanel.transform, font, "ทำภารกิจให้ครบ (0/3)", new Vector2(0, -58), new Vector2(300, 40));
            tStartBtn.image.color = Palette.Accent;
            tStartBtn.transition = Selectable.Transition.ColorTint; // ล็อกอยู่ → หรี่จนทำครบ
            var tCb = tStartBtn.colors;
            tCb.normalColor = Color.white;
            tCb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            tStartBtn.colors = tCb;
            var tStartLabel = tStartBtn.GetComponentInChildren<Text>();
            if (tStartLabel != null) { tStartLabel.color = Color.white; tStartLabel.fontSize = 15; }

            var tutGo = GameObject.Find("TutorialManager") ?? new GameObject("TutorialManager");
            var tut = tutGo.GetComponent<TutorialManager>() ?? tutGo.AddComponent<TutorialManager>();
            if (tut.tutorialPanel != null && tut.tutorialPanel != tutPanel)
                Undo.DestroyObjectImmediate(tut.tutorialPanel); // ลบ popup tutorial เดิม (ถ้ายังลอยอยู่)
            tut.tutorialPanel = tutPanel;
            tut.task1Text = tTask1;
            tut.task2Text = tTask2;
            tut.task3Text = tTask3;
            tut.startButton = tStartBtn;
            tut.startLabel = tStartLabel;
            EditorUtility.SetDirty(tut);

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
            // สกินโลหะ (จาก Codex/StoryUI) — null-safe: ถ้ายังไม่ import ให้ fallback สีทึบ
            var frameMetal = LoadUISkin("Assets/Resources/CodexUI/frame_metal.png");
            var plateInset = LoadUISkin("Assets/Resources/StoryUI/plate_inset.png");
            var bookIcon   = LoadUISkin("Assets/Resources/CodexUI/header_book.png");

            var gold  = new Color(0.94f, 0.77f, 0.29f);
            var blue  = new Color(0.59f, 0.80f, 1f);
            var gray  = new Color(0.62f, 0.62f, 0.66f);
            var light = new Color(0.90f, 0.90f, 0.86f);
            var chip  = new Color(0.20f, 0.47f, 0.80f);

            const float DW = 1220f, DH = 700f;

            // วางแบบ pin มุมบนซ้ายของ dialog (x จากซ้าย, y จากบน)
            void TL(Component c, float x, float y, float w, float h)
            {
                var r = c.GetComponent<RectTransform>();
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 1f);
                r.anchoredPosition = new Vector2(x, -y);
                r.sizeDelta = new Vector2(w, h);
            }
            Image Plate(string name, Transform parent, float x, float y, float w, float h)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var img = go.AddComponent<Image>();
                if (plateInset != null) { img.sprite = plateInset; img.type = Image.Type.Sliced; img.color = Color.white; }
                else img.color = new Color(0.12f, 0.13f, 0.17f, 0.98f);
                TL(img, x, y, w, h);
                return img;
            }
            Text Label(string name, Transform parent, string s, int size, Color col, TextAnchor anchor,
                       float x, float y, float w, float h, bool bold = false)
            {
                var t = CreateText(name, parent, font, s, size, Vector2.zero, Vector2.zero, anchor);
                t.color = col; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                TL(t, x, y, w, h);
                return t;
            }

            // ===== Full-screen overlay (มืด) =====
            var overlay = CreatePanel("QuizPopupPanel", canvasTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.74f);

            // ===== Dialog (กรอบโลหะ 9-slice, กลางจอ) =====
            var dialog = CreatePanel("QuizDialog", overlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(DW, DH));
            var dialogRect = dialog.GetComponent<RectTransform>();
            dialogRect.pivot = new Vector2(0.5f, 0.5f);
            var dialogImage = dialog.AddComponent<Image>();
            if (frameMetal != null) { dialogImage.sprite = frameMetal; dialogImage.type = Image.Type.Sliced; dialogImage.color = Color.white; }
            else dialogImage.color = new Color(0.10f, 0.11f, 0.14f, 0.98f);

            // แถบสีหมวด (แท่งตั้งซ้ายหัวเรื่อง) — controller เปลี่ยนสีตาม category
            var categoryBar = new GameObject("QuizCategoryBar", typeof(RectTransform)).AddComponent<Image>();
            categoryBar.transform.SetParent(dialog.transform, false);
            categoryBar.color = chip;
            TL(categoryBar, 76, 70, 8, 112);

            // ── LEFT: หัวข้อ + ผู้พูด + โจทย์ ──
            Label("QuizKicker", dialog.transform, "หัวข้อควิซ", 19, gray, TextAnchor.UpperLeft, 98, 66, 520, 24);
            var topicText = Label("QuizTopicText", dialog.transform, "หัวข้อ", 40, gold, TextAnchor.UpperLeft, 98, 92, 548, 58, true);
            var speakerText = Label("QuizSpeakerText", dialog.transform, "VESTA", 22, blue, TextAnchor.UpperLeft, 98, 158, 548, 30, true);

            var qPlate = Plate("QuizQuestionPlate", dialog.transform, 76, 204, 560, 360);
            Label("QuizQuestionLabel", qPlate.transform, "โจทย์", 20, gray, TextAnchor.UpperLeft, 24, 18, 200, 24);
            var questionText = Label("QuizQuestionText", qPlate.transform, "คำถาม", 25, light, TextAnchor.UpperLeft, 24, 54, 512, 150);
            var explainText = Label("QuizExplainText", qPlate.transform, "", 20, new Color(0.88f, 0.90f, 0.78f), TextAnchor.UpperLeft, 24, 202, 512, 150);
            explainText.gameObject.SetActive(false);

            // ── RIGHT: ตัวเลือก 3 ปุ่ม (แผ่นจม + letter chip + เรืองขอบ Outline) ──
            var optionButtons = new Button[3];
            var optionTexts = new Text[3];
            const float rx = 668f, rw = DW - 54f - 668f, ah = 118f, gap = 14f, ry0 = 182f;
            for (int i = 0; i < 3; i++)
            {
                float y = ry0 + i * (ah + gap);
                var plate = Plate($"QuizOption{i}", dialog.transform, rx, y, rw, ah);
                var btn = plate.gameObject.AddComponent<Button>();
                btn.targetGraphic = plate;
                btn.transition = Selectable.Transition.None; // controller คุมสี/เรืองขอบเอง
                var outline = plate.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.95f, 0.80f, 0.30f);
                outline.effectDistance = new Vector2(3f, -3f);
                outline.enabled = false;

                // chip ตัวอักษร A/B/C
                var chipImg = new GameObject("Letter", typeof(RectTransform)).AddComponent<Image>();
                chipImg.transform.SetParent(plate.transform, false);
                chipImg.color = chip;
                var cr = chipImg.GetComponent<RectTransform>();
                cr.anchorMin = cr.anchorMax = new Vector2(0f, 0.5f);
                cr.pivot = new Vector2(0f, 0.5f);
                cr.anchoredPosition = new Vector2(16, 0);
                cr.sizeDelta = new Vector2(44, 44);
                var cl = CreateText("L", chipImg.transform, font, ((char)('A' + i)).ToString(), 28, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
                cl.fontStyle = FontStyle.Bold; cl.color = new Color(0.95f, 0.96f, 1f);
                var clr = cl.GetComponent<RectTransform>();
                clr.anchorMin = Vector2.zero; clr.anchorMax = Vector2.one; clr.offsetMin = Vector2.zero; clr.offsetMax = Vector2.zero;

                optionTexts[i] = Label($"QuizOptionText{i}", plate.transform, $"ตัวเลือก {i + 1}", 22, new Color(0.90f, 0.90f, 0.86f), TextAnchor.MiddleLeft, 76, 0, rw - 96, ah);
                // MiddleLeft เต็มความสูงปุ่ม → จัดกลางแนวตั้ง
                var otr = optionTexts[i].GetComponent<RectTransform>();
                otr.anchorMin = otr.anchorMax = new Vector2(0f, 0.5f);
                otr.pivot = new Vector2(0f, 0.5f);
                otr.anchoredPosition = new Vector2(76, 0);
                optionButtons[i] = btn;
            }

            // ── footer "ปลดล็อก Codex : …" (ซ่อนถ้าไม่มี reward) ──
            var codexFooter = Plate("QuizCodexFooter", dialog.transform, 76, 590, 560, 54).gameObject;
            var fIcon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
            fIcon.transform.SetParent(codexFooter.transform, false);
            if (bookIcon != null) { fIcon.sprite = bookIcon; fIcon.preserveAspect = true; } else fIcon.color = chip;
            var fir = fIcon.GetComponent<RectTransform>();
            fir.anchorMin = fir.anchorMax = new Vector2(0f, 0.5f); fir.pivot = new Vector2(0f, 0.5f);
            fir.anchoredPosition = new Vector2(12, 0); fir.sizeDelta = new Vector2(38, 38);
            Label("QuizCodexLabel", codexFooter.transform, "ปลดล็อก Codex :", 19, gray, TextAnchor.MiddleLeft, 60, 0, 185, 54);
            var codexRewardText = Label("QuizCodexName", codexFooter.transform, "—", 20, blue, TextAnchor.MiddleLeft, 248, 0, 300, 54, true);
            // ปรับ label/name ให้จัดกลางแนวตั้ง (MiddleLeft เต็มสูง footer)
            foreach (var nm in new[] { "QuizCodexLabel", "QuizCodexName" })
            {
                var rt = codexFooter.transform.Find(nm).GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(nm == "QuizCodexLabel" ? 60 : 248, 0);
            }

            // ── ปุ่ม ยืนยัน / ปิด (ตำแหน่งเดียวกัน มุมล่างขวา) ──
            var confirmButton = QuizFooterButton("QuizConfirmButton", dialog.transform, font, "ยืนยัน", plateInset, gold, DW - 54f - 220f, 590);
            var closeButton = QuizFooterButton("QuizCloseButton", dialog.transform, font, "ปิด", plateInset, gold, DW - 54f - 220f, 590);
            closeButton.gameObject.SetActive(false);
            void _place(Component c) => TL(c, DW - 54f - 220f, 590, 220, 54);
            _place(confirmButton); _place(closeButton);

            overlay.SetActive(false);

            // ===== QuizPopupController (GameObject เดี่ยว — รอด re-run) =====
            var quizGO = GameObject.Find("QuizPopupController") ?? new GameObject("QuizPopupController");
            var quiz = quizGO.GetComponent<QuizPopupController>() ?? quizGO.AddComponent<QuizPopupController>();
            quiz.popupPanel = overlay;
            quiz.categoryBar = categoryBar;
            quiz.topicText = topicText;
            quiz.speakerText = speakerText;
            quiz.questionText = questionText;
            quiz.explainText = explainText;
            quiz.optionButtons = optionButtons;
            quiz.optionTexts = optionTexts;
            quiz.confirmButton = confirmButton;
            quiz.closeButton = closeButton;
            quiz.codexFooter = codexFooter;
            quiz.codexRewardText = codexRewardText;

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

        // ===== Hotkey Help: แผงสรุปคีย์ลัด (กลางจอ + ฉากหลังทึบ) + ปุ่ม toggle + F1 =====
        // ดีไซน์ใหม่: แผงกลางจอบนฉากหลังมืด — ไม่ทับ/ไม่ถูกทับกับปุ่มมุมจออีก (ผู้ใช้ขอ #5)
        private static void SetupHotkeyHelp(GameObject canvasGO, Font font)
        {
            // ฉากหลังเต็มจอมืดโปร่ง — เป็น root ของหน้าต่าง (toggle ตัวนี้ = เปิด/ปิดทั้งหน้าต่าง)
            var backdrop = CreatePanel("HotkeyHelpPanel", canvasGO.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bdRect = backdrop.GetComponent<RectTransform>();
            bdRect.anchorMin = Vector2.zero; bdRect.anchorMax = Vector2.one;
            bdRect.offsetMin = Vector2.zero; bdRect.offsetMax = Vector2.zero;
            var bdImg = backdrop.AddComponent<Image>();
            bdImg.color = new Color(0f, 0f, 0f, 0.6f);
            // คลิกฉากหลังเพื่อปิด (สะดวก · ไม่หยุดเวลาอยู่แล้ว)
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;

            // แผงจริง — กึ่งกลางจอ ทึบ + ขอบ
            var panel = CreatePanel("HotkeyHelpBox", backdrop.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 560));
            var pRect = panel.GetComponent<RectTransform>();
            pRect.pivot = new Vector2(0.5f, 0.5f);
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.07f, 0.12f, 0.98f);
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.6f, 0.85f, 1f);
            outline.effectDistance = new Vector2(2.5f, 2.5f);
            outline.useGraphicAlpha = false;

            var title = CreateText("HelpTitle", panel.transform, font, "คีย์ลัด", 26, new Vector2(0, -18), new Vector2(520, 34), TextAnchor.MiddleCenter);
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f); titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            title.color = new Color(0.5f, 0.9f, 1f);
            title.fontStyle = FontStyle.Bold;

            var body = CreateText("HelpBody", panel.transform, font, HotkeyHelpText(), 18, new Vector2(0, -62), new Vector2(500, 440), TextAnchor.UpperLeft);
            var bodyRect = body.GetComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0.5f, 1f); bodyRect.anchorMax = new Vector2(0.5f, 1f);
            bodyRect.pivot = new Vector2(0.5f, 1f);
            body.lineSpacing = 1.28f;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            var hint = CreateText("HelpHint", panel.transform, font, "กด F1 หรือคลิกนอกกรอบเพื่อปิด", 15, new Vector2(0, 14), new Vector2(520, 24), TextAnchor.LowerCenter);
            var hintRect = hint.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 0f); hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hint.color = new Color(0.55f, 0.62f, 0.7f);

            // ปุ่ม toggle — ซ้ายล่าง เหนือปุ่ม Codex (Codex อยู่ (20,210) สูง 36 — CodexSetup สร้างทีหลังในลำดับ chain)
            var toggleBtn = CreateButton("HotkeyHelpButton", canvasGO.transform, font, "คีย์ลัด (F1)", new Vector2(20, 254), new Vector2(120, 36));
            var btnRect = toggleBtn.GetComponent<RectTransform>();
            btnRect.anchorMin = Vector2.zero; btnRect.anchorMax = Vector2.zero;
            btnRect.pivot = Vector2.zero;
            toggleBtn.image.color = new Color(0.1f, 0.15f, 0.28f, 0.9f);
            var btnLabel = toggleBtn.GetComponentInChildren<Text>();
            if (btnLabel != null) { btnLabel.fontSize = 15; btnLabel.color = Color.white; }

            var helpGO = GameObject.Find("HotkeyHelpController") ?? new GameObject("HotkeyHelpController");
            var help = helpGO.GetComponent<HotkeyHelpController>() ?? helpGO.AddComponent<HotkeyHelpController>();
            help.helpPanel = backdrop;
            UnityEventTools.AddPersistentListener(toggleBtn.onClick, new UnityAction(help.Toggle));
            UnityEventTools.AddPersistentListener(bdBtn.onClick, new UnityAction(help.Toggle));
            EditorUtility.SetDirty(help);

            backdrop.SetActive(false); // เริ่มซ่อน — เปิดด้วยปุ่ม/F1
        }

        private static string HotkeyHelpText() =>
            "การวางอาคาร\n" +
            "   1–7 — เลือกอาคารจากแถบล่าง\n" +
            "   คลิกซ้าย — วางอาคาร / ยืนยัน\n" +
            "   คลิกขวา — ยกเลิกการวาง / ออกโหมดทุบ\n" +
            "   U — อัประดับอาคารใต้เมาส์ (จ่ายแร่+พลังงาน)\n" +
            "   Q / E — ลด / เพิ่มคนงานประจำอาคารใต้เมาส์\n" +
            "   B — พับ / กางแถบเลือกอาคาร\n\n" +
            "กล้อง\n" +
            "   WASD / ลูกศร — เลื่อนกล้อง\n" +
            "   คลิกกลางค้างลาก — จับแมพเลื่อน\n" +
            "   Scroll — ซูมเข้าหาตำแหน่งเมาส์\n\n" +
            "เกม\n" +
            "   Space — หยุด / เล่นต่อ\n" +
            "   ESC — เมนูหยุดชั่วคราว\n" +
            "   F5 / F9 — บันทึก / โหลดเกม\n" +
            "   F1 — เปิด/ปิดหน้าต่างนี้";

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
            }
            else
            {
                var iconText = CreateText(name + "Icon", row.transform, font, icon, 18, new Vector2(2, 0), new Vector2(24, 24), TextAnchor.MiddleCenter);
                iconRect = iconText.GetComponent<RectTransform>();
            }
            // กล่อง icon เท่ากันทุกแถว (sprite/text) → จุดกึ่งกลางตรงกันทั้งคอลัมน์
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(2, 0);
            iconRect.sizeDelta = new Vector2(24, 24);

            var slider = CreateSlider(name + "Slider", row.transform, fillColor, new Vector2(28, 0), new Vector2(150, 20));
            var sliderRect = slider.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 0.5f);
            sliderRect.anchorMax = new Vector2(0f, 0.5f);
            sliderRect.pivot = new Vector2(0f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(28, 0);
            var fillImage = slider.transform.Find("Fill Area/Fill").GetComponent<Image>();

            var valueText = CreateText(name + "ValueText", row.transform, font, "0", 16, new Vector2(0, 0), new Vector2(80, 24), TextAnchor.MiddleRight);
            var valueRect = valueText.GetComponent<RectTransform>();
            valueRect.anchorMin = new Vector2(1f, 0.5f);
            valueRect.anchorMax = new Vector2(1f, 0.5f);
            valueRect.pivot = new Vector2(1f, 0.5f);
            valueRect.anchoredPosition = new Vector2(0, 0);

            return new UIManagerHUD.ResourceBarUI
            {
                bar = slider,
                valueText = valueText,
                fillImage = fillImage
            };
        }

        private static Text CreateTextRow(string name, Transform parent, Font font, string content, Sprite icon = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260, 24);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleRight;  // ข้อความชิดขวา — icon ซ้ายสุดไม่ทับ
            text.text = content;
            text.verticalOverflow = VerticalWrapMode.Overflow; // กัน Kanit โดน truncate ทั้งบรรทัด

            // icon อาร์ตจริง (optional) — วางชิดซ้ายของแถว
            if (icon != null)
            {
                var iconGO = new GameObject(name + "Icon", typeof(RectTransform));
                iconGO.transform.SetParent(go.transform, false);
                var img = iconGO.AddComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                var ir = iconGO.GetComponent<RectTransform>();
                ir.anchorMin = new Vector2(0f, 0.5f);
                ir.anchorMax = new Vector2(0f, 0.5f);
                ir.pivot = new Vector2(0f, 0.5f);
                ir.anchoredPosition = new Vector2(2, 0);
                ir.sizeDelta = new Vector2(22, 22);
            }
            return text;
        }

        // ตั้งไอคอนกรอบให้ปุ่ม speed (sprite รวมกรอบมาแล้ว) แล้วซ่อนตัวอักษร II/1x/2x
        private static void SetSpeedIcon(Button btn, Sprite icon)
        {
            if (btn == null || icon == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) { img.sprite = icon; img.color = Color.white; }
            var label = btn.GetComponentInChildren<Text>();
            if (label != null) label.text = "";
        }

        // โหลด icon อาร์ตจริงจาก Assets/Sprites/Icons/<fileName>.png
        // ไม่พบ → placeholder (ถ้าระบุ) → null (CreateResourceBar จะ fallback เป็น emoji/text)
        private static Sprite LoadIcon(string fileName, string placeholderName = null)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Icons/" + fileName + ".png");
            if (s != null) return s;
            return placeholderName != null ? PlaceholderSpriteGenerator.EnsureIconSprite(placeholderName) : null;
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

        // โหลดสไปรต์สกิน (null ถ้ายังไม่ import — ผู้เรียกต้อง fallback สีทึบเอง)
        private static Sprite LoadUISkin(string assetPath)
            => AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

        // ปุ่มโลหะของ Quiz footer: แผ่นจม 9-slice + label ทองกลางปุ่ม (fallback สีทึบถ้าไม่มีสกิน)
        private static Button QuizFooterButton(string name, Transform parent, Font font, string label, Sprite plate, Color labelColor, float x, float y)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            if (plate != null) { img.sprite = plate; img.type = Image.Type.Sliced; img.color = Color.white; }
            else img.color = new Color(0.16f, 0.17f, 0.21f, 0.98f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.highlightedColor = new Color(0.82f, 0.82f, 0.82f); cb.pressedColor = new Color(0.66f, 0.66f, 0.66f);
            btn.colors = cb;

            var t = CreateText("Label", go.transform, font, label, 26, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            t.fontStyle = FontStyle.Bold; t.color = labelColor;
            var tr = t.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            return btn;
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

        // ลดขนาด font ของ label ปุ่ม (ปุ่มแคบ — ข้อความไทยยาวจะล้น/โดน truncate)
        private static void ShrinkButtonLabel(Button btn, int fontSize)
        {
            var label = btn != null ? btn.GetComponentInChildren<Text>() : null;
            if (label != null) label.fontSize = fontSize;
        }

        // ปุ่ม −/+ จัดสรรคนงาน: สีพื้น + label ขาวตัวใหญ่ + หรี่เองตอน disabled (assigned เต็ม/ไม่มี idle)
        private static void StyleWorkerButton(Button btn, Color bg)
        {
            btn.image.color = bg;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.92f, 0.96f, 1f);
            cb.pressedColor = new Color(0.8f, 0.85f, 0.9f);
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            btn.colors = cb;
            var label = btn.GetComponentInChildren<Text>();
            if (label != null) { label.color = Color.white; label.fontSize = 22; }
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
