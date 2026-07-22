using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NuclearReMind
{
    /// <summary>
    /// Research Lab panel — v2 skin ported from nuclear_remind_research_lab_thai_v2.html (the approved
    /// mockup). Replaces ResearchQueuePanel as the live window into the v6.3 knowledge economy; the old
    /// panel stays compiled as the rollback path (its AutoSpawn is a dormant stub).
    ///
    /// View layer ONLY — every mechanic stays in the real systems:
    ///   · ruin → repair            ResearchLab (Iron 80 · 2 days · 2 workers, all from GameConfigSO)
    ///   · notes / leads / gating   KnowledgeDB (rule #4: locked notes stay ???, never revealed)
    ///   · engineer staffing        WorkerAssignmentManager via RaiseWorkerAssignRequested (cap-clamped)
    ///   · record decryption        DataRecovery (PASSIVE — the mockup's decode buttons are dropped;
    ///                              the real system decrypts as the lab runs, so rows show live progress)
    ///   · mastery chips            MasteryRegistry via CodexQuizManager.GetAll() (earned = quiz answered,
    ///                              not research — chips link the lab view to the Codex loop)
    ///
    /// Mockup rules NOT carried over (conflict with shipped systems / CONFIG.md): "2 engineers = 1 slot",
    /// the CORE ≥ 40% repair gate, and per-record decode buttons. Numbers all come from GameConfigSO and
    /// note assets (rule #2). Rounded corners come from RoundedSprite (runtime 9-slice, no assets).
    /// </summary>
    public class ResearchLabPanelUI : MonoBehaviour, GameUIStack.IPanel
    {
        // ── palette (hex lifted verbatim from the mockup) ──
        static readonly Color CBackdrop   = new Color(0f, 0f, 0f, 0.78f); // darker dim — the mockup sits on a near-black page
        static readonly Color CRing       = Hex("#171310");   // box-shadow ring around the card
        static readonly Color CCard       = Hex("#211d17");   // main card
        static readonly Color CCardBorder = Hex("#4a3d28");
        static readonly Color CHead       = Hex("#2b2418");
        static readonly Color CHeadLine   = Hex("#6b5a3f");
        static readonly Color CSection    = Hex("#1c1813");
        static readonly Color CSectionBd  = Hex("#3a3020");
        static readonly Color CStatCard   = Hex("#26211a");
        static readonly Color CText       = Hex("#e8dcc0");
        static readonly Color CMuted      = Hex("#9a8a6a");
        static readonly Color CDim        = Hex("#7a6d52");
        static readonly Color CFaint      = Hex("#5f5648");
        static readonly Color CGold       = Hex("#d9a441");
        static readonly Color CPink       = Hex("#ed93b1");
        static readonly Color CBlue       = Hex("#85b7eb");
        static readonly Color CGreen      = Hex("#97c459");
        static readonly Color CTeal       = Hex("#5dcaa5");
        static readonly Color CTrack      = Hex("#0f0d0a");
        static readonly Color CFillGreen  = Hex("#639922");
        static readonly Color CAmber      = Hex("#efa027");

        private Font _font;
        private bool _shown;
        private Vector2Int _labCell;
        private string _filter = "all"; // all / notes / records

        private GameObject _backdrop, _root, _repairBlock, _mainBlock, _repairBarWrap;
        private float _edge; // content inset that clears the metal frame border (0 on the fallback look)
        private Text _statusPill, _repairLine, _repairEngDisplay;
        private Image _statusPillBg, _repairFill;
        private Button _repairBtn;
        private Text _statEng, _statSlots, _statLabor, _statRec, _engDisplay;
        private GameObject _laborWarn, _ratioWarn;
        private Text _laborWarnText, _ratioWarnText, _masteryHead;
        private readonly List<(Button btn, Text lbl, Image bg, string id)> _tabs = new List<(Button, Text, Image, string)>();
        private Transform _listContainer, _masteryContainer;
        private readonly List<GameObject> _rows = new List<GameObject>();
        private readonly List<GameObject> _chips = new List<GameObject>();

        // ── auto-spawn (same pattern as the panel it replaces) ──
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
                Debug.LogError($"[ResearchLabPanelUI] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private static void AutoSpawnUnsafe()
        {
            if (FindFirstObjectByType<ResearchLabPanelUI>() != null) return;
            var canvas = FindBestCanvas();
            if (canvas == null) return; // MainMenu — a later sceneLoaded retries
            var go = new GameObject("ResearchLabPanelUI (auto)");
            go.transform.SetParent(canvas.transform, false);
            go.AddComponent<ResearchLabPanelUI>();
            // Success is logged because on WebGL "the panel never spawned" and "the panel ignored the
            // click" look identical from the player's seat — this line is how the two are told apart.
            Debug.Log($"[ResearchLabPanelUI] AutoSpawn — scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, canvas={canvas.name}");
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
            _font = LoadFont();
            _panelFrame = Resources.Load<Sprite>("CardUI/panel_frame"); // metal skin — same as the card/quiz panels

            // ★ 2026-07-23 (owner art): pixel-art skin set for the lab — slate boxes + baked-text metal
            //   buttons. Every Load is null-safe; a missing sprite falls back to the code-drawn look.
            _sprBox        = Resources.Load<Sprite>("ResearchUI/box_slate");
            _sprBtnStart   = Resources.Load<Sprite>("ResearchUI/btn_start");
            _sprBtnDecode  = Resources.Load<Sprite>("ResearchUI/btn_decode");
            _sprBtnPlus    = Resources.Load<Sprite>("ResearchUI/btn_plus");
            _sprBtnMinus   = Resources.Load<Sprite>("ResearchUI/btn_minus");
            _sprTabAll     = Resources.Load<Sprite>("ResearchUI/btn_filter_all");
            _sprTabNotes   = Resources.Load<Sprite>("ResearchUI/btn_filter_notes");
            _sprTabRecords = Resources.Load<Sprite>("ResearchUI/btn_filter_records");

            // ★ 2026-07-23 (owner): editable prefabs. When "Setup Research Lab Prefabs" has been run,
            //   the whole panel + every row kind comes from Resources/ResearchUI/*.prefab so the owner
            //   tunes position/size/color in the Editor. Missing prefab = code-built look (fallback).
            _pfPanel        = Resources.Load<GameObject>("ResearchUI/ResearchLabPanel");
            _pfRowNote      = Resources.Load<GameObject>("ResearchUI/Row_Note");
            _pfRowRecActive = Resources.Load<GameObject>("ResearchUI/Row_RecordActive");
            _pfRowRecDone   = Resources.Load<GameObject>("ResearchUI/Row_RecordDone");
            _pfRowSimple    = Resources.Load<GameObject>("ResearchUI/Row_Simple");
            _pfChip         = Resources.Load<GameObject>("ResearchUI/Chip_Mastery");
        }

        private Sprite _panelFrame;
        private Sprite _sprBox, _sprBtnStart, _sprBtnDecode, _sprBtnPlus, _sprBtnMinus;
        private Sprite _sprTabAll, _sprTabNotes, _sprTabRecords;
        private GameObject _pfPanel, _pfRowNote, _pfRowRecActive, _pfRowRecDone, _pfRowSimple, _pfChip;

        /// <summary>Skin a code-drawn box with the slate plate (nine-slice). False = sprite missing.</summary>
        private bool ApplySlate(GameObject go, float ppuMult = 11f)
        {
            if (_sprBox == null || go == null) return false;
            var img = go.GetComponent<Image>();
            if (img == null) return false;
            img.sprite = _sprBox;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ppuMult;
            img.color = Color.white;
            return true;
        }

        /// <summary>Button from a baked-art sprite (text/icon already in the image). preserveAspect keeps it undistorted.</summary>
        private Button SpriteBtn(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            UIClickPop.Attach(go);
            return btn;
        }

        private void OnEnable()
        {
            var em = EventManager.Instance;
            if (em == null) return;
            em.OnDayStarted += HandleDayStarted;
            em.OnLeadUnlocked += HandleChangedStr;
            em.OnResearchNoteStarted += HandleChangedStr;
            em.OnResearchNoteCompleted += HandleChangedStr;
            em.OnResearchLabRepaired += HandleRepaired;
            em.OnWorkerAssignmentChanged += HandleAssignChanged;
            // ★ fires AFTER WAM's ReconcileJobPool moved bodies into the "lab" job — the assignment
            //   event alone arrives one step early, leaving crew/engineer counts lagging each click.
            em.OnWorkerPoolChanged += HandlePoolChanged;
            em.OnRecordRecovered += HandleRecord;
            em.OnMasteryEarned += HandleChangedStr;
        }

        private void OnDisable()
        {
            var em = EventManager.Instance;
            if (em == null) return;
            em.OnDayStarted -= HandleDayStarted;
            em.OnLeadUnlocked -= HandleChangedStr;
            em.OnResearchNoteStarted -= HandleChangedStr;
            em.OnResearchNoteCompleted -= HandleChangedStr;
            em.OnResearchLabRepaired -= HandleRepaired;
            em.OnWorkerAssignmentChanged -= HandleAssignChanged;
            em.OnWorkerPoolChanged -= HandlePoolChanged;
            em.OnRecordRecovered -= HandleRecord;
            em.OnMasteryEarned -= HandleChangedStr;
        }

        private void HandleDayStarted(int d, bool t)            { if (_shown) Refresh(); }
        private void HandleChangedStr(string _)                 { if (_shown) Refresh(); }
        private void HandleRepaired()                           { if (_shown) Refresh(); }
        private void HandleAssignChanged(Vector2Int c, int n)   { if (_shown) Refresh(); }
        private void HandlePoolChanged(int idle, int total)     { if (_shown) Refresh(); }
        private void HandleRecord(RecordCardSO _)               { if (_shown) Refresh(); }

        private void Start()
        {
            // Prefab path first (owner-editable); the code-built panel is the fallback.
            if (!TryBindPrefab()) BuildPanel();
            Hide();
        }

        private void Update()
        {
            // Q/E hotkeys while open — same shortcut pair as BuildingUpgradeUI, same event path as the
            // −/+ buttons (WAM clamps caps/idle, so keys can never bypass the rules).
            if (_shown)
            {
                if (Input.GetKeyDown(KeyCode.Q)) EventManager.Instance?.RaiseWorkerAssignRequested(_labCell, -1);
                else if (Input.GetKeyDown(KeyCode.E)) EventManager.Instance?.RaiseWorkerAssignRequested(_labCell, +1);
                TrackLiveProgress();
                return;
            }

            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (Input.GetMouseButtonDown(0) && !overUI && ClickedLab()) Open();
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
            if (_backdrop != null) { UIPopIn.Ensure(_backdrop); _backdrop.SetActive(true); }
            GameUIStack.Push(this);
            TimeManager.Instance?.Pause(PauseReason.LabPopup); // §15: time stops while a popup is open
            Refresh();
        }

        private void Hide()
        {
            _shown = false;
            GameUIStack.Pop(this);
            TimeManager.Instance?.Resume(PauseReason.LabPopup);
            if (_backdrop != null) UIPopIn.PlayClose(_backdrop);
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
            var cfg = GameConfigSO.Instance;
            if (lab == null || cfg == null) return;

            bool ruined = lab.IsRuined;
            if (_repairBlock != null) _repairBlock.SetActive(ruined);
            if (_mainBlock != null) _mainBlock.SetActive(!ruined);

            // status pill — ruined red / repairing amber / ready green (mockup #lab-status)
            if (_statusPill != null && _statusPillBg != null)
            {
                if (ruined && !lab.RepairPaid)
                { _statusPill.text = "ซากปรักหักพัง"; StylePill(Hex("#5a2418"), Hex("#f0997b"), Hex("#993c1d")); }
                else if (ruined)
                { _statusPill.text = "กำลังซ่อม"; StylePill(Hex("#2a1e08"), CAmber, Hex("#854f0b")); }
                else
                { _statusPill.text = "พร้อมใช้งาน"; StylePill(Hex("#1a3312"), CGreen, Hex("#3b6d11")); }
            }

            if (ruined)
            {
                if (_repairLine != null)
                    _repairLine.text = lab.RepairPaid
                        ? $"กำลังซ่อม {lab.RepairProgress:0}/{cfg.repairDays} วัน · ต้องมีคนซ่อมครบทุกวันถึงจะคืบ"
                        : $"ต้องซ่อมก่อนใช้งาน · เหล็ก {cfg.repairIron} · {cfg.repairDays} วัน · {cfg.repairWorkers} คน";
                if (_repairBtn != null) _repairBtn.gameObject.SetActive(!lab.RepairPaid);
                if (_repairBarWrap != null) _repairBarWrap.SetActive(lab.RepairPaid);
                if (_repairFill != null)
                    SetFill(_repairFill, cfg.repairDays > 0 ? lab.RepairProgress / cfg.repairDays : 0f);

                // ★ repair crew — TickRepair reads workers on job "lab", so the assign control MUST be
                //   available while ruined too (the main block's −/+ is hidden here). Same event path.
                if (_repairEngDisplay != null)
                {
                    var wmR = WorkerManager.Instance;
                    int crew = wmR != null ? wmR.GetWorkers(WorkerJobs.Lab).Count : 0;
                    bool enough = crew >= cfg.repairWorkers;
                    _repairEngDisplay.text =
                        $"คนซ่อม <color={(enough ? "#97c459" : "#f0997b")}>{crew}</color> / {cfg.repairWorkers}";
                }
                return; // main block hidden — nothing else to fill
            }

            var wm = WorkerManager.Instance;
            var wam = WorkerAssignmentManager.Instance;
            var dr = DataRecovery.Instance;
            int labCount = wm != null ? wm.GetWorkers(WorkerJobs.Lab).Count : 0;
            int idle = wm != null ? wm.GetWorkers(WorkerJobs.Idle).Count : 0;
            int slotsUsed = (lab.ActiveJob != null ? 1 : 0) + lab.QueuedNoteIds.Count;
            int assigned = wam != null ? wam.GetAssigned(_labCell) : 0;

            if (_statEng != null) _statEng.text = labCount.ToString();
            if (_statSlots != null) _statSlots.text = $"{slotsUsed}/{lab.QueueCapacity}";
            if (_statLabor != null) { _statLabor.text = idle.ToString(); _statLabor.color = idle <= 2 ? Hex("#f0997b") : CText; }
            if (_statRec != null) _statRec.text = dr != null ? $"{dr.RecordsRecovered}/{dr.TotalRecords}" : "—";

            if (_engDisplay != null)
            {
                int cap = 0;
                var reg = BuildingRegistry.Instance;
                if (wam != null && reg != null && reg.PlacedBuildings.TryGetValue(_labCell, out var d) && d != null)
                    cap = wam.EffectiveCap(_labCell, d);
                _engDisplay.text = $"{assigned} <color=#7a6d52><size=14>/ {cap} ช่องประจำ</size></color>";
            }

            // warnings — labor drain + staff-ratio stall (both live states, not mockup timers)
            if (_laborWarn != null)
            {
                bool showLabor = assigned > 0;
                _laborWarn.SetActive(showLabor);
                if (showLabor && _laborWarnText != null)
                    _laborWarnText.text = $"⚠ ดึงคนเข้าวิจัย {assigned} คน → เมืองเหลือแรงงานว่าง {idle} คน ผลผลิตส่วนอื่นลดลง";
            }
            if (_ratioWarn != null)
            {
                bool stalled = lab.ActiveJob != null &&
                               labCount < lab.ActiveJob.note.researcherSlots * cfg.staffRatioMin;
                _ratioWarn.SetActive(stalled);
                if (stalled && _ratioWarnText != null)
                    _ratioWarnText.text = $"⏸ นักวิจัยไม่พอ (ต่ำกว่า {cfg.staffRatioMin * 100f:0}% ของ {lab.ActiveJob.note.researcherSlots} ที่ต้องใช้) — งานวิจัยหยุดเดิน";
            }

            RefreshTabs();
            RebuildList(lab, cfg, dr);
            RebuildMastery();
        }

        private void StylePill(Color bg, Color fg, Color bd)
        {
            _statusPillBg.color = bg;
            _statusPill.color = fg;
            var ol = _statusPillBg.GetComponent<Outline>();
            if (ol != null) ol.effectColor = bd;
        }

        // ─────────── project list ───────────
        private void RebuildList(ResearchLab lab, GameConfigSO cfg, DataRecovery dr)
        {
            if (_listContainer == null) return;
            foreach (var go in _rows) Destroy(go);
            _rows.Clear();
            _activeBarFill = null; // the row that owned it is gone; AddBar re-caches if a job is active

            var db = KnowledgeDB.Instance;
            var wm = WorkerManager.Instance;

            // ── notes (หมวด "ความรู้") ──
            if (_filter != "records" && db != null)
            {
                int hidden = 0;
                var ordered = new List<ResearchNoteSO>(db.AllNotes);
                // stable, readable order: active → queued → researchable → blocked → done (hidden collapse)
                int Rank(ResearchNoteSO n)
                {
                    if (lab.ActiveJob != null && lab.ActiveJob.note.noteId == n.noteId) return 0;
                    for (int qi = 0; qi < lab.QueuedNoteIds.Count; qi++)
                        if (lab.QueuedNoteIds[qi] == n.noteId) return 1;
                    if (db.IsResearchable(n)) return 2;
                    if (!string.IsNullOrEmpty(n.requiredLead) && db.HasLead(n.requiredLead)) return 3;
                    return db.HasNote(n.noteId) ? 4 : 5;
                }
                ordered.Sort((a, b) => Rank(a).CompareTo(Rank(b)));

                foreach (var note in ordered)
                {
                    int rank = Rank(note);
                    if (rank == 5) { hidden++; continue; } // rule #4 — no lead, no reveal
                    AddNoteRow(note, rank, lab, cfg, wm);
                }
                if (hidden > 0)
                    AddSimpleRow($"[ล็อก] ยังไม่มีเบาะแส — อีก {hidden} หัวข้อ (หาบันทึก/สำรวจเพื่อปลดเบาะแส)", CFaint, 34f);
            }

            // ── records (หมวด "กู้บันทึก") — PASSIVE decode, live progress ──
            if (_filter != "notes" && dr != null)
            {
                for (int i = 0; i < dr.TotalRecords; i++)
                {
                    if (i < dr.RecordsRecovered)
                    {
                        var rec = dr.GetRecord(RecordIdAt(dr, i));
                        AddRecordDoneRow(rec != null && !string.IsNullOrEmpty(rec.archiveTitle)
                            ? rec.archiveTitle : $"บันทึก #{i + 1}");
                    }
                    else if (i == dr.RecordsRecovered)
                        AddRecordActiveRow(dr, cfg, lab);
                    else
                        AddSimpleRow("[ล็อก] บันทึกที่เข้ารหัส — ถอดได้ตามลำดับเท่านั้น", CFaint, 34f);
                }
            }
        }

        // Recovered records come back in order from DataRecovery.Recovered — index into it.
        private static string RecordIdAt(DataRecovery dr, int index)
        {
            int i = 0;
            foreach (var r in dr.Recovered)
            {
                if (i == index) return r.recordId;
                i++;
            }
            return null;
        }

        private void AddNoteRow(ResearchNoteSO note, int rank, ResearchLab lab, GameConfigSO cfg, WorkerManager wm)
        {
            if (AddNoteRowPrefab(note, rank, lab, cfg, wm)) return; // owner-editable template first
            bool done = rank == 4;
            bool active = rank == 0;
            bool queued = rank == 1;
            bool blocked = rank == 3;

            // meta line: cost is display-only here — payment stays inside TryStartResearch (bug #2)
            var cost = new StringBuilder();
            if (note.costPower > 0) cost.Append($" · ไฟ {note.costPower}");
            if (note.costIron > 0) cost.Append($" · เหล็ก {note.costIron}");
            if (note.costLabMat > 0) cost.Append($" · วัสดุแล็บ {note.costLabMat}");
            string meta = done ? "" : $"{note.researcherSlots} คน × {note.daysRequired} วัน{cost}";

            string hint = (!done && !string.IsNullOrEmpty(note.leadHint)) ? $"ℹ \"{note.leadHint}\"" : null;
            bool crit = note.noteId == "tritium"; // the documented win-gate (CLAUDE.md core principle)
            bool bar = active;

            float h = 52f + (meta.Length > 0 ? 18f : 0f) + (hint != null ? 18f : 0f) + (bar ? 15f : 0f);
            var row = MakeRowShell(h, blocked ? 0.55f : 1f);

            // title line
            string icon = done ? "<color=#97c459>✓</color> " : blocked ? "⛔ " : "";
            var title = Txt("Title", row.transform, icon + note.title, 17, done ? CMuted : CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetTL(title.rectTransform, new Vector2(14f, -10f), new Vector2(430f, 24f)); // right column width — clear the action button

            float bx = 14f + EstWidth(note.title, 17f) + (done || blocked ? 26f : 6f);
            bx = AddBadge(row.transform, string.IsNullOrEmpty(note.category) ? "ความรู้" : note.category,
                Hex("#12283f"), CBlue, Hex("#185fa5"), bx);
            if (crit && !done)
                bx = AddBadge(row.transform, "บังคับเพื่อชนะ", Hex("#501313"), Hex("#f09595"), Hex("#a32d2d"), bx);
            if (queued) AddBadge(row.transform, "▸ รอคิว", Hex("#2a1e08"), CAmber, Hex("#854f0b"), bx);

            float y = -32f;
            if (meta.Length > 0)
            {
                var m = Txt("Meta", row.transform, meta, 13, CMuted, TextAnchor.UpperLeft, FontStyle.Normal);
                SetTL(m.rectTransform, new Vector2(14f, y), new Vector2(460f, 18f));
                y -= 18f;
            }
            if (hint != null)
            {
                var hTxt = Txt("Hint", row.transform, hint, 13, CDim, TextAnchor.UpperLeft, FontStyle.Italic);
                SetTL(hTxt.rectTransform, new Vector2(14f, y), new Vector2(460f, 18f));
                y -= 18f;
            }

            // right-side action
            if (rank == 2)
            {
                bool canAfford = lab.CanAfford(note);
                var btn = _sprBtnStart != null
                    ? SpriteBtn("Start", row.transform, _sprBtnStart)   // ★ baked "เริ่มวิจัย" metal plate
                    : RoundBtn("Start", row.transform, "เริ่มวิจัย", 14, CHead, CText, CHeadLine, 4);
                var brt = (RectTransform)btn.transform;
                brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(1f, 1f);
                brt.anchoredPosition = new Vector2(-12f, -6f);
                brt.sizeDelta = _sprBtnStart != null ? new Vector2(170f, 58f) : new Vector2(96f, 32f);
                string id = note.noteId;
                btn.onClick.AddListener(() => { ResearchLab.Instance?.TryStartResearch(id); Refresh(); });
                if (!canAfford) { var cg = btn.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0.4f; } // Notice explains on click
            }
            else if (blocked)
            {
                var b = Txt("Blocked", row.transform, "ขาด prerequisite", 12, CFaint, TextAnchor.UpperRight, FontStyle.Normal);
                SetTR(b.rectTransform, new Vector2(-12f, -12f), new Vector2(160f, 18f));
            }

            if (bar)
            {
                float frac = note.daysRequired > 0 ? lab.ActiveJob.progress / note.daysRequired : 0f;
                bool stalled = wm != null && wm.GetWorkers(WorkerJobs.Lab).Count < note.researcherSlots * cfg.staffRatioMin;
                // Cached so Update can slide it as ResearchLab accrues, without rebuilding the row —
                // a rebuild 4x a second would destroy buttons out from under the player's cursor.
                _activeBarFill = AddBar(row.transform, new Vector2(14f, 8f), 856f, frac, stalled ? Hex("#854f0b") : CGold);
                _activeBarDays = note.daysRequired;
            }

            _rows.Add(row);
        }

        private void AddRecordDoneRow(string recTitle)
        {
            if (AddRecordDoneRowPrefab(recTitle)) return;
            var row = MakeRowShell(38f, 1f);
            var t = Txt("T", row.transform, $"<color=#97c459>✓</color> {recTitle}", 15, CMuted, TextAnchor.MiddleLeft, FontStyle.Normal);
            SetTL(t.rectTransform, new Vector2(14f, -7f), new Vector2(650f, 24f));
            AddBadge(row.transform, "กู้บันทึก", Hex("#2a1230"), CPink, Hex("#993556"), 14f + EstWidth(recTitle, 15f) + 26f);
            _rows.Add(row);
        }

        private void AddRecordActiveRow(DataRecovery dr, GameConfigSO cfg, ResearchLab lab)
        {
            if (AddRecordActiveRowPrefab(dr, cfg, lab)) return;
            var row = MakeRowShell(72f, 1f);
            bool decoding = dr.IsDecoding;

            string head = decoding
                ? $"⏳ กำลังถอดรหัสบันทึกของ Elara #{dr.RecordsRecovered + 1}"
                : $"บันทึกของ Elara #{dr.RecordsRecovered + 1}";
            var t = Txt("T", row.transform, head, 15, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetTL(t.rectTransform, new Vector2(14f, -10f), new Vector2(400f, 22f));
            AddBadge(row.transform, "กู้บันทึก", Hex("#2a1230"), CPink, Hex("#993556"), 14f + EstWidth(head, 15f));

            // Decryption is a deliberate act now, so the row has to say what pressing it costs and what
            // is stalling it — an idle bar with no explanation reads as a bug (owner's report 2026-07-21).
            string sub = decoding
                ? "นักวิจัยที่ว่างจากโครงการช่วยให้เร็วขึ้น"
                : (lab != null && lab.IsRuined
                    ? "ห้องวิจัยพัง — ซ่อมก่อนถึงจะถอดรหัสได้"
                    : "กดถอดรหัสเพื่อเริ่ม — ใช้ห้องวิจัยร่วมกับงานวิจัย นักวิจัยที่ว่างช่วยให้เร็วขึ้น");
            var m = Txt("M", row.transform, sub, 12, CDim, TextAnchor.UpperLeft, FontStyle.Italic);
            SetTL(m.rectTransform, new Vector2(14f, -32f), new Vector2(440f, 18f));

            if (!decoding)
            {
                var btn = _sprBtnDecode != null
                    ? SpriteBtn("Decode", row.transform, _sprBtnDecode) // ★ baked "ถอดรหัส" metal plate
                    : RoundBtn("Decode", row.transform, "ถอดรหัส", 14, CHead, CText, CHeadLine, 4);
                var brt = (RectTransform)btn.transform;
                brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(1f, 1f);
                brt.anchoredPosition = new Vector2(-12f, -6f);
                brt.sizeDelta = _sprBtnDecode != null ? new Vector2(170f, 58f) : new Vector2(96f, 32f);
                btn.onClick.AddListener(() => { DataRecovery.Instance?.StartDecoding(); Refresh(); });
                if (!dr.CanStartDecoding) { var cg = btn.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0.4f; }
            }

            float frac = cfg.dataRecoveryTarget > 0f ? dr.Progress / cfg.dataRecoveryTarget : 0f;
            AddBar(row.transform, new Vector2(14f, 10f), 856f, frac, CPink);
            _rows.Add(row);
        }

        private void AddSimpleRow(string text, Color col, float h)
        {
            if (AddSimpleRowPrefab(text, col, h)) return;
            var row = MakeRowShell(h, 0.55f);
            var t = Txt("T", row.transform, text, 13, col, TextAnchor.MiddleLeft, FontStyle.Normal);
            SetTL(t.rectTransform, new Vector2(14f, -(h - 20f) * 0.5f), new Vector2(600f, 20f));
            _rows.Add(row);
        }

        // ─────────── prefab row fill (owner-editable templates) ───────────
        // Mirrors the legacy Add*Row logic 1:1 but pours the live data into an instantiated template
        // instead of constructing widgets, so the owner owns layout while code owns content. The
        // legacy builders below stay as the no-prefab fallback — keep both in sync when logic changes.

        private bool TryRow(GameObject pf, float height, float alpha, out GameObject row, out ResearchLabRowRefs r)
        {
            row = null; r = null;
            if (pf == null || _listContainer == null) return false;
            row = Instantiate(pf, _listContainer, false);
            r = row.GetComponent<ResearchLabRowRefs>();
            if (r == null) { Destroy(row); row = null; return false; }
            if (r.badgeTemplate != null) r.badgeTemplate.SetActive(false); // template stays a template
            if (r.layout != null) { r.layout.minHeight = height; r.layout.preferredHeight = height; }
            if (alpha < 1f)
            {
                var cg = row.GetComponent<CanvasGroup>();
                if (cg == null) cg = row.AddComponent<CanvasGroup>();
                cg.alpha = alpha;
            }
            return true;
        }

        private float CloneBadge(ResearchLabRowRefs r, Transform parent, string text, Color bg, Color fg, Color bd, float x)
        {
            if (r == null || r.badgeTemplate == null) return AddBadge(parent, text, bg, fg, bd, x);
            float w = EstWidth(text, 11f) + 16f;
            var pill = Instantiate(r.badgeTemplate, parent, false);
            pill.SetActive(true);
            var rt = pill.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y); // keep the template's own Y
            rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
            var img = pill.GetComponent<Image>();
            if (img != null) img.color = bg;
            var bdT = pill.transform.Find("Border");
            if (bdT != null) { var bi = bdT.GetComponent<Image>(); if (bi != null) bi.color = bd; }
            var t = pill.GetComponentInChildren<Text>();
            if (t != null) { t.text = text; t.color = fg; }
            return x + w + 5f;
        }

        private bool AddNoteRowPrefab(ResearchNoteSO note, int rank, ResearchLab lab, GameConfigSO cfg, WorkerManager wm)
        {
            bool done = rank == 4;
            bool active = rank == 0;
            bool queued = rank == 1;
            bool blocked = rank == 3;

            var cost = new StringBuilder();
            if (note.costPower > 0) cost.Append($" · ไฟ {note.costPower}");
            if (note.costIron > 0) cost.Append($" · เหล็ก {note.costIron}");
            if (note.costLabMat > 0) cost.Append($" · วัสดุแล็บ {note.costLabMat}");
            string meta = done ? "" : $"{note.researcherSlots} คน × {note.daysRequired} วัน{cost}";
            string hint = (!done && !string.IsNullOrEmpty(note.leadHint)) ? $"ℹ \"{note.leadHint}\"" : null;
            bool crit = note.noteId == "tritium";
            bool bar = active;

            float h = 52f + (meta.Length > 0 ? 18f : 0f) + (hint != null ? 18f : 0f) + (bar ? 15f : 0f);
            if (!TryRow(_pfRowNote, h, blocked ? 0.55f : 1f, out var row, out var r)) return false;

            string icon = done ? "<color=#97c459>✓</color> " : blocked ? "⛔ " : "";
            if (r.title != null)
            {
                r.title.text = icon + note.title;
                r.title.color = done ? CMuted : CText;
            }

            float bx = 14f + EstWidth(note.title, 17f) + (done || blocked ? 26f : 6f);
            bx = CloneBadge(r, row.transform, string.IsNullOrEmpty(note.category) ? "ความรู้" : note.category,
                Hex("#12283f"), CBlue, Hex("#185fa5"), bx);
            if (crit && !done)
                bx = CloneBadge(r, row.transform, "บังคับเพื่อชนะ", Hex("#501313"), Hex("#f09595"), Hex("#a32d2d"), bx);
            if (queued) CloneBadge(r, row.transform, "▸ รอคิว", Hex("#2a1e08"), CAmber, Hex("#854f0b"), bx);

            if (r.meta != null)
            {
                r.meta.gameObject.SetActive(meta.Length > 0);
                if (meta.Length > 0) r.meta.text = meta;
            }
            if (r.hint != null)
            {
                r.hint.gameObject.SetActive(hint != null);
                if (hint != null)
                {
                    r.hint.text = hint;
                    // no meta line (never happens for hint-bearing rows today, but stay safe):
                    // slide the hint up into the meta slot so the row doesn't gap.
                    if (meta.Length == 0 && r.meta != null)
                        r.hint.rectTransform.anchoredPosition = r.meta.rectTransform.anchoredPosition;
                }
            }

            if (r.actionButton != null)
            {
                bool show = rank == 2;
                r.actionButton.gameObject.SetActive(show);
                if (show)
                {
                    string id = note.noteId;
                    r.actionButton.onClick.AddListener(() => { ResearchLab.Instance?.TryStartResearch(id); Refresh(); });
                    if (!lab.CanAfford(note))
                    {
                        var cg = r.actionButton.GetComponent<CanvasGroup>();
                        if (cg == null) cg = r.actionButton.gameObject.AddComponent<CanvasGroup>();
                        cg.alpha = 0.4f; // Notice explains on click
                    }
                }
            }
            if (r.blockedNote != null) r.blockedNote.gameObject.SetActive(blocked);

            if (r.barTrack != null)
            {
                r.barTrack.SetActive(bar);
                if (bar && r.barFill != null)
                {
                    float frac = note.daysRequired > 0 ? lab.ActiveJob.progress / note.daysRequired : 0f;
                    bool stalled = wm != null && wm.GetWorkers(WorkerJobs.Lab).Count < note.researcherSlots * cfg.staffRatioMin;
                    r.barFill.color = stalled ? Hex("#854f0b") : CGold;
                    SetFill(r.barFill, frac);
                    _activeBarFill = r.barFill;
                    _activeBarDays = note.daysRequired;
                }
            }

            _rows.Add(row);
            return true;
        }

        private bool AddRecordDoneRowPrefab(string recTitle)
        {
            if (!TryRow(_pfRowRecDone, 38f, 1f, out var row, out var r)) return false;
            if (r.title != null) r.title.text = $"<color=#97c459>✓</color> {recTitle}";
            CloneBadge(r, row.transform, "กู้บันทึก", Hex("#2a1230"), CPink, Hex("#993556"), 14f + EstWidth(recTitle, 15f) + 26f);
            _rows.Add(row);
            return true;
        }

        private bool AddRecordActiveRowPrefab(DataRecovery dr, GameConfigSO cfg, ResearchLab lab)
        {
            if (!TryRow(_pfRowRecActive, 72f, 1f, out var row, out var r)) return false;
            bool decoding = dr.IsDecoding;

            string head = decoding
                ? $"⏳ กำลังถอดรหัสบันทึกของ Elara #{dr.RecordsRecovered + 1}"
                : $"บันทึกของ Elara #{dr.RecordsRecovered + 1}";
            if (r.title != null) r.title.text = head;
            CloneBadge(r, row.transform, "กู้บันทึก", Hex("#2a1230"), CPink, Hex("#993556"), 14f + EstWidth(head, 15f));

            string sub = decoding
                ? "นักวิจัยที่ว่างจากโครงการช่วยให้เร็วขึ้น"
                : (lab != null && lab.IsRuined
                    ? "ห้องวิจัยพัง — ซ่อมก่อนถึงจะถอดรหัสได้"
                    : "กดถอดรหัสเพื่อเริ่ม — ใช้ห้องวิจัยร่วมกับงานวิจัย นักวิจัยที่ว่างช่วยให้เร็วขึ้น");
            if (r.meta != null) r.meta.text = sub;

            if (r.actionButton != null)
            {
                r.actionButton.gameObject.SetActive(!decoding);
                if (!decoding)
                {
                    r.actionButton.onClick.AddListener(() => { DataRecovery.Instance?.StartDecoding(); Refresh(); });
                    if (!dr.CanStartDecoding)
                    {
                        var cg = r.actionButton.GetComponent<CanvasGroup>();
                        if (cg == null) cg = r.actionButton.gameObject.AddComponent<CanvasGroup>();
                        cg.alpha = 0.4f;
                    }
                }
            }

            if (r.barTrack != null) r.barTrack.SetActive(true);
            if (r.barFill != null)
            {
                float frac = cfg.dataRecoveryTarget > 0f ? dr.Progress / cfg.dataRecoveryTarget : 0f;
                SetFill(r.barFill, frac);
            }
            _rows.Add(row);
            return true;
        }

        private bool AddSimpleRowPrefab(string text, Color col, float h)
        {
            if (!TryRow(_pfRowSimple, h, 0.55f, out var row, out var r)) return false;
            if (r.title != null) { r.title.text = text; r.title.color = col; }
            _rows.Add(row);
            return true;
        }

        private GameObject MakeRowShell(float height, float alpha, Transform parent = null)
        {
            var row = Rounded("Row", parent != null ? parent : _listContainer, alpha < 1f ? Hex("#181510") : CSection, 6);
            if (!ApplySlate(row))                  // ★ slate plate skin; border only on the fallback look
                AddRoundBorder(row, CSectionBd, 6);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = height; le.preferredHeight = height;
            if (alpha < 1f) { var cg = row.AddComponent<CanvasGroup>(); cg.alpha = alpha; }
            return row;
        }

        private float AddBadge(Transform parent, string text, Color bg, Color fg, Color bd, float x)
        {
            float w = EstWidth(text, 11f) + 16f;
            var pill = Rounded("Badge", parent, bg, 3);
            AddRoundBorder(pill, bd, 3);
            var rt = pill.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -10f); rt.sizeDelta = new Vector2(w, 20f);
            var t = Txt("T", pill.transform, text, 11, fg, TextAnchor.MiddleCenter, FontStyle.Normal);
            StretchRT(t.rectTransform);
            return x + w + 5f;
        }

        // width is a legacy hint — the bar now STRETCHES to the row with bottomLeft.x side margins,
        // so it fits every panel width (★ 2026-07-23 resize).
        private Image AddBar(Transform parent, Vector2 bottomLeft, float width, float frac, Color fill)
        {
            var track = Rounded("BarTrack", parent, CTrack, 3);
            var rt = track.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(bottomLeft.x, bottomLeft.y);
            rt.offsetMax = new Vector2(-bottomLeft.x, bottomLeft.y + 6f);
            var f = Rounded("Fill", track.transform, fill, 3);
            var frt = f.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            return f.GetComponent<Image>();
        }

        // Live progress bar — ResearchLab now accrues continuously instead of stepping once at midnight,
        // so the bar has to be nudged every frame. Only the fill rect is touched; Refresh() still owns
        // everything else and runs on events as before. Cleared when the row is rebuilt or the job ends,
        // so a stale Image from a destroyed row is never written to.
        private Image _activeBarFill;
        private int _activeBarDays;

        private void TrackLiveProgress()
        {
            if (_activeBarFill == null) return;

            var job = ResearchLab.Instance?.ActiveJob;
            if (job == null || _activeBarDays <= 0)
            {
                _activeBarFill = null;   // job finished or was dropped — the next Refresh redraws the row
                return;
            }
            SetFill(_activeBarFill, job.progress / _activeBarDays);
        }

        private void SetFill(Image fillImg, float frac)
        {
            var rt = fillImg.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ─────────── mastery chips ───────────
        private void RebuildMastery()
        {
            if (_masteryContainer == null) return;
            foreach (var go in _chips) Destroy(go);
            _chips.Clear();

            var cq = CodexQuizManager.Instance;
            int earned = 0, total = 0;
            float x = 0f, y = 0f;
            const float rowH = 30f, maxW = 840f;

            if (cq != null)
            {
                foreach (var v in cq.GetAll())
                {
                    total++;
                    if (v.state != QuizState.Earned) continue;
                    earned++;
                    string label = !string.IsNullOrEmpty(v.quiz.topicTitle) ? v.quiz.topicTitle : v.quiz.quizId;
                    float w = EstWidth(label, 12f) + 20f;
                    if (x + w > maxW && x > 0f) { x = 0f; y -= rowH; }
                    GameObject chip;
                    if (_pfChip != null) // ★ owner-editable chip template
                    {
                        chip = Instantiate(_pfChip, _masteryContainer, false);
                        var rt = chip.GetComponent<RectTransform>();
                        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                        rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
                        var ct = chip.GetComponentInChildren<Text>();
                        if (ct != null) ct.text = label;
                    }
                    else
                    {
                        chip = Rounded("Chip", _masteryContainer, Hex("#12283f"), 4);
                        AddRoundBorder(chip, Hex("#185fa5"), 4);
                        var rt = chip.GetComponent<RectTransform>();
                        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                        rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, 26f);
                        var t = Txt("T", chip.transform, label, 12, CBlue, TextAnchor.MiddleCenter, FontStyle.Normal);
                        StretchRT(t.rectTransform);
                    }
                    _chips.Add(chip);
                    x += w + 6f;
                }
            }

            if (earned == 0)
            {
                var t = Txt("None", _masteryContainer, "ยังไม่มี — ตอบคำถามใน Codex ให้ถูกเพื่อปลดความเชี่ยวชาญ", 13, CFaint, TextAnchor.UpperLeft, FontStyle.Italic);
                SetTL(t.rectTransform, Vector2.zero, new Vector2(940f, 20f));
                _chips.Add(t.gameObject);
            }
            if (_masteryHead != null)
                _masteryHead.text = $"ความเชี่ยวชาญที่ปลดแล้ว <color=#7a6d52>({earned}/{total})</color>";

            // container tall enough for the wrapped rows (parent layout reads this)
            var le = _masteryContainer.GetComponent<LayoutElement>();
            if (le != null) le.preferredHeight = -y + rowH + 4f;
        }

        // ─────────── tabs ───────────
        private void RefreshTabs()
        {
            foreach (var tab in _tabs)
            {
                bool on = tab.id == _filter;

                // ★ sprite tabs (baked art): active = full brightness · inactive = dimmed plate
                if (tab.bg != null && tab.bg.sprite != null && tab.lbl == null)
                {
                    tab.bg.color = on ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.9f);
                    continue;
                }

                tab.bg.color = on ? CSectionBd : CSection;
                if (tab.lbl != null) tab.lbl.color = on ? CText : CDim;
                var ol = tab.bg.GetComponent<Outline>();
                if (ol != null) ol.effectColor = on ? CHeadLine : CSectionBd;
            }
        }

        // ═══════════════ PREFAB BIND (owner-editable path) ═══════════════
        /// <summary>
        /// Instantiate Resources/ResearchUI/ResearchLabPanel.prefab and wire behaviour through its
        /// ResearchLabPanelRefs. Layout/skin lives entirely in the prefab; this only adds listeners
        /// and grabs the fields Refresh() writes to. Every hookup is null-guarded so a deleted
        /// child costs one feature, not the panel.
        /// </summary>
        private bool TryBindPrefab()
        {
            if (_pfPanel == null) return false;
            var inst = Instantiate(_pfPanel, transform, false);
            var r = inst.GetComponent<ResearchLabPanelRefs>();
            if (r == null)
            {
                Debug.LogError("[ResearchLabPanelUI] ResearchLabPanel.prefab ไม่มี ResearchLabPanelRefs — รัน Setup Research Lab Prefabs ใหม่");
                Destroy(inst);
                return false;
            }

            inst.name = "Backdrop (prefab)";
            _backdrop = inst;
            _root = r.card != null ? r.card : inst;
            _edge = r.edge;

            if (r.backdropButton != null) r.backdropButton.onClick.AddListener(Hide);
            if (r.closeButton != null) r.closeButton.onClick.AddListener(Hide);
            _statusPillBg = r.statusPillBg;
            _statusPill = r.statusPillText;

            // repair block
            _repairBlock = r.repairBlock;
            _repairLine = r.repairLine;
            _repairBtn = r.repairButton;
            if (_repairBtn != null) _repairBtn.onClick.AddListener(() => { ResearchLab.Instance?.StartRepair(); Refresh(); });
            if (r.repairCrewMinus != null) r.repairCrewMinus.onClick.AddListener(() => EventManager.Instance?.RaiseWorkerAssignRequested(_labCell, -1));
            if (r.repairCrewPlus != null) r.repairCrewPlus.onClick.AddListener(() => EventManager.Instance?.RaiseWorkerAssignRequested(_labCell, +1));
            _repairEngDisplay = r.repairCrewDisplay;
            _repairBarWrap = r.repairBarWrap;
            _repairFill = r.repairBarFill;

            // main block
            _mainBlock = r.mainBlock;
            if (r.statValues != null && r.statValues.Length >= 4)
            {
                _statEng = r.statValues[0];
                _statSlots = r.statValues[1];
                _statLabor = r.statValues[2];
                _statRec = r.statValues[3];
            }

            // assign sidebar
            if (r.crewMinus != null) r.crewMinus.onClick.AddListener(() => EventManager.Instance?.RaiseWorkerAssignRequested(_labCell, -1));
            if (r.crewPlus != null) r.crewPlus.onClick.AddListener(() => EventManager.Instance?.RaiseWorkerAssignRequested(_labCell, +1));
            _engDisplay = r.crewDisplay;
            _laborWarn = r.laborWarn; _laborWarnText = r.laborWarnText;
            _ratioWarn = r.ratioWarn; _ratioWarnText = r.ratioWarnText;

            // filter tabs — same tuple shape RefreshTabs consumes; sprite tabs have no Text child,
            // which is exactly what selects the brightness-toggle branch there.
            _tabs.Clear();
            BindTab(r.tabAll, "all");
            BindTab(r.tabNotes, "notes");
            BindTab(r.tabRecords, "records");

            _listContainer = r.listContent;
            _masteryHead = r.masteryHead;
            _masteryContainer = r.masteryChips;
            return true;
        }

        private void BindTab(Button b, string id)
        {
            if (b == null) return;
            b.onClick.AddListener(() => { _filter = id; Refresh(); });
            _tabs.Add((b, b.GetComponentInChildren<Text>(), b.GetComponent<Image>(), id));
        }

        // ═══════════════ BUILD (runtime uGUI — layout mirrors the mockup's DOM) ═══════════════
        private void BuildPanel()
        {
            _backdrop = Flat("Backdrop", transform, CBackdrop);
            StretchRT(_backdrop.GetComponent<RectTransform>());
            var bd = _backdrop.AddComponent<Button>();
            bd.transition = Selectable.Transition.None;
            bd.onClick.AddListener(Hide);

            // ★ 2026-07-23 (owner): bigger + on-theme — 1060×800 with the metal panel_frame skin the
            //   crisis/quiz/note panels wear. The old mockup card/ring stays as the no-sprite fallback.
            const float W = 1060f, H = 800f;
            bool skinned = _panelFrame != null;
            // Content insets clear the ~30px metal border when the frame skin is on.
            float edge = skinned ? 28f : 0f;

            // box-shadow ring (mockup: box-shadow 0 0 0 4px #171310) — a rounded rect 8px larger behind the card
            var ring = Rounded("Ring", _backdrop.transform, CRing, 10);
            var ringRt = ring.GetComponent<RectTransform>();
            ringRt.anchorMin = ringRt.anchorMax = ringRt.pivot = new Vector2(0.5f, 0.5f);
            ringRt.sizeDelta = new Vector2(W + 8f, H + 8f);
            if (skinned) ring.GetComponent<Image>().enabled = false; // the metal frame is its own border

            _root = Rounded("Card", ring.transform, CCard, 8);
            if (skinned)
            {
                var cardImg = _root.GetComponent<Image>();
                cardImg.sprite = _panelFrame;
                cardImg.type = Image.Type.Sliced;
                cardImg.pixelsPerUnitMultiplier = 3f;
                cardImg.color = Color.white;
            }
            else AddRoundBorder(_root, CCardBorder, 8);
            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(W, H);
            _root.GetComponent<Image>().raycastTarget = true; // absorb clicks so they don't hit the backdrop
            _edge = edge;

            // ★ 2026-07-23 (owner): dark interior. The metal frame sprite's CENTER is light grey, so the
            //   whole panel body rendered washed-out and every dark-theme color read wrong. A near-black
            //   fill inset just inside the metal border restores the mockup's dark page; header/content
            //   are created after this, so they draw on top.
            if (skinned)
            {
                var interior = Flat("Interior", _root.transform, CCard);
                var irt = interior.GetComponent<RectTransform>();
                irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
                irt.offsetMin = new Vector2(edge - 2f, edge - 2f);
                irt.offsetMax = new Vector2(-(edge - 2f), -(edge - 2f));
                interior.GetComponent<Image>().raycastTarget = false;
            }

            // ═════ header ═════
            var head = Flat("Head", _root.transform, CHead);
            var hrt = head.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = Vector2.one; hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = new Vector2(0f, -edge); hrt.sizeDelta = new Vector2(-edge * 2f, 62f);
            var headLine = Flat("Line", head.transform, CHeadLine);
            var hlRt = headLine.GetComponent<RectTransform>();
            hlRt.anchorMin = Vector2.zero; hlRt.anchorMax = new Vector2(1f, 0f); hlRt.pivot = new Vector2(0.5f, 0f);
            hlRt.sizeDelta = new Vector2(0f, 2f);

            var flask = Txt("Flask", head.transform, "⚗", 24, CGold, TextAnchor.MiddleCenter, FontStyle.Normal);
            SetTL(flask.rectTransform, new Vector2(16f, -14f), new Vector2(32f, 32f));
            var title = Txt("Title", head.transform, "ห้องวิจัย", 18, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetTL(title.rectTransform, new Vector2(54f, -10f), new Vector2(300f, 24f));
            var sub = Txt("Sub", head.transform, "ฐานความรู้ · เมือง Veltara", 12, CMuted, TextAnchor.UpperLeft, FontStyle.Normal);
            SetTL(sub.rectTransform, new Vector2(54f, -34f), new Vector2(300f, 18f));

            var pill = Rounded("StatusPill", head.transform, Hex("#5a2418"), 4);
            AddRoundBorder(pill, Hex("#993c1d"), 4);
            _statusPillBg = pill.GetComponent<Image>();
            var prt = pill.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1f, 0.5f);
            prt.anchoredPosition = new Vector2(-66f, 0f); prt.sizeDelta = new Vector2(126f, 26f);
            _statusPill = Txt("T", pill.transform, "ซากปรักหักพัง", 12, Hex("#f0997b"), TextAnchor.MiddleCenter, FontStyle.Bold);
            StretchRT(_statusPill.rectTransform);

            var close = RoundBtn("Close", head.transform, "✕", 16, Hex("#3a1c14"), Hex("#f0997b"), Hex("#993c1d"), 4);
            var crt = (RectTransform)close.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(-16f, 0f); crt.sizeDelta = new Vector2(36f, 26f);
            close.onClick.AddListener(Hide);

            BuildRepairBlock();
            BuildMainBlock(W, H);
        }

        private void BuildRepairBlock()
        {
            _repairBlock = Flat("RepairBlock", _root.transform, CSection);
            var rt = _repairBlock.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -62f - _edge); rt.sizeDelta = new Vector2(-_edge * 2f, 158f);
            var line = Flat("Line", _repairBlock.transform, CSectionBd);
            var lrt = line.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = new Vector2(1f, 0f); lrt.pivot = new Vector2(0.5f, 0f);
            lrt.sizeDelta = new Vector2(0f, 1f);

            var h = Txt("H", _repairBlock.transform, "ห้องวิจัยยังเป็นซาก", 15, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetTL(h.rectTransform, new Vector2(18f, -14f), new Vector2(380f, 22f));
            _repairLine = Txt("D", _repairBlock.transform, "", 12, CMuted, TextAnchor.UpperLeft, FontStyle.Normal);
            SetTL(_repairLine.rectTransform, new Vector2(18f, -38f), new Vector2(640f, 34f));

            _repairBtn = RoundBtn("RepairBtn", _repairBlock.transform, "ซ่อมห้องวิจัย", 14, Hex("#3b6d11"), Hex("#eaf3de"), CFillGreen, 4);
            var brt = (RectTransform)_repairBtn.transform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(1f, 0.5f);
            brt.anchoredPosition = new Vector2(-18f, 6f); brt.sizeDelta = new Vector2(150f, 38f);
            _repairBtn.onClick.AddListener(() => { ResearchLab.Instance?.StartRepair(); Refresh(); });
            var brtPos = (RectTransform)_repairBtn.transform;
            brtPos.anchorMin = brtPos.anchorMax = brtPos.pivot = new Vector2(1f, 1f);
            brtPos.anchoredPosition = new Vector2(-18f, -18f);

            // ★ repair-crew assign row — visible while ruined (the main block's −/+ is hidden then).
            //   Routed through the same RaiseWorkerAssignRequested path (cap-clamped by WAM).
            var rMinus = _sprBtnMinus != null
                ? SpriteBtn("CrewMinus", _repairBlock.transform, _sprBtnMinus)
                : RoundBtn("CrewMinus", _repairBlock.transform, "−", 18, CHead, CText, CHeadLine, 4);
            var rmRt = (RectTransform)rMinus.transform;
            rmRt.anchorMin = rmRt.anchorMax = rmRt.pivot = new Vector2(0f, 1f);
            rmRt.anchoredPosition = new Vector2(18f, -78f);
            rmRt.sizeDelta = _sprBtnMinus != null ? new Vector2(44f, 44f) : new Vector2(36f, 36f);
            rMinus.onClick.AddListener(() => EventManager.Instance?.RaiseWorkerAssignRequested(_labCell, -1));

            var rDisp = Rounded("CrewDisp", _repairBlock.transform, CTrack, 6);
            var rdRt = rDisp.GetComponent<RectTransform>();
            rdRt.anchorMin = new Vector2(0f, 1f); rdRt.anchorMax = new Vector2(1f, 1f); rdRt.pivot = new Vector2(0.5f, 1f);
            rdRt.anchoredPosition = new Vector2(0f, -78f);
            rdRt.sizeDelta = new Vector2(-124f, 36f); // symmetric 62px margins → clears −/+
            _repairEngDisplay = Txt("V", rDisp.transform, "คนซ่อม 0 / 2", 15, CText, TextAnchor.MiddleCenter, FontStyle.Bold);
            StretchRT(_repairEngDisplay.rectTransform);

            var rPlus = _sprBtnPlus != null
                ? SpriteBtn("CrewPlus", _repairBlock.transform, _sprBtnPlus)
                : RoundBtn("CrewPlus", _repairBlock.transform, "+", 18, CHead, CText, CHeadLine, 4);
            var rpRt = (RectTransform)rPlus.transform;
            rpRt.anchorMin = rpRt.anchorMax = rpRt.pivot = new Vector2(1f, 1f);
            rpRt.anchoredPosition = new Vector2(-18f, -78f);
            rpRt.sizeDelta = _sprBtnPlus != null ? new Vector2(44f, 44f) : new Vector2(36f, 36f);
            rPlus.onClick.AddListener(() => EventManager.Instance?.RaiseWorkerAssignRequested(_labCell, +1));

            var crewHint = Txt("CrewHint", _repairBlock.transform, "ปุ่มลัด Q ลด / E เพิ่ม", 10, CDim, TextAnchor.UpperLeft, FontStyle.Normal);
            SetTL(crewHint.rectTransform, new Vector2(18f, -118f), new Vector2(300f, 14f));

            _repairBarWrap = Rounded("BarWrap", _repairBlock.transform, CTrack, 3);
            var wrt = _repairBarWrap.GetComponent<RectTransform>();
            wrt.anchorMin = new Vector2(0f, 0f); wrt.anchorMax = new Vector2(1f, 0f); wrt.pivot = new Vector2(0.5f, 0f);
            wrt.anchoredPosition = new Vector2(0f, 8f); wrt.sizeDelta = new Vector2(-36f, 6f);
            var fill = Rounded("Fill", _repairBarWrap.transform, CFillGreen, 3);
            _repairFill = fill.GetComponent<Image>();
            SetFill(_repairFill, 0f);
        }

        private void BuildMainBlock(float W, float H)
        {
            _mainBlock = new GameObject("Main", typeof(RectTransform));
            _mainBlock.transform.SetParent(_root.transform, false);
            var mrt = _mainBlock.GetComponent<RectTransform>();
            mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
            mrt.offsetMin = new Vector2(_edge, _edge); mrt.offsetMax = new Vector2(-_edge, -62f - _edge); // below header, inside the frame
            float iw = W - _edge * 2f; // width the absolute children actually have

            // ═════ stats grid (4 cards) ═════
            string[] labels = { "วิศวกรในห้องวิจัย", "ช่องวิจัย (คิว)", "แรงงานว่างในเมือง", "บันทึกที่กู้" };
            Color[] valueCols = { CText, CGold, CText, CPink };
            var statTexts = new Text[4];
            float cardW = (iw - 36f - 21f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                var card = Rounded($"Stat{i}", _mainBlock.transform, CStatCard, 6);
                ApplySlate(card); // ★ "4 ช่อง" slate plate (owner art) — falls back to the flat card
                var srt = card.GetComponent<RectTransform>();
                srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0f, 1f);
                srt.anchoredPosition = new Vector2(18f + i * (cardW + 7f), -12f);
                srt.sizeDelta = new Vector2(cardW, 56f);
                var l = Txt("L", card.transform, labels[i], 10, CMuted, TextAnchor.UpperLeft, FontStyle.Normal);
                SetTL(l.rectTransform, new Vector2(9f, -7f), new Vector2(cardW - 14f, 14f));
                statTexts[i] = Txt("V", card.transform, "0", 20, valueCols[i], TextAnchor.UpperLeft, FontStyle.Bold);
                SetTL(statTexts[i].rectTransform, new Vector2(9f, -24f), new Vector2(cardW - 14f, 26f));
            }
            _statEng = statTexts[0]; _statSlots = statTexts[1]; _statLabor = statTexts[2]; _statRec = statTexts[3];

            // ═════ two-column body (★ 2026-07-23 owner layout): left = assign sidebar · right = projects ═════
            const float SideW = 300f;   // left sidebar width (mockup ~30%)
            const float ColGap = 14f;
            float topY = -80f;          // everything below the stats row

            var leftCol = Rounded("LeftCol", _mainBlock.transform, CSection, 6);
            if (!ApplySlate(leftCol)) AddRoundBorder(leftCol, CSectionBd, 6);
            var lcRt = leftCol.GetComponent<RectTransform>();
            lcRt.anchorMin = new Vector2(0f, 0f); lcRt.anchorMax = new Vector2(0f, 1f); lcRt.pivot = new Vector2(0f, 1f);
            lcRt.offsetMin = new Vector2(18f, 14f);
            lcRt.offsetMax = new Vector2(18f + SideW, topY);

            var aHead = Txt("AH", leftCol.transform, "จัดวิศวกรเข้าห้องวิจัย", 14, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetTL(aHead.rectTransform, new Vector2(14f, -12f), new Vector2(SideW - 28f, 20f));

            var minus = _sprBtnMinus != null
                ? SpriteBtn("Minus", leftCol.transform, _sprBtnMinus)
                : RoundBtn("Minus", leftCol.transform, "−", 20, CHead, CText, CHeadLine, 4);
            var mnRt = (RectTransform)minus.transform;
            mnRt.anchorMin = mnRt.anchorMax = mnRt.pivot = new Vector2(0f, 1f);
            mnRt.anchoredPosition = new Vector2(14f, -44f);
            mnRt.sizeDelta = _sprBtnMinus != null ? new Vector2(48f, 48f) : new Vector2(40f, 40f);
            minus.onClick.AddListener(() => EventManager.Instance?.RaiseWorkerAssignRequested(_labCell, -1));

            var disp = Rounded("Disp", leftCol.transform, CTrack, 6);
            var dRt = disp.GetComponent<RectTransform>();
            dRt.anchorMin = new Vector2(0f, 1f); dRt.anchorMax = new Vector2(1f, 1f); dRt.pivot = new Vector2(0.5f, 1f);
            dRt.anchoredPosition = new Vector2(0f, -44f);
            dRt.sizeDelta = new Vector2(-136f, 48f); // symmetric 68px margins → clears the −/+ buttons
            _engDisplay = Txt("V", disp.transform, "0", 17, CGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            StretchRT(_engDisplay.rectTransform);
            _engDisplay.supportRichText = true;

            var plus = _sprBtnPlus != null
                ? SpriteBtn("Plus", leftCol.transform, _sprBtnPlus)
                : RoundBtn("Plus", leftCol.transform, "+", 20, CHead, CText, CHeadLine, 4);
            var plRt = (RectTransform)plus.transform;
            plRt.anchorMin = plRt.anchorMax = plRt.pivot = new Vector2(1f, 1f);
            plRt.anchoredPosition = new Vector2(-14f, -44f);
            plRt.sizeDelta = _sprBtnPlus != null ? new Vector2(48f, 48f) : new Vector2(40f, 40f);
            plus.onClick.AddListener(() => EventManager.Instance?.RaiseWorkerAssignRequested(_labCell, +1));

            var aSub = Txt("AS", leftCol.transform, "ปุ่มลัด Q ลด / E เพิ่ม · งานวิจัยเดินเมื่อคนพอตามที่หัวข้อกำหนด", 11, CDim, TextAnchor.UpperLeft, FontStyle.Normal);
            SetTL(aSub.rectTransform, new Vector2(14f, -100f), new Vector2(SideW - 28f, 34f));

            // warnings live in the sidebar now (toggled in Refresh)
            _laborWarn = WarnBox("LaborWarn", leftCol.transform, new Vector2(0f, -142f), Hex("#2b1814"), Hex("#6b3020"), out _laborWarnText, Hex("#f0997b"));
            _ratioWarn = WarnBox("RatioWarn", leftCol.transform, new Vector2(0f, -196f), Hex("#2a1e08"), Hex("#854f0b"), out _ratioWarnText, CAmber);

            // ═════ right column: projects header + filter tabs ═════
            float rx = 18f + SideW + ColGap;
            float py = topY - 6f;
            var pHead = Txt("PH", _mainBlock.transform, "☰ โครงการวิจัย", 14, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetTL(pHead.rectTransform, new Vector2(rx + 4f, py - 10f), new Vector2(220f, 20f));

            (string id, string label, Sprite spr)[] tabs =
            {
                ("all", "ทั้งหมด", _sprTabAll),
                ("notes", "ความรู้", _sprTabNotes),
                ("records", "กู้บันทึก", _sprTabRecords),
            };
            float tx = -18f;
            for (int i = tabs.Length - 1; i >= 0; i--)
            {
                var (id, label, spr) = tabs[i];
                Button b;
                float w, hTab;
                if (spr != null) // ★ baked metal filter plates (owner art) — text lives in the sprite
                {
                    hTab = 48f;
                    w = hTab * (spr.rect.width / spr.rect.height);
                    b = SpriteBtn($"Tab_{id}", _mainBlock.transform, spr);
                }
                else
                {
                    hTab = 24f;
                    w = EstWidth(label, 12f) + 24f;
                    b = RoundBtn($"Tab_{id}", _mainBlock.transform, label, 12, CSection, CDim, CSectionBd, 4);
                }
                var trt = (RectTransform)b.transform;
                trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(1f, 1f);
                trt.anchoredPosition = new Vector2(tx, py + (spr != null ? 4f : -8f));
                trt.sizeDelta = new Vector2(w, hTab);
                tx -= w + 6f;
                string fid = id;
                b.onClick.AddListener(() => { _filter = fid; Refresh(); });
                _tabs.Add((b, b.GetComponentInChildren<Text>(), b.GetComponent<Image>(), id));
            }

            // ═════ scrollable list (projects + mastery) — right column ═════
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(_mainBlock.transform, false);
            var scRt = scrollGo.GetComponent<RectTransform>();
            scRt.anchorMin = Vector2.zero; scRt.anchorMax = Vector2.one;
            scRt.offsetMin = new Vector2(rx, 14f); scRt.offsetMax = new Vector2(-18f, py - 48f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRt = viewport.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = vpRt.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f); // raycast target for drag

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var ctRt = content.GetComponent<RectTransform>();
            ctRt.anchorMin = new Vector2(0f, 1f); ctRt.anchorMax = new Vector2(1f, 1f); ctRt.pivot = new Vector2(0.5f, 1f);
            ctRt.sizeDelta = new Vector2(0f, 0f);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 7f; vlg.padding = new RectOffset(0, 0, 2, 8);
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = vpRt; scroll.content = ctRt;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            _listContainer = content.transform;

            // mastery section lives at the bottom of the scroll content
            var mHeadGo = new GameObject("MasteryHead", typeof(RectTransform));
            mHeadGo.transform.SetParent(content.transform, false);
            _masteryHead = mHeadGo.AddComponent<Text>();
            _masteryHead.font = _font; _masteryHead.fontSize = 14; _masteryHead.color = CText;
            _masteryHead.fontStyle = FontStyle.Bold; _masteryHead.alignment = TextAnchor.MiddleLeft;
            _masteryHead.supportRichText = true;
            _masteryHead.text = "ความเชี่ยวชาญที่ปลดแล้ว";
            var mhLe = mHeadGo.AddComponent<LayoutElement>();
            mhLe.minHeight = 34f; mhLe.preferredHeight = 34f;

            var mBox = new GameObject("MasteryChips", typeof(RectTransform));
            mBox.transform.SetParent(content.transform, false);
            var mbLe = mBox.AddComponent<LayoutElement>();
            mbLe.minHeight = 34f; mbLe.preferredHeight = 34f;
            _masteryContainer = mBox.transform;
        }

        // ★ 2026-07-23: parent-aware (warnings live in the assign sidebar now) + tall enough to wrap.
        private GameObject WarnBox(string name, Transform parent, Vector2 topPos, Color bg, Color bd, out Text txt, Color fg)
        {
            var box = Rounded(name, parent, bg, 4);
            AddRoundBorder(box, bd, 4);
            var rt = box.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = topPos;
            rt.sizeDelta = new Vector2(-24f, 48f); // 12px side margins inside the sidebar
            txt = Txt("T", box.transform, "", 11, fg, TextAnchor.MiddleLeft, FontStyle.Normal);
            StretchRT(txt.rectTransform);
            txt.rectTransform.offsetMin = new Vector2(10f, 2f);
            txt.rectTransform.offsetMax = new Vector2(-8f, -2f);
            box.SetActive(false);
            return box;
        }

        // ═══════════════ PREFAB BAKE (called by Assets/Editor/ResearchLabPrefabSetup) ═══════════════
        // These run in EDIT mode on a throwaway host object: build the exact same hierarchy the
        // runtime fallback would, attach the Refs component, and hand the root to the editor script
        // to save as a prefab. Listeners added during build are runtime-only and don't serialize —
        // TryBindPrefab rewires them on load, so the baked prefab carries layout/skin only.

        private void EnsureArt()
        {
            if (_font == null) Awake();
        }

        /// <summary>Build the full panel and index every element into ResearchLabPanelRefs.</summary>
        public GameObject BakePanelSource()
        {
            EnsureArt();
            BuildPanel();

            var r = _backdrop.AddComponent<ResearchLabPanelRefs>();
            Transform card = _root.transform;
            Transform head = card.Find("Head");
            Transform rb = _repairBlock != null ? _repairBlock.transform : null;
            Transform mb = _mainBlock != null ? _mainBlock.transform : null;

            r.backdropButton = _backdrop.GetComponent<Button>();
            r.ring = card.parent != null ? card.parent.gameObject : null;
            r.card = _root;
            r.interior = card.Find("Interior") != null ? card.Find("Interior").gameObject : null;
            r.edge = _edge;

            r.head = head != null ? head.gameObject : null;
            if (head != null)
            {
                r.headLine = head.Find("Line") != null ? head.Find("Line").gameObject : null;
                r.flask = head.Find("Flask") != null ? head.Find("Flask").GetComponent<Text>() : null;
                r.title = head.Find("Title") != null ? head.Find("Title").GetComponent<Text>() : null;
                r.subtitle = head.Find("Sub") != null ? head.Find("Sub").GetComponent<Text>() : null;
                r.closeButton = head.Find("Close") != null ? head.Find("Close").GetComponent<Button>() : null;
            }
            r.statusPillBg = _statusPillBg;
            r.statusPillText = _statusPill;

            r.repairBlock = _repairBlock;
            if (rb != null)
            {
                r.repairHead = rb.Find("H") != null ? rb.Find("H").GetComponent<Text>() : null;
                r.repairCrewMinus = rb.Find("CrewMinus") != null ? rb.Find("CrewMinus").GetComponent<Button>() : null;
                r.repairCrewPlus = rb.Find("CrewPlus") != null ? rb.Find("CrewPlus").GetComponent<Button>() : null;
                r.repairCrewHint = rb.Find("CrewHint") != null ? rb.Find("CrewHint").GetComponent<Text>() : null;
            }
            r.repairLine = _repairLine;
            r.repairButton = _repairBtn;
            r.repairCrewDisplay = _repairEngDisplay;
            r.repairBarWrap = _repairBarWrap;
            r.repairBarFill = _repairFill;

            r.mainBlock = _mainBlock;
            r.statCards = new GameObject[4];
            r.statLabels = new Text[4];
            r.statValues = new Text[4];
            if (mb != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    var c = mb.Find($"Stat{i}");
                    if (c == null) continue;
                    r.statCards[i] = c.gameObject;
                    r.statLabels[i] = c.Find("L") != null ? c.Find("L").GetComponent<Text>() : null;
                    r.statValues[i] = c.Find("V") != null ? c.Find("V").GetComponent<Text>() : null;
                }
                var lc = mb.Find("LeftCol");
                if (lc != null)
                {
                    r.leftCol = lc.gameObject;
                    r.assignHead = lc.Find("AH") != null ? lc.Find("AH").GetComponent<Text>() : null;
                    r.crewMinus = lc.Find("Minus") != null ? lc.Find("Minus").GetComponent<Button>() : null;
                    r.crewPlus = lc.Find("Plus") != null ? lc.Find("Plus").GetComponent<Button>() : null;
                    r.crewDisplayBox = lc.Find("Disp") != null ? lc.Find("Disp").gameObject : null;
                    r.assignHint = lc.Find("AS") != null ? lc.Find("AS").GetComponent<Text>() : null;
                }
                r.projectsHead = mb.Find("PH") != null ? mb.Find("PH").GetComponent<Text>() : null;
                r.tabAll = mb.Find("Tab_all") != null ? mb.Find("Tab_all").GetComponent<Button>() : null;
                r.tabNotes = mb.Find("Tab_notes") != null ? mb.Find("Tab_notes").GetComponent<Button>() : null;
                r.tabRecords = mb.Find("Tab_records") != null ? mb.Find("Tab_records").GetComponent<Button>() : null;
                r.scroll = mb.Find("Scroll") != null ? mb.Find("Scroll").GetComponent<ScrollRect>() : null;
            }
            r.crewDisplay = _engDisplay;
            r.laborWarn = _laborWarn; r.laborWarnText = _laborWarnText;
            r.ratioWarn = _ratioWarn; r.ratioWarnText = _ratioWarnText;
            r.listContent = _listContainer as RectTransform;
            r.masteryHead = _masteryHead;
            r.masteryChips = _masteryContainer as RectTransform;

            return _backdrop;
        }

        private GameObject MakeBadgeTemplate(Transform parent)
        {
            var pill = Rounded("BadgeTemplate", parent, Hex("#12283f"), 3);
            AddRoundBorder(pill, Hex("#185fa5"), 3);
            var rt = pill.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(200f, -10f);
            rt.sizeDelta = new Vector2(70f, 20f);
            var t = Txt("T", pill.transform, "ป้าย", 11, CBlue, TextAnchor.MiddleCenter, FontStyle.Normal);
            StretchRT(t.rectTransform);
            pill.SetActive(false);
            return pill;
        }

        /// <summary>Row template for research notes — every optional element present and indexed.</summary>
        public GameObject BakeNoteRowTemplate()
        {
            EnsureArt();
            var row = MakeRowShell(88f, 1f, transform);
            row.name = "Row_Note";
            var r = row.AddComponent<ResearchLabRowRefs>();
            r.background = row.GetComponent<Image>();
            r.layout = row.GetComponent<LayoutElement>();

            var title = Txt("Title", row.transform, "หัวข้อวิจัย (ตัวอย่าง)", 17, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetTL(title.rectTransform, new Vector2(14f, -10f), new Vector2(430f, 24f));
            r.title = title;

            r.badgeTemplate = MakeBadgeTemplate(row.transform);

            var m = Txt("Meta", row.transform, "2 คน × 3 วัน · เหล็ก 20", 13, CMuted, TextAnchor.UpperLeft, FontStyle.Normal);
            SetTL(m.rectTransform, new Vector2(14f, -32f), new Vector2(460f, 18f));
            r.meta = m;

            var hTxt = Txt("Hint", row.transform, "ℹ \"เบาะแส (ตัวอย่าง)\"", 13, CDim, TextAnchor.UpperLeft, FontStyle.Italic);
            SetTL(hTxt.rectTransform, new Vector2(14f, -50f), new Vector2(460f, 18f));
            r.hint = hTxt;

            var blockedT = Txt("Blocked", row.transform, "ขาด prerequisite", 12, CFaint, TextAnchor.UpperRight, FontStyle.Normal);
            SetTR(blockedT.rectTransform, new Vector2(-12f, -12f), new Vector2(160f, 18f));
            blockedT.gameObject.SetActive(false);
            r.blockedNote = blockedT;

            var btn = _sprBtnStart != null
                ? SpriteBtn("Start", row.transform, _sprBtnStart)
                : RoundBtn("Start", row.transform, "เริ่มวิจัย", 14, CHead, CText, CHeadLine, 4);
            var brt = (RectTransform)btn.transform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(1f, 1f);
            brt.anchoredPosition = new Vector2(-12f, -6f);
            brt.sizeDelta = _sprBtnStart != null ? new Vector2(170f, 58f) : new Vector2(96f, 32f);
            r.actionButton = btn;

            var fill = AddBar(row.transform, new Vector2(14f, 8f), 856f, 0.4f, CGold);
            r.barFill = fill;
            r.barTrack = fill.transform.parent.gameObject;
            r.barTrack.SetActive(false);
            return row;
        }

        /// <summary>Row template for the record currently being decoded.</summary>
        public GameObject BakeRecordActiveRowTemplate()
        {
            EnsureArt();
            var row = MakeRowShell(72f, 1f, transform);
            row.name = "Row_RecordActive";
            var r = row.AddComponent<ResearchLabRowRefs>();
            r.background = row.GetComponent<Image>();
            r.layout = row.GetComponent<LayoutElement>();

            var t = Txt("T", row.transform, "บันทึกของ Elara #1", 15, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetTL(t.rectTransform, new Vector2(14f, -10f), new Vector2(400f, 22f));
            r.title = t;

            r.badgeTemplate = MakeBadgeTemplate(row.transform);

            var m = Txt("M", row.transform, "กดถอดรหัสเพื่อเริ่ม — ใช้ห้องวิจัยร่วมกับงานวิจัย", 12, CDim, TextAnchor.UpperLeft, FontStyle.Italic);
            SetTL(m.rectTransform, new Vector2(14f, -32f), new Vector2(440f, 18f));
            r.meta = m;

            var btn = _sprBtnDecode != null
                ? SpriteBtn("Decode", row.transform, _sprBtnDecode)
                : RoundBtn("Decode", row.transform, "ถอดรหัส", 14, CHead, CText, CHeadLine, 4);
            var brt = (RectTransform)btn.transform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(1f, 1f);
            brt.anchoredPosition = new Vector2(-12f, -6f);
            brt.sizeDelta = _sprBtnDecode != null ? new Vector2(170f, 58f) : new Vector2(96f, 32f);
            r.actionButton = btn;

            var fill = AddBar(row.transform, new Vector2(14f, 10f), 856f, 0.3f, CPink);
            r.barFill = fill;
            r.barTrack = fill.transform.parent.gameObject;
            return row;
        }

        /// <summary>Row template for an already-recovered record.</summary>
        public GameObject BakeRecordDoneRowTemplate()
        {
            EnsureArt();
            var row = MakeRowShell(38f, 1f, transform);
            row.name = "Row_RecordDone";
            var r = row.AddComponent<ResearchLabRowRefs>();
            r.background = row.GetComponent<Image>();
            r.layout = row.GetComponent<LayoutElement>();

            var t = Txt("T", row.transform, "<color=#97c459>✓</color> บันทึก (ตัวอย่าง)", 15, CMuted, TextAnchor.MiddleLeft, FontStyle.Normal);
            SetTL(t.rectTransform, new Vector2(14f, -7f), new Vector2(650f, 24f));
            r.title = t;

            r.badgeTemplate = MakeBadgeTemplate(row.transform);
            return row;
        }

        /// <summary>Row template for locked/hidden placeholder lines.</summary>
        public GameObject BakeSimpleRowTemplate()
        {
            EnsureArt();
            var row = MakeRowShell(34f, 1f, transform);
            row.name = "Row_Simple";
            var r = row.AddComponent<ResearchLabRowRefs>();
            r.background = row.GetComponent<Image>();
            r.layout = row.GetComponent<LayoutElement>();

            // stretched + middle-left so any row height the code sets stays vertically centered
            var t = Txt("T", row.transform, "[ล็อก] แถวข้อความ (ตัวอย่าง)", 13, CFaint, TextAnchor.MiddleLeft, FontStyle.Normal);
            StretchRT(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(14f, 0f);
            t.rectTransform.offsetMax = new Vector2(-14f, 0f);
            r.title = t;
            return row;
        }

        /// <summary>Mastery chip template — cloned per earned quiz.</summary>
        public GameObject BakeMasteryChipTemplate()
        {
            EnsureArt();
            var chip = Rounded("Chip_Mastery", transform, Hex("#12283f"), 4);
            AddRoundBorder(chip, Hex("#185fa5"), 4);
            var rt = chip.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(120f, 26f);
            var t = Txt("T", chip.transform, "ความเชี่ยวชาญ", 12, CBlue, TextAnchor.MiddleCenter, FontStyle.Normal);
            StretchRT(t.rectTransform);
            return chip;
        }

        // ─────────── uGUI helpers ───────────
        // How hard to push sRGB hex toward linear (Linear color space washes UGUI colors out).
        // 1 = full .linear (playtest: too dark) · 0 = raw hex (too pale) · 0.5 = approved middle.
        const float GammaFix = 0.5f;

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            if (QualitySettings.activeColorSpace != ColorSpace.Linear) return c;
            return Color.Lerp(c, c.linear, GammaFix);
        }

        // Rough Thai/Latin mixed-width estimate for badge/chip sizing (legacy Text can't pre-measure).
        private static float EstWidth(string text, float fontSize) => text.Length * fontSize * 0.62f;

        private GameObject Flat(string name, Transform parent, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = col;
            return go;
        }

        private GameObject Rounded(string name, Transform parent, Color col, int radius)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = RoundedSprite.Get(radius);
            img.type = Image.Type.Sliced;
            img.color = col;
            // RoundedSprite is runtime-generated (not an asset) — the tag re-applies it after the
            // hierarchy round-trips through a baked prefab, where the sprite ref serializes to None.
            go.AddComponent<RoundedCornerTag>().radius = radius;
            return go;
        }

        // CSS-style 1px border on a rounded rect: a border-colored rounded rect 2px larger BEHIND the fill.
        private void AddRoundBorder(GameObject target, Color border, int radius)
        {
            var bd = new GameObject("Border", typeof(RectTransform), typeof(Image));
            bd.transform.SetParent(target.transform, false);
            bd.transform.SetAsFirstSibling();
            var img = bd.GetComponent<Image>();
            img.sprite = RoundedSprite.Get(radius + 1);
            img.type = Image.Type.Sliced;
            img.color = border;
            img.raycastTarget = false;
            bd.AddComponent<RoundedCornerTag>().radius = radius + 1; // survive the prefab round-trip

            var rt = bd.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-1f, -1f); rt.offsetMax = new Vector2(1f, 1f);
        }

        private Button RoundBtn(string name, Transform parent, string label, int size, Color bg, Color fg, Color border, int radius)
        {
            var go = Rounded(name, parent, bg, radius);
            AddRoundBorder(go, border, radius);
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var t = Txt("T", go.transform, label, size, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
            StretchRT(t.rectTransform);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIClickPop.Attach(go);
            return btn;
        }

        private Text Txt(string name, Transform parent, string text, int size, Color col, TextAnchor anchor, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font; t.text = text; t.fontSize = size; t.color = col;
            t.alignment = anchor; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        private static void StretchRT(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void SetTL(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        private static void SetTR(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        private static Font LoadFont()
        {
            return UIFonts.Body;
        }
    }
}
