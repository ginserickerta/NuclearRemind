using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: แดชบอร์ดคนงาน (กด K เปิด) — ตาราง 1 แถวต่อคน โชว์ค่าดิบ ล้า/หิว/รังสี/ประสิทธิภาพ + แถบสรุป
    /// [TH] อ่านอย่างเดียว ไม่แก้เกม — แค่ทำให้ตัวเลขที่เกมใช้จริงมองเห็นได้ (รวม Σ ประสิทธิภาพต่องานที่ footer)
    /// Worker dashboard (GDD §17) — one row per worker plus a summary strip. Read-only: nothing here
    /// changes the game, it only makes the numbers the game already runs on visible.
    ///
    /// Why per-worker and not just totals. WorkerManager.RecalcStatus gives each worker exactly ONE
    /// status, severity-ordered (Dying > Sick > Hungry > Exhausted > Tired), so a worker who is both
    /// starving and spent is counted as Hungry and vanishes from ExhaustedCount — while still dragging
    /// Hope down through both. A dashboard that summed those labels would repeat that loss of
    /// information, so the raw fatigue / hunger / radiation numbers are the point of the table.
    ///
    /// The summary strip therefore counts THRESHOLD CROSSINGS, not labels: a worker can appear in more
    /// than one bucket and the totals can exceed the population. Where that disagrees with the
    /// label-based count, both are shown — the crisis-card triggers read the label counts
    /// (sickWorkers >= 3 etc.), so the gap between the two numbers is exactly the reason a city that is
    /// visibly falling apart can still fail to trigger a card.
    ///
    /// View layer only (same rule as Inventory, CLAUDE.md #9): every value is read from
    /// WorkerManager on refresh. The one thing kept between days is yesterday's snapshot, used to show
    /// each number's daily change — a display concern, never read back by any system.
    /// </summary>
    public class WorkerDashboardPanel : MonoBehaviour, GameUIStack.IPanel
    {
        static readonly Color CBackdrop = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color CPanel    = new Color(0.10f, 0.10f, 0.09f, 0.98f);
        static readonly Color CBorder   = new Color(0.32f, 0.30f, 0.26f, 1f);
        static readonly Color CRowAlt   = new Color(1f, 1f, 1f, 0.035f);
        static readonly Color CText     = new Color(0.93f, 0.92f, 0.86f, 1f);
        static readonly Color CMuted    = new Color(0.62f, 0.62f, 0.56f, 1f);
        static readonly Color CGold     = new Color(0.96f, 0.80f, 0.35f, 1f);
        static readonly Color CWarn     = new Color(0.95f, 0.72f, 0.30f, 1f); // over the soft threshold
        static readonly Color CBad      = new Color(0.94f, 0.42f, 0.34f, 1f); // over the hard threshold
        static readonly Color CGood     = new Color(0.55f, 0.80f, 0.55f, 1f);

        /// <summary>K for "คนงาน" — J belonged to the retired WorkerAssignPanel, so it stays free.</summary>
        public KeyCode toggleKey = KeyCode.K;

        // Rows are built once at a fixed count because the panel exists before the population does, and
        // the population only ever shrinks (startPopulation = 14, nobody is born). A little headroom
        // covers a config bump; spare rows are hidden and the footer is pinned to the bottom edge so
        // they leave no gap.
        private const int Rows = 18;
        private const float RowH = 27f;

        // column widths, in order: name · job · fatigue · hunger · radiation · efficiency · status
        private static readonly float[] Col = { 66f, 118f, 96f, 96f, 96f, 64f, 104f };
        private static readonly string[] Head = { "คน", "งาน", "ล้า", "หิว", "รังสี", "ประสิทธิภาพ", "สถานะ" };

        private Font _font;
        private bool _shown;
        private GameObject _backdrop, _root;
        private Text _header, _summary, _mismatch, _footer;
        private readonly List<GameObject> _rowRoots = new List<GameObject>();
        private readonly List<Text[]> _rowCells = new List<Text[]>();

        // yesterday's values per worker id — display only (see class doc)
        private readonly Dictionary<int, Vector3> _prev = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, Vector3> _delta = new Dictionary<int, Vector3>();

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
                if (FindFirstObjectByType<WorkerDashboardPanel>() != null) return;
                var canvas = FindBestCanvas();
                if (canvas == null) return;
                var go = new GameObject("WorkerDashboardPanel (auto)");
                go.transform.SetParent(canvas.transform, false);
                go.AddComponent<WorkerDashboardPanel>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[WorkerDashboard] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
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

        private void Awake()
        {
            _font = UIFonts.Body;
        }

        private void OnEnable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayStarted += HandleDayStarted;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayStarted -= HandleDayStarted;
        }

        private void Start()
        {
            BuildPanel();
            // Instant hide — Hide() would play the close animation and flash the panel on scene load.
            _shown = false;
            if (_backdrop != null) _backdrop.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) { if (_shown) Hide(); else Open(); }
        }

        // ─────────────────────────────────────────
        //  daily deltas
        // ─────────────────────────────────────────

        /// <summary>
        /// [TH] เก็บ snapshot ค่าของแต่ละคนทุกเช้า แล้วเทียบกับเมื่อวานเพื่อโชว์ delta ต่อวัน (+7/−3) ในตาราง
        /// A day starts with last night's tick already applied, so comparing now against the snapshot
        /// taken at the previous day start yields exactly what that tick did to each worker.
        /// </summary>
        private void HandleDayStarted(int day, bool timed)
        {
            var wm = WorkerManager.Instance;
            if (wm == null) return;
            foreach (var w in wm.Workers)
            {
                var now = new Vector3(w.fatigue, w.hunger, w.radiation);
                _delta[w.id] = _prev.TryGetValue(w.id, out var was) ? now - was : Vector3.zero;
                _prev[w.id] = now;
            }
            if (_shown) Refresh();
        }

        // ─────────────────────────────────────────
        //  show / hide
        // ─────────────────────────────────────────

        private void Open()
        {
            _shown = true;
            if (_backdrop != null) UIPopIn.PlayOpen(_backdrop); // pop-in like the card/quiz panels
            GameUIStack.Push(this);
            TimeManager.Instance?.Pause(PauseReason.LabPopup);
            Refresh();
        }

        private void Hide()
        {
            _shown = false;
            if (_backdrop != null) UIPopIn.PlayClose(_backdrop); // shrink-out, then SetActive(false)
            GameUIStack.Pop(this);
            TimeManager.Instance?.Resume(PauseReason.LabPopup);
        }

        bool GameUIStack.IPanel.ClosableByEscape => true;
        void GameUIStack.IPanel.BringToFront() => GameUIStack.RaiseToTop(_backdrop);
        GameObject GameUIStack.IPanel.PanelRoot => _backdrop;
        void GameUIStack.IPanel.CloseFromStack() => Hide();

        // ─────────────────────────────────────────
        //  refresh
        // ─────────────────────────────────────────

        // [TH] อัปเดตทั้งแผง: หัวเรื่อง (จำนวนคน/คนตาย) → แถบสรุป → ตารางรายคน → footer กำลังผลิตจริง
        private void Refresh()
        {
            var wm = WorkerManager.Instance;
            var cfg = GameConfigSO.Instance;
            if (wm == null || cfg == null)
            {
                if (_header != null) _header.text = "คนงาน — ยังไม่มีข้อมูล";
                return;
            }

            var alive = new List<Worker>();
            int dead = 0;
            foreach (var w in wm.Workers) { if (w.alive) alive.Add(w); else dead++; }
            alive.Sort((a, b) => a.id.CompareTo(b.id));

            if (_header != null)
                _header.text = dead > 0
                    ? $"คนงาน {alive.Count} คน   <size=16><color=#B06A62>เสียชีวิต {dead}</color></size>"
                    : $"คนงาน {alive.Count} คน";

            RefreshSummary(wm, cfg, alive);
            RefreshRows(wm, cfg, alive);
            RefreshFooter(wm);
        }

        /// <summary>
        /// [TH] แถบสรุป: นับจาก "ค่าดิบข้ามเกณฑ์" (คนเดียวติดได้หลายช่อง) และโชว์เทียบกับตัวเลขที่
        /// [TH] ระบบการ์ดวิกฤตนับจริง (ป้ายสถานะคนละ 1 ป้าย) — ช่องว่างระหว่างสองเลขนี้คือเหตุที่การ์ดบางใบไม่ยอมเด้ง
        /// Threshold-crossing counts (a worker can land in several) + the label counts the crisis-card
        /// triggers actually read, shown side by side whenever they disagree.
        /// </summary>
        private void RefreshSummary(WorkerManager wm, GameConfigSO cfg, List<Worker> alive)
        {
            int hungry = 0, exhausted = 0, tired = 0, sick = 0, dying = 0, useless = 0;
            foreach (var w in alive)
            {
                if (w.hunger > cfg.hungryThreshold) hungry++;
                // เกณฑ์ป้ายสถานะ (68) ไม่ใช่เกณฑ์ประสิทธิภาพ (85) — ต้องตรงกับ RecalcStatus
                // ไม่งั้นแถบสรุปจะเถียงกับคอลัมน์สถานะในตารางเดียวกัน
                if (w.fatigue > cfg.exhaustedThreshold) exhausted++;
                else if (w.fatigue > cfg.tiredThreshold) tired++;
                if (w.radiation > cfg.dyingThreshold) dying++;
                else if (w.radiation > cfg.sickThreshold) sick++;
                if (wm.GetEfficiency(w) <= 0f) useless++;
            }

            if (_summary != null)
                _summary.text =
                    $"{Chip("หิว", hungry, CWarn)}   {Chip("หมดแรง", exhausted, CBad)}   {Chip("ล้า", tired, CWarn)}   " +
                    $"{Chip("ป่วย", sick, CBad)}   {Chip("ใกล้ตาย", dying, CBad)}   " +
                    $"{Chip("ทำงานไม่ได้เลย", useless, CBad)}" +
                    "\n<size=14><color=#8A867C>นับจากค่าดิบ — คนเดียวติดได้หลายช่อง ผลรวมจึงเกินจำนวนคนได้</color></size>";

            // The gap that matters: cards count labels, and a label is one-per-worker.
            if (_mismatch == null) return;
            var gaps = new List<string>();
            if (wm.HungryCount != hungry)      gaps.Add($"หิว {hungry} → การ์ดนับ {wm.HungryCount}");
            if (wm.ExhaustedCount != exhausted) gaps.Add($"หมดแรง {exhausted} → การ์ดนับ {wm.ExhaustedCount}");
            if (wm.SickCount != sick)          gaps.Add($"ป่วย {sick} → การ์ดนับ {wm.SickCount}");

            bool any = gaps.Count > 0;
            _mismatch.gameObject.SetActive(any);
            if (any)
                _mismatch.text = "⚠ ระบบการ์ดวิกฤตนับได้น้อยกว่าความจริง (คน 1 คนมีได้สถานะเดียว):  "
                               + string.Join("   ·   ", gaps);
        }

        private static string Chip(string label, int n, Color c)
        {
            string col = n > 0 ? ColorUtility.ToHtmlStringRGB(c) : "8A867C";
            return $"<color=#{col}>{label} {n}</color>";
        }

        // [TH] เติมข้อมูลตารางรายคน: ชื่อ · งาน · ล้า · หิว · รังสี · ประสิทธิภาพ · สถานะ (แถวเกินจำนวนคนถูกซ่อน)
        private void RefreshRows(WorkerManager wm, GameConfigSO cfg, List<Worker> alive)
        {
            for (int i = 0; i < _rowRoots.Count; i++)
            {
                bool used = i < alive.Count;
                _rowRoots[i].SetActive(used);
                if (!used) continue;

                var w = alive[i];
                var cells = _rowCells[i];
                var d = _delta.TryGetValue(w.id, out var dv) ? dv : Vector3.zero;
                float eff = wm.GetEfficiency(w);

                Set(cells[0], string.IsNullOrEmpty(w.displayName) ? $"#{w.id:00}" : w.displayName, CText);
                Set(cells[1], JobLabel(w), w.IsWorking ? CText : CMuted);
                // ▲ ใช้เกณฑ์ป้ายสถานะ (45/68) ไม่ใช่เกณฑ์ประสิทธิภาพ (60/85) — ให้ทั้งแถวพูดภาษาเดียวกัน
                // กับคอลัมน์ "สถานะ" และแถบสรุปข้างบน · ผลกระทบเชิงกลไกอ่านได้จากคอลัมน์ประสิทธิภาพอยู่แล้ว
                Set(cells[2], Metric(w.fatigue, d.x, cfg.tiredThreshold, cfg.exhaustedThreshold), CText);
                Set(cells[3], Metric(w.hunger, d.y, cfg.hungryThreshold, 100f), CText);
                Set(cells[4], Metric(w.radiation, d.z, cfg.sickThreshold, cfg.dyingThreshold), CText);
                Set(cells[5], $"{eff * 100f:0}%", eff <= 0f ? CBad : eff < 1f ? CWarn : CGood);
                Set(cells[6], StatusLabel(w), StatusColor(w.status));
            }
        }

        /// <summary>
        /// [TH] จัดรูปตัวเลข 1 ช่อง: ค่า + ลูกศรเตือนเมื่อข้ามเกณฑ์ + delta ของเมื่อคืน เช่น "92 ▲ +7"
        /// "92 ▲ +7" — value, a marker when it has crossed a threshold, and last night's change.
        /// </summary>
        private static string Metric(float value, float delta, float soft, float hard)
        {
            string mark = value > hard ? $" <color=#{ColorUtility.ToHtmlStringRGB(CBad)}>▲</color>"
                        : value > soft ? $" <color=#{ColorUtility.ToHtmlStringRGB(CWarn)}>▲</color>"
                        : "";
            string change = Mathf.Abs(delta) < 0.05f
                ? ""
                : $"  <size=13><color=#8A867C>{(delta > 0 ? "+" : "")}{delta:0.#}</color></size>";
            return $"{value:0}{mark}{change}";
        }

        // [TH] footer โชว์กติกาข้อ 7 ให้เห็นจริง: "เหมือง 5 คน → 3.2" คือกำลังผลิตจริงหลังหักคนหิว/ป่วย
        private void RefreshFooter(WorkerManager wm)
        {
            if (_footer == null) return;
            // Rule #7 made visible: production multiplies Σ efficiency, never headcount. A job showing
            // "5 คน → 3.2" is the whole reason its building is under-producing.
            var parts = new List<string>();
            foreach (var (job, label) in JobList)
            {
                int heads = wm.GetWorkers(job).Count;
                if (heads == 0) continue;
                float sum = wm.SumEfficiency(job);
                string col = ColorUtility.ToHtmlStringRGB(sum < heads ? CWarn : CGood);
                parts.Add($"{label} {heads} คน → <color=#{col}>{sum:0.0}</color>");
            }
            _footer.text = parts.Count == 0
                ? "<color=#8A867C>ยังไม่ได้จัดคนเข้างาน</color>"
                : "กำลังผลิตจริง (Σ ประสิทธิภาพ):   " + string.Join("    ", parts);
        }

        private static readonly (string job, string label)[] JobList =
        {
            (WorkerJobs.Farm, "ฟาร์ม"), (WorkerJobs.Water, "น้ำ"), (WorkerJobs.Power, "โรงไฟ"),
            (WorkerJobs.Mine, "เหมือง"), (WorkerJobs.Lab, "วิจัย"), (WorkerJobs.Cool, "หล่อเย็น"),
            (WorkerJobs.ZoneB, "Zone B"),
        };

        private static string JobLabel(Worker w)
        {
            if (w.strikeDaysLeft > 0) return $"ประท้วง ({w.strikeDaysLeft}ว)";
            if (w.resting) return "พัก";
            switch (w.job)
            {
                case WorkerJobs.Farm:  return "ฟาร์ม";
                case WorkerJobs.Water: return "น้ำ";
                case WorkerJobs.Power: return "โรงไฟ";
                case WorkerJobs.Mine:  return "เหมือง";
                case WorkerJobs.Lab:   return "วิจัย";
                case WorkerJobs.Cool:  return "หล่อเย็น";
                // ☢ and ✔ are BMP; the shield 🛡 (U+1F6E1) is not, and legacy uGUI Text cannot draw a
                // surrogate pair — the same reason the worker badge emoji never appeared.
                case WorkerJobs.ZoneB: return w.hasRadSuit ? "Zone B ☢✔" : "Zone B ☢";
                case WorkerJobs.Extract: return "สกัด";
                case WorkerJobs.Build: return "ก่อสร้าง";
                default:               return "ว่าง";
            }
        }

        private static string StatusLabel(Worker w)
        {
            // Sickness icons are BMP glyphs (☣ U+2623 · ☠ U+2620 · ⚰ U+26B0) — the same class as the
            // ☢/✔ already used in JobLabel. True emoji (🤒 U+1F912 …) are surrogate pairs that legacy
            // uGUI Text cannot draw (see WorkerHealthBadge), so BMP symbols are the only safe icons.
            switch (w.status)
            {
                case WorkerStatus.Dying:     return "☠ ใกล้ตาย";
                case WorkerStatus.Sick:      return "☣ ป่วย";
                case WorkerStatus.Hungry:    return "หิว";
                case WorkerStatus.Exhausted: return "หมดแรง";
                case WorkerStatus.Tired:     return "ล้า";
                case WorkerStatus.Dead:      return "⚰ เสียชีวิต";
                default:                     return "ปกติ";
            }
        }

        private static Color StatusColor(WorkerStatus s)
        {
            switch (s)
            {
                case WorkerStatus.Dying:
                case WorkerStatus.Dead:
                case WorkerStatus.Sick:      return CBad;
                case WorkerStatus.Hungry:
                case WorkerStatus.Exhausted: return CBad;
                case WorkerStatus.Tired:     return CWarn;
                default:                     return CGood;
            }
        }

        private static void Set(Text t, string s, Color c) { if (t != null) { t.text = s; t.color = c; } }

        // ═══════════════ BUILD ═══════════════

        // [TH] ประกอบหน้าตาแผงด้วยโค้ดล้วน: ฉากหลัง + กรอบ + หัวเรื่อง + สรุป + ตาราง + footer
        private void BuildPanel()
        {
            _backdrop = NewUI("Backdrop", transform, CBackdrop);
            Stretch(_backdrop);
            var close = _backdrop.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(Hide); // click outside = close (read-only panel, nothing to lose)

            float width = 28f * 2f + Sum(Col) + 6f * (Col.Length - 1);
            _root = NewUI("Panel", _backdrop.transform, CPanel);
            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, 250f + Rows * RowH);
            var outline = _root.AddComponent<Outline>();
            outline.effectColor = CBorder; outline.effectDistance = new Vector2(2f, -2f);
            // swallow clicks on the panel itself so they don't reach the close-backdrop behind it
            _root.AddComponent<Button>().transition = Selectable.Transition.None;

            float y = -20f;
            _header = MakeText("Header", _root.transform, "", 24, CGold, TextAnchor.UpperLeft);
            Place(_header.gameObject, 28f, y, width - 56f, 32f); y -= 36f;

            _summary = MakeText("Summary", _root.transform, "", 18, CText, TextAnchor.UpperLeft);
            Place(_summary.gameObject, 28f, y, width - 56f, 48f); y -= 52f;

            _mismatch = MakeText("Mismatch", _root.transform, "", 15, CBad, TextAnchor.UpperLeft);
            Place(_mismatch.gameObject, 28f, y, width - 56f, 24f); y -= 30f;

            // table header
            var head = MakeRow("Head", y, false);
            for (int c = 0; c < Col.Length; c++) Set(head[c], Head[c], CMuted);
            y -= RowH;

            for (int i = 0; i < Rows; i++)
            {
                var cells = MakeRow($"Row{i:00}", y, i % 2 == 1);
                _rowCells.Add(cells);
                y -= RowH;
            }

            // Pinned to the bottom edge, not to the last row — the table always has spare hidden rows.
            _footer = MakeText("Footer", _root.transform, "", 16, CText, TextAnchor.LowerLeft);
            var frt = _footer.rectTransform;
            frt.anchorMin = frt.anchorMax = frt.pivot = new Vector2(0f, 0f);
            frt.anchoredPosition = new Vector2(28f, 38f);
            frt.sizeDelta = new Vector2(width - 56f, 44f);

            var hint = MakeText("Hint", _root.transform, $"กด {toggleKey} หรือ Esc เพื่อปิด", 14, CMuted, TextAnchor.LowerRight);
            var hrt = hint.rectTransform;
            hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(1f, 0f);
            hrt.anchoredPosition = new Vector2(-20f, 12f);
            hrt.sizeDelta = new Vector2(260f, 20f);
        }

        /// <summary>One table row: a strip of Texts at fixed column offsets (Thai font is not monospace).</summary>
        private Text[] MakeRow(string name, float y, bool striped)
        {
            var row = NewUI(name, _root.transform, striped ? CRowAlt : new Color(0f, 0f, 0f, 0f));
            Place(row, 20f, y, Sum(Col) + 6f * (Col.Length - 1) + 16f, RowH);
            row.GetComponent<Image>().raycastTarget = false;
            if (name.StartsWith("Row")) _rowRoots.Add(row);

            var cells = new Text[Col.Length];
            float x = 8f;
            for (int c = 0; c < Col.Length; c++)
            {
                var t = MakeText($"C{c}", row.transform, "", 16, CText, TextAnchor.MiddleLeft);
                Place(t.gameObject, x, 0f, Col[c], RowH);
                var crt = t.rectTransform;
                crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0f, 0.5f);
                crt.anchoredPosition = new Vector2(x, 0f);
                cells[c] = t;
                x += Col[c] + 6f;
            }
            return cells;
        }

        private static float Sum(float[] a) { float s = 0f; foreach (var v in a) s += v; return s; }

        // ── uGUI helpers (same shape as the other v6.3 panels) ──
        private static GameObject NewUI(string name, Transform parent, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = bg;
            return go;
        }

        private static void Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        /// <summary>Top-left anchored placement — x from the left edge, y downward from the top.</summary>
        private static void Place(GameObject go, float x, float y, float w, float h)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private Text MakeText(string name, Transform parent, string text, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = _font; t.text = text; t.fontSize = size; t.color = color;
            t.alignment = anchor; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }
    }
}
