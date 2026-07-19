using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// v6.3 crisis-card popup (GDD §25 / CARDS.md) — the live in-game modal that appears when CardManager
    /// presents a card (EventManager.OnCrisisCardShown). Shows the title, the character-clash dialogue, and
    /// the options. ★ A "good" option gated behind an unresearched note is shown GREYED with 🔒 + the note
    /// name and is NOT clickable — never hidden (CLAUDE.md rule #6). Choosing a legal option resolves it via
    /// CardManager (which owns the day-clock pause/resume).
    ///
    /// ★ cutover (slice 4 Cards): self-contained (auto-spawn onto HUDCanvas + code-built uGUI). Modal — no
    /// dismiss, no Esc; you must pick an option. Disables the legacy scene-placed DilemmaPopupController.
    /// </summary>
    public class CrisisCardPanelUI : MonoBehaviour, GameUIStack.IPanel
    {
        static readonly Color CBackdrop = new Color(0f, 0f, 0f, 0.7f);
        static readonly Color CPanel    = new Color(0.11f, 0.10f, 0.09f, 0.99f);
        static readonly Color CBorder   = new Color(0.55f, 0.30f, 0.20f, 1f);
        static readonly Color CText      = new Color(0.93f, 0.92f, 0.86f, 1f);
        static readonly Color CMuted     = new Color(0.66f, 0.66f, 0.60f, 1f);
        static readonly Color CTitle     = new Color(0.95f, 0.55f, 0.35f, 1f);
        static readonly Color CDialogue  = new Color(0.80f, 0.82f, 0.90f, 1f);
        static readonly Color COpt       = new Color(0.20f, 0.30f, 0.24f, 1f);
        static readonly Color CLocked    = new Color(0.16f, 0.16f, 0.15f, 1f);
        static readonly Color CLockText  = new Color(0.50f, 0.50f, 0.46f, 1f);

        /// <summary>
        /// True while a crisis card is on screen. CardManager checks this right after raising
        /// OnCrisisCardShown: if the card never made it to a panel it must not stay Pending, because
        /// Pending is cleared only by resolving an option and would block the rest of the run silently.
        /// </summary>
        public static bool IsShowing { get; private set; }

        private Font _font;
        private bool _shown, _legacyDisabled;
        private GameObject _backdrop, _root;
        private Text _title, _body;
        private Transform _optContainer;
        private readonly List<GameObject> _optRows = new List<GameObject>();

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
                Debug.LogError($"[CrisisCardPanelUI] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private static void AutoSpawnUnsafe()
        {
            if (FindFirstObjectByType<CrisisCardPanelUI>() != null) return;
            var canvas = FindBestCanvas();
            if (canvas == null) return;
            var go = new GameObject("CrisisCardPanelUI (auto)");
            go.transform.SetParent(canvas.transform, false);
            go.AddComponent<CrisisCardPanelUI>();
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

        private static Font LoadFont()
        {
            var f = Resources.Load<Font>("Fonts/Kanit-Regular");
            if (f == null) f = Resources.Load<Font>("Fonts/Kanit");
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f;
        }

        private void Awake() => _font = LoadFont();

        private void OnEnable()
        {
            if (EventManager.Instance != null)
                EventManager.Instance.OnCrisisCardShown += Show;
        }

        private void OnDisable()
        {
            if (EventManager.Instance != null)
                EventManager.Instance.OnCrisisCardShown -= Show;
        }

        private void Start()
        {
            if (_root == null) BuildPanel(); // Show() may have built it already this frame
            if (!_shown) Hide();
            DisableLegacy();
        }

        private void DisableLegacy()
        {
            if (_legacyDisabled) return;
            var legacy = FindFirstObjectByType<DilemmaPopupController>();
            if (legacy != null) legacy.enabled = false;
            _legacyDisabled = true;
        }

        // ── show / hide (CardManager owns the day-clock pause/resume) ──
        private void Show(CrisisCardSO card)
        {
            if (card == null) return;
            // A card can arrive before Start() on the frame we auto-spawn — build on demand rather than
            // dropping it on the floor (a dropped card used to hang the run; see CardManager.Present).
            if (_root == null) BuildPanel();
            if (_root == null) return;
            Populate(card);
            _shown = true;
            IsShowing = true;
            if (_backdrop != null) _backdrop.SetActive(true);
            GameUIStack.Push(this);
        }

        private void Hide()
        {
            _shown = false;
            IsShowing = false;
            if (_backdrop != null) _backdrop.SetActive(false);
            GameUIStack.Pop(this);
        }

        // Modal: cannot be dismissed by Esc — a choice is required.
        bool GameUIStack.IPanel.ClosableByEscape => false;
        void GameUIStack.IPanel.BringToFront() => GameUIStack.RaiseToTop(_backdrop);
        GameObject GameUIStack.IPanel.PanelRoot => _backdrop;
        void GameUIStack.IPanel.CloseFromStack() { } // no-op: only resolving an option closes it

        private void Choose(int optionIndex)
        {
            var cm = CardManager.Instance;
            if (cm == null) return;
            var res = cm.ResolveOption(optionIndex);
            if (res.valid) Hide(); // invalid (e.g. locked) → leave the card up
        }

        private void Populate(CrisisCardSO card)
        {
            if (_title != null) _title.text = $"⚠ {card.title}";

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(card.description)) sb.Append(card.description);
            if (card.dialogueLines != null)
                foreach (var line in card.dialogueLines)
                    if (!string.IsNullOrEmpty(line)) sb.Append("\n\n<color=#CCD2E6>").Append(line).Append("</color>");
            if (_body != null) _body.text = sb.ToString();

            foreach (var go in _optRows) Destroy(go);
            _optRows.Clear();

            var db = KnowledgeDB.Instance;
            if (card.options == null) return;
            for (int i = 0; i < card.options.Length; i++)
            {
                var opt = card.options[i];
                bool locked = opt.IsLocked(db);
                int idx = i;

                string head = $"{(char)('A' + i)}. {opt.label}";
                string sub = opt.effectSummary ?? "";
                string text = locked
                    ? $"🔒 {head}\n<size=15><color=#7F7F76>ต้องวิจัย: {opt.requiredNoteId}</color></size>"
                    : (string.IsNullOrEmpty(sub) ? head : $"{head}\n<size=15><color=#9EA69A>{sub}</color></size>");

                var btn = MakeButton("Opt", _optContainer, text, locked ? CLocked : COpt, locked ? (UnityEngine.Events.UnityAction)null : () => Choose(idx));
                btn.interactable = !locked;
                var lbl = btn.GetComponentInChildren<Text>();
                if (lbl != null) { lbl.alignment = TextAnchor.MiddleLeft; if (locked) lbl.color = CLockText; }
                var le = btn.gameObject.AddComponent<LayoutElement>(); le.minHeight = 62f; le.preferredHeight = 62f;
                _optRows.Add(btn.gameObject);
            }
        }

        // ═══════════════ BUILD ═══════════════
        private void BuildPanel()
        {
            _backdrop = NewUI("Backdrop", transform, CBackdrop);
            Stretch(_backdrop, Vector2.zero, Vector2.one);
            // modal: an invisible blocker Button that swallows clicks but does NOT close
            var block = _backdrop.AddComponent<Button>();
            block.transition = Selectable.Transition.None;

            _root = NewUI("Panel", _backdrop.transform, CPanel);
            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(760f, 720f);
            var outline = _root.AddComponent<Outline>();
            outline.effectColor = CBorder; outline.effectDistance = new Vector2(2f, -2f);

            _title = MakeText("Title", _root.transform, "", 28, CTitle, TextAnchor.UpperLeft);
            Anchor(_title.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -24f), new Vector2(-28f, -74f));

            _body = MakeText("Body", _root.transform, "", 19, CText, TextAnchor.UpperLeft);
            Anchor(_body.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -82f), new Vector2(-28f, -320f));

            var optRoot = NewUI("Options", _root.transform, new Color(0f, 0f, 0f, 0f));
            Anchor(optRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(28f, 24f), new Vector2(-28f, -330f));
            var vlg = optRoot.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f; vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.LowerCenter;
            _optContainer = optRoot.transform;
        }

        // ── uGUI helpers (same as the other v6.3 panels) ──
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
            var label = MakeText("Label", go.transform, text, 18, CText, TextAnchor.MiddleLeft);
            Stretch(label.gameObject, Vector2.zero, Vector2.one);
            label.rectTransform.offsetMin = new Vector2(14f, 0f);
            label.rectTransform.offsetMax = new Vector2(-14f, 0f);
            return btn;
        }
    }
}
