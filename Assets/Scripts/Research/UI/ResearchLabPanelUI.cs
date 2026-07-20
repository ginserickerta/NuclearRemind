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
            BuildPanel();
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
            SetTL(title.rectTransform, new Vector2(14f, -10f), new Vector2(650f, 24f));

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
                SetTL(m.rectTransform, new Vector2(14f, y), new Vector2(720f, 18f));
                y -= 18f;
            }
            if (hint != null)
            {
                var hTxt = Txt("Hint", row.transform, hint, 13, CDim, TextAnchor.UpperLeft, FontStyle.Italic);
                SetTL(hTxt.rectTransform, new Vector2(14f, y), new Vector2(720f, 18f));
                y -= 18f;
            }

            // right-side action
            if (rank == 2)
            {
                bool canAfford = lab.CanAfford(note);
                var btn = RoundBtn("Start", row.transform, "เริ่มวิจัย", 14, CHead, CText, CHeadLine, 4);
                var brt = (RectTransform)btn.transform;
                brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(1f, 1f);
                brt.anchoredPosition = new Vector2(-12f, -10f); brt.sizeDelta = new Vector2(96f, 32f);
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
            var row = MakeRowShell(38f, 1f);
            var t = Txt("T", row.transform, $"<color=#97c459>✓</color> {recTitle}", 15, CMuted, TextAnchor.MiddleLeft, FontStyle.Normal);
            SetTL(t.rectTransform, new Vector2(14f, -7f), new Vector2(650f, 24f));
            AddBadge(row.transform, "กู้บันทึก", Hex("#2a1230"), CPink, Hex("#993556"), 14f + EstWidth(recTitle, 15f) + 26f);
            _rows.Add(row);
        }

        private void AddRecordActiveRow(DataRecovery dr, GameConfigSO cfg, ResearchLab lab)
        {
            var row = MakeRowShell(72f, 1f);
            var t = Txt("T", row.transform, $"⏳ กำลังถอดรหัสบันทึกของ Elara #{dr.RecordsRecovered + 1}", 15, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetTL(t.rectTransform, new Vector2(14f, -10f), new Vector2(400f, 22f));
            AddBadge(row.transform, "กู้บันทึก", Hex("#2a1230"), CPink, Hex("#993556"), 14f + EstWidth("⏳ กำลังถอดรหัสบันทึกของ Elara #0", 15f));

            var m = Txt("M", row.transform,
                "ถอดรหัสอัตโนมัติเมื่อห้องวิจัยทำงาน — นักวิจัยที่ว่างจากโครงการช่วยให้เร็วขึ้น", 12, CDim,
                TextAnchor.UpperLeft, FontStyle.Italic);
            SetTL(m.rectTransform, new Vector2(14f, -32f), new Vector2(720f, 18f));

            float frac = cfg.dataRecoveryTarget > 0f ? dr.Progress / cfg.dataRecoveryTarget : 0f;
            AddBar(row.transform, new Vector2(14f, 10f), 856f, frac, CPink);
            _rows.Add(row);
        }

        private void AddSimpleRow(string text, Color col, float h)
        {
            var row = MakeRowShell(h, 0.55f);
            var t = Txt("T", row.transform, text, 13, col, TextAnchor.MiddleLeft, FontStyle.Normal);
            SetTL(t.rectTransform, new Vector2(14f, -(h - 20f) * 0.5f), new Vector2(820f, 20f));
            _rows.Add(row);
        }

        private GameObject MakeRowShell(float height, float alpha)
        {
            var row = Rounded("Row", _listContainer, alpha < 1f ? Hex("#181510") : CSection, 6);
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

        private Image AddBar(Transform parent, Vector2 bottomLeft, float width, float frac, Color fill)
        {
            var track = Rounded("BarTrack", parent, CTrack, 3);
            var rt = track.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = bottomLeft; rt.sizeDelta = new Vector2(width, 6f);
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
                    var chip = Rounded("Chip", _masteryContainer, Hex("#12283f"), 4);
                    AddRoundBorder(chip, Hex("#185fa5"), 4);
                    var rt = chip.GetComponent<RectTransform>();
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, 26f);
                    var t = Txt("T", chip.transform, label, 12, CBlue, TextAnchor.MiddleCenter, FontStyle.Normal);
                    StretchRT(t.rectTransform);
                    _chips.Add(chip);
                    x += w + 6f;
                }
            }

            if (earned == 0)
            {
                var t = Txt("None", _masteryContainer, "ยังไม่มี — ตอบคำถามใน Codex ให้ถูกเพื่อปลดความเชี่ยวชาญ", 13, CFaint, TextAnchor.UpperLeft, FontStyle.Italic);
                SetTL(t.rectTransform, Vector2.zero, new Vector2(820f, 20f));
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
                tab.bg.color = on ? CSectionBd : CSection;
                tab.lbl.color = on ? CText : CDim;
                var ol = tab.bg.GetComponent<Outline>();
                if (ol != null) ol.effectColor = on ? CHeadLine : CSectionBd;
            }
        }

        // ═══════════════ BUILD (runtime uGUI — layout mirrors the mockup's DOM) ═══════════════
        private void BuildPanel()
        {
            _backdrop = Flat("Backdrop", transform, CBackdrop);
            StretchRT(_backdrop.GetComponent<RectTransform>());
            var bd = _backdrop.AddComponent<Button>();
            bd.transition = Selectable.Transition.None;
            bd.onClick.AddListener(Hide);

            const float W = 920f, H = 700f; // wider + shorter per playtest feedback

            // box-shadow ring (mockup: box-shadow 0 0 0 4px #171310) — a rounded rect 8px larger behind the card
            var ring = Rounded("Ring", _backdrop.transform, CRing, 10);
            var ringRt = ring.GetComponent<RectTransform>();
            ringRt.anchorMin = ringRt.anchorMax = ringRt.pivot = new Vector2(0.5f, 0.5f);
            ringRt.sizeDelta = new Vector2(W + 8f, H + 8f);

            _root = Rounded("Card", ring.transform, CCard, 8);
            AddRoundBorder(_root, CCardBorder, 8);
            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(W, H);
            _root.GetComponent<Image>().raycastTarget = true; // absorb clicks so they don't hit the backdrop

            // ═════ header ═════
            var head = Flat("Head", _root.transform, CHead);
            var hrt = head.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = Vector2.one; hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = Vector2.zero; hrt.sizeDelta = new Vector2(0f, 62f);
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
            rt.anchoredPosition = new Vector2(0f, -62f); rt.sizeDelta = new Vector2(0f, 158f);
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
            var rMinus = RoundBtn("CrewMinus", _repairBlock.transform, "−", 18, CHead, CText, CHeadLine, 4);
            var rmRt = (RectTransform)rMinus.transform;
            rmRt.anchorMin = rmRt.anchorMax = rmRt.pivot = new Vector2(0f, 1f);
            rmRt.anchoredPosition = new Vector2(18f, -78f); rmRt.sizeDelta = new Vector2(36f, 36f);
            rMinus.onClick.AddListener(() => EventManager.Instance?.RaiseWorkerAssignRequested(_labCell, -1));

            var rDisp = Rounded("CrewDisp", _repairBlock.transform, CTrack, 6);
            var rdRt = rDisp.GetComponent<RectTransform>();
            rdRt.anchorMin = new Vector2(0f, 1f); rdRt.anchorMax = new Vector2(1f, 1f); rdRt.pivot = new Vector2(0.5f, 1f);
            rdRt.anchoredPosition = new Vector2(0f, -78f);
            rdRt.sizeDelta = new Vector2(-124f, 36f); // symmetric 62px margins → clears −/+
            _repairEngDisplay = Txt("V", rDisp.transform, "คนซ่อม 0 / 2", 15, CText, TextAnchor.MiddleCenter, FontStyle.Bold);
            StretchRT(_repairEngDisplay.rectTransform);

            var rPlus = RoundBtn("CrewPlus", _repairBlock.transform, "+", 18, CHead, CText, CHeadLine, 4);
            var rpRt = (RectTransform)rPlus.transform;
            rpRt.anchorMin = rpRt.anchorMax = rpRt.pivot = new Vector2(1f, 1f);
            rpRt.anchoredPosition = new Vector2(-18f, -78f); rpRt.sizeDelta = new Vector2(36f, 36f);
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
            mrt.offsetMin = Vector2.zero; mrt.offsetMax = new Vector2(0f, -62f); // below header

            // ═════ stats grid (4 cards) ═════
            string[] labels = { "วิศวกรในห้องวิจัย", "ช่องวิจัย (คิว)", "แรงงานว่างในเมือง", "บันทึกที่กู้" };
            Color[] valueCols = { CText, CGold, CText, CPink };
            var statTexts = new Text[4];
            float cardW = (W - 36f - 21f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                var card = Rounded($"Stat{i}", _mainBlock.transform, CStatCard, 6);
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

            // ═════ engineer assignment ═════
            float ay = -80f;
            var aHead = Txt("AH", _mainBlock.transform, "จัดวิศวกรเข้าห้องวิจัย", 14, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetTL(aHead.rectTransform, new Vector2(18f, ay), new Vector2(300f, 20f));
            var aSub = Txt("AS", _mainBlock.transform, "ปุ่มลัด Q ลด / E เพิ่ม · งานวิจัยเดินเมื่อคนพอตามที่หัวข้อกำหนด", 11, CDim, TextAnchor.UpperRight, FontStyle.Normal);
            SetTR(aSub.rectTransform, new Vector2(-18f, ay), new Vector2(320f, 18f));

            var minus = RoundBtn("Minus", _mainBlock.transform, "−", 20, CHead, CText, CHeadLine, 4);
            var mnRt = (RectTransform)minus.transform;
            mnRt.anchorMin = mnRt.anchorMax = mnRt.pivot = new Vector2(0f, 1f);
            mnRt.anchoredPosition = new Vector2(18f, ay - 26f); mnRt.sizeDelta = new Vector2(40f, 40f);
            minus.onClick.AddListener(() => EventManager.Instance?.RaiseWorkerAssignRequested(_labCell, -1));

            var disp = Rounded("Disp", _mainBlock.transform, CTrack, 6);
            var dRt = disp.GetComponent<RectTransform>();
            dRt.anchorMin = new Vector2(0f, 1f); dRt.anchorMax = new Vector2(1f, 1f); dRt.pivot = new Vector2(0.5f, 1f);
            dRt.anchoredPosition = new Vector2(0f, ay - 26f);
            dRt.sizeDelta = new Vector2(-132f, 40f); // symmetric 66px margins → clears the −/+ buttons
            _engDisplay = Txt("V", disp.transform, "0", 18, CGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            StretchRT(_engDisplay.rectTransform);
            _engDisplay.supportRichText = true;

            var plus = RoundBtn("Plus", _mainBlock.transform, "+", 20, CHead, CText, CHeadLine, 4);
            var plRt = (RectTransform)plus.transform;
            plRt.anchorMin = plRt.anchorMax = plRt.pivot = new Vector2(1f, 1f);
            plRt.anchoredPosition = new Vector2(-18f, ay - 26f); plRt.sizeDelta = new Vector2(40f, 40f);
            plus.onClick.AddListener(() => EventManager.Instance?.RaiseWorkerAssignRequested(_labCell, +1));

            // warnings (toggled in Refresh — space reserved so the layout never jumps)
            _laborWarn = WarnBox("LaborWarn", new Vector2(0f, ay - 74f), Hex("#2b1814"), Hex("#6b3020"), out _laborWarnText, Hex("#f0997b"));
            _ratioWarn = WarnBox("RatioWarn", new Vector2(0f, ay - 104f), Hex("#2a1e08"), Hex("#854f0b"), out _ratioWarnText, CAmber);

            // ═════ projects header: title + filter tabs ═════
            float py = ay - 140f;
            var pHead = Txt("PH", _mainBlock.transform, "โครงการวิจัย", 14, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetTL(pHead.rectTransform, new Vector2(18f, py), new Vector2(220f, 20f));

            (string id, string label)[] tabs = { ("all", "ทั้งหมด"), ("notes", "ความรู้"), ("records", "กู้บันทึก") };
            float tx = -18f;
            for (int i = tabs.Length - 1; i >= 0; i--)
            {
                var (id, label) = tabs[i];
                float w = EstWidth(label, 12f) + 24f;
                var b = RoundBtn($"Tab_{id}", _mainBlock.transform, label, 12, CSection, CDim, CSectionBd, 4);
                var trt = (RectTransform)b.transform;
                trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(1f, 1f);
                trt.anchoredPosition = new Vector2(tx, py + 2f); trt.sizeDelta = new Vector2(w, 24f);
                tx -= w + 5f;
                string fid = id;
                b.onClick.AddListener(() => { _filter = fid; Refresh(); });
                _tabs.Add((b, b.GetComponentInChildren<Text>(), b.GetComponent<Image>(), id));
            }

            var legend = Txt("Legend", _mainBlock.transform,
                "<color=#85b7eb>ความรู้</color> = ปลดความรู้หลัก + ความเชี่ยวชาญ · <color=#ed93b1>กู้บันทึก</color> = ถอดรหัสบันทึกของ Elara ทีละใบ",
                11, CDim, TextAnchor.UpperLeft, FontStyle.Normal);
            SetTL(legend.rectTransform, new Vector2(18f, py - 24f), new Vector2(720f, 16f));
            legend.supportRichText = true;

            // ═════ scrollable list (projects + mastery) ═════
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(_mainBlock.transform, false);
            var scRt = scrollGo.GetComponent<RectTransform>();
            scRt.anchorMin = Vector2.zero; scRt.anchorMax = Vector2.one;
            scRt.offsetMin = new Vector2(18f, 14f); scRt.offsetMax = new Vector2(-18f, py - 48f);

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

        private GameObject WarnBox(string name, Vector2 topPos, Color bg, Color bd, out Text txt, Color fg)
        {
            var box = Rounded(name, _mainBlock.transform, bg, 4);
            AddRoundBorder(box, bd, 4);
            var rt = box.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = topPos; rt.offsetMin = new Vector2(18f, rt.offsetMin.y);
            rt.sizeDelta = new Vector2(-36f, 26f);
            txt = Txt("T", box.transform, "", 12, fg, TextAnchor.MiddleLeft, FontStyle.Normal);
            StretchRT(txt.rectTransform);
            txt.rectTransform.offsetMin = new Vector2(10f, 0f);
            box.SetActive(false);
            return box;
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
