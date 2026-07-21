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
        // Tints for the metal option frame — near-white keeps the art true, the dark tint reads as
        // disabled while leaving the 🔒 and the "ต้องวิจัย" line legible (rule 6: never hide a locked option).
        static readonly Color CFrameOn   = new Color(1f, 1f, 1f, 1f);
        static readonly Color CFrameLock = new Color(0.46f, 0.45f, 0.43f, 1f);

        /// <summary>
        /// True while a crisis card is on screen. CardManager checks this right after raising
        /// OnCrisisCardShown: if the card never made it to a panel it must not stay Pending, because
        /// Pending is cleared only by resolving an option and would block the rest of the run silently.
        /// </summary>
        public static bool IsShowing { get; private set; }

        private Font _font;
        private Sprite _optFrame;
        private Sprite _panelFrame;
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
            return UIFonts.Body;
        }

        private void Awake()
        {
            _font = LoadFont();
            _optFrame = Resources.Load<Sprite>("CardUI/opt_frame");     // null → flat colour, as before
            _panelFrame = Resources.Load<Sprite>("CardUI/panel_frame"); // metal panel skin (null → flat + outline)
        }

        private void OnEnable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnCrisisCardShown += Show;
            EventManager.Instance.OnStoryCardDismissed += HandleDialogueFinished;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnCrisisCardShown -= Show;
            EventManager.Instance.OnStoryCardDismissed -= HandleDialogueFinished;
        }

        private void Start()
        {
            if (_root == null) BuildPanel(); // Show() may have built it already this frame
            if (!_shown && _backdrop != null) _backdrop.SetActive(false); // instant — no close animation on scene start
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
        private bool _awaitingDialogue;

        private void Show(CrisisCardSO card)
        {
            if (card == null) return;
            // A card can arrive before Start() on the frame we auto-spawn — build on demand rather than
            // dropping it on the floor (a dropped card used to hang the run; see CardManager.Present).
            if (_root == null) BuildPanel();
            if (_root == null) return;
            Populate(card);

            // ★ Set IsShowing here, not in Reveal(). CardManager reads it immediately after raising
            // OnCrisisCardShown and clears Pending if no panel took the card — so accepting the card and
            // revealing it have to be separable, otherwise playing the dialogue first would look like a
            // dropped card and silently cancel the crisis.
            IsShowing = true;

            if (TryPlayDialogue(card)) { _awaitingDialogue = true; return; }
            Reveal();
        }

        /// <summary>
        /// Hand the card's character lines to the story dialogue system — portraits and speech frames,
        /// the same presentation every other conversation in the game gets, instead of grey text stacked
        /// inside the card body. The options panel follows once the conversation ends, so the player reads
        /// the argument first and then decides.
        /// </summary>
        private bool TryPlayDialogue(CrisisCardSO card)
        {
            if (card.dialogueLines == null || card.dialogueLines.Length == 0) return false;
            if (EventManager.Instance == null || DialogueUIController.Instance == null) return false;

            var lines = new List<DialogueLine>(card.dialogueLines.Length);
            foreach (var raw in card.dialogueLines)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                var line = SpeakerMeta.FromPrefixedLine(raw);
                if (!string.IsNullOrEmpty(line.textTH)) lines.Add(line);
            }
            if (lines.Count == 0) return false;

            EventManager.Instance.RaiseStoryDialogueShown(lines.ToArray());

            // If the dialogue UI declined to open, do NOT wait for a callback that will never come: the
            // day clock is already paused for this crisis and the player would be stuck with no panel.
            // Same guard shape as RecordCardUI's CanBeDismissed check.
            return DialogueUIController.Instance.IsShowing;
        }

        private void HandleDialogueFinished()
        {
            if (!_awaitingDialogue) return; // fired by some other story card, not ours
            _awaitingDialogue = false;
            Reveal();
        }

        private void Reveal()
        {
            _shown = true;
            IsShowing = true;
            // UIPopIn animates the inner Panel (scale+fade, unscaled time — runs while the day clock
            // is paused for the crisis); the dim backdrop itself stays put.
            if (_backdrop != null) { UIPopIn.Ensure(_backdrop); _backdrop.SetActive(true); }
            GameUIStack.Push(this);
        }

        private void Hide()
        {
            _shown = false;
            IsShowing = false;
            _awaitingDialogue = false;
            if (_backdrop != null) UIPopIn.PlayClose(_backdrop); // shrink-out, then SetActive(false) itself
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

            // Body is the situation only. The character lines used to be appended here as grey text;
            // they now play as story dialogue before the card appears (TryPlayDialogue), so the panel
            // stays a decision screen rather than a wall of text with buttons underneath.
            if (_body != null) _body.text = card.description ?? "";

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
                // Legacy uGUI Text cannot draw astral-plane characters, so the 🔒 this used to carry
                // rendered as a blank gap — a locked option has to READ as locked (rule 6).
                string text = locked
                    ? $"[ล็อก] {head}\n<size=15><color=#7F7F76>ต้องวิจัย: {opt.requiredNoteId}</color></size>"
                    : (string.IsNullOrEmpty(sub) ? head : $"{head}\n<size=15><color=#9EA69A>{sub}</color></size>");

                var btn = MakeButton("Opt", _optContainer, text, locked ? CLocked : COpt, locked ? (UnityEngine.Events.UnityAction)null : () => Choose(idx));
                btn.interactable = !locked;
                var lbl = btn.GetComponentInChildren<Text>();
                if (lbl != null) { lbl.alignment = TextAnchor.MiddleLeft; if (locked) lbl.color = CLockText; }
                var le = btn.gameObject.AddComponent<LayoutElement>(); le.minHeight = RowH; le.preferredHeight = RowH;
                _optRows.Add(btn.gameObject);
            }

            FitPanelToContent(card.options.Length);
        }

        // Layout constants — the panel is rebuilt per card, so these are the single source of truth.
        private const float PanelWidth = 760f;
        // Insets are wide enough to clear the ~30px metal border of the frame skin (panel_frame).
        private const float PadSide    = 44f;
        private const float TitleTop   = 40f;
        private const float TitleH     = 50f;
        private const float BodyTop    = 98f;   // TitleTop + TitleH + 8
        private const float BodyGap    = 30f;   // breathing room between the body and the first option
        private const float RowH       = 140f;  // option-row height — text size is unchanged, only the frame grows
        private const float RowGap     = 10f;
        private const float PadBottom  = 40f;
        private const float PanelMaxH  = 880f;

        /// <summary>
        /// Short cards used to leave ~200px of dead space because the body reserved a fixed 238px
        /// and the options were pinned to the bottom. Measure the body instead and shrink to fit.
        /// </summary>
        private void FitPanelToContent(int optionCount)
        {
            if (_root == null || _body == null) return;

            // preferredHeight is measured against the current rect width, so make sure one layout
            // pass has run — on the first card the panel is built and populated in the same frame.
            Canvas.ForceUpdateCanvases();

            var bodyRT = _body.rectTransform;
            float bodyH = Mathf.Max(_body.preferredHeight, 24f);
            bodyRT.offsetMax = new Vector2(-PadSide, -BodyTop);
            bodyRT.offsetMin = new Vector2(PadSide, -(BodyTop + bodyH));

            float optTop = BodyTop + bodyH + BodyGap;
            float optH   = optionCount * RowH + Mathf.Max(0, optionCount - 1) * RowGap;
            float wanted = optTop + optH + PadBottom;

            // Very long cards keep the old behaviour: cap the height and let the body absorb the loss.
            if (wanted > PanelMaxH)
            {
                float over = wanted - PanelMaxH;
                bodyH  = Mathf.Max(bodyH - over, 24f);
                bodyRT.offsetMin = new Vector2(PadSide, -(BodyTop + bodyH));
                optTop = BodyTop + bodyH + BodyGap;
                wanted = PanelMaxH;
            }

            _root.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelWidth, wanted);

            if (_optContainer is RectTransform optRT)
            {
                optRT.offsetMax = new Vector2(-PadSide, -optTop);
                optRT.offsetMin = new Vector2(PadSide, PadBottom);
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
            rt.sizeDelta = new Vector2(PanelWidth, 720f); // provisional — FitPanelToContent sets the real height
            ApplyPanelSkin(_root);

            // Anchor() takes offsetMin (left, BOTTOM) then offsetMax (right, TOP). Both rows used to pass
            // them the other way round, giving the title a height of -50 and dropping it out of view.
            // Insets use the PadSide/TitleTop consts so they track the frame border in one place.
            _title = MakeText("Title", _root.transform, "", 30, CTitle, TextAnchor.UpperLeft);
            Anchor(_title.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(PadSide, -(TitleTop + TitleH)), new Vector2(-PadSide, -TitleTop));

            _body = MakeText("Body", _root.transform, "", 19, CText, TextAnchor.UpperLeft);
            Anchor(_body.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(PadSide, -320f), new Vector2(-PadSide, -BodyTop));

            var optRoot = NewUI("Options", _root.transform, new Color(0f, 0f, 0f, 0f));
            Anchor(optRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(PadSide, PadBottom), new Vector2(-PadSide, -330f));
            var vlg = optRoot.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f; vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter; // the region is measured to fit exactly now
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

        // Skin the panel background: the metal frame sprite (nine-sliced) when the art is present, else
        // the flat colour + Outline the card shipped with. ppuMultiplier 3 renders the 90px art border at
        // ~30px on screen; the sprite carries the interior fill, so its tint stays white.
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
            var img = go.GetComponent<Image>();
            if (_optFrame != null)
            {
                // The frame art is 1600x666 with a 150px border; a row is ~84px tall, so scale the
                // border down rather than letting the nine-slice corners overlap and smear the bolts.
                img.sprite = _optFrame;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 5f;
                img.color = bg == CLocked ? CFrameLock : CFrameOn;
            }
            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(onClick);
            var label = MakeText("Label", go.transform, text, 18, CText, TextAnchor.MiddleLeft);
            Stretch(label.gameObject, Vector2.zero, Vector2.one);
            float pad = _optFrame != null ? 34f : 14f; // clear the corner bolts when the frame is used
            label.rectTransform.offsetMin = new Vector2(pad, 0f);
            label.rectTransform.offsetMax = new Vector2(-pad, 0f);
            return btn;
        }
    }
}
