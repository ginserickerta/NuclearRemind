using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NuclearReMind
{
    /// <summary>
    /// ป๊อปอัพห้องวิจัย (ResearchLab_Spec §5) — คลิกห้องวิจัยใน city-view เปิด 3 ส่วน:
    ///   • ส่วนหัว: ชื่อ + ระดับ (พร้อมปุ่มอัปเกรด) + วิศวกรประจำ x/2 (ปุ่ม −/+) + ค่าเดินระบบ ⚡/วัน
    ///   • ส่วน 1 — ฝึกวิศวกร: ปุ่มเดียว แสดงต้นทุน (Food/Energy จาก PopulationManager) + Worker ที่เหลือ
    ///     (ไม่มี Worker เหลือ → ปุ่ม disable ตามสเปก §2)
    ///   • ส่วน 2 — โครงการวิจัย 3 อัน: ปุ่ม "วิจัย" ต่ออัน · ยังไม่ถึงเฟส/ของไม่พอ → ล็อกจางลง + เหตุผล
    /// เวลาหยุดตอนเปิด (PauseReason.LabPopup ตาม §15 — idiom CardUIController) · Esc/✕/คลิกนอก = ปิด
    /// สร้าง runtime ทั้งแผง (idiom CoreTowerPanelUI) — Laboratory ถูก exclude จาก BuildingUpgradeUI
    /// จึงรวมปุ่มจัดคน/อัปเกรดไว้ที่นี่ · สั่งงานทุกอย่างผ่าน EventManager (ห้ามเรียก manager ตรง)
    /// </summary>
    public class LabPanelUI : MonoBehaviour, GameUIStack.IPanel
    {
        static readonly Color CBackdrop = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color CPanel    = new Color(0.075f, 0.09f, 0.11f, 0.96f);
        static readonly Color CInset    = new Color(0.12f, 0.15f, 0.18f, 1f);
        static readonly Color CBorder   = new Color(0.30f, 0.40f, 0.46f, 1f);
        static readonly Color CText     = new Color(0.92f, 0.94f, 0.96f, 1f);
        static readonly Color CMuted    = new Color(0.58f, 0.66f, 0.72f, 1f);
        static readonly Color CGreen    = new Color(0.55f, 0.82f, 0.45f, 1f);
        static readonly Color CGold     = new Color(0.95f, 0.78f, 0.30f, 1f);
        static readonly Color CBtn      = new Color(0.21f, 0.42f, 0.55f, 1f);
        static readonly Color CBtnDim   = new Color(0.16f, 0.20f, 0.24f, 1f);
        static readonly Color CDone     = new Color(0.24f, 0.42f, 0.26f, 1f);

        public Vector2 panelSize = new Vector2(860f, 760f);

        private Font _font;
        private bool _shown;
        private Vector2Int _labCell;

        private GameObject _backdrop, _root;
        private Text _headLevel, _headStaff, _headUpkeep, _trainInfo;
        private Button _trainBtn, _assignMinus, _assignPlus, _upgradeBtn;
        private readonly ProjectRow[] _rows = new ProjectRow[3];

        private class ProjectRow
        {
            public string id;
            public Button btn;
            public Image btnBg;
            public Text btnLabel, status;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindFirstObjectByType<LabPanelUI>() != null) return;
            var canvas = FindBestCanvas();
            var go = new GameObject("LabPanelUI (auto)");
            if (canvas != null) go.transform.SetParent(canvas.transform, false);
            go.AddComponent<LabPanelUI>();
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
            EventManager.Instance.OnResourceChanged += HandleAnyChange;
            EventManager.Instance.OnPopulationChanged += HandlePopChange;
            EventManager.Instance.OnWorkerAssignmentChanged += HandleAssignChange;
            EventManager.Instance.OnResearchCompleted += HandleResearchDone;
            EventManager.Instance.OnBuildingUpgraded += HandleUpgraded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnResourceChanged -= HandleAnyChange;
            EventManager.Instance.OnPopulationChanged -= HandlePopChange;
            EventManager.Instance.OnWorkerAssignmentChanged -= HandleAssignChange;
            EventManager.Instance.OnResearchCompleted -= HandleResearchDone;
            EventManager.Instance.OnBuildingUpgraded -= HandleUpgraded;
        }

        private void HandleAnyChange(ResourceData _)              { if (_shown) Refresh(); }
        private void HandlePopChange(PopulationData _)            { if (_shown) Refresh(); }
        private void HandleAssignChange(Vector2Int c, int n)      { if (_shown) Refresh(); }
        private void HandleResearchDone(string _)                 { if (_shown) Refresh(); }
        private void HandleUpgraded(Vector2Int c, int lvl)        { if (_shown) Refresh(); }

        private void Start()
        {
            BuildPanel();
            Hide();
        }

        private void Update()
        {
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (Input.GetMouseButtonDown(0) && !overUI && !_shown && ClickedLab()) Open();
            // Esc จัดการรวมที่ GameUIStack (ผ่าน PauseMenuController) — ไม่เช็คเองแล้ว
        }

        // คลิกโดน "ตัวสไปรต์" ของโรงวิจัย (raycast Collider2D) — เดิมเช็ก footprint ต้องเล็งฐาน
        private bool ClickedLab()
        {
            if (InputManager.Instance == null) return false;
            var hits = Physics2D.OverlapPointAll((Vector2)InputManager.Instance.GetMouseWorldPosition());
            foreach (var h in hits)
            {
                var t = h.GetComponent<BuildingClickTarget>();
                if (t != null && t.data != null && t.data.buildingType == BuildingType.Laboratory)
                {
                    _labCell = t.originCell;
                    return true;
                }
            }
            return false;
        }

        private void Open()
        {
            _shown = true;
            if (_backdrop != null) { UIPopIn.Ensure(_backdrop); _backdrop.SetActive(true); }
            GameUIStack.Push(this); // ขึ้นบนสุด + ลงทะเบียน (บล็อก Pause / Esc=ปิด)
            TimeManager.Instance?.Pause(PauseReason.LabPopup); // §15: เวลาหยุดตอนเปิด popup
            Refresh();
            var pop = _root != null ? _root.GetComponent<UIClickPop>() : null;
            if (pop != null) pop.PlayFrom(0.9f);
        }

        private void Hide()
        {
            _shown = false;
            if (_backdrop != null) _backdrop.SetActive(false);
            GameUIStack.Pop(this);
            TimeManager.Instance?.Resume(PauseReason.LabPopup);
        }

        // ── GameUIStack (แผงปิดได้: Esc=ปิดเหมือน ✕ · กติกากลางใน PauseMenuController) ──
        bool GameUIStack.IPanel.ClosableByEscape => true;
        void GameUIStack.IPanel.BringToFront() => GameUIStack.RaiseToTop(_backdrop);
        void GameUIStack.IPanel.CloseFromStack() => Hide();

        // ═══════════════ POPULATE ═══════════════
        private void Refresh()
        {
            if (_root == null) return;
            var reg = BuildingRegistry.Instance;
            BuildingData lab = null;
            if (reg != null) reg.PlacedBuildings.TryGetValue(_labCell, out lab);
            if (lab == null) { Hide(); return; }

            int level = reg.GetLevel(_labCell);
            int need = reg.WorkersRequired(_labCell); if (need <= 0) need = lab.workerRequired;
            int assigned = WorkerAssignmentManager.Instance != null
                ? WorkerAssignmentManager.Instance.GetAssigned(_labCell) : 0;

            if (_headLevel != null) _headLevel.text = $"ระดับ L{level}";
            if (_headStaff != null) _headStaff.text = $"วิศวกรประจำ  {assigned}/{need}";
            if (_headUpkeep != null) _headUpkeep.text = $"ค่าเดินระบบ ⚡{lab.energyConsumption:0}/วัน";

            // ส่วน 1 — ฝึกวิศวกร (ปุ่ม disable เมื่อไม่มี Worker เหลือ — สเปก §2)
            var pm = PopulationManager.Instance;
            int freeWorkers = pm != null ? pm.Current.workers : 0;
            if (_trainInfo != null && pm != null)
                _trainInfo.text = $"ต้นทุน Food {pm.trainEngineerFood} + Energy {pm.trainEngineerEnergy} · ใช้เวลา 1 วัน · Worker เหลือ {freeWorkers} คน";
            if (_trainBtn != null) _trainBtn.interactable = freeWorkers > 0;

            // ส่วน 2 — โครงการวิจัย
            var rs = ResearchManager.Instance;
            foreach (var row in _rows)
            {
                if (row == null || rs == null) continue;
                if (rs.IsDone(row.id))
                {
                    row.btnBg.color = CDone;
                    row.btnLabel.text = "✔ สำเร็จ";
                    row.btn.interactable = false;
                    row.status.text = "";
                    continue;
                }
                bool can = rs.CanResearch(row.id, out string why);
                row.btnBg.color = can ? CBtn : CBtnDim;
                row.btnLabel.text = "วิจัย";
                row.btn.interactable = can;
                row.status.text = can ? "" : why; // เหตุผลที่ล็อก (จางลงตามสเปก §5)
            }
        }

        // ═══════════════ ACTIONS (ผ่าน EventManager เท่านั้น) ═══════════════
        private void RequestTrain()      => EventManager.Instance.RaiseTrainEngineerRequested();
        private void RequestResearch(string id) => EventManager.Instance.RaiseResearchRequested(id);
        private void RequestAssign(int d) => EventManager.Instance.RaiseWorkerAssignRequested(_labCell, d);
        private void RequestUpgrade()    => EventManager.Instance.RaiseUpgradeBuildingRequested(_labCell);

        // ═══════════════ BUILD (runtime) ═══════════════
        private void BuildPanel()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            _backdrop = Panel("LabBackdrop", parent, CBackdrop);
            Stretch(_backdrop.GetComponent<RectTransform>());
            var bd = _backdrop.AddComponent<Button>(); bd.transition = Selectable.Transition.None;
            bd.onClick.AddListener(Hide);

            _root = Panel("LabPanel", _backdrop.transform, CPanel);
            var rr = _root.GetComponent<RectTransform>();
            rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0.5f, 0.5f);
            rr.sizeDelta = panelSize;
            AddBorder(_root, CBorder, 3f);
            var rootPop = UIClickPop.Attach(_root); rootPop.playOnClick = false;

            float W = panelSize.x, pad = 26f;

            // ── ส่วนหัว ──
            var title = Txt("Title", _root.transform, "ห้องวิจัย (Research Lab)", 30, CText, TextAnchor.UpperCenter, FontStyle.Bold);
            SetRect(title.rectTransform, new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1), new Vector2(0,-18), new Vector2(-40,40));
            var close = Btn("Close", _root.transform, "✕", 22, new Color(0.5f,0.22f,0.2f,1f), out _);
            SetRect((RectTransform)close.transform, new Vector2(1,1), new Vector2(1,1), new Vector2(1,1), new Vector2(-16,-16), new Vector2(44,44));
            close.onClick.AddListener(Hide);

            _headLevel = Txt("Lv", _root.transform, "ระดับ L1", 20, CGold, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetRect(_headLevel.rectTransform, new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(pad,-66), new Vector2(150,30));
            _upgradeBtn = Btn("Up", _root.transform, "อัปเกรด", 16, CBtn, out _);
            SetRect((RectTransform)_upgradeBtn.transform, new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(pad+150,-62), new Vector2(96,36));
            _upgradeBtn.onClick.AddListener(RequestUpgrade);

            _headStaff = Txt("Staff", _root.transform, "วิศวกรประจำ 0/2", 20, CText, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(_headStaff.rectTransform, new Vector2(0.5f,1), new Vector2(0.5f,1), new Vector2(0.5f,1), new Vector2(30,-66), new Vector2(240,30));
            _assignMinus = Btn("A-", _root.transform, "−", 20, CBtnDim, out _);
            SetRect((RectTransform)_assignMinus.transform, new Vector2(0.5f,1), new Vector2(0.5f,1), new Vector2(0.5f,1), new Vector2(170,-62), new Vector2(40,36));
            _assignMinus.onClick.AddListener(() => RequestAssign(-1));
            _assignPlus = Btn("A+", _root.transform, "+", 20, CBtn, out _);
            SetRect((RectTransform)_assignPlus.transform, new Vector2(0.5f,1), new Vector2(0.5f,1), new Vector2(0.5f,1), new Vector2(214,-62), new Vector2(40,36));
            _assignPlus.onClick.AddListener(() => RequestAssign(1));

            _headUpkeep = Txt("Upkeep", _root.transform, "ค่าเดินระบบ ⚡20/วัน", 18, CMuted, TextAnchor.MiddleRight, FontStyle.Normal);
            SetRect(_headUpkeep.rectTransform, new Vector2(1,1), new Vector2(1,1), new Vector2(1,1), new Vector2(-pad,-66), new Vector2(240,30));

            // ── ส่วน 1: ฝึกวิศวกร ──
            var s1 = Panel("Train", _root.transform, CInset);
            SetRect(s1.GetComponent<RectTransform>(), new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1), new Vector2(0,-112), new Vector2(-pad*2,120));
            AddBorder(s1, CBorder, 1.5f);
            var s1h = Txt("h", s1.transform, "ฝึกวิศวกร — แปลง Worker 1 คน → Engineer", 20, CGreen, TextAnchor.UpperLeft, FontStyle.Bold);
            SetRect(s1h.rectTransform, new Vector2(0,1), new Vector2(1,1), new Vector2(0,1), new Vector2(16,-10), new Vector2(-200,28));
            _trainInfo = Txt("i", s1.transform, "", 16, CMuted, TextAnchor.UpperLeft, FontStyle.Normal);
            SetRect(_trainInfo.rectTransform, new Vector2(0,1), new Vector2(1,1), new Vector2(0,1), new Vector2(16,-44), new Vector2(-200,52));
            _trainBtn = Btn("TrainBtn", s1.transform, "ฝึกวิศวกร", 18, CBtn, out _);
            SetRect((RectTransform)_trainBtn.transform, new Vector2(1,0.5f), new Vector2(1,0.5f), new Vector2(1,0.5f), new Vector2(-16,0), new Vector2(150,54));
            _trainBtn.onClick.AddListener(RequestTrain);

            // ── ส่วน 2: โครงการวิจัย 3 อัน ──
            var s2h = Txt("s2h", _root.transform, "โครงการวิจัย (3 โครงการ)", 20, CGreen, TextAnchor.UpperLeft, FontStyle.Bold);
            SetRect(s2h.rectTransform, new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(pad,-252), new Vector2(400,28));

            _rows[0] = ProjectRowUI(0, ResearchManager.ProjectSeeds, "1. เมล็ดพันธุ์ฉายรังสี",
                "Iron 250 + วิศวกร 3 คน → ผลผลิตฟาร์ม +100% ถาวร (ทางยั่งยืนของวิกฤตอาหาร)");
            _rows[1] = ProjectRowUI(1, ResearchManager.ProjectIsotope, "2. ยาไอโซโทปการแพทย์",
                "Iron 200 + ฟลักซ์นิวตรอน (ตั้งเตาโหมด Idle 1 วัน) → รักษาคนป่วยสูงสุด 15 คน");
            _rows[2] = ProjectRowUI(2, ResearchManager.ProjectCoreTower, "3. ปลดล็อก CORE TOWER",
                "Iron 200 + Energy 200 → เงื่อนไขวิจัยก่อนเดินเตา (พร้อมใช้เฟส 3)");
        }

        // แถวโครงการ: ชื่อ+คำอธิบาย (ซ้าย) · ปุ่มวิจัย (ขวา) · เหตุผลล็อก (ล่างขวา)
        private ProjectRow ProjectRowUI(int index, string id, string name, string desc)
        {
            float y = -288f - index * 128f;
            var row = Panel($"Proj_{id}", _root.transform, CInset);
            SetRect(row.GetComponent<RectTransform>(), new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1), new Vector2(0,y), new Vector2(-52,116));
            AddBorder(row, CBorder, 1.5f);

            var nm = Txt("n", row.transform, name, 19, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetRect(nm.rectTransform, new Vector2(0,1), new Vector2(1,1), new Vector2(0,1), new Vector2(16,-10), new Vector2(-190,26));
            var ds = Txt("d", row.transform, desc, 15, CMuted, TextAnchor.UpperLeft, FontStyle.Normal);
            SetRect(ds.rectTransform, new Vector2(0,1), new Vector2(1,1), new Vector2(0,1), new Vector2(16,-40), new Vector2(-190,48));

            var btn = Btn("R", row.transform, "วิจัย", 18, CBtn, out var bg);
            SetRect((RectTransform)btn.transform, new Vector2(1,1), new Vector2(1,1), new Vector2(1,1), new Vector2(-14,-12), new Vector2(140,50));
            string pid = id;
            btn.onClick.AddListener(() => RequestResearch(pid));

            var st = Txt("s", row.transform, "", 13, new Color(0.85f,0.6f,0.4f,1f), TextAnchor.LowerRight, FontStyle.Normal);
            SetRect(st.rectTransform, new Vector2(0.3f,0), new Vector2(1,0), new Vector2(1,0), new Vector2(-14,8), new Vector2(0,22));

            var labelT = btn.GetComponentInChildren<Text>();
            return new ProjectRow { id = id, btn = btn, btnBg = bg, btnLabel = labelT, status = st };
        }

        // ─────────── helpers (idiom CoreTowerPanelUI) ───────────
        private GameObject Panel(string name, Transform parent, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = col;
            return go;
        }
        private Text Txt(string name, Transform parent, string text, int size, Color col, TextAnchor anchor, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font; t.text = text; t.fontSize = size; t.color = col; t.alignment = anchor; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }
        private Button Btn(string name, Transform parent, string label, int size, Color col, out Image bg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            bg = go.GetComponent<Image>(); bg.color = col;
            var t = Txt("T", go.transform, label, size, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform); t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var b = go.GetComponent<Button>();
            b.transition = Selectable.Transition.None; // คุมสีเองใน Refresh (can/locked/done)
            var cb = b.colors; cb.disabledColor = new Color(0.45f,0.45f,0.45f,0.6f); b.colors = cb;
            UIClickPop.Attach(go);
            return b;
        }
        private Outline AddBorder(GameObject target, Color col, float w)
        {
            var ol = target.AddComponent<Outline>();
            ol.effectColor = col; ol.effectDistance = new Vector2(w, w); ol.useGraphicAlpha = false;
            return ol;
        }
        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        private static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }
        private static Font LoadFont()
        {
            var f = Resources.Load<Font>("Fonts/Kanit-Regular");
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f;
        }
    }
}
