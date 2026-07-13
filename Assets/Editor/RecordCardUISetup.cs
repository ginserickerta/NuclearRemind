using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// สร้างการ์ด "บันทึกกู้คืน" ธีมไม้ (mockup v2) + wire RecordCardUI — idempotent
    /// อยู่บน "RecordCardCanvas" แยกของตัวเอง (ไม่ใช่ StoryCanvas) → รัน "Setup Story UI" ซ้ำแล้วไม่โดนลบ
    ///
    /// Layout (บนลงล่าง): หัว "การ์ดบันทึก" + {statusLabel} · เส้นคั่น · "- ผู้บันทึก: {recorderName}"
    ///   · ornament (ข้าวหลามตัด) · กล่องเนื้อหา (auto-size จัดกลาง) · ornament · ปุ่ม "รับทราบ" / "เก็บเข้าแผง Record"
    /// binding มาจาก RecordCardSO (statusLabel/recorderName/bodyTH) · ส่วนคงที่ (หัว/label/ปุ่ม/กรอบ) ไม่เปลี่ยนตามการ์ด
    /// รัน: NuclearReMind/Setup Record Card UI — หรือรวมใน Run All Setups
    /// </summary>
    public static class RecordCardUISetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const float DW = 560f, DH = 700f, Pad = 54f; // safe area ~9.6% ของกว้าง (≥8%)

        private static readonly Color Gold  = new Color(0.93f, 0.84f, 0.58f);
        private static readonly Color Cream = new Color(0.92f, 0.88f, 0.80f);
        private static readonly Color Ink   = new Color(0.20f, 0.13f, 0.08f);
        private static readonly Color DivCol = new Color(0.24f, 0.17f, 0.10f, 1f);

        [MenuItem("NuclearReMind/Setup Record Card UI")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            EnsureEventSystem();

            // ลบ canvas เก่า (กัน duplicate เมื่อรันซ้ำ) — controller GO อยู่นอก canvas จึงรอด แล้ว re-wire
            var old = GameObject.Find("RecordCardCanvas");
            if (old != null) Object.DestroyImmediate(old);

            var font = LoadFont();
            var canvasGO = new GameObject("RecordCardCanvas", typeof(RectTransform));
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60; // เหนือ StoryCanvas · ใต้ PauseCanvas(100)
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UIScaleSetup.ReferenceResolution;
            canvasGO.AddComponent<GraphicRaycaster>();

            var frameWood = Skin("frame_wood");
            var btnMetal  = Skin("btn_metal");
            var smear     = Skin("smear");

            // overlay มืดเต็มจอ — บังคลิกหลังการ์ด
            var overlay = Panel("RecordOverlay", canvasGO.transform);
            Stretch(overlay);
            overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            // แผ่นไม้ 9-slice (cardRoot — ตัว pop-in scale)
            var card = Panel("RecordCard", overlay.transform);
            Center(card, new Vector2(DW, DH));
            var cImg = card.AddComponent<Image>();
            if (frameWood != null) { cImg.sprite = frameWood; cImg.type = Image.Type.Sliced; cImg.color = Color.white; }
            else cImg.color = new Color(0.30f, 0.21f, 0.13f, 0.99f);
            var cardRoot = card.GetComponent<RectTransform>();

            // ── หัวการ์ด: ซ้าย "การ์ดบันทึก" · ขวา "{statusLabel}" (smear หลังหัว) ──
            Smear(card.transform, smear, 44, DW - 2 * Pad, 52);
            var head = MakeText("HeadTitle", card.transform, font, "การ์ดบันทึก", 30, TextAnchor.MiddleLeft, Gold);
            head.fontStyle = FontStyle.Bold;
            AnchorTop(head, 42, new Vector2(DW - 2 * Pad, 44), new Vector2(Pad, 0));
            var status = MakeText("StatusLabel", card.transform, font, "กู้คืนสำเร็จ", 22, TextAnchor.MiddleRight, Cream);
            status.fontStyle = FontStyle.Bold;
            AnchorTop(status, 42, new Vector2(DW - 2 * Pad, 44), new Vector2(-Pad, 0));

            Divider(card.transform, 88);

            // ── ผู้บันทึก ──
            var recorder = MakeText("Recorder", card.transform, font, "- ผู้บันทึก:  Dr. Elara Vane", 20, TextAnchor.MiddleLeft, Ink);
            AnchorTop(recorder, 112, new Vector2(DW - 2 * Pad, 30), new Vector2(Pad, 0));

            Ornament(card.transform, 150);

            // ── กล่องเนื้อหา (ใหญ่สุด กลางการ์ด · auto-size จัดกลาง H+V · anchor คงที่) ──
            var body = MakeText("Body", card.transform, font, "", 26, TextAnchor.MiddleCenter, Ink);
            AnchorTop(body, 176, new Vector2(DW - 2 * Pad, DH - 176 - 220), Vector2.zero);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            body.lineSpacing = 1.3f;
            body.resizeTextForBestFit = true;   // ยาว → ย่อพอดีกล่อง ไม่ล้น
            body.resizeTextMinSize = 15;
            body.resizeTextMaxSize = 28;

            Ornament(card.transform, DH - 208);

            // ── ปุ่มแนวตั้ง 2 ปุ่ม (เต็มกว้าง margin เท่ากัน) — บน "รับทราบ" · ล่าง "เก็บเข้าแผง Record" ──
            float btnW = DW - 2 * Pad, btnH = 56f;
            var archiveBtn = MetalButton("ArchiveButton", card.transform, font, "เก็บเข้าแผง Record", btnMetal, btnW, btnH, 30f);
            var ackBtn     = MetalButton("AckButton", card.transform, font, "รับทราบ", btnMetal, btnW, btnH, 30f + btnH + 16f);

            overlay.SetActive(false);

            // controller GO (นอก canvas → รอดตอนลบ canvas) + re-wire
            var ctrlGO = GameObject.Find("RecordCardUI") ?? new GameObject("RecordCardUI");
            var ctrl = ctrlGO.GetComponent<RecordCardUI>() ?? ctrlGO.AddComponent<RecordCardUI>();
            ctrl.overlayPanel = overlay;
            ctrl.cardRoot = cardRoot;
            ctrl.statusText = status;
            ctrl.recorderText = recorder;
            ctrl.bodyText = body;
            ctrl.ackButton = ackBtn;
            ctrl.archiveButton = archiveBtn;
            EditorUtility.SetDirty(ctrl);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[RecordCardUISetup] ✅ การ์ดบันทึก (ธีมไม้ 2 ปุ่ม) พร้อม + wire RecordCardUI — กด Save Scene แล้ว");
        }

        // ─────────── helpers ───────────
        private static Sprite Skin(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/StoryUI/{n}.png");

        private static GameObject Panel(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(GameObject go)
        {
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        private static void Center(GameObject go, Vector2 size)
        {
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = Vector2.zero; r.sizeDelta = size;
        }

        // วาง element ยึดขอบบนการ์ด (yFromTop = ระยะจากขอบบน) — offset เลื่อนซ้าย/ขวาเพิ่มได้
        private static void AnchorTop(Component c, float yFromTop, Vector2 size, Vector2 offset)
        {
            var r = c.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(offset.x, -yFromTop + offset.y);
            r.sizeDelta = size;
        }

        private static Text MakeText(string name, Transform parent, Font font, string txt, int size, TextAnchor anchor, Color color)
        {
            var go = Panel(name, parent);
            var t = go.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.text = txt; t.alignment = anchor; t.color = color;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static void Divider(Transform parent, float yFromTop)
        {
            var go = Panel("Divider", parent);
            go.AddComponent<Image>().color = DivCol;
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0, -yFromTop); r.sizeDelta = new Vector2(DW - 2 * Pad, 3);
        }

        // เส้นคั่นประดับ: เส้นบาง + ข้าวหลามตัด (สี่เหลี่ยมหมุน 45°) สองปลาย
        private static void Ornament(Transform parent, float yFromTop)
        {
            float lineW = DW - 2 * Pad - 44;
            var line = Panel("OrnamentLine", parent);
            line.AddComponent<Image>().color = DivCol;
            var lr = line.GetComponent<RectTransform>();
            lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 1f);
            lr.anchoredPosition = new Vector2(0, -yFromTop); lr.sizeDelta = new Vector2(lineW, 2);

            for (int s = -1; s <= 1; s += 2)
            {
                var d = Panel("OrnamentDiamond", parent);
                d.AddComponent<Image>().color = DivCol;
                var dr = d.GetComponent<RectTransform>();
                dr.anchorMin = dr.anchorMax = dr.pivot = new Vector2(0.5f, 1f);
                dr.anchoredPosition = new Vector2(s * (lineW / 2f + 8f), -yFromTop + 5f);
                dr.sizeDelta = new Vector2(10, 10);
                dr.localRotation = Quaternion.Euler(0, 0, 45f);
            }
        }

        // ปุ่มแผ่นโลหะเข้ม ขอบนูน (btn_metal 9-slice) label สีทองกลางปุ่ม เต็มกว้าง · yFromBottom = ระยะจากขอบล่าง
        private static Button MetalButton(string name, Transform parent, Font font, string label, Sprite metal, float w, float h, float yFromBottom)
        {
            var go = Panel(name, parent);
            var img = go.AddComponent<Image>();
            if (metal != null) { img.sprite = metal; img.type = Image.Type.Sliced; img.color = Color.white; }
            else img.color = new Color(0.18f, 0.16f, 0.14f, 1f);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(0, yFromBottom); r.sizeDelta = new Vector2(w, h);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
            cb.pressedColor = new Color(0.68f, 0.68f, 0.68f);
            btn.colors = cb;

            var t = MakeText("Label", go.transform, font, label, 24, TextAnchor.MiddleCenter, Gold);
            t.fontStyle = FontStyle.Bold;
            var tr = t.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            return btn;
        }

        private static void Smear(Transform parent, Sprite smear, float yFromTop, float w, float h)
        {
            var go = Panel("HeadSmear", parent);
            var img = go.AddComponent<Image>();
            if (smear != null) { img.sprite = smear; img.color = Color.white; } else img.color = new Color(0.10f, 0.08f, 0.07f, 0.9f);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0, -yFromTop); r.sizeDelta = new Vector2(w, h);
        }

        private static Font LoadFont()
        {
            var f = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Kanit-Regular.ttf");
            return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }
}
