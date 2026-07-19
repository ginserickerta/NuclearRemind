using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NuclearReMind
{
    /// <summary>
    /// Research queue panel (GDD §19 UI) — the live in-game window into the v6.3 knowledge economy:
    ///   · lab state (ruin → repair → level) + active job progress
    ///   · researchable notes with slots×days + cost and a start button
    ///   · unlocked leads with their hints (NOTES.md)
    ///
    /// ★ v6.3 cutover (slice 1): self-contained — auto-spawns onto HUDCanvas and builds its own uGUI in
    /// code (same pattern as LabPanelUI), so no baked prefab / serialized refs are needed. Opens when the
    /// player clicks the Laboratory building (mirrors LabPanelUI.ClickedLab); pauses the day clock while
    /// open (PauseReason.LabPopup). Drives ResearchLab + KnowledgeDB (the v6.3 systems), replacing the
    /// legacy 3-project LabPanelUI/ResearchManager path.
    /// </summary>
    public class ResearchQueuePanel : MonoBehaviour, GameUIStack.IPanel
    {
        static readonly Color CBackdrop = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color CPanel    = new Color(0.10f, 0.10f, 0.09f, 0.98f);
        static readonly Color CBorder   = new Color(0.32f, 0.30f, 0.26f, 1f);
        static readonly Color CText      = new Color(0.93f, 0.92f, 0.86f, 1f);
        static readonly Color CMuted     = new Color(0.62f, 0.62f, 0.56f, 1f);
        static readonly Color CGold      = new Color(0.96f, 0.80f, 0.35f, 1f);
        static readonly Color CBtn       = new Color(0.20f, 0.34f, 0.24f, 1f);
        static readonly Color CBtnDim    = new Color(0.15f, 0.15f, 0.13f, 1f);
        static readonly Color CRepair    = new Color(0.40f, 0.28f, 0.14f, 1f);

        private Font _font;
        private bool _shown;
        private Vector2Int _labCell;

        private GameObject _backdrop, _root;
        private Text _statusText, _activeText;
        private Transform _listContainer;
        private Button _repairButton;
        private readonly List<GameObject> _rows = new List<GameObject>();

        // ★ AfterSceneLoad fires once at the app's first scene (MainMenu); the instance spawned there is
        //   destroyed on LoadScene(Gamescene) → re-spawn on every sceneLoaded (same as LabPanelUI).
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
                Debug.LogError($"[ResearchQueuePanel] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private static void AutoSpawnUnsafe()
        {
            // ★ 2026-07-19: superseded by ResearchLabPanelUI (HTML-mockup skin, same ResearchLab/KnowledgeDB
            //   backend). Kept compiled as the rollback path — delete this early-return to restore it.
            return;
#pragma warning disable CS0162 // unreachable — intentional dormant stub (same pattern as LabPanelUI)
            if (FindFirstObjectByType<ResearchQueuePanel>() != null) return;
            var canvas = FindBestCanvas();
            if (canvas == null) return; // no HUD yet (e.g. MainMenu) — a later sceneLoaded will retry
            var go = new GameObject("ResearchQueuePanel (auto)");
            go.transform.SetParent(canvas.transform, false);
            go.AddComponent<ResearchQueuePanel>();
#pragma warning restore CS0162
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

        private void Awake() => _font = LoadFont();

        private static Font LoadFont()
        {
            return UIFonts.Body;
        }

        private void OnEnable()
        {
            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnLeadUnlocked += HandleChanged;
                EventManager.Instance.OnResearchNoteStarted += HandleChanged;
                EventManager.Instance.OnResearchNoteCompleted += HandleChanged;
                EventManager.Instance.OnResearchLabRepaired += Refresh;
                EventManager.Instance.OnDayStarted += HandleDayStarted;
            }
        }

        private void OnDisable()
        {
            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnLeadUnlocked -= HandleChanged;
                EventManager.Instance.OnResearchNoteStarted -= HandleChanged;
                EventManager.Instance.OnResearchNoteCompleted -= HandleChanged;
                EventManager.Instance.OnResearchLabRepaired -= Refresh;
                EventManager.Instance.OnDayStarted -= HandleDayStarted;
            }
        }

        private void HandleChanged(string _)          { if (_shown) Refresh(); }
        private void HandleDayStarted(int day, bool t) { if (_shown) Refresh(); }

        private void Start()
        {
            BuildPanel();
            Hide();
        }

        private void Update()
        {
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (Input.GetMouseButtonDown(0) && !overUI && !_shown && ClickedLab()) Open();
        }

        // Clicked the Laboratory building — grid footprint first (WebGL-proven), sprite bounds fallback.
        private bool ClickedLab()
        {
            var im = InputManager.Instance;
            var reg = BuildingRegistry.Instance;
            if (im == null || reg == null) return false;

            if (reg.TryGetBuildingAt(im.GetMouseGridPosition(), out var origin, out var data) && data != null
                && data.buildingType == BuildingType.Laboratory)
            { _labCell = origin; return true; }

            var t = BuildingClickTarget.PickAt(im.GetMouseWorldPosition(), x => x.data.buildingType == BuildingType.Laboratory);
            if (t != null) { _labCell = t.originCell; return true; }
            return false;
        }

        private void Open()
        {
            _shown = true;
            if (_backdrop != null) _backdrop.SetActive(true);
            GameUIStack.Push(this);
            InnerVoiceDirector.Instance?.Fire("V04"); // BARKS.md: "เปิดเมนูวิจัยครั้งแรก" (Fire is once-only)
            TimeManager.Instance?.Pause(PauseReason.LabPopup); // §15: time stops while a popup is open
            Refresh();
        }

        private void Hide()
        {
            _shown = false;
            if (_backdrop != null) _backdrop.SetActive(false);
            GameUIStack.Pop(this);
            TimeManager.Instance?.Resume(PauseReason.LabPopup);
        }

        // ── GameUIStack.IPanel ──
        bool GameUIStack.IPanel.ClosableByEscape => true;
        void GameUIStack.IPanel.BringToFront() => GameUIStack.RaiseToTop(_backdrop);
        GameObject GameUIStack.IPanel.PanelRoot => _backdrop;
        void GameUIStack.IPanel.CloseFromStack() => Hide();

        // ═══════════════ POPULATE ═══════════════
        public void Refresh()
        {
            if (_root == null) return;
            var lab = ResearchLab.Instance;
            var db = KnowledgeDB.Instance;
            if (lab == null)
            {
                if (_statusText != null) _statusText.text = "⏳ ระบบวิจัยยังไม่พร้อม";
                return;
            }

            if (_statusText != null) _statusText.text = LabStatusLine(lab);
            if (_activeText != null) _activeText.text = ActiveJobLine(lab);
            if (_repairButton != null) _repairButton.gameObject.SetActive(lab.IsRuined && !lab.RepairPaid);

            RebuildList(lab, db);
        }

        /// <summary>Status line — static + pure for tests.</summary>
        public static string LabStatusLine(ResearchLab lab)
        {
            if (lab.IsRuined)
                return lab.RepairPaid
                    ? $"กำลังซ่อม {lab.RepairProgress:0}/{GameConfigSO.Instance.repairDays} วัน (ต้องมีคน lab ≥ {GameConfigSO.Instance.repairWorkers})"
                    : $"ห้องวิจัยเป็นซาก — ซ่อม: เหล็ก {GameConfigSO.Instance.repairIron} · {GameConfigSO.Instance.repairDays} วัน · {GameConfigSO.Instance.repairWorkers} คน";
            return $"ห้องวิจัย Lv{lab.Level} · ที่นั่งนักวิจัย {lab.ResearcherSlots} · คิว {lab.QueueCapacity}";
        }

        public static string ActiveJobLine(ResearchLab lab)
        {
            if (lab.IsRuined || lab.ActiveJob == null) return "— ไม่มีงานวิจัย —";
            var j = lab.ActiveJob;
            var sb = new StringBuilder($"⏳ {j.note.title}  {j.progress:0.#}/{j.note.daysRequired} วัน");
            foreach (var q in lab.QueuedNoteIds) sb.Append($"\n   ▸ รอคิว: {q}");
            return sb.ToString();
        }

        private void RebuildList(ResearchLab lab, KnowledgeDB db)
        {
            if (_listContainer == null || db == null) return;

            foreach (var go in _rows) Destroy(go);
            _rows.Clear();

            foreach (var note in db.AllNotes)
            {
                if (db.HasNote(note.noteId))
                {
                    AddRow($"✅ {note.title}", CMuted);
                }
                else if (db.IsResearchable(note))
                {
                    string id = note.noteId;
                    AddButtonRow(NoteOfferLine(note), () => { ResearchLab.Instance?.TryStartResearch(id); Refresh(); });
                }
                else if (!string.IsNullOrEmpty(note.requiredLead) && db.HasLead(note.requiredLead))
                {
                    AddRow($"⛔ {note.title} — ขาด prerequisite", CMuted);
                }
                else
                {
                    AddRow("[ล็อก] ??? — ยังไม่มีเบาะแส", CMuted); // rule #4: never reveal before research
                }
            }

            foreach (var kvp in KnowledgeDB.LeadMap)
            {
                if (!db.HasLead(kvp.Key)) continue;
                var note = db.GetNote(kvp.Value);
                if (note != null && !db.HasNote(note.noteId))
                    AddRow($"\"{note.leadHint}\"", CGold);
            }
        }

        /// <summary>"เชื้อเพลิงที่ซ่อนอยู่ในน้ำ · 2 คน × 2 วัน · P80" — static for tests.</summary>
        public static string NoteOfferLine(ResearchNoteSO note)
        {
            var cost = new StringBuilder();
            if (note.costPower > 0) cost.Append($" P{note.costPower}");
            if (note.costIron > 0) cost.Append($" Fe{note.costIron}");
            if (note.costLabMat > 0) cost.Append($" Lab{note.costLabMat}");
            return $"{note.title} · {note.researcherSlots} คน × {note.daysRequired} วัน ·{cost}";
        }

        // ═══════════════ BUILD (runtime uGUI) ═══════════════
        private void BuildPanel()
        {
            // full-screen dark backdrop — click outside closes
            _backdrop = NewUI("Backdrop", transform, CBackdrop);
            Stretch(_backdrop, Vector2.zero, Vector2.one);
            var bdBtn = _backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            // centered panel
            _root = NewUI("Panel", _backdrop.transform, CPanel);
            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(760f, 760f);
            var outline = _root.AddComponent<Outline>();
            outline.effectColor = CBorder; outline.effectDistance = new Vector2(2f, -2f);
            // NewUI already gave _root a raycast-target Image, so clicks on the panel are absorbed here
            // and never reach the backdrop's close button — no extra Image needed.

            var title = MakeText("Title", _root.transform, "ห้องวิจัย (Research)", 30, CGold, TextAnchor.UpperLeft);
            Anchor(title.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -20f), new Vector2(-24f, -64f));

            var close = MakeButton("Close", _root.transform, "✕", CBtnDim, Hide);
            var crt = close.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-16f, -16f); crt.sizeDelta = new Vector2(48f, 48f);

            _statusText = MakeText("Status", _root.transform, "", 20, CText, TextAnchor.UpperLeft);
            Anchor(_statusText.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -78f), new Vector2(-24f, -132f));

            _activeText = MakeText("Active", _root.transform, "", 18, CMuted, TextAnchor.UpperLeft);
            Anchor(_activeText.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -136f), new Vector2(-24f, -200f));

            // repair button (shown only when ruined & unpaid)
            _repairButton = MakeButton("Repair", _root.transform,
                $"จ่ายเหล็ก {GameConfigSO.Instance.repairIron} เริ่มซ่อม", CRepair,
                () => { ResearchLab.Instance?.StartRepair(); Refresh(); });
            var rrt = _repairButton.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0f, 1f); rrt.anchorMax = new Vector2(1f, 1f); rrt.pivot = new Vector2(0.5f, 1f);
            rrt.offsetMin = new Vector2(24f, 0f); rrt.offsetMax = new Vector2(-24f, 0f);
            rrt.anchoredPosition = new Vector2(0f, -206f); rrt.sizeDelta = new Vector2(0f, 48f);

            // scrollable note list
            var listRoot = NewUI("List", _root.transform, new Color(0f, 0f, 0f, 0.25f));
            Anchor(listRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 24f), new Vector2(-24f, -262f));
            var vlg = listRoot.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f; vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            _listContainer = listRoot.transform;
        }

        private void AddRow(string text, Color color)
        {
            var t = MakeText("Row", _listContainer, text, 18, color, TextAnchor.MiddleLeft);
            var le = t.gameObject.AddComponent<LayoutElement>(); le.minHeight = 30f; le.preferredHeight = 30f;
            _rows.Add(t.gameObject);
        }

        private void AddButtonRow(string text, UnityEngine.Events.UnityAction onClick)
        {
            var btn = MakeButton("NoteBtn", _listContainer, text, CBtn, onClick);
            var le = btn.gameObject.AddComponent<LayoutElement>(); le.minHeight = 40f; le.preferredHeight = 40f;
            _rows.Add(btn.gameObject);
        }

        // ── uGUI helpers ──
        private static GameObject NewUI(string name, Transform parent, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = bg;
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
            Stretch(label.gameObject, Vector2.zero, Vector2.one);
            return btn;
        }
    }
}
