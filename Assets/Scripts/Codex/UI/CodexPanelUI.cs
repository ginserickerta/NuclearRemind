using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// v6.3 Codex + quiz panel (GDD §21 / QUIZZES.md / CODEX.md) — the live in-game window into the
    /// knowledge economy. Quizzes are OPTIONAL and live here (no forced popup, no timer): an answerable
    /// quiz shows its options; a correct answer grants permanent Mastery + unlocks its Codex entry; a wrong
    /// answer costs nothing and can be retried. Locked entries show 🔒 ??? (never hidden). Header counts x/11.
    ///
    /// ★ cutover (slice 3 Codex): self-contained (auto-spawn onto HUDCanvas + code-built uGUI, toggle key C).
    /// Drives the plain-singleton CodexQuizManager. Disables the legacy scene-placed CodexUIController +
    /// QuizPopupController on spawn so the old C-toggle codex and forced quiz popups stop.
    /// </summary>
    public class CodexPanelUI : MonoBehaviour, GameUIStack.IPanel
    {
        static readonly Color CBackdrop = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color CPanel    = new Color(0.10f, 0.10f, 0.09f, 0.98f);
        static readonly Color CBorder   = new Color(0.32f, 0.30f, 0.26f, 1f);
        static readonly Color CText      = new Color(0.93f, 0.92f, 0.86f, 1f);
        static readonly Color CMuted     = new Color(0.62f, 0.62f, 0.56f, 1f);
        static readonly Color CGold      = new Color(0.96f, 0.80f, 0.35f, 1f);
        static readonly Color CGreen     = new Color(0.55f, 0.85f, 0.55f, 1f);
        static readonly Color CQ         = new Color(0.42f, 0.72f, 0.92f, 1f);
        static readonly Color CBtn       = new Color(0.18f, 0.26f, 0.34f, 1f);
        static readonly Color CBtnDim    = new Color(0.15f, 0.15f, 0.13f, 1f);
        // Row cards — state should be readable at a glance, before anyone reads a word.
        static readonly Color CRowEarned = new Color(0.16f, 0.24f, 0.17f, 0.85f);
        static readonly Color CRowOpen   = new Color(0.14f, 0.20f, 0.27f, 0.90f);
        static readonly Color CRowLocked = new Color(1f, 1f, 1f, 0.035f);

        public static CodexPanelUI Instance { get; private set; }

        public KeyCode toggleKey = KeyCode.C;

        private Font _font;
        private bool _shown, _legacyDisabled;
        private GameObject _backdrop, _root;
        private Text _header, _result;
        private Transform _listContainer;
        private readonly List<GameObject> _rows = new List<GameObject>();

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
            // called TimeManager.Pause / GameUIStack.Push with nothing behind them. Its siblings
            // (QuizNotificationHUD, QuizAppliedWatcher) already guard the same way.
            if (EventManager.Instance == null) return;
            if (FindFirstObjectByType<CodexPanelUI>() != null) return;
            var canvas = FindBestCanvas();
            if (canvas == null) return;
            var go = new GameObject("CodexPanelUI (auto)");
            go.transform.SetParent(canvas.transform, false);
            go.AddComponent<CodexPanelUI>();
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

        private void Awake() { Instance = this; _font = LoadFont(); }

        /// <summary>Open the Codex from a HUD control (the quiz notification icon). Safe if already open.</summary>
        public void OpenFromHud() { if (!_shown) Open(); }
        public bool IsShown => _shown;

        private static Font LoadFont()
        {
            return UIFonts.Body;
        }

        private void Start()
        {
            BuildPanel();
            Hide();
            DisableLegacy();
        }

        // Retire the legacy scene-placed codex window so the old C-toggle stops.
        //
        // ★ QuizPopupController is deliberately LEFT ENABLED. It is the scene-authored quiz popup (category
        // bar, speaker, Codex-reward footer, shuffled options, outline highlighting) and it is now the
        // v6.3 prompt: it submits to CodexQuizManager and can be skipped. Disabling it here would silently
        // kill the centre-screen quiz again.
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
            if (_backdrop != null) _backdrop.SetActive(true);
            GameUIStack.Push(this);
            TimeManager.Instance?.Pause(PauseReason.LabPopup);
            if (_result != null) _result.text = "";
            Refresh();
        }

        private void Hide()
        {
            _shown = false;
            if (_backdrop != null) _backdrop.SetActive(false);
            GameUIStack.Pop(this);
            TimeManager.Instance?.Resume(PauseReason.LabPopup);
        }

        bool GameUIStack.IPanel.ClosableByEscape => true;
        void GameUIStack.IPanel.BringToFront() => GameUIStack.RaiseToTop(_backdrop);
        GameObject GameUIStack.IPanel.PanelRoot => _backdrop;
        void GameUIStack.IPanel.CloseFromStack() => Hide();

        private void Answer(string quizId, int optionIndex)
        {
            var res = CodexQuizManager.Instance.Submit(quizId, optionIndex);
            if (_result != null)
            {
                if (!res.valid) _result.text = "";
                else if (res.correct)
                    _result.text = $"<color=#8CD98C>✔ ถูกต้อง! ปลดล็อกความเชี่ยวชาญ</color>\n{res.explanation}";
                else
                    _result.text = $"<color=#D98C8C>✘ ยังไม่ใช่ — ลองใหม่พรุ่งนี้ได้ (ไม่มีโทษ)</color>\n{res.explanation}";
            }
            Refresh();
        }

        private void Refresh()
        {
            var cq = CodexQuizManager.Instance;
            if (_header != null)
            {
                int open = cq.AnswerableCount;
                string badge = open > 0 ? $"   <color=#6BB8EB>· ตอบได้ {open} ข้อ</color>" : "";
                _header.text = $"Codex — เชี่ยวชาญ {cq.UnlockedCodexCount} / {cq.TotalCodex}{badge}";
            }

            if (_listContainer == null) return;
            foreach (var go in _rows) Destroy(go);
            _rows.Clear();

            foreach (var view in cq.GetAll())
            {
                string title = view.quiz != null && !string.IsNullOrEmpty(view.quiz.topicTitle)
                    ? view.quiz.topicTitle
                    : (view.codex != null ? view.codex.titleTh : view.quiz?.quizId ?? "?");

                switch (view.state)
                {
                    case QuizState.Earned:
                        AddText($"✔  {title}", CGreen, 19, CRowEarned);
                        break;

                    case QuizState.Answerable:
                        AddText($"?  {view.quiz.question}", CQ, 19, CRowOpen);
                        if (view.quiz.options != null)
                            for (int i = 0; i < view.quiz.options.Length; i++)
                            {
                                string opt = view.quiz.options[i];
                                string qid = view.quiz.quizId;
                                int idx = i;
                                AddButton($"{(char)('A' + i)}.  {opt}", CBtn, () => Answer(qid, idx));
                            }
                        AddSpacer();
                        break;

                    default: // Locked — never hidden (QUIZZES.md UI spec). "[ล็อก]" not 🔒: legacy
                             // uGUI Text cannot draw astral-plane glyphs, so the padlock came out blank.
                        AddText($"[ล็อก]  ??? — {title}", CMuted, 18, CRowLocked);
                        break;
                }
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
            rt.sizeDelta = new Vector2(820f, 820f);
            var outline = _root.AddComponent<Outline>();
            outline.effectColor = CBorder; outline.effectDistance = new Vector2(2f, -2f);

            _header = MakeText("Header", _root.transform, "", 26, CGold, TextAnchor.UpperLeft);
            Anchor(_header.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -20f), new Vector2(-24f, -66f));

            var close = MakeButton("Close", _root.transform, "✕", CBtnDim, Hide);
            var crt = close.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-16f, -16f); crt.sizeDelta = new Vector2(48f, 48f);

            // Scrolling list. The viewport clips; Content grows with its children so an answerable
            // quiz (question + 3 options) can no longer push the rest off the bottom of the panel.
            var list = NewUI("List", _root.transform, new Color(0f, 0f, 0f, 0.22f));
            Anchor(list, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 150f), new Vector2(-24f, -74f));
            list.AddComponent<RectMask2D>();
            var scroll = list.AddComponent<ScrollRect>();

            var content = NewUI("Content", list.transform, new Color(0f, 0f, 0f, 0f));
            var cnt = content.GetComponent<RectTransform>();
            cnt.anchorMin = new Vector2(0f, 1f); cnt.anchorMax = new Vector2(1f, 1f);
            cnt.pivot = new Vector2(0.5f, 1f);
            cnt.offsetMin = new Vector2(0f, 0f); cnt.offsetMax = new Vector2(0f, 0f);

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f; vlg.padding = new RectOffset(14, 14, 14, 14);
            // ★ childControlHeight MUST be true. While it was false the group ignored every
            // LayoutElement and used each row's default 100x100 rect, which is where the huge
            // gaps between entries came from.
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

            // result / explanation area
            _result = MakeText("Result", _root.transform, "", 17, CText, TextAnchor.UpperLeft);
            Anchor(_result.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 20f), new Vector2(-24f, 138f));
        }

        /// <summary>
        /// One list row on its own tinted card. preferredHeight is left at -1 on purpose so the group
        /// falls back to the Text's own preferred height — a long question wraps instead of clipping.
        /// </summary>
        private void AddText(string text, Color color, int size, Color rowBg)
        {
            var row = NewUI("Row", _listContainer, rowBg);
            var le = row.AddComponent<LayoutElement>(); le.minHeight = 40f; le.preferredHeight = -1f;
            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(14, 14, 9, 9);
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = true; hl.childForceExpandHeight = false;

            var t = MakeText("Label", row.transform, text, size, color, TextAnchor.MiddleLeft);
            t.verticalOverflow = VerticalWrapMode.Truncate; // the row grows instead
            _rows.Add(row);
        }

        private void AddButton(string text, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var btn = MakeButton("Opt", _listContainer, text, bg, onClick);
            var le = btn.gameObject.AddComponent<LayoutElement>(); le.minHeight = 44f; le.preferredHeight = 44f;
            var lbl = btn.GetComponentInChildren<Text>();
            if (lbl != null) { lbl.alignment = TextAnchor.MiddleLeft; lbl.fontSize = 17; }
            _rows.Add(btn.gameObject);
        }

        private void AddSpacer()
        {
            var go = NewUI("Spacer", _listContainer, new Color(0f, 0f, 0f, 0f));
            var le = go.AddComponent<LayoutElement>(); le.minHeight = 10f; le.preferredHeight = 10f;
            _rows.Add(go);
        }

        // ── uGUI helpers (same as ResearchQueuePanel/WorkerAssignPanel) ──
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
