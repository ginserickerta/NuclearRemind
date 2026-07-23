using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: หน้าต่างสารานุกรม Codex (กดปุ่ม C) — โชว์ควิซ/ความรู้ทั้ง 11 หัวข้อใน 3 สถานะ
    /// [TH]   เชี่ยวชาญแล้ว (คลิกอ่านเนื้อความรู้) · ตอบได้ (คลิกเปิดควิซ) · ล็อก (โชว์แถวหรี่ ไม่ซ่อน)
    /// [TH] auto-spawn ลง HUDCanvas เอง · มี prefab ที่ author ไว้ก็ใช้แทนการ build จากโค้ด
    /// v6.3 Codex panel (GDD §21 / QUIZZES.md / CODEX.md) — the permanent window into the knowledge
    /// economy, rebuilt 2026-07-22 in the crisis-card visual language (metal panel frame, shared
    /// palette/fonts, UIPopIn animation, bakeable prefab).
    ///
    /// Three row states, all always visible (locked is NEVER hidden — QUIZZES.md UI spec):
    ///   • เชี่ยวชาญแล้ว — click to expand and READ the entry's bodyText (the actual knowledge; the old
    ///     panel never showed it at all), with its category accent and English title.
    ///   • ตอบได้ — click to open the quiz in QuizCardPanelUI (RaiseQuizShown), so the one styled quiz
    ///     popup with its reveal + explanation flow is used everywhere instead of inline A/B/C buttons.
    ///   • ล็อก — dim "??? " row. "[ล็อก]" text, not 🔒: legacy uGUI Text cannot draw astral-plane glyphs.
    ///
    /// Keeps the v6.3 contract: auto-spawn onto HUDCanvas (guarded — MainMenu has no core systems),
    /// toggle key C, drives the plain-singleton CodexQuizManager, disables the legacy CodexUIController.
    /// Authored prefab (Resources/CardUI/CodexPanel) wins over the code-build when present.
    /// </summary>
    public class CodexPanelUI : MonoBehaviour, GameUIStack.IPanel
    {
        // Shared family palette (crisis card / quiz / note popup).
        static readonly Color CBackdrop = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color CPanel    = new Color(0.10f, 0.10f, 0.09f, 0.98f);
        static readonly Color CBorder   = new Color(0.32f, 0.30f, 0.26f, 1f);
        static readonly Color CText     = new Color(0.93f, 0.92f, 0.86f, 1f);
        static readonly Color CMuted    = new Color(0.62f, 0.62f, 0.56f, 1f);
        static readonly Color CGold     = new Color(0.96f, 0.80f, 0.35f, 1f);
        static readonly Color CGreen    = new Color(0.55f, 0.85f, 0.55f, 1f);
        static readonly Color CQ        = new Color(0.42f, 0.72f, 0.92f, 1f);
        static readonly Color CBtnDim   = new Color(0.15f, 0.15f, 0.13f, 1f);
        // Row cards — state must read at a glance, before anyone reads a word.
        static readonly Color CRowEarned = new Color(0.16f, 0.24f, 0.17f, 0.85f);
        static readonly Color CRowOpen   = new Color(0.14f, 0.20f, 0.27f, 0.90f);
        static readonly Color CRowLocked = new Color(1f, 1f, 1f, 0.035f);
        static readonly Color CBodyCard  = new Color(0.06f, 0.07f, 0.08f, 0.92f);

        public static CodexPanelUI Instance { get; private set; }

        public KeyCode toggleKey = KeyCode.C;

        private Font _font;
        private Sprite _panelFrame;
        private bool _shown, _legacyDisabled;
        // [SerializeField] so a baked prefab keeps the refs — the runtime instantiate then skips
        // BuildPanel and the authored layout wins (same pattern as the other card panels).
        [SerializeField] private GameObject _backdrop;
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _header;
        [SerializeField] private Transform _listContainer;
        [SerializeField] private Button _closeBtn;
        private readonly List<GameObject> _rows = new List<GameObject>();
        private string _expandedId; // entryId whose knowledge body is open (one at a time)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnHook()
        {
            AutoSpawn();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
            => AutoSpawn();

        private static void AutoSpawn()
        {
            try { AutoSpawnUnsafe(); }
            catch (System.Exception e)
            {
                Debug.LogError($"[CodexPanelUI] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private static void AutoSpawnUnsafe()
        {
            // MainMenu has no core systems. Without this guard the panel attached itself to the menu
            // canvas and bound KeyCode.C there, so pressing C in the menu opened a Codex window and
            // called TimeManager.Pause / GameUIStack.Push with nothing behind them.
            if (EventManager.Instance == null) return;
            if (FindFirstObjectByType<CodexPanelUI>() != null) return;
            var canvas = FindBestCanvas();
            if (canvas == null) return;
            // authored prefab (hand-edited in the Editor) wins; no prefab → code-build as before.
            var prefab = Resources.Load<GameObject>("CardUI/CodexPanel");
            if (prefab != null)
            {
                var go = Instantiate(prefab, canvas.transform, false);
                go.name = "CodexPanelUI (prefab)";
                StretchToCanvas(go);
            }
            else
            {
                var go = new GameObject("CodexPanelUI (auto)");
                go.transform.SetParent(canvas.transform, false);
                go.AddComponent<CodexPanelUI>();
            }
        }

        private static void StretchToCanvas(GameObject go)
        {
            var rt = go.transform as RectTransform;
            if (rt == null) return;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        private static Canvas FindBestCanvas()
        {
            Canvas fallback = null;
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c == null || !c.isActiveAndEnabled) continue;
                var root = c.rootCanvas != null ? c.rootCanvas : c;
                if (root.name == "HUDCanvas") return root;
                if (fallback == null) fallback = root;
            }
            return fallback;
        }

        private void Awake()
        {
            Instance = this;
            _font = UIFonts.Body;
            _panelFrame = Resources.Load<Sprite>("CardUI/panel_frame"); // metal panel skin (null → flat + outline)
        }

        private void OnEnable()
        {
            if (EventManager.Instance != null)
                EventManager.Instance.OnQuizAnswered += HandleQuizAnswered;
        }

        private void OnDisable()
        {
            if (EventManager.Instance != null)
                EventManager.Instance.OnQuizAnswered -= HandleQuizAnswered;
        }

        // The quiz popup (QuizCardPanelUI) answers on our behalf — keep the list and header live.
        private void HandleQuizAnswered(string quizId, bool correct) { if (_shown) Refresh(); }

        /// <summary>[TH] เปิด Codex จากปุ่ม/ไอคอนแจ้งเตือนบน HUD — เปิดอยู่แล้วก็ไม่พัง
        /// Open the Codex from a HUD control (the quiz notification icon). Safe if already open.</summary>
        public void OpenFromHud() { if (!_shown) Open(); }
        /// <summary>[TH] สลับเปิด/ปิดจากปุ่ม CODEX บน HUD — พฤติกรรมเดียวกับกดปุ่ม C
        /// Toggle from the HUD CODEX button — same behaviour as the C key.</summary>
        public void Toggle() { if (_shown) Hide(); else Open(); }
        public bool IsShown => _shown;

        private void Start()
        {
            if (_root == null) BuildPanel();
            // prefab path: onClick added in code is NOT serialized — rebind (idempotent after BuildPanel)
            if (_closeBtn != null) { _closeBtn.onClick.RemoveAllListeners(); _closeBtn.onClick.AddListener(Hide); }
            if (_backdrop != null)
            {
                var bd = _backdrop.GetComponent<Button>();
                if (bd != null) { bd.onClick.RemoveAllListeners(); bd.onClick.AddListener(Hide); }
            }
            if (!_shown && _backdrop != null) _backdrop.SetActive(false); // instant — no close animation on scene start
            DisableLegacy();
        }

        private void DisableLegacy()
        {
            if (_legacyDisabled) return;
            var legacyCodex = FindFirstObjectByType<CodexUIController>();
            if (legacyCodex != null) legacyCodex.enabled = false;
            _legacyDisabled = true;
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                if (_shown) Hide(); else Open();
            }
        }

        private void Open()
        {
            _shown = true;
            // UIPopIn animates the inner Panel (scale+fade, unscaled time — runs while paused).
            if (_backdrop != null) { UIPopIn.Ensure(_backdrop); _backdrop.SetActive(true); }
            GameUIStack.Push(this);
            TimeManager.Instance?.Pause(PauseReason.LabPopup);
            Refresh();
        }

        private void Hide()
        {
            _shown = false;
            if (_backdrop != null) UIPopIn.PlayClose(_backdrop); // shrink-out, then SetActive(false) itself
            GameUIStack.Pop(this);
            TimeManager.Instance?.Resume(PauseReason.LabPopup);
        }

        bool GameUIStack.IPanel.ClosableByEscape => true;
        void GameUIStack.IPanel.BringToFront() => GameUIStack.RaiseToTop(_backdrop);
        GameObject GameUIStack.IPanel.PanelRoot => _backdrop;
        void GameUIStack.IPanel.CloseFromStack() => Hide();

        // ═══════════════ LIST ═══════════════
        private void Refresh()
        {
            var cq = CodexQuizManager.Instance;
            if (_header != null)
            {
                int open = cq.AnswerableCount;
                string badge = open > 0 ? $"   <color=#6BB8EB>· ตอบได้ {open} ข้อ</color>" : "";
                _header.text = $"CODEX — เชี่ยวชาญ {cq.UnlockedCodexCount} / {cq.TotalCodex}{badge}";
            }

            if (_listContainer == null) return;
            // Clear ALL children, not just _rows — a baked prefab ships preview sample rows that are
            // not in _rows, and they must not survive into the live list.
            _rows.Clear();
            for (int i = _listContainer.childCount - 1; i >= 0; i--)
                Destroy(_listContainer.GetChild(i).gameObject);

            foreach (var view in cq.GetAll())
            {
                string title = view.quiz != null && !string.IsNullOrEmpty(view.quiz.topicTitle)
                    ? view.quiz.topicTitle
                    : (view.codex != null ? view.codex.titleTh : view.quiz?.quizId ?? "?");

                switch (view.state)
                {
                    case QuizState.Earned:
                        AddEarnedRow(view, title);
                        break;

                    case QuizState.Answerable:
                        AddAnswerableRow(view, title);
                        break;

                    default: // Locked — never hidden (QUIZZES.md UI spec)
                        AddRow($"[ล็อก]  ??? — {title}", CMuted, 18, CRowLocked, null);
                        break;
                }
            }
        }

        // เชี่ยวชาญแล้ว: accent stripe by category · click toggles the knowledge body card under it.
        private void AddEarnedRow(QuizView view, string title)
        {
            string entryId = view.codex != null ? view.codex.entryId : view.quiz.quizId;
            bool expanded = _expandedId == entryId;
            string arrow = expanded ? "▾" : "▸";
            var accent = ColorFor(view.quiz != null ? view.quiz.category : QuizCategory.Reactor);

            var row = AddRow($"{arrow}  ✔  {title}", CGreen, 19, CRowEarned,
                () => { _expandedId = expanded ? null : entryId; Refresh(); });
            AddAccentStripe(row, accent);

            if (!expanded || view.codex == null) return;

            // Knowledge body — the point of the Codex. bodyText == the quiz's explanation (CODEX.md §7).
            var card = NewUI("Body", _listContainer, CBodyCard);
            var le = card.AddComponent<LayoutElement>(); le.minHeight = 40f; le.preferredHeight = -1f;
            var vl = card.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(18, 14, 10, 12); vl.spacing = 4f;
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;

            if (!string.IsNullOrEmpty(view.codex.titleEn))
            {
                var en = MakeText("TitleEn", card.transform, view.codex.titleEn, 14, accent, TextAnchor.UpperLeft);
                en.fontStyle = FontStyle.Bold;
            }
            MakeText("BodyText", card.transform, view.codex.bodyText ?? "", 17, CText, TextAnchor.UpperLeft);
            AddAccentStripe(card, accent);
            _rows.Add(card);
        }

        // ตอบได้: click hands the quiz to QuizCardPanelUI — one styled quiz popup everywhere, with the
        // same reveal + explanation flow, instead of bare inline option buttons in the list.
        private void AddAnswerableRow(QuizView view, string title)
        {
            var quiz = view.quiz;
            var row = AddRow($"?  {title}   <size=14><color=#6BB8EB>คลิกเพื่อตอบ</color></size>", CQ, 19, CRowOpen,
                () => EventManager.Instance?.RaiseQuizShown(quiz));
            AddAccentStripe(row, CQ);
        }

        /// <summary>
        /// One list row on its own tinted card. preferredHeight stays -1 so the group falls back to the
        /// Text's preferred height — long titles wrap instead of clipping. onClick null → plain row.
        /// </summary>
        private GameObject AddRow(string text, Color color, int size, Color rowBg, UnityEngine.Events.UnityAction onClick)
        {
            var row = NewUI("Row", _listContainer, rowBg);
            var le = row.AddComponent<LayoutElement>(); le.minHeight = 44f; le.preferredHeight = -1f;
            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(18, 14, 9, 9);
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = true; hl.childForceExpandHeight = false;

            if (onClick != null)
            {
                var btn = row.AddComponent<Button>();
                btn.onClick.AddListener(onClick);
            }

            var t = MakeText("Label", row.transform, text, size, color, TextAnchor.MiddleLeft);
            t.verticalOverflow = VerticalWrapMode.Truncate; // the row grows instead
            _rows.Add(row);
            return row;
        }

        // Thin category-coloured stripe down the row's left edge — state + subject at a glance.
        private void AddAccentStripe(GameObject row, Color color)
        {
            var stripe = new GameObject("Accent", typeof(RectTransform));
            stripe.transform.SetParent(row.transform, false);
            var img = stripe.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var le = stripe.AddComponent<LayoutElement>(); le.ignoreLayout = true; // not a layout child
            var rt = stripe.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(5f, 0f);
        }

        private static Color ColorFor(QuizCategory category)
        {
            switch (category)
            {
                case QuizCategory.Reactor:     return new Color(0.36f, 0.62f, 0.92f); // blue
                case QuizCategory.Agriculture: return new Color(0.42f, 0.80f, 0.45f); // green
                case QuizCategory.Medical:     return new Color(0.90f, 0.45f, 0.45f); // red
                case QuizCategory.Ethics:      return new Color(0.70f, 0.68f, 0.75f); // grey-violet
                default:                       return CText;
            }
        }

        // ═══════════════ BUILD ═══════════════
        private void BuildPanel()
        {
            _backdrop = NewUI("Backdrop", transform, CBackdrop);
            Stretch(_backdrop, Vector2.zero, Vector2.one);
            var bdBtn = _backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            _root = NewUI("Panel", _backdrop.transform, CPanel);
            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(860f, 840f);
            ApplyPanelSkin(_root);

            // Insets clear the ~30px metal border of the frame skin.
            _header = MakeText("Header", _root.transform, "", 26, CGold, TextAnchor.UpperLeft);
            Anchor(_header.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(44f, -84f), new Vector2(-44f, -38f));

            _closeBtn = MakeButton("Close", _root.transform, "✕", CBtnDim, Hide);
            var crt = _closeBtn.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-34f, -34f); crt.sizeDelta = new Vector2(46f, 46f);

            // Scrolling list — the viewport clips; Content grows with its children.
            var list = NewUI("List", _root.transform, new Color(0f, 0f, 0f, 0.22f));
            Anchor(list, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(44f, 40f), new Vector2(-44f, -92f));
            list.AddComponent<RectMask2D>();
            var scroll = list.AddComponent<ScrollRect>();

            var content = NewUI("Content", list.transform, new Color(0f, 0f, 0f, 0f));
            var cnt = content.GetComponent<RectTransform>();
            cnt.anchorMin = new Vector2(0f, 1f); cnt.anchorMax = new Vector2(1f, 1f);
            cnt.pivot = new Vector2(0.5f, 1f);
            cnt.offsetMin = Vector2.zero; cnt.offsetMax = Vector2.zero;

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f; vlg.padding = new RectOffset(14, 14, 14, 14);
            // ★ childControlHeight MUST be true — false makes the group ignore every LayoutElement
            //   and use each row's default 100x100 rect (the huge-gaps bug).
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = cnt;
            scroll.viewport = list.GetComponent<RectTransform>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            _listContainer = content.transform;
        }

        // Skin the panel background: the metal frame sprite (nine-sliced) when present, else the flat
        // colour + Outline. ppuMultiplier 3 renders the 90px art border at ~30px on screen.
        private void ApplyPanelSkin(GameObject root)
        {
            var img = root.GetComponent<Image>();
            if (img == null) return;
            if (_panelFrame != null)
            {
                img.sprite = _panelFrame;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 3f;
                img.color = Color.white;
            }
            else
            {
                img.color = CPanel;
                var outline = root.AddComponent<Outline>();
                outline.effectColor = CBorder; outline.effectDistance = new Vector2(2f, -2f);
            }
        }

        /// <summary>
        /// [TH] ใช้ใน Editor เท่านั้น: build แผงทั้งอัน + แถวตัวอย่างครบ 3 สถานะ แล้วเซฟเป็น prefab
        /// [TH] (แถวตัวอย่างถูก Refresh ล้างทิ้งตอนรันจริง)
        /// Editor baker only: build the whole panel under this object — with one sample row per state so
        /// the prefab previews like the real list — then save as a prefab. Sample rows are cleared by
        /// Refresh (it wipes ALL list children). Backdrop ships ACTIVE for preview; Start() hides it.
        /// </summary>
        public void BuildForBake()
        {
            _font = UIFonts.Body;
            _panelFrame = Resources.Load<Sprite>("CardUI/panel_frame");
            BuildPanel();
            if (_header != null) _header.text = "CODEX — เชี่ยวชาญ 3 / 11   <color=#6BB8EB>· ตอบได้ 2 ข้อ</color>";
            AddAccentStripe(AddRow("▸  ✔  หัวข้อที่เชี่ยวชาญแล้ว (ตัวอย่าง)", CGreen, 19, CRowEarned, null),
                new Color(0.36f, 0.62f, 0.92f));
            AddAccentStripe(AddRow("?  หัวข้อที่ตอบได้ (ตัวอย่าง)   <size=14><color=#6BB8EB>คลิกเพื่อตอบ</color></size>", CQ, 19, CRowOpen, null), CQ);
            AddRow("[ล็อก]  ??? — หัวข้อที่ยังไม่ปลด (ตัวอย่าง)", CMuted, 18, CRowLocked, null);
        }

        // ── uGUI helpers (same shape as the other card panels) ──
        private static GameObject NewUI(string name, Transform parent, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = bg;
            return go;
        }

        private static void Stretch(GameObject go, Vector2 min, Vector2 max)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void Anchor(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
        }

        private Text MakeText(string name, Transform parent, string text, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = _font; t.text = text; t.fontSize = size; t.color = color;
            t.alignment = anchor; t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow; t.supportRichText = true;
            return t;
        }

        private Button MakeButton(string name, Transform parent, string text, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var go = NewUI(name, parent, bg);
            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(onClick);
            var label = MakeText("Label", go.transform, text, 18, CText, TextAnchor.MiddleCenter);
            Stretch(label.gameObject, new Vector2(0f, 0f), new Vector2(1f, 1f));
            label.rectTransform.offsetMin = new Vector2(10f, 0f);
            label.rectTransform.offsetMax = new Vector2(-10f, 0f);
            return btn;
        }
    }
}
