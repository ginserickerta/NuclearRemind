using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// v6.3 quiz popup (GDD §21 / QUIZZES.md) — the centre-screen invitation that appears when
    /// QuizAppliedWatcher raises EventManager.OnQuizShown, once a piece of knowledge has actually been
    /// USED. Rebuilt from the scene-wired QuizPopupController in the crisis-card visual language: the same
    /// metal option frames, palette, fonts and self-contained auto-spawn (onto HUDCanvas + code-built
    /// uGUI, nothing to wire in the Inspector).
    ///
    /// Two things differ from a crisis card, and both are required by spec:
    ///   • SKIPPABLE — a quiz is optional (QUIZZES.md rule table). Esc closes it and a visible
    ///     "ไว้ทีหลัง" button says so; skipping submits nothing and the quiz stays answerable in the Codex.
    ///   • A REVEAL step — after "ยืนยัน" the correct option turns green, a wrong pick turns red, and the
    ///     explanation appears (shown right OR wrong, QUIZZES.md). The answer is submitted to
    ///     CodexQuizManager on close, never before.
    ///
    /// ★ Supersedes the scene-authored QuizPopupController, which is disabled on Start (same move
    ///   CrisisCardPanelUI makes on DilemmaPopupController) so the old popup never shows again.
    /// Pauses the day clock while open (PauseReason.QuizPopup — pause-reason stack §3, never Time.timeScale).
    /// </summary>
    public class QuizCardPanelUI : MonoBehaviour, GameUIStack.IPanel
    {
        // Shared with the crisis card so the two popups read as one family.
        static readonly Color CBackdrop = new Color(0f, 0f, 0f, 0.7f);
        static readonly Color CPanel    = new Color(0.11f, 0.10f, 0.09f, 0.99f);
        static readonly Color CBorder   = new Color(0.30f, 0.42f, 0.55f, 1f);  // cooler than the crisis border
        static readonly Color CText     = new Color(0.93f, 0.92f, 0.86f, 1f);
        static readonly Color CMuted    = new Color(0.66f, 0.66f, 0.60f, 1f);
        static readonly Color CCodex    = new Color(0.42f, 0.72f, 0.92f, 1f);  // codex-reward blue
        static readonly Color COpt      = new Color(0.20f, 0.26f, 0.30f, 1f);
        // Metal option-frame tints (near-white keeps the art true; the state tints read at a glance).
        static readonly Color CFrameOn      = new Color(1f, 1f, 1f, 1f);
        static readonly Color CFrameSelect  = new Color(0.95f, 0.86f, 0.45f, 1f); // picked, not yet confirmed
        static readonly Color CFrameCorrect = new Color(0.50f, 0.90f, 0.55f, 1f); // right answer (green)
        static readonly Color CFrameWrong   = new Color(0.92f, 0.45f, 0.45f, 1f); // your wrong pick (red)
        static readonly Color CFrameDim     = new Color(0.42f, 0.42f, 0.40f, 1f); // the rest, after reveal

        private const float PanelWidth = 760f;

        private Font _font;
        private Sprite _optFrame;
        private Sprite _panelFrame;
        private bool _shown, _revealed, _legacyDisabled;
        // [SerializeField] so a baked prefab keeps the refs — the runtime instantiate then skips
        // BuildPanel and the authored layout wins. Option rows stay runtime-built (per-quiz content).
        [SerializeField] private GameObject _backdrop;
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _eyebrow, _title, _question, _explain;
        [SerializeField] private GameObject _codexFooter, _explainRow;
        [SerializeField] private Text _codexText;
        [SerializeField] private Transform _optContainer;
        [SerializeField] private Button _confirmBtn, _skipBtn;
        [SerializeField] private Text _confirmLabel, _skipLabel;

        private readonly List<Button> _optButtons = new List<Button>();
        private QuizQuestionSO _quiz;
        private int _selected = -1;         // display slot chosen (0..n-1), -1 = none
        private int[] _displayToOriginal;   // display slot → real index in quiz.options (shuffled per show)

        // ── auto-spawn (same pattern as the other v6.3 panels) ──
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
            try
            {
                if (FindFirstObjectByType<QuizCardPanelUI>() != null) return;
                var canvas = FindBestCanvas();
                if (canvas == null) return;
                // authored prefab (hand-edited in the Editor) wins; no prefab → code-build as before.
                var prefab = Resources.Load<GameObject>("CardUI/QuizCardPanel");
                if (prefab != null)
                {
                    var go = Instantiate(prefab, canvas.transform, false);
                    go.name = "QuizCardPanelUI (prefab)";
                    StretchToCanvas(go);
                }
                else
                {
                    var go = new GameObject("QuizCardPanelUI (auto)");
                    go.transform.SetParent(canvas.transform, false);
                    go.AddComponent<QuizCardPanelUI>();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuizCardPanelUI] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
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

        /// <summary>Editor baker only: build the whole panel under this object so it can be saved as a prefab.</summary>
        public void BuildForBake()
        {
            _font = UIFonts.Body;
            _optFrame = Resources.Load<Sprite>("CardUI/opt_frame");
            _panelFrame = Resources.Load<Sprite>("CardUI/panel_frame");
            BuildPanel();
            if (_backdrop != null) _backdrop.SetActive(false); // prefab ships hidden — a quiz opens it
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
            _font = UIFonts.Body;
            _optFrame = Resources.Load<Sprite>("CardUI/opt_frame");     // null → flat colour, as before
            _panelFrame = Resources.Load<Sprite>("CardUI/panel_frame"); // metal panel skin (null → flat + outline)
        }

        private void OnEnable()
        {
            if (EventManager.Instance != null)
                EventManager.Instance.OnQuizShown += HandleQuizShown;
        }

        private void OnDisable()
        {
            if (EventManager.Instance != null)
                EventManager.Instance.OnQuizShown -= HandleQuizShown;
        }

        private void Start()
        {
            if (_root == null) BuildPanel(); // a quiz may have arrived before Start on the spawn frame
            HookListeners();                 // prefab path: onClick added in code is NOT serialized — rebind
            if (!_shown && _backdrop != null) _backdrop.SetActive(false); // instant — no close animation on scene start
            DisableLegacy();
        }

        // Idempotent: safe after BuildPanel too. Only the two static footer buttons need it — option
        // rows are created fresh per quiz and get their listeners at creation time.
        private void HookListeners()
        {
            if (_confirmBtn != null) { _confirmBtn.onClick.RemoveAllListeners(); _confirmBtn.onClick.AddListener(Confirm); }
            if (_skipBtn != null)    { _skipBtn.onClick.RemoveAllListeners();    _skipBtn.onClick.AddListener(Close); }
        }

        // Retire the scene-authored QuizPopupController so the old popup never shows. Hiding its panel as
        // well as disabling the component matters: disabling alone unsubscribes it from OnQuizShown but
        // leaves any panel it had already opened on screen.
        private void DisableLegacy()
        {
            if (_legacyDisabled) return;
            var legacy = FindFirstObjectByType<QuizPopupController>();
            if (legacy != null)
            {
                if (legacy.popupPanel != null) legacy.popupPanel.SetActive(false);
                legacy.enabled = false;
            }
            _legacyDisabled = true;
        }

        // ── show / hide ──
        private void HandleQuizShown(QuizQuestionSO quiz)
        {
            if (quiz == null) return;
            if (_shown) return;   // one quiz at a time; a second invitation waits for the next day anyway
            if (_root == null) BuildPanel();
            if (_root == null)
            {
                // Never pause for a popup that cannot be drawn (RecordCardUI's lesson — a pause held for an
                // invisible panel stops the day clock forever).
                Debug.LogWarning($"[QuizCardPanelUI] แสดงควิซ '{quiz.quizId}' ไม่ได้ (สร้างแผงไม่สำเร็จ) — ปล่อยเวลาเดินต่อ");
                return;
            }

            _quiz = quiz;
            _selected = -1;
            _revealed = false;
            Populate(quiz);

            _shown = true;
            // UIPopIn animates the inner Panel (scale+fade, unscaled time — keeps working while paused).
            UIPopIn.Ensure(_backdrop);
            _backdrop.SetActive(true);
            GameUIStack.Push(this);
            TimeManager.Instance?.Pause(PauseReason.QuizPopup); // นาฬิกาหยุด — popup ควิซ (§3)
        }

        private void Hide()
        {
            _shown = false;
            if (_backdrop != null) UIPopIn.PlayClose(_backdrop); // shrink-out, then SetActive(false) itself
        }

        /// <summary>
        /// Close the popup. If the quiz was answered (revealed), submit the chosen option to
        /// CodexQuizManager — permanent Mastery + Codex unlock on a correct answer. Skipping (Esc or the
        /// "ไว้ทีหลัง" button before answering) submits nothing; the quiz stays answerable in the Codex.
        /// </summary>
        private void Close()
        {
            bool answered = _revealed;
            string quizId = _quiz != null ? _quiz.quizId : null;
            int answer = (_displayToOriginal != null && _selected >= 0 && _selected < _displayToOriginal.Length)
                ? _displayToOriginal[_selected]
                : _selected;

            _quiz = null;
            _selected = -1;
            _revealed = false;

            Hide();
            GameUIStack.Pop(this);
            TimeManager.Instance?.Resume(PauseReason.QuizPopup);

            if (answered && !string.IsNullOrEmpty(quizId))
                CodexQuizManager.Instance.Submit(quizId, answer);
        }

        private void Select(int slot)
        {
            if (_revealed || _quiz == null) return;
            _selected = slot;
            for (int i = 0; i < _optButtons.Count; i++)
                TintFrame(_optButtons[i], i == slot ? CFrameSelect : CFrameOn);
            SetConfirmEnabled(true);
        }

        private void Confirm()
        {
            if (_quiz == null || _selected < 0 || _revealed) return;
            _revealed = true;

            // Map the real correctIndex back to the shuffled display slot so the right button lights up.
            int correctSlot = _displayToOriginal != null
                ? System.Array.IndexOf(_displayToOriginal, _quiz.correctIndex)
                : _quiz.correctIndex;

            for (int i = 0; i < _optButtons.Count; i++)
            {
                var btn = _optButtons[i];
                btn.interactable = false;
                if (i == correctSlot)      TintFrame(btn, CFrameCorrect);
                else if (i == _selected)   TintFrame(btn, CFrameWrong);
                else                       TintFrame(btn, CFrameDim);
            }

            // Explanation is shown in every answered case — right or wrong (QUIZZES.md).
            if (_explain != null) _explain.text = _quiz.explainText ?? "";
            if (_explainRow != null) _explainRow.SetActive(!string.IsNullOrEmpty(_quiz.explainText));

            if (_confirmBtn != null) _confirmBtn.gameObject.SetActive(false);
            if (_skipLabel != null) _skipLabel.text = "ปิด"; // the skip button becomes a plain close
        }

        private void Populate(QuizQuestionSO quiz)
        {
            Color accent = ColorFor(quiz.category);

            if (_eyebrow != null)
            {
                _eyebrow.text = CategoryLabel(quiz.category);
                _eyebrow.color = accent;
            }
            if (_title != null)
            {
                _title.text = string.IsNullOrEmpty(quiz.topicTitle) ? quiz.speaker : quiz.topicTitle;
                _title.color = accent;
            }
            if (_question != null) _question.text = quiz.question ?? "";

            // Codex reward footer — the entry this quiz unlocks (hidden when it unlocks nothing).
            string reward = ResolveCodexName(quiz.codexUnlockId);
            if (_codexText != null) _codexText.text = string.IsNullOrEmpty(reward) ? "" : $"ปลดล็อก Codex : {reward}";
            if (_codexFooter != null) _codexFooter.SetActive(!string.IsNullOrEmpty(reward));

            // Shuffle option positions so the correct answer is not always in the same slot (V4 §12).
            int count = quiz.options != null ? quiz.options.Length : 0;
            _displayToOriginal = MakeShuffledIndices(count);

            foreach (var b in _optButtons) Destroy(b.gameObject);
            _optButtons.Clear();

            for (int i = 0; i < count; i++)
            {
                int slot = i;
                string label = $"{(char)('A' + i)}. {quiz.options[_displayToOriginal[i]]}";
                var btn = MakeButton("Opt", _optContainer, label, COpt, () => Select(slot));
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 92f; le.preferredHeight = 92f;
                _optButtons.Add(btn);
            }

            // Reset the reveal-only rows and the two footer buttons.
            if (_explainRow != null) _explainRow.SetActive(false);
            if (_explain != null) _explain.text = "";
            if (_confirmBtn != null) { _confirmBtn.gameObject.SetActive(true); SetConfirmEnabled(false); }
            if (_skipLabel != null) _skipLabel.text = "ไว้ทีหลัง — อยู่ใน Codex (กด C)";

            LayoutRebuilder.ForceRebuildLayoutImmediate(_root.GetComponent<RectTransform>());
        }

        private void SetConfirmEnabled(bool on)
        {
            if (_confirmBtn == null) return;
            _confirmBtn.interactable = on;
            if (_confirmLabel != null) _confirmLabel.color = on ? CText : CMuted;
        }

        // ── GameUIStack ──
        // Esc dismisses it: a quiz is optional and skipping is a legal outcome (QUIZZES.md).
        bool GameUIStack.IPanel.ClosableByEscape => true;
        void GameUIStack.IPanel.BringToFront() => GameUIStack.RaiseToTop(_backdrop);
        GameObject GameUIStack.IPanel.PanelRoot => _backdrop;
        void GameUIStack.IPanel.CloseFromStack() => Close();

        // ═══════════════ BUILD ═══════════════
        // A vertical stack that sizes itself to its content — the question length and the explanation
        // appearing after the answer both change the height, and a ContentSizeFitter absorbs that without
        // the manual measuring the crisis card has to do for its fixed-anchor body.
        private void BuildPanel()
        {
            _backdrop = NewUI("Backdrop", transform, CBackdrop);
            Stretch(_backdrop, Vector2.zero, Vector2.one);
            var block = _backdrop.AddComponent<Button>();      // swallow clicks behind the card
            block.transition = Selectable.Transition.None;

            _root = NewUI("Panel", _backdrop.transform, CPanel);
            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(PanelWidth, 600f); // width fixed; height driven by the fitter below
            ApplyPanelSkin(_root);

            var vlg = _root.AddComponent<VerticalLayoutGroup>();
            // Padding clears the ~30px metal border of the frame skin so text never sits on the bolts.
            vlg.padding = new RectOffset(44, 44, 40, 40);
            vlg.spacing = 12f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperLeft;

            var fitter = _root.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; // width stays 760
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _eyebrow  = AddText("Eyebrow", 15, CMuted, TextAnchor.UpperLeft);
            _title    = AddText("Title", 27, CText, TextAnchor.UpperLeft);
            _question = AddText("Question", 20, CText, TextAnchor.UpperLeft);

            _optContainer = NewUI("Options", _root.transform, new Color(0f, 0f, 0f, 0f)).transform;
            var ovlg = _optContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            ovlg.spacing = 10f;
            ovlg.childControlWidth = true; ovlg.childControlHeight = false;
            ovlg.childForceExpandWidth = true; ovlg.childForceExpandHeight = false;
            var ofit = _optContainer.gameObject.AddComponent<ContentSizeFitter>();
            ofit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Explanation block — hidden until the player answers.
            _explainRow = NewUI("ExplainRow", _root.transform, new Color(0.06f, 0.07f, 0.08f, 0.9f));
            var erow = _explainRow.AddComponent<VerticalLayoutGroup>();
            erow.padding = new RectOffset(16, 16, 12, 12); erow.spacing = 4f;
            erow.childControlWidth = true; erow.childControlHeight = true;
            erow.childForceExpandWidth = true; erow.childForceExpandHeight = false;
            _explainRow.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _explain = AddTextTo(_explainRow.transform, "Explain", 17, CText, TextAnchor.UpperLeft);
            _explainRow.SetActive(false);

            // Codex reward footer — hidden when the quiz unlocks nothing.
            _codexFooter = NewUI("CodexFooter", _root.transform, new Color(0.10f, 0.16f, 0.22f, 0.9f));
            var cf = _codexFooter.AddComponent<VerticalLayoutGroup>();
            cf.padding = new RectOffset(16, 16, 8, 8);
            cf.childControlWidth = true; cf.childControlHeight = true;
            cf.childForceExpandWidth = true; cf.childForceExpandHeight = false;
            _codexFooter.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _codexText = AddTextTo(_codexFooter.transform, "CodexText", 16, CCodex, TextAnchor.MiddleLeft);
            _codexFooter.SetActive(false);

            // Button row: [ยืนยัน] + [ไว้ทีหลัง]. After the answer, ยืนยัน hides and ไว้ทีหลัง reads "ปิด".
            var btnRow = NewUI("Buttons", _root.transform, new Color(0f, 0f, 0f, 0f));
            var brl = btnRow.AddComponent<HorizontalLayoutGroup>();
            brl.spacing = 12f; brl.childAlignment = TextAnchor.MiddleRight;
            brl.childControlWidth = true; brl.childControlHeight = true;
            brl.childForceExpandWidth = false; brl.childForceExpandHeight = false;
            btnRow.AddComponent<LayoutElement>().minHeight = 52f;

            _skipBtn = MakeButton("Skip", btnRow.transform, "ไว้ทีหลัง — อยู่ใน Codex (กด C)",
                new Color(0.22f, 0.22f, 0.20f, 1f), Close);
            AddButtonSize(_skipBtn, 320f);
            _skipLabel = _skipBtn.GetComponentInChildren<Text>();

            _confirmBtn = MakeButton("Confirm", btnRow.transform, "ยืนยัน", new Color(0.20f, 0.34f, 0.28f, 1f), Confirm);
            AddButtonSize(_confirmBtn, 150f);
            _confirmLabel = _confirmBtn.GetComponentInChildren<Text>();
            SetConfirmEnabled(false);
        }

        private static void AddButtonSize(Button btn, float minWidth)
        {
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.minWidth = minWidth; le.minHeight = 52f; le.preferredHeight = 52f;
        }

        private Text AddText(string name, int size, Color color, TextAnchor anchor)
            => AddTextTo(_root.transform, name, size, color, anchor);

        private Text AddTextTo(Transform parent, string name, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = _font; t.fontSize = size; t.color = color; t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow; t.supportRichText = true;
            return t;
        }

        private void TintFrame(Button btn, Color tint)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img == null) return;
            img.color = _optFrame != null ? tint : COpt; // no frame art → keep the flat colour
        }

        // ── uGUI helpers (shared shape with the crisis card) ──
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

        // Skin the panel background: the metal frame sprite (nine-sliced) when the art is present, else
        // the flat colour + Outline the panels shipped with. ppuMultiplier 3 renders the 90px art border
        // at ~30px on screen; the sprite already carries the interior fill, so its tint stays white.
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

        private Button MakeButton(string name, Transform parent, string text, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var go = NewUI(name, parent, bg);
            var img = go.GetComponent<Image>();
            if (_optFrame != null && (name == "Opt"))
            {
                // Same nine-slice frame the crisis card uses. 1600x666 art, 150px border → scale the
                // border down so the corner bolts do not overlap on a ~92px row.
                img.sprite = _optFrame;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 5f;
                img.color = CFrameOn;
            }
            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(onClick);
            bool isOption = name == "Opt";
            var label = AddTextTo(go.transform, "Label", isOption ? 18 : 17, CText,
                isOption ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
            label.text = text;
            Stretch(label.gameObject, Vector2.zero, Vector2.one);
            float pad = (_optFrame != null && isOption) ? 34f : 14f; // clear the corner bolts on framed rows
            label.rectTransform.offsetMin = new Vector2(pad, 0f);
            label.rectTransform.offsetMax = new Vector2(-pad, 0f);
            return btn;
        }

        // ── quiz helpers (mirror the retired QuizPopupController) ──
        private static int[] MakeShuffledIndices(int count)
        {
            var indices = new int[count];
            for (int i = 0; i < count; i++) indices[i] = i;
            for (int i = count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }
            return indices;
        }

        private static string ResolveCodexName(string codexId)
        {
            if (string.IsNullOrEmpty(codexId) || CodexManager.Instance == null) return "";
            if (!CodexManager.Instance.AllEntries.TryGetValue(codexId, out var entry) || entry == null) return "";
            return !string.IsNullOrEmpty(entry.titleEn) ? entry.titleEn : entry.title;
        }

        private static string CategoryLabel(QuizCategory category)
        {
            switch (category)
            {
                case QuizCategory.Reactor:     return "เครื่องปฏิกรณ์";
                case QuizCategory.Agriculture: return "เกษตรกรรม";
                case QuizCategory.Medical:     return "การแพทย์";
                case QuizCategory.Ethics:      return "จริยธรรม";
                default:                       return "ความรู้";
            }
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
    }
}
