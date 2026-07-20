using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Note completion popup (GDD §19 + NOTES.md) — fires on OnResearchNoteCompleted:
    ///   page 1: knowledgeBody (the actual knowledge, verbatim from the asset)
    ///   page 2: the 1-line NPC reaction (completionSpeaker/completionLine)
    ///
    /// ★ Deliberately NOT routed through BarkManager — barks are capped at 2/day but this
    ///   dialogue must ALWAYS show (NOTES.md implement note).
    /// Pauses the day clock while open (PauseReason.NotePopup — pause-reason stack §3,
    /// never Time.timeScale).
    ///
    /// ★ Self-contained since 2026-07-20: auto-spawns onto HUDCanvas and builds its own uGUI, the same
    /// shape as CrisisCardPanelUI/CodexPanelUI. It previously relied on Inspector-wired fields while
    /// living in no scene and having no spawn hook, so the completion event fired into a listener that
    /// did not exist and finishing research showed the player nothing at all.
    /// </summary>
    public class NoteCardPopup : MonoBehaviour, GameUIStack.IPanel
    {
        static readonly Color CBackdrop = new Color(0f, 0f, 0f, 0.68f);
        static readonly Color CPanel    = new Color(0.09f, 0.11f, 0.13f, 0.99f);
        static readonly Color CBorder   = new Color(0.22f, 0.48f, 0.55f, 1f);
        static readonly Color CTitle    = new Color(0.42f, 0.82f, 0.88f, 1f);
        static readonly Color CText     = new Color(0.90f, 0.92f, 0.93f, 1f);
        static readonly Color CSpeaker  = new Color(0.78f, 0.72f, 0.46f, 1f);
        static readonly Color CBtn      = new Color(0.16f, 0.30f, 0.34f, 1f);
        static readonly Color CEyebrow  = new Color(0.55f, 0.62f, 0.66f, 1f);

        private const float PanelWidth = 720f;
        private const float PanelHeight = 460f;

        private Font _font;
        private GameObject _backdrop, _root;
        private Text _eyebrow, _title, _speaker, _body, _btnLabel;
        private bool _shown;

        private ResearchNoteSO _note;
        private int _page;                                        // 0 = knowledgeBody, 1 = NPC line
        private readonly Queue<ResearchNoteSO> _pending = new Queue<ResearchNoteSO>();

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
                if (FindFirstObjectByType<NoteCardPopup>() != null) return;
                var canvas = FindBestCanvas();
                if (canvas == null) return;
                var go = new GameObject("NoteCardPopup (auto)");
                go.transform.SetParent(canvas.transform, false);
                go.AddComponent<NoteCardPopup>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NoteCardPopup] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
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

        private void Awake() => _font = UIFonts.Body;

        private void OnEnable()
        {
            if (EventManager.Instance != null)
                EventManager.Instance.OnResearchNoteCompleted += HandleNoteCompleted;
        }

        private void OnDisable()
        {
            if (EventManager.Instance != null)
                EventManager.Instance.OnResearchNoteCompleted -= HandleNoteCompleted;
        }

        private void Start()
        {
            if (_root == null) BuildPanel();
            if (!_shown) HideRoot();
        }

        private void HandleNoteCompleted(string noteId)
        {
            var db = KnowledgeDB.Instance;
            if (db == null) return;
            var note = db.GetNote(noteId);
            if (note == null) return;
            Show(note);
        }

        /// <summary>Open the two-page popup for a note (public so tests/panels can drive it).</summary>
        public void Show(ResearchNoteSO note)
        {
            if (note == null) return;

            // Two notes can finish on the same tick — queue rather than overwrite, so neither is lost.
            if (_shown) { _pending.Enqueue(note); return; }

            if (_root == null) BuildPanel();
            if (_root == null)
            {
                // Never pause for a popup that cannot be drawn. RecordCardUI learned this the hard way:
                // a pause taken for an invisible panel is held forever and stops the day clock.
                Debug.LogWarning($"[NoteCardPopup] แสดงโน้ต '{note.noteId}' ไม่ได้ (สร้างแผงไม่สำเร็จ) — ปล่อยเวลาเดินต่อ");
                return;
            }

            _note = note;
            _page = 0;
            _shown = true;
            _backdrop.SetActive(true);
            GameUIStack.Push(this);
            TimeManager.Instance?.Pause(PauseReason.NotePopup); // นาฬิกาหยุด — popup Note (§3)
            Render();
        }

        /// <summary>Continue button: knowledgeBody → NPC line → close.</summary>
        public void Advance()
        {
            // ข้ามหน้า NPC ถ้าโน้ตไม่มีบท (ไม่ควรเกิด — NOTES.md มีครบ 8)
            if (_page == 0 && !string.IsNullOrEmpty(_note?.completionLine))
            {
                _page = 1;
                Render();
                return;
            }
            Close();
        }

        private void Render()
        {
            if (_note == null) return;

            if (_page == 0)
            {
                if (_eyebrow != null) _eyebrow.text = "วิจัยสำเร็จ";
                if (_title != null)   _title.text = _note.title;
                if (_speaker != null) _speaker.text = "";
                if (_body != null)    _body.text = _note.knowledgeBody;
                if (_btnLabel != null)
                    _btnLabel.text = string.IsNullOrEmpty(_note.completionLine) ? "ปิด" : "ต่อไป";
            }
            else
            {
                if (_eyebrow != null) _eyebrow.text = "";
                if (_title != null)   _title.text = "";
                if (_speaker != null) _speaker.text = _note.completionSpeaker;
                if (_body != null)    _body.text = $"“{_note.completionLine}”";
                if (_btnLabel != null) _btnLabel.text = "ปิด";
            }
        }

        private void Close()
        {
            _note = null;
            _page = 0;
            _shown = false;
            HideRoot();
            GameUIStack.Pop(this);
            TimeManager.Instance?.Resume(PauseReason.NotePopup);

            if (_pending.Count > 0) Show(_pending.Dequeue());
        }

        private void HideRoot()
        {
            if (_backdrop != null) _backdrop.SetActive(false);
        }

        // ── GameUIStack ──
        // Informational, so Esc may dismiss it — unlike a crisis card, nothing is being decided here.
        bool GameUIStack.IPanel.ClosableByEscape => true;
        void GameUIStack.IPanel.BringToFront() => GameUIStack.RaiseToTop(_backdrop);
        GameObject GameUIStack.IPanel.PanelRoot => _backdrop;
        void GameUIStack.IPanel.CloseFromStack() => Close();

        // ═══════════════ BUILD ═══════════════
        private void BuildPanel()
        {
            _backdrop = NewUI("Backdrop", transform, CBackdrop);
            Stretch(_backdrop, Vector2.zero, Vector2.one);
            var block = _backdrop.AddComponent<Button>();      // swallow clicks behind the card
            block.transition = Selectable.Transition.None;

            _root = NewUI("Panel", _backdrop.transform, CPanel);
            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            var outline = _root.AddComponent<Outline>();
            outline.effectColor = CBorder; outline.effectDistance = new Vector2(2f, -2f);

            // Anchor() takes offsetMin as (left, BOTTOM) and offsetMax as (right, TOP) — passing them the
            // other way round yields a negative height and the row vanishes. This bit CrisisCardPanelUI.
            _eyebrow = MakeText("Eyebrow", _root.transform, "", 15, CEyebrow, TextAnchor.UpperLeft);
            Anchor(_eyebrow.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -50f), new Vector2(-30f, -24f));

            _title = MakeText("Title", _root.transform, "", 28, CTitle, TextAnchor.UpperLeft);
            Anchor(_title.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -98f), new Vector2(-30f, -52f));

            _speaker = MakeText("Speaker", _root.transform, "", 19, CSpeaker, TextAnchor.UpperLeft);
            Anchor(_speaker.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -128f), new Vector2(-30f, -100f));

            _body = MakeText("Body", _root.transform, "", 19, CText, TextAnchor.UpperLeft);
            Anchor(_body.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -(PanelHeight - 92f)), new Vector2(-30f, -136f));

            var btn = MakeButton("Continue", _root.transform, "ต่อไป", CBtn, Advance);
            Anchor(btn.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-110f, 24f), new Vector2(110f, 68f));
            _btnLabel = btn.GetComponentInChildren<Text>();
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
            t.verticalOverflow = VerticalWrapMode.Truncate; t.supportRichText = true;
            return t;
        }

        private Button MakeButton(string name, Transform parent, string text, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var go = NewUI(name, parent, bg);
            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(onClick);
            var label = MakeText("Label", go.transform, text, 18, CText, TextAnchor.MiddleCenter);
            Stretch(label.gameObject, Vector2.zero, Vector2.one);
            return btn;
        }
    }
}
