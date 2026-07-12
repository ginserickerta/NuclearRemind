using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// สร้างระบบเมนู 2 ส่วน:
    ///   • เมนูหลัก (ซีน MainMenu.unity ใหม่ — build index 0) : เริ่มเกม / ออก
    ///   • เมนูหยุดชั่วคราว (overlay ในซีนเกม กด ESC) : เล่นต่อ / เริ่มใหม่ / กลับเมนูหลัก / ออก
    ///
    /// เมนูหยุดอยู่บน Canvas แยก (PauseCanvas) จึงไม่โดน Setup HUD Canvas ลบทิ้ง — รันซ้ำได้
    /// </summary>
    public static class MenuSystemSetup
    {
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string GameScenePath = "Assets/Scenes/Gamescene.unity";

        // ── Pause menu art: ปุ่มแยก 4 อัน (สไปรต์ ~1600×320 · ข้อความ+กรอบ baked ในภาพ) ──
        private const string BtnResumePath   = "Assets/Sprites/UI/pause_btn_resume.png";
        private const string BtnRestartPath  = "Assets/Sprites/UI/pause_btn_restart.png";
        private const string BtnMainMenuPath = "Assets/Sprites/UI/pause_btn_mainmenu.png";
        private const string BtnQuitPath     = "Assets/Sprites/UI/pause_btn_quit.png";
        // y-offset ของ 4 ปุ่มจากกลางจอ (pitch 126) — เล่นต่อ/เริ่มใหม่/กลับเมนู/ออก · จูนได้
        private static readonly float[] ButtonY = { 189f, 63f, -63f, -189f };

        // ─────────────────────────────────────────────────────────────
        //  เมนูรวม
        // ─────────────────────────────────────────────────────────────

        [MenuItem("NuclearReMind/Setup Menu System (Main + Pause)")]
        public static void SetupAll()
        {
            SetupPauseMenu();
            SetupMainMenuScene();
            EditorUtility.DisplayDialog("Menu System",
                "สร้างเสร็จแล้ว:\n\n" +
                "  • MainMenu.unity (build index 0) — เมนูหลัก\n" +
                "  • PauseCanvas ใน Gamescene — กด ESC ตอนเล่น\n\n" +
                "ทดสอบ: เปิด MainMenu.unity แล้วกด Play (หรือ build จะเริ่มที่เมนูหลักเอง)\n" +
                "อย่าลืม Save Scene (Ctrl+S) ที่ Gamescene", "OK");
        }

        // ─────────────────────────────────────────────────────────────
        //  1. เมนูหยุดชั่วคราว (overlay ในซีนเกมที่เปิดอยู่)
        // ─────────────────────────────────────────────────────────────

        [MenuItem("NuclearReMind/Setup Pause Menu")]
        public static void SetupPauseMenu()
        {
            var font = LoadFont();

            // regenerate — ลบ PauseCanvas เดิม (รันซ้ำได้ ; Ctrl+Z คืน)
            var existing = GameObject.Find("PauseCanvas");
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var canvasGO = new GameObject("PauseCanvas", typeof(RectTransform));
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // เหนือ HUDCanvas (0)
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UIScaleSetup.ReferenceResolution;
            canvasGO.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            // พื้นหลัง Pause: สีทึบโปร่งแสง (semi-transparent solid — บล็อกคลิกทะลุไป HUD ด้วย)
            var panel = NewUI("PausePanel", canvasGO.transform);
            Stretch(panel);
            panel.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.82f);

            var controller = canvasGO.AddComponent<PauseMenuController>();
            controller.pausePanel = panel;

            var title = MakeText(panel.transform, "Title", "หยุดชั่วคราว", 46, new Color(0.55f, 0.9f, 1f),
                TextAnchor.MiddleCenter, font);
            Center(title, new Vector2(0, 300), new Vector2(760, 80));
            title.fontStyle = FontStyle.Bold;

            // ปุ่ม = สไปรต์แยก 4 อัน (ข้อความ baked ในภาพ) วางเป็น Button จริงบนพื้นหลังโปร่งแสง
            var resumeSp   = AssetDatabase.LoadAssetAtPath<Sprite>(BtnResumePath);
            var restartSp  = AssetDatabase.LoadAssetAtPath<Sprite>(BtnRestartPath);
            var mainmenuSp = AssetDatabase.LoadAssetAtPath<Sprite>(BtnMainMenuPath);
            var quitSp     = AssetDatabase.LoadAssetAtPath<Sprite>(BtnQuitPath);

            Button resume, restart, toMenu, quit;
            if (resumeSp != null && restartSp != null && mainmenuSp != null && quitSp != null)
            {
                var btnSize = new Vector2(560f, 112f); // ~อัตราส่วนภาพ 5:1 (preserveAspect)
                resume  = MakeSpriteButton(panel.transform, "ResumeButton",   resumeSp,   new Vector2(0, ButtonY[0]), btnSize);
                restart = MakeSpriteButton(panel.transform, "RestartButton",  restartSp,  new Vector2(0, ButtonY[1]), btnSize);
                toMenu  = MakeSpriteButton(panel.transform, "MainMenuButton", mainmenuSp, new Vector2(0, ButtonY[2]), btnSize);
                quit    = MakeSpriteButton(panel.transform, "QuitButton",     quitSp,     new Vector2(0, ButtonY[3]), btnSize);
            }
            else
            {
                // fallback (ยังไม่ import art) — ปุ่มสี+ข้อความแบบเดิม · รัน Setup ซ้ำหลัง Unity import
                Debug.LogWarning("[MenuSystemSetup] ไม่พบสไปรต์ปุ่ม Pause — ใช้ปุ่มแบบเรียบชั่วคราว");
                var fb = new Vector2(360f, 84f);
                resume  = MakeButton(panel.transform, "ResumeButton",   "เล่นต่อ",     fb, new Color(0.16f, 0.45f, 0.20f), font);
                restart = MakeButton(panel.transform, "RestartButton",  "เริ่มใหม่",    fb, new Color(0.20f, 0.35f, 0.55f), font);
                toMenu  = MakeButton(panel.transform, "MainMenuButton", "กลับเมนูหลัก", fb, new Color(0.30f, 0.30f, 0.36f), font);
                quit    = MakeButton(panel.transform, "QuitButton",     "ออกจากเกม",    fb, new Color(0.50f, 0.16f, 0.16f), font);
                Center(resume,  new Vector2(0, ButtonY[0]), fb);
                Center(restart, new Vector2(0, ButtonY[1]), fb);
                Center(toMenu,  new Vector2(0, ButtonY[2]), fb);
                Center(quit,    new Vector2(0, ButtonY[3]), fb);
            }

            UnityEventTools.AddPersistentListener(resume.onClick,  new UnityAction(controller.Resume));
            UnityEventTools.AddPersistentListener(restart.onClick, new UnityAction(controller.Restart));
            UnityEventTools.AddPersistentListener(toMenu.onClick,  new UnityAction(controller.GoToMainMenu));
            UnityEventTools.AddPersistentListener(quit.onClick,    new UnityAction(controller.QuitGame));

            panel.SetActive(false); // เริ่มซ่อน — ESC เปิด
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[MenuSystemSetup] สร้าง PauseCanvas + PauseMenuController แล้ว (กด ESC ทดสอบ)");
        }

        // ─────────────────────────────────────────────────────────────
        //  2. ซีนเมนูหลัก (สร้างแบบ additive — ไม่แตะ Gamescene ที่เปิดอยู่)
        // ─────────────────────────────────────────────────────────────

        [MenuItem("NuclearReMind/Setup Main Menu Scene")]
        public static void SetupMainMenuScene()
        {
            var font = LoadFont();
            var prev = EditorSceneManager.GetActiveScene();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene); // ให้ GameObject ใหม่ตกลงซีนนี้

            BuildMainMenu(font);

            EditorSceneManager.SaveScene(scene, MainMenuPath);
            if (prev.IsValid()) SceneManager.SetActiveScene(prev);
            EditorSceneManager.CloseScene(scene, true); // ปิดซีนเมนู เหลือ Gamescene เปิดอยู่

            AddScenesToBuildSettings();
            Debug.Log($"[MenuSystemSetup] สร้าง {MainMenuPath} + ตั้ง build index 0 แล้ว");
        }

        private static void BuildMainMenu(Font font)
        {
            // กล้อง (กัน "No cameras rendering" + สีพื้นหลัง)
            var camGO = new GameObject("Main Camera");
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.09f, 1f);
            camGO.tag = "MainCamera";
            camGO.AddComponent<AudioListener>();

            var canvasGO = new GameObject("MenuCanvas", typeof(RectTransform));
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UIScaleSetup.ReferenceResolution;
            canvasGO.AddComponent<GraphicRaycaster>();

            // ⚠ ห้ามใช้ EnsureEventSystem ที่นี่ — ตอน setup ซีนนี้ถูกสร้าง additive ขณะ Gamescene
            // ยังเปิดอยู่ FindFirstObjectByType จะไปเจอ EventSystem ของ Gamescene แล้วข้ามการสร้าง
            // → MainMenu.unity ไม่มี EventSystem = ปุ่มกดไม่ได้ทั้งจอ · ซีนใหม่ว่างเปล่า สร้างเสมอ
            CreateEventSystem();

            // พื้นหลังไล่เฉดเข้ม
            var bg = NewUI("Background", canvasGO.transform);
            Stretch(bg);
            bg.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.13f, 1f);

            // โลโก้/ชื่อเกม
            var title = MakeText(canvasGO.transform, "Title", "Nuclear Re:Mind", 84,
                new Color(0.55f, 0.9f, 1f), TextAnchor.MiddleCenter, font);
            Anchor(title, new Vector2(0.5f, 1f), new Vector2(0, -230), new Vector2(1200, 120));
            title.fontStyle = FontStyle.Bold;

            var subtitle = MakeText(canvasGO.transform, "Subtitle", "นิวเคลียร์เปลี่ยนความคิดโลก", 34,
                new Color(0.8f, 0.85f, 0.95f), TextAnchor.MiddleCenter, font);
            Anchor(subtitle, new Vector2(0.5f, 1f), new Vector2(0, -330), new Vector2(1000, 60));

            // ปุ่ม
            var controllerGO = new GameObject("MainMenuController");
            var controller = controllerGO.AddComponent<MainMenuController>();

            var btnSize = new Vector2(360, 66);
            var newGame = MakeButton(canvasGO.transform, "NewGameButton", "เริ่มเกมใหม่", btnSize,
                new Color(0.16f, 0.45f, 0.22f), font);
            Center(newGame, new Vector2(0, -20), btnSize);

            var quit = MakeButton(canvasGO.transform, "QuitButton", "ออกจากเกม", btnSize,
                new Color(0.45f, 0.17f, 0.17f), font);
            Center(quit, new Vector2(0, -110), btnSize);

            UnityEventTools.AddPersistentListener(newGame.onClick, new UnityAction(controller.NewGame));
            UnityEventTools.AddPersistentListener(quit.onClick,    new UnityAction(controller.QuitGame));

            // เครดิตล่าง
            var credit = MakeText(canvasGO.transform, "Credit", "NSC 2026 · ทีมพัฒนา Veltara", 22,
                new Color(0.5f, 0.55f, 0.65f), TextAnchor.MiddleCenter, font);
            Anchor(credit, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(800, 40));
        }

        private static void AddScenesToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MainMenuPath, true),   // index 0 = โหลดตอนเปิดเกม
                new EditorBuildSettingsScene(GameScenePath, true),  // index 1
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ─────────────────────────────────────────────────────────────
        //  UI helpers
        // ─────────────────────────────────────────────────────────────

        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static Text MakeText(Transform parent, string name, string content, int size,
            Color color, TextAnchor anchor, Font font)
        {
            var go = NewUI(name, parent);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.text = content;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow; // กัน Kanit โดน truncate ทั้งบรรทัด
            return t;
        }

        private static Button MakeButton(Transform parent, string name, string label, Vector2 size,
            Color bg, Font font)
        {
            var go = NewUI(name, parent);
            go.GetComponent<RectTransform>().sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = bg;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var lbl = MakeText(go.transform, "Label", label, 24, Color.white, TextAnchor.MiddleCenter, font);
            lbl.fontStyle = FontStyle.Bold;
            Stretch(lbl.gameObject);
            return btn;
        }

        // ปุ่มจากสไปรต์ (ข้อความ baked ในภาพ) — hover/press = มืดลงเล็กน้อยเป็น feedback (ColorTint)
        private static Button MakeSpriteButton(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size)
        {
            var go = NewUI(name, parent);
            Center(go.transform, pos, size);

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.color = Color.white;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(0.86f, 0.86f, 0.86f, 1f); // hover มืดลงนิด
            cb.pressedColor     = new Color(0.70f, 0.70f, 0.70f, 1f); // กดมืดลงชัด
            cb.selectedColor    = Color.white;
            cb.disabledColor    = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            cb.colorMultiplier  = 1f;
            cb.fadeDuration     = 0.08f;
            btn.colors = cb;
            return btn;
        }

        private static void Stretch(GameObject go)
        {
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        private static void Center(Component c, Vector2 pos, Vector2 size)
        {
            var r = c.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        private static void Anchor(Component c, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var r = c.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = anchor;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        // ใช้ได้เฉพาะซีนเกม (เช็กว่ามีอยู่แล้วข้ามได้) — ซีนใหม่ที่สร้าง additive ต้อง CreateEventSystem ตรง
        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            CreateEventSystem();
        }

        private static void CreateEventSystem()
        {
            var es = new GameObject("EventSystem", typeof(RectTransform));
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private static Font LoadFont()
        {
            var kanit = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Kanit-Regular.ttf");
            if (kanit != null) return kanit;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
