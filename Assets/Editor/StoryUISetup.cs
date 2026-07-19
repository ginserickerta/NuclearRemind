using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Setup UI ระบบเนื้อเรื่อง (เฟส 3 — Story Guide §2) รันผ่าน NuclearReMind / Setup Story UI
    /// สร้างบน "StoryCanvas" แยกจาก HUDCanvas → รัน Setup HUD Canvas ซ้ำแล้ว story UI ไม่โดนลบ
    /// ชิ้นส่วน:
    ///   1. StoryDirector (GameObject เดี่ยว — beats wire โดย Story Setup เฟส 4)
    ///   2. การ์ดเนื้อเรื่องกลางจอ + CardUIController (record / info / outcome)
    ///   2b. บทสนทนาหลายตัวละคร VN (portrait ซ้าย/ขวา + บอลลูน) + DialogueUIController (v8.5)
    ///   3. แผง Records ย้อนอ่านบันทึก + ปุ่ม "บันทึก" ซ้ายล่าง + RecordsPanelController
    ///   4. แผงอนุสรณ์ + MemorialPanelController (ตึก/ข้อมูล wire โดยเฟส 4)
    /// ปุ่มทุกปุ่ม wire onClick ตอน runtime ใน controller.Start — รัน setup ซ้ำได้ไม่มี listener ซ้ำ
    /// </summary>
    public static class StoryUISetup
    {
        [MenuItem("NuclearReMind/Setup Story UI")]
        public static void Setup()
        {
            var font = LoadFont();

            // ลบ StoryCanvas เก่า (กัน duplicate เมื่อรันซ้ำ) — controller GO อยู่นอก canvas จึงรอด แล้ว re-wire ใหม่
            var existing = GameObject.Find("StoryCanvas");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                Debug.Log("[StoryUISetup] ลบ StoryCanvas เก่าออก (Ctrl+Z เพื่อคืน)");
            }

            // ===== StoryCanvas — เหนือ HUDCanvas(0) ใต้ PauseCanvas(100) =====
            var canvasGO = new GameObject("StoryCanvas", typeof(RectTransform));
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UIScaleSetup.ReferenceResolution;
            canvasGO.AddComponent<GraphicRaycaster>();

            // StoryDirector ไม่ถูกสร้างอีกแล้ว (v6.3 cutover) — beats ผูก `day == X` ขัดกฎข้อ 1 และถูก
            // LegacyNarrativeSilencer ปิดตอนรันอยู่แล้ว การสร้างซ้ำที่นี่คือการยัดมันกลับเข้าซีนทุกครั้ง
            // ที่รัน Run All Setups · SaveManager null-guard ไว้แล้ว (ไม่มี director = list ว่าง)
            SetupCardUI(canvasGO.transform, font);
            SetupDialogueUI(canvasGO.transform, font);
            SetupRecordsPanel(canvasGO.transform, font);
            SetupMemorialPanel(canvasGO.transform, font);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[StoryUISetup] สร้าง Story UI (การ์ด + Records + อนุสรณ์) สำเร็จ — กด Save Scene (Ctrl+S)");
        }

        // ─────────────────────────────────────────────
        //  2. การ์ดเนื้อเรื่องกลางจอ (record / info / outcome)
        // ─────────────────────────────────────────────
        private static void SetupCardUI(Transform canvas, Font font)
        {
            // สกินไม้/โลหะ (StoryUI) — null-safe: ยังไม่ import → fallback สีทึบ
            Sprite Skin(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/StoryUI/{n}.png");
            var frameWood = Skin("frame_wood");
            var btnMetal  = Skin("btn_metal");
            var smear     = Skin("smear");

            var gold  = new Color(0.93f, 0.84f, 0.58f);
            var cream = new Color(0.92f, 0.88f, 0.80f);
            var ink   = new Color(0.20f, 0.13f, 0.08f);
            var divCol = new Color(0.24f, 0.17f, 0.10f, 1f);

            const float DW = 780f, DH = 640f, pad = 68f;

            // overlay มืดเต็มจอ — บังคลิกข้างหลังระหว่างอ่านการ์ด
            var overlay = CreatePanel("StoryCardPanel", canvas);
            Stretch(overlay);
            overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            // แผงไม้ 9-slice
            var dialog = CreatePanel("StoryCardDialog", overlay.transform);
            Center(dialog, new Vector2(DW, DH));
            var dImg = dialog.AddComponent<Image>();
            if (frameWood != null) { dImg.sprite = frameWood; dImg.type = Image.Type.Sliced; dImg.color = Color.white; }
            else dImg.color = new Color(0.30f, 0.21f, 0.13f, 0.99f);

            // รอยแปรงเข้มหลังหัวเรื่อง (redaction look)
            Image Smear(string name, float yFromTop, float w, float h)
            {
                var go = CreatePanel(name, dialog.transform);
                var img = go.AddComponent<Image>();
                if (smear != null) { img.sprite = smear; img.color = Color.white; } else img.color = new Color(0.10f, 0.08f, 0.07f, 0.9f);
                var r = go.GetComponent<RectTransform>();
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f); r.pivot = new Vector2(0.5f, 1f);
                r.anchoredPosition = new Vector2(0, -yFromTop); r.sizeDelta = new Vector2(w, h);
                return img;
            }
            // เส้นคั่นบาง ๆ
            void Divider(float yFromTop)
            {
                var go = CreatePanel("Divider", dialog.transform);
                go.AddComponent<Image>().color = divCol;
                var r = go.GetComponent<RectTransform>();
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f); r.pivot = new Vector2(0.5f, 1f);
                r.anchoredPosition = new Vector2(0, -yFromTop); r.sizeDelta = new Vector2(DW - 2 * pad, 3);
            }

            Smear("TitleSmear", 62, DW - 2 * pad - 10, 56);
            var title = CreateText("CardTitle", dialog.transform, font, "หัวเรื่อง", 32, TextAnchor.MiddleCenter);
            AnchorTop(title, 60, new Vector2(DW - 2 * pad, 52));
            title.fontStyle = FontStyle.Bold;
            title.color = gold;

            Smear("KickerSmear", 128, 360, 40);
            var kicker = CreateText("CardKicker", dialog.transform, font, "บันทึกกู้คืน", 21, TextAnchor.MiddleCenter);
            AnchorTop(kicker, 126, new Vector2(DW - 2 * pad, 30));
            kicker.fontStyle = FontStyle.Bold;
            kicker.color = cream;

            // แถบสีหมวด (เส้นบาง) — CardUIController เปลี่ยนสีตามชนิดการ์ด
            var barGO = CreatePanel("CategoryBar", dialog.transform);
            var barRect = barGO.GetComponent<RectTransform>();
            barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.anchoredPosition = new Vector2(0, -170);
            barRect.sizeDelta = new Vector2(DW - 2 * pad, 6);
            var bar = barGO.AddComponent<Image>();
            bar.color = new Color(0.85f, 0.65f, 0.25f);

            var body = CreateText("CardBody", dialog.transform, font, "", 24, TextAnchor.UpperLeft);
            AnchorTop(body, 196, new Vector2(DW - 2 * pad - 6, 300));
            body.color = ink;
            body.lineSpacing = 1.35f;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            Divider(DH - 118);

            var dismissBtn = CreateButton("CardDismissButton", dialog.transform, font, "รับทราบ", 26, new Vector2(300, 58));
            var dBtnImg = dismissBtn.GetComponent<Image>();
            if (btnMetal != null) { dBtnImg.sprite = btnMetal; dBtnImg.type = Image.Type.Sliced; dBtnImg.color = Color.white; }
            else dBtnImg.color = new Color(0.18f, 0.16f, 0.14f, 1f);
            var dLabel = dismissBtn.GetComponentInChildren<Text>();
            if (dLabel != null) dLabel.color = gold;
            var dRect = dismissBtn.GetComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0.5f, 0f);
            dRect.anchorMax = new Vector2(0.5f, 0f);
            dRect.pivot = new Vector2(0.5f, 0f);
            dRect.anchoredPosition = new Vector2(0, 30);

            overlay.SetActive(false);

            var cardGO = GameObject.Find("CardUIController") ?? new GameObject("CardUIController");
            var card = cardGO.GetComponent<CardUIController>() ?? cardGO.AddComponent<CardUIController>();
            card.overlayPanel = overlay;
            card.categoryBar = bar;
            card.kickerText = kicker;
            card.titleText = title;
            card.bodyText = body;
            card.dismissButton = dismissBtn;
            card.dismissLabel = dismissBtn.GetComponentInChildren<Text>();
            EditorUtility.SetDirty(card);
        }

        // ─────────────────────────────────────────────
        //  2b. บทสนทนาหลายตัวละคร (v8.5) — VN portrait ซ้าย/ขวา + บอลลูนล่างจอ
        // ─────────────────────────────────────────────
        private static void SetupDialogueUI(Transform canvas, Font font)
        {
            // โหลดอาร์ต (import โดย Setup Dialogue Art) — null-safe: ไม่มี → fallback สีทึบ/placeholder
            Sprite Load(string p) => AssetDatabase.LoadAssetAtPath<Sprite>(p);
            string KovaPath(string n) => $"Assets/Sprites/Characters/Kova/kova_{n}.png";
            string AurenPath(string n) => $"Assets/Sprites/Characters/Auren/auren_{n}.png";
            string MiraPath(string n) => $"Assets/Sprites/Characters/Mira/mira_{n}.png";
            string DornPath(string n) => $"Assets/Sprites/Characters/Dorn/dorn_{n}.png";
            string FramePath(string n) => $"Assets/Resources/DialogueUI/frame_{n}.png";

            // overlay หรี่จอ + คลุมคลิกทั้งจอ (คลิกที่ไหนก็ไปบรรทัดถัดไป)
            var overlay = CreatePanel("StoryDialoguePanel", canvas);
            Stretch(overlay);
            var ovImg = overlay.AddComponent<Image>();
            ovImg.color = new Color(0f, 0f, 0f, 0.35f);
            var advBtn = overlay.AddComponent<Button>();
            advBtn.targetGraphic = ovImg;
            advBtn.transition = Selectable.Transition.None; // คลิกแล้วไม่ tint แผงหรี่

            // ── Kova (ขวาเสมอ) — ภาพจริงครึ่งตัวตามอารมณ์ · preserveAspect · คลิกทะลุไป advanceButton ──
            var kovaGO = CreatePanel("DialoguePortraitKova", overlay.transform);
            var kovaImg = kovaGO.AddComponent<Image>();
            kovaImg.preserveAspect = true;
            kovaImg.raycastTarget = false;
            var kovaSprite = Load(KovaPath("neutral"));
            if (kovaSprite != null) kovaImg.sprite = kovaSprite;
            else kovaImg.color = new Color(0.30f, 0.70f, 0.95f); // fallback สีวิศวกร
            var kRect = kovaGO.GetComponent<RectTransform>();
            kRect.anchorMin = kRect.anchorMax = new Vector2(1f, 0f);
            kRect.pivot = new Vector2(1f, 0f);
            kRect.anchoredPosition = new Vector2(-16f, 0f);
            kRect.sizeDelta = new Vector2(480, 560); // preserveAspect → ท่ากว้าง (เชื่อม/พิมพ์เขียว) ไม่ล้นทับกรอบยาว

            // ── ฝั่งซ้าย (Auren portrait + กล่องบทพูด) — ใช้ prefab ถ้ามี (แก้ด้วยตา ไม่หายตอน re-run) ไม่งั้นสร้างสด ──
            // controller คุม sprite ตามอารมณ์/สีแถบ/กรอบตามความยาว เอง · prefab คุม ตำแหน่ง/ขนาด/anchor/โครง
            Image aurenImg = null;
            GameObject box = null;
            Image plate = null;
            Text nameText = null, body = null, hint = null;
            var frameMed = Load(FramePath("medium"));

            var leftPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialogueAurenPortraitBaker.PrefabPath);
            if (leftPrefab != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(leftPrefab, overlay.transform);

                var portraitTf = FindDeep(inst.transform, "DialoguePortraitAuren");
                aurenImg = portraitTf != null ? portraitTf.GetComponent<Image>() : inst.GetComponentInChildren<Image>(true);
                if (aurenImg != null) aurenImg.gameObject.SetActive(false); // controller เปิดเมื่อ Auren พูด

                var boxTf = FindDeep(inst.transform, "DialogueBox");
                if (boxTf != null)
                {
                    box = boxTf.gameObject;
                    plate    = FindDeep(boxTf, "DialogueNamePlate")?.GetComponent<Image>();
                    nameText = FindDeep(boxTf, "DialogueName")?.GetComponent<Text>();
                    body     = FindDeep(boxTf, "DialogueBody")?.GetComponent<Text>();
                    hint     = FindDeep(boxTf, "DialogueHint")?.GetComponent<Text>();
                    var bi = box.GetComponent<Image>();
                    if (bi != null && bi.sprite == null && frameMed != null)
                    { bi.sprite = frameMed; bi.type = Image.Type.Simple; bi.color = Color.white; }
                }
            }

            if (aurenImg == null) // ไม่มี prefab (หรือ prefab ไม่มี portrait) → สร้าง portrait สด
            {
                var aurenGO = CreatePanel("DialoguePortraitAuren", overlay.transform);
                aurenImg = aurenGO.AddComponent<Image>();
                aurenImg.preserveAspect = true;
                aurenImg.raycastTarget = false;
                var aurenSprite = Load(AurenPath("thinking"));
                if (aurenSprite != null) aurenImg.sprite = aurenSprite;
                else aurenImg.color = new Color(0.72f, 0.68f, 0.85f); // fallback สีเสียงในใจ
                var aRect = aurenGO.GetComponent<RectTransform>();
                aRect.anchorMin = aRect.anchorMax = new Vector2(0f, 0f);
                aRect.pivot = new Vector2(0f, 0f);
                aRect.anchoredPosition = new Vector2(16f, 0f);
                aRect.sizeDelta = new Vector2(480, 560);
                aurenGO.SetActive(false); // controller เปิดเมื่อ Auren พูด
            }

            // ── กล่องบทพูด (สร้างสด ถ้า prefab ไม่ได้ให้มา) · controller ย้ายซ้าย/ขวา + ปรับกว้างตามความยาว ──
            if (box == null)
            {
                box = CreatePanel("DialogueBox", overlay.transform);
                var boxRect = box.GetComponent<RectTransform>();
                boxRect.anchorMin = boxRect.anchorMax = new Vector2(0f, 0f);
                boxRect.pivot = new Vector2(0f, 0f);
                boxRect.anchoredPosition = new Vector2(48f, 56f);
                boxRect.sizeDelta = new Vector2(900, 240);
                var boxImg = box.AddComponent<Image>();
                boxImg.raycastTarget = false;
                if (frameMed != null) { boxImg.sprite = frameMed; boxImg.type = Image.Type.Simple; boxImg.color = Color.white; }
                else boxImg.color = new Color(0.08f, 0.09f, 0.13f, 0.95f);

                // แถบชื่อผู้พูด — อยู่ "ในกรอบ" (ใต้ขอบบน · เปลี่ยนสีตามคน)
                var plateGO = CreatePanel("DialogueNamePlate", box.transform);
                var plateRect = plateGO.GetComponent<RectTransform>();
                plateRect.anchorMin = plateRect.anchorMax = new Vector2(0f, 1f);
                plateRect.pivot = new Vector2(0f, 1f);
                plateRect.anchoredPosition = new Vector2(52f, -34f);
                plateRect.sizeDelta = new Vector2(200, 40);
                plate = plateGO.AddComponent<Image>();
                plate.raycastTarget = false;
                plate.color = new Color(0.3f, 0.7f, 0.95f);

                nameText = CreateText("DialogueName", plateGO.transform, font, "Kova", 21, TextAnchor.MiddleLeft);
                var nameRect = nameText.GetComponent<RectTransform>();
                nameRect.anchorMin = Vector2.zero; nameRect.anchorMax = Vector2.one;
                nameRect.offsetMin = new Vector2(16, 0); nameRect.offsetMax = new Vector2(-10, 0);
                nameText.fontStyle = FontStyle.Bold;
                nameText.raycastTarget = false;
                nameText.color = new Color(0.06f, 0.07f, 0.10f);

                // ข้อความ — best-fit ให้พอดีในกรอบเสมอ · เว้นที่ด้านบนให้แถบชื่อ
                body = CreateText("DialogueBody", box.transform, font, "", 24, TextAnchor.UpperLeft);
                var bodyRect = body.GetComponent<RectTransform>();
                bodyRect.anchorMin = Vector2.zero; bodyRect.anchorMax = Vector2.one;
                bodyRect.offsetMin = new Vector2(58, 46); bodyRect.offsetMax = new Vector2(-58, -86);
                body.color = new Color(0.93f, 0.94f, 0.88f);
                body.lineSpacing = 1.2f;
                body.raycastTarget = false;
                body.horizontalOverflow = HorizontalWrapMode.Wrap;
                body.verticalOverflow = VerticalWrapMode.Truncate;
                body.resizeTextForBestFit = true;
                body.resizeTextMinSize = 14;
                body.resizeTextMaxSize = 26;

                hint = CreateText("DialogueHint", box.transform, font, "▼ คลิกเพื่อไปต่อ", 15, TextAnchor.LowerRight);
                var hintRect = hint.GetComponent<RectTransform>();
                hintRect.anchorMin = new Vector2(1f, 0f); hintRect.anchorMax = new Vector2(1f, 0f);
                hintRect.pivot = new Vector2(1f, 0f);
                hintRect.anchoredPosition = new Vector2(-46, 20);
                hintRect.sizeDelta = new Vector2(220, 22);
                hint.raycastTarget = false;
                hint.color = new Color(0.75f, 0.72f, 0.62f, 0.85f);
            }

            overlay.SetActive(false);

            var dlgGO = GameObject.Find("DialogueUIController") ?? new GameObject("DialogueUIController");
            var dlg = dlgGO.GetComponent<DialogueUIController>() ?? dlgGO.AddComponent<DialogueUIController>();
            dlg.overlayPanel = overlay;
            dlg.advanceButton = advBtn;
            dlg.leftPortrait = aurenImg;   // Auren (เสียงในใจ) ฝั่งซ้าย
            dlg.leftInitial = null;
            dlg.rightPortrait = kovaImg;   // Kova ฝั่งขวา
            dlg.dialogBox = box;
            dlg.namePlate = plate;
            dlg.nameText = nameText;
            dlg.bodyText = body;
            dlg.hintText = hint;
            dlg.frameShort  = Load(FramePath("short"));
            dlg.frameMedium = frameMed;
            dlg.frameLong   = Load(FramePath("long"));
            // Kova 9 อารมณ์ (index ตาม enum Emotion 0..8)
            dlg.kovaEmotionSprites = new Sprite[]
            {
                Load(KovaPath("neutral")), Load(KovaPath("happy")),   Load(KovaPath("worried")),
                Load(KovaPath("serious")), Load(KovaPath("explain")), Load(KovaPath("excited")),
                Load(KovaPath("welding")), Load(KovaPath("sad")),     Load(KovaPath("proud")),
            };
            // Auren 6 อารมณ์ วางตาม index enum (ช่องที่ Auren ไม่มี = ภาพใกล้เคียง/null → Pick fallback)
            dlg.aurenEmotionSprites = new Sprite[]
            {
                Load(AurenPath("thinking")),   // 0 Neutral → คิด (สงบ)
                Load(AurenPath("happy")),      // 1 Happy
                Load(AurenPath("worried")),    // 2 Worried
                null,                          // 3 Serious
                null,                          // 4 Explain
                Load(AurenPath("excited")),    // 5 Excited
                null,                          // 6 Welding
                Load(AurenPath("worried")),    // 7 Sad → หน้ากังวล
                Load(AurenPath("determined")), // 8 Proud → มุ่งมั่น
                Load(AurenPath("thinking")),   // 9 Thinking
                Load(AurenPath("surprised")),  // 10 Surprised
                Load(AurenPath("determined")), // 11 Determined
            };
            // Mira 6 อารมณ์ (หมอ · ซ้าย)
            dlg.miraEmotionSprites = new Sprite[]
            {
                Load(MiraPath("serious")),     // 0 Neutral → กอดอก (สงบ)
                Load(MiraPath("happy")),       // 1 Happy
                Load(MiraPath("serious")),     // 2 Worried
                Load(MiraPath("serious")),     // 3 Serious
                Load(MiraPath("explain")),     // 4 Explain
                Load(MiraPath("happy")),       // 5 Excited
                null,                          // 6 Welding
                Load(MiraPath("serious")),     // 7 Sad
                Load(MiraPath("determined")),  // 8 Proud
                Load(MiraPath("thinking")),    // 9 Thinking
                Load(MiraPath("surprised")),   // 10 Surprised
                Load(MiraPath("determined")),  // 11 Determined
            };
            // Dorn 5 อารมณ์ (ชาวสวน · ซ้าย)
            dlg.dornEmotionSprites = new Sprite[]
            {
                Load(DornPath("neutral")),     // 0 Neutral
                Load(DornPath("happy")),       // 1 Happy
                Load(DornPath("serious")),     // 2 Worried
                Load(DornPath("serious")),     // 3 Serious
                Load(DornPath("neutral")),     // 4 Explain
                Load(DornPath("happy")),       // 5 Excited
                null,                          // 6 Welding
                Load(DornPath("serious")),     // 7 Sad
                Load(DornPath("proud")),       // 8 Proud → อุ้มตะกร้า (พอใจ)
                Load(DornPath("thinking")),    // 9 Thinking
                Load(DornPath("thinking")),    // 10 Surprised → กังขา (ใกล้สุด)
                Load(DornPath("serious")),     // 11 Determined
            };
            EditorUtility.SetDirty(dlg);
        }

        // ─────────────────────────────────────────────
        //  3. แผง Records (ขวา แบบเดียวกับ Codex) + ปุ่ม "บันทึก" ซ้ายล่าง
        // ─────────────────────────────────────────────
        private static void SetupRecordsPanel(Transform canvas, Font font)
        {
            var panel = CreatePanel("RecordsPanel", canvas);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(640, 0);
            panel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.97f);

            // header
            var header = CreatePanel("RecordsHeader", panel.transform);
            var hRect = header.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0, 1); hRect.anchorMax = new Vector2(1, 1);
            hRect.pivot = new Vector2(0.5f, 1); hRect.anchoredPosition = Vector2.zero;
            hRect.sizeDelta = new Vector2(0, 50);
            header.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.08f, 1f);

            var headTitle = CreateText("RecordsTitle", header.transform, font, "RECORDS — บันทึกที่กู้คืน", 20, TextAnchor.MiddleLeft);
            var htRect = headTitle.GetComponent<RectTransform>();
            htRect.anchorMin = new Vector2(0, 0); htRect.anchorMax = new Vector2(1, 1);
            htRect.offsetMin = new Vector2(14, 0); htRect.offsetMax = new Vector2(-60, 0);
            headTitle.fontStyle = FontStyle.Bold;
            headTitle.color = new Color(0.95f, 0.8f, 0.45f); // อำพัน — โทนเดียวกับแถบการ์ด record

            var closeBtn = CreateButton("RecordsCloseBtn", header.transform, font, "✕", 20, new Vector2(40, 40));
            var cRect = closeBtn.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(1, 0.5f); cRect.anchorMax = new Vector2(1, 0.5f);
            cRect.pivot = new Vector2(1, 0.5f);
            cRect.anchoredPosition = new Vector2(-5, 0);

            // list ซ้าย
            var listPane = CreatePanel("RecordListPane", panel.transform);
            var lRect = listPane.GetComponent<RectTransform>();
            lRect.anchorMin = new Vector2(0, 0); lRect.anchorMax = new Vector2(0, 1);
            lRect.pivot = new Vector2(0, 0.5f);
            lRect.anchoredPosition = new Vector2(0, -25);
            lRect.sizeDelta = new Vector2(240, -50);
            listPane.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.10f, 1f);

            var listContent = CreatePanel("RecordListContent", listPane.transform);
            var lcRect = listContent.GetComponent<RectTransform>();
            lcRect.anchorMin = new Vector2(0, 1); lcRect.anchorMax = new Vector2(1, 1);
            lcRect.pivot = new Vector2(0.5f, 1);
            lcRect.anchoredPosition = new Vector2(0, -4);
            lcRect.sizeDelta = new Vector2(0, 0);
            var vlg = listContent.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;
            var csf = listContent.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // template ปุ่มรายการ (inactive — RecordsPanelController clone แล้วเปิดใช้)
            var template = CreateButton("RecordButtonTemplate", listContent.transform, font, "บันทึก", 15, new Vector2(228, 40));
            var tmplLabel = template.GetComponentInChildren<Text>();
            if (tmplLabel != null) tmplLabel.alignment = TextAnchor.MiddleLeft;
            template.gameObject.SetActive(false);

            // detail ขวา
            var detailPane = CreatePanel("RecordDetailPane", panel.transform);
            var dRect = detailPane.GetComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0, 0); dRect.anchorMax = new Vector2(1, 1);
            dRect.offsetMin = new Vector2(244, 0);
            dRect.offsetMax = new Vector2(0, -50);
            detailPane.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.09f, 1f);

            var dAuthor = CreateText("RecordAuthor", detailPane.transform, font, "", 14, TextAnchor.UpperLeft);
            AnchorTop(dAuthor, 14, new Vector2(370, 20));
            dAuthor.color = new Color(0.95f, 0.8f, 0.45f, 0.8f);

            var dTitle = CreateText("RecordTitle", detailPane.transform, font, "เลือกบันทึกทางซ้าย", 22, TextAnchor.UpperLeft);
            AnchorTop(dTitle, 38, new Vector2(370, 34));
            dTitle.fontStyle = FontStyle.Bold;

            var dBody = CreateText("RecordBody", detailPane.transform, font, "", 16, TextAnchor.UpperLeft);
            AnchorTop(dBody, 82, new Vector2(370, 800));
            dBody.color = new Color(0.9f, 0.9f, 0.85f);
            dBody.lineSpacing = 1.3f;
            dBody.horizontalOverflow = HorizontalWrapMode.Wrap;

            panel.SetActive(false);

            // ปุ่มเปิดแผง Records เรียบสีเข้มเดิม (RecordsToggleButton) ถอดออกแล้ว — ใช้ปุ่มสไปรต์ใหม่แทน
            // RecordsPanelController.toggleButton null-safe · ปุ่มใหม่ผูก RecordsPanelController.Toggle เอง

            var recGO = GameObject.Find("RecordsPanelController") ?? new GameObject("RecordsPanelController");
            var rec = recGO.GetComponent<RecordsPanelController>() ?? recGO.AddComponent<RecordsPanelController>();
            rec.recordsPanel = panel;
            rec.toggleButton = null;
            rec.closeButton = closeBtn;
            rec.entryListParent = listContent.transform;
            rec.entryButtonTemplate = template.gameObject;
            rec.detailTitle = dTitle;
            rec.detailAuthor = dAuthor;
            rec.detailBody = dBody;
            EditorUtility.SetDirty(rec);
        }

        // ─────────────────────────────────────────────
        //  4. แผงอนุสรณ์ (memorialData + ตึก wire โดย Story Setup เฟส 4)
        // ─────────────────────────────────────────────
        private static void SetupMemorialPanel(Transform canvas, Font font)
        {
            var panel = CreatePanel("MemorialPanel", canvas);
            Center(panel, new Vector2(600, 460));
            panel.AddComponent<Image>().color = new Color(0.09f, 0.10f, 0.13f, 0.97f);

            var header = CreateText("MemorialHeader", panel.transform, font, "อนุสรณ์", 22, TextAnchor.UpperCenter);
            AnchorTop(header, 24, new Vector2(540, 60));
            header.fontStyle = FontStyle.Bold;
            header.color = new Color(0.85f, 0.88f, 0.95f);
            header.horizontalOverflow = HorizontalWrapMode.Wrap;

            var names = CreateText("MemorialNames", panel.transform, font, "", 18, TextAnchor.UpperCenter);
            AnchorTop(names, 100, new Vector2(540, 260));
            names.lineSpacing = 1.5f;
            names.color = new Color(0.92f, 0.9f, 0.82f);

            var closeBtn = CreateButton("MemorialCloseButton", panel.transform, font, "ปิด", 18, new Vector2(220, 46));
            var cRect = closeBtn.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0.5f, 0f);
            cRect.anchorMax = new Vector2(0.5f, 0f);
            cRect.pivot = new Vector2(0.5f, 0f);
            cRect.anchoredPosition = new Vector2(0, 24);

            panel.SetActive(false);

            var memGO = GameObject.Find("MemorialPanelController") ?? new GameObject("MemorialPanelController");
            var mem = memGO.GetComponent<MemorialPanelController>() ?? memGO.AddComponent<MemorialPanelController>();
            mem.panel = panel;
            mem.headerText = header;
            mem.namesText = names;
            mem.closeButton = closeBtn;
            EditorUtility.SetDirty(mem);
        }

        // ─────────────────────────────────────────────
        //  Helpers (แบบเดียวกับ HUDCanvasSetup)
        // ─────────────────────────────────────────────
        private static Font LoadFont()
        {
            var kanit = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Kanit-Regular.ttf");
            if (kanit != null) return kanit;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static GameObject CreatePanel(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        // หา child ตามชื่อแบบลึก (ใช้ค้น ref จาก prefab ฝั่งซ้ายบทสนทนา)
        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        private static void Stretch(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Center(GameObject go, Vector2 size)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        // ยึดขอบบน เลื่อนลง yFromTop (px) — แบบเดียวกับ HUDCanvasSetup.AnchorTop
        private static void AnchorTop(Component target, float yFromTop, Vector2 size)
        {
            var rect = target.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -yFromTop);
            rect.sizeDelta = size;
        }

        private static Text CreateText(string name, Transform parent, Font font, string content, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = anchor;
            text.text = content;
            text.verticalOverflow = VerticalWrapMode.Overflow; // Kanit line height สูง — กัน truncate ทั้งบรรทัด
            return text;
        }

        private static Button CreateButton(string name, Transform parent, Font font, string label, int fontSize, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.3f, 1f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGO = new GameObject("Label", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var tRect = textGO.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = new Vector2(8, 0);
            tRect.offsetMax = new Vector2(-8, 0);
            var t = textGO.AddComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = label;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            return btn;
        }
    }
}
