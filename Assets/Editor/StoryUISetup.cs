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
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            SetupStoryDirector();
            SetupCardUI(canvasGO.transform, font);
            SetupRecordsPanel(canvasGO.transform, font);
            SetupMemorialPanel(canvasGO.transform, font);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[StoryUISetup] สร้าง Story UI (การ์ด + Records + อนุสรณ์) สำเร็จ — กด Save Scene (Ctrl+S)");
        }

        // ─────────────────────────────────────────────
        //  1. StoryDirector (beats wire โดย Story Setup เฟส 4)
        // ─────────────────────────────────────────────
        private static void SetupStoryDirector()
        {
            var go = GameObject.Find("StoryDirector") ?? new GameObject("StoryDirector");
            if (go.GetComponent<StoryDirector>() == null)
                go.AddComponent<StoryDirector>();
            EditorUtility.SetDirty(go);
        }

        // ─────────────────────────────────────────────
        //  2. การ์ดเนื้อเรื่องกลางจอ (record / info / outcome)
        // ─────────────────────────────────────────────
        private static void SetupCardUI(Transform canvas, Font font)
        {
            // overlay มืดเต็มจอ — บังคลิกข้างหลังระหว่างอ่านการ์ด
            var overlay = CreatePanel("StoryCardPanel", canvas);
            Stretch(overlay);
            overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            var dialog = CreatePanel("StoryCardDialog", overlay.transform);
            Center(dialog, new Vector2(720, 520));
            dialog.AddComponent<Image>().color = new Color(0.10f, 0.11f, 0.14f, 0.97f);

            // แถบสีหมวด (บนสุด) — CardUIController เปลี่ยนสีตามชนิดการ์ด
            var barGO = CreatePanel("CategoryBar", dialog.transform);
            var barRect = barGO.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(0, 10);
            var bar = barGO.AddComponent<Image>();
            bar.color = new Color(0.2f, 0.5f, 0.85f);

            var kicker = CreateText("CardKicker", dialog.transform, font, "บันทึกกู้คืน", 17, TextAnchor.UpperLeft);
            AnchorTop(kicker, 22, new Vector2(660, 24));
            kicker.fontStyle = FontStyle.Bold;
            kicker.color = new Color(0.6f, 0.85f, 1f);

            var title = CreateText("CardTitle", dialog.transform, font, "หัวเรื่อง", 24, TextAnchor.UpperLeft);
            AnchorTop(title, 50, new Vector2(660, 36));
            title.fontStyle = FontStyle.Bold;

            var body = CreateText("CardBody", dialog.transform, font, "", 18, TextAnchor.UpperLeft);
            AnchorTop(body, 96, new Vector2(660, 330));
            body.color = new Color(0.9f, 0.92f, 0.85f);
            body.lineSpacing = 1.3f;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            var dismissBtn = CreateButton("CardDismissButton", dialog.transform, font, "รับทราบ", 18, new Vector2(320, 48));
            var dRect = dismissBtn.GetComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0.5f, 0f);
            dRect.anchorMax = new Vector2(0.5f, 0f);
            dRect.pivot = new Vector2(0.5f, 0f);
            dRect.anchoredPosition = new Vector2(0, 24);

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

            // ปุ่มเปิดแผง — ซ้ายล่าง เหนือปุ่ม "คีย์ลัด (F1)" (y 254 สูง 36 → 298 พ้นกัน)
            var toggleBtn = CreateButton("RecordsToggleButton", canvas, font, "บันทึก", 15, new Vector2(120, 36));
            var tRect = toggleBtn.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.zero;
            tRect.pivot = Vector2.zero;
            tRect.anchoredPosition = new Vector2(20, 298);
            toggleBtn.image.color = new Color(0.1f, 0.15f, 0.28f, 0.9f);
            var tLabel = toggleBtn.GetComponentInChildren<Text>();
            if (tLabel != null) tLabel.color = Color.white;

            var recGO = GameObject.Find("RecordsPanelController") ?? new GameObject("RecordsPanelController");
            var rec = recGO.GetComponent<RecordsPanelController>() ?? recGO.AddComponent<RecordsPanelController>();
            rec.recordsPanel = panel;
            rec.toggleButton = toggleBtn;
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
