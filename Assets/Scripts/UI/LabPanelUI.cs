using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NuclearReMind
{
    /// <summary>
    /// ป๊อปอัพห้องวิจัย (ResearchLab_Spec §5) — สกินโลหะตาม mockup (Assets/Resources/LabUI/*)
    ///   • ส่วนหัว: ไอคอนฟลาสก์ + ชื่อ+ระดับ + บรรทัดย่อย (วิศวกรประจำ x/2 · ค่าเดินระบบ ⚡/วัน)
    ///     + ปุ่มอัปเกรด + ปุ่ม −/+ จัดวิศวกร (restyle เข้าธีม — ไม่มีใน mockup แต่คงฟังก์ชันไว้)
    ///   • ส่วน 1 — ฝึกวิศวกร: ไอคอนวิศวกร + คำอธิบาย + ป้ายต้นทุน Food/Energy + ปุ่มฝึก (สไปรต์ + เลขสดทับ)
    ///   • ส่วน 2 — โครงการวิจัย 3 อัน: ไอคอนกล้องจุลทรรศน์ + ปุ่ม "วิจัย" (สไปรต์) ต่ออัน
    /// สไปรต์ที่ไม่มีในชุดอ้างอิง (กรอบพาเนล/พื้น section/พื้นแถว/ป้ายต้นทุน/ไอคอนโครงการ) = พื้นดำโปร่ง 80%
    /// เวลาหยุดตอนเปิด (PauseReason.LabPopup) · Esc/✕/คลิกนอก = ปิด · สั่งงานผ่าน EventManager เท่านั้น
    /// </summary>
    public class LabPanelUI : MonoBehaviour, GameUIStack.IPanel
    {
        static readonly Color CBackdrop = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color CBlack80  = new Color(0f, 0f, 0f, 0.80f);   // ★ พื้นสไปรต์ที่ขาด = ดำโปร่ง 80%
        static readonly Color CBorder   = new Color(0.32f, 0.30f, 0.26f, 1f);
        static readonly Color CBorderLo = new Color(0.18f, 0.17f, 0.15f, 1f);
        static readonly Color CText     = new Color(0.93f, 0.92f, 0.86f, 1f);
        static readonly Color CMuted    = new Color(0.62f, 0.62f, 0.56f, 1f);
        static readonly Color CGold     = new Color(0.96f, 0.80f, 0.35f, 1f);
        static readonly Color CFood     = new Color(0.88f, 0.58f, 0.30f, 1f);
        static readonly Color CEnergy   = new Color(0.42f, 0.72f, 0.92f, 1f);
        static readonly Color CBtnDim   = new Color(0.13f, 0.13f, 0.11f, 0.92f);
        static readonly Color CDoneTint = new Color(0.55f, 0.85f, 0.55f, 1f); // tint สไปรต์ปุ่มตอนวิจัยเสร็จ
        static readonly Color CLockTint = new Color(0.45f, 0.45f, 0.45f, 1f); // tint สไปรต์ปุ่มตอนล็อก

        public Vector2 panelSize = new Vector2(1180f, 800f);

        private Font _font;
        private bool _shown;
        private Vector2Int _labCell;

        // สไปรต์สกิน (Resources/LabUI) — โหลดตอน build
        private Sprite _sprLab, _sprEng, _sprMicro, _sprBtnTrain, _sprBtnResearch;

        private GameObject _backdrop, _root;
        private Text _headTitle, _headSub, _trainLbl, _foodPill, _energyPill;
        private Button _trainBtn, _assignMinus, _assignPlus, _upgradeBtn;
        private readonly ProjectRow[] _rows = new ProjectRow[3];

        private class ProjectRow
        {
            public string id;
            public Button btn;
            public Image btnBg;      // สไปรต์ปุ่ม (tint ตามสถานะ)
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
            var im = InputManager.Instance;
            var reg = BuildingRegistry.Instance;
            if (im == null || reg == null) return false;

            // ★ grid footprint ก่อน — ทางเดียวกับ hover nameplate ที่พิสูจน์แล้วว่าทำงานบน WebGL
            if (reg.TryGetBuildingAt(im.GetMouseGridPosition(), out var origin, out var data) && data != null
                && data.buildingType == BuildingType.Laboratory)
            { _labCell = origin; return true; }

            // สำรอง: คลิกตัวสไปรต์สูงเหนือ footprint (bounds เรขาคณิตล้วน ไม่พึ่ง Physics2D)
            var t = BuildingClickTarget.PickAt(im.GetMouseWorldPosition(), x => x.data.buildingType == BuildingType.Laboratory);
            if (t != null) { _labCell = t.originCell; return true; }
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

            if (_headTitle != null) _headTitle.text = $"ห้องวิจัย (Research Lab) · L{level}";
            if (_headSub != null)
                _headSub.text = $"วิศวกรประจำ {assigned} / {need}  ·  ค่าเดินระบบ {lab.energyConsumption:0} Energy/วัน";

            // ── ส่วน 1: ฝึกวิศวกร — ต้นทุน (ป้าย) + เลข Worker เหลือ (ทับบนปุ่มสไปรต์) ──
            var pm = PopulationManager.Instance;
            int freeWorkers = pm != null ? pm.Current.workers : 0;
            if (_foodPill != null && pm != null)   _foodPill.text = $"Food {pm.trainEngineerFood}";
            if (_energyPill != null && pm != null) _energyPill.text = $"Energy {pm.trainEngineerEnergy}";
            if (_trainLbl != null) _trainLbl.text = $"ฝึก\n(Worker : {freeWorkers})"; // เลขสดทับสไปรต์
            if (_trainBtn != null) _trainBtn.interactable = freeWorkers > 0;

            // ── ส่วน 2: โครงการวิจัย ──
            var rs = ResearchManager.Instance;
            foreach (var row in _rows)
            {
                if (row == null || rs == null) continue;
                if (rs.IsDone(row.id))
                {
                    if (row.btnBg != null) row.btnBg.color = CDoneTint;
                    row.btnLabel.text = "✔ สำเร็จ";
                    row.btn.interactable = false;
                    row.status.text = "";
                    continue;
                }
                bool can = rs.CanResearch(row.id, out string why);
                if (row.btnBg != null) row.btnBg.color = can ? Color.white : CLockTint;
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
            _sprLab         = Spr("icon_lab");
            _sprEng         = Spr("icon_engineer");
            _sprMicro       = Spr("icon_microscope");
            _sprBtnTrain    = Spr("btn_train");
            _sprBtnResearch = Spr("btn_research");

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            _backdrop = Panel("LabBackdrop", parent, CBackdrop);
            Stretch(_backdrop.GetComponent<RectTransform>());
            var bd = _backdrop.AddComponent<Button>(); bd.transition = Selectable.Transition.None;
            bd.onClick.AddListener(Hide);

            // พาเนลหลัก — ไม่มีสไปรต์กรอบในชุดอ้างอิง → พื้นดำโปร่ง 80% (+ ขอบบางให้เห็นขอบ)
            _root = Panel("LabPanel", _backdrop.transform, CBlack80);
            var rr = _root.GetComponent<RectTransform>();
            rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0.5f, 0.5f);
            rr.sizeDelta = panelSize;
            AddBorder(_root, CBorder, 3f);
            var rootPop = UIClickPop.Attach(_root); rootPop.playOnClick = false;

            float W = panelSize.x, pad = 24f;

            // ═════ ส่วนหัว ═════
            var labIcon = SprImg("LabIcon", _root.transform, _sprLab);
            SetRect(labIcon.rectTransform, TL, TL, TL, new Vector2(pad, -pad), new Vector2(112, 112));

            _headTitle = Txt("Title", _root.transform, "ห้องวิจัย (Research Lab) · L1", 34, CText, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetRect(_headTitle.rectTransform, TL, TL, TL, new Vector2(pad + 132, -pad - 10), new Vector2(720, 46));
            _headSub = Txt("Sub", _root.transform, "วิศวกรประจำ 0 / 2  ·  ค่าเดินระบบ 20 Energy/วัน", 20, CMuted, TextAnchor.MiddleLeft, FontStyle.Normal);
            SetRect(_headSub.rectTransform, TL, TL, TL, new Vector2(pad + 132, -pad - 62), new Vector2(760, 30));

            var close = Btn("Close", _root.transform, "✕", 22, new Color(0.42f, 0.18f, 0.16f, 0.95f), out _);
            SetRect((RectTransform)close.transform, TR, TR, TR, new Vector2(-14, -14), new Vector2(44, 44));
            close.onClick.AddListener(Hide);

            // ปุ่มอัปเกรด + จัดวิศวกร −/+ (restyle ดำโปร่ง — คงฟังก์ชันไว้ ไม่มีใน mockup)
            _upgradeBtn = Btn("Up", _root.transform, "อัปเกรด", 16, CBtnDim, out _);
            SetRect((RectTransform)_upgradeBtn.transform, TR, TR, TR, new Vector2(-14, -70), new Vector2(140, 40));
            AddBorder(_upgradeBtn.gameObject, CBorder, 1.5f);
            _upgradeBtn.onClick.AddListener(RequestUpgrade);

            _assignMinus = Btn("A-", _root.transform, "−", 22, CBtnDim, out _);
            SetRect((RectTransform)_assignMinus.transform, TR, TR, TR, new Vector2(-236, -70), new Vector2(40, 40));
            AddBorder(_assignMinus.gameObject, CBorder, 1.5f);
            _assignMinus.onClick.AddListener(() => RequestAssign(-1));
            _assignPlus = Btn("A+", _root.transform, "+", 22, CBtnDim, out _);
            SetRect((RectTransform)_assignPlus.transform, TR, TR, TR, new Vector2(-192, -70), new Vector2(40, 40));
            AddBorder(_assignPlus.gameObject, CBorder, 1.5f);
            _assignPlus.onClick.AddListener(() => RequestAssign(1));

            // เส้นแบ่งใต้หัว
            var div = Panel("Divider", _root.transform, new Color(1f, 1f, 1f, 0.10f));
            SetRect(div.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -148), new Vector2(-pad * 2, 3));

            // ═════ ส่วน 1: ฝึกวิศวกร ═════
            var s1 = Panel("Train", _root.transform, CBlack80);
            SetRect(s1.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -164), new Vector2(-pad * 2, 156));
            AddBorder(s1, CBorderLo, 1.5f);

            var engIcon = SprImg("EngIcon", s1.transform, _sprEng);
            SetRect(engIcon.rectTransform, TL, TL, TL, new Vector2(16, -16), new Vector2(100, 100));

            var s1h = Txt("h", s1.transform, "ฝึกวิศวกร (Train Engineer)", 23, CGold, TextAnchor.UpperLeft, FontStyle.Bold);
            SetRect(s1h.rectTransform, TL, TL, TL, new Vector2(132, -14), new Vector2(520, 30));
            var s1d = Txt("d", s1.transform, "แปลง Worker 1 คน → Engineer · ใช้เวลา 1 วัน", 17, CMuted, TextAnchor.UpperLeft, FontStyle.Normal);
            SetRect(s1d.rectTransform, TL, TL, TL, new Vector2(132, -50), new Vector2(620, 26));

            _foodPill = CostPill(s1.transform, new Vector2(132, -92), new Vector2(154, 44), "Food 30", CFood);
            _energyPill = CostPill(s1.transform, new Vector2(298, -92), new Vector2(168, 44), "Energy 50", CEnergy);

            _trainBtn = SprBtn("TrainBtn", s1.transform, _sprBtnTrain, "ฝึก\n(Worker : 5)", 20, out _trainLbl, out _);
            SetRect((RectTransform)_trainBtn.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(232, 120));
            _trainBtn.onClick.AddListener(RequestTrain);

            // ═════ ส่วน 2: โครงการวิจัย ═════
            var s2 = Panel("Projects", _root.transform, CBlack80);
            SetRect(s2.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -332), new Vector2(-pad * 2, 452));
            AddBorder(s2, CBorderLo, 1.5f);

            var microIcon = SprImg("MicroIcon", s2.transform, _sprMicro);
            SetRect(microIcon.rectTransform, TL, TL, TL, new Vector2(14, -14), new Vector2(92, 92));
            var s2h = Txt("s2h", s2.transform, "โครงงานวิจัย (Research Projects)", 23, CGold, TextAnchor.UpperLeft, FontStyle.Bold);
            SetRect(s2h.rectTransform, TL, TL, TL, new Vector2(120, -18), new Vector2(560, 30));
            var s2c = Txt("s2c", s2.transform, "3 รายการ", 18, CMuted, TextAnchor.UpperRight, FontStyle.Normal);
            SetRect(s2c.rectTransform, TR, TR, TR, new Vector2(-16, -20), new Vector2(160, 28));

            _rows[0] = ProjectRowUI(s2.transform, 0, ResearchManager.ProjectSeeds, "เมล็ดพันธุ์ฉายรังสี",
                "แก้วิกฤต 3 · เพิ่มผลผลิตถาวร · Iron 250");
            _rows[1] = ProjectRowUI(s2.transform, 1, ResearchManager.ProjectIsotope, "ยาไอโซโทปการแพทย์",
                "แก้วิกฤต 2 · วัสดุแล็บ 200 + ฟลักซ์นิวตรอน");
            _rows[2] = ProjectRowUI(s2.transform, 2, ResearchManager.ProjectCoreTower, "ปลดล็อก CORE TOWER",
                "เงื่อนไขสร้างเตา · เปิดได้ Phase 3");
        }

        // แถวโครงการ (ใน s2): ไอคอน(ดำ80%) + ชื่อ/คำอธิบาย + ปุ่มวิจัย(สไปรต์) + เหตุผลล็อก
        private ProjectRow ProjectRowUI(Transform parent, int index, string id, string name, string desc)
        {
            float y = -116f - index * 112f;
            var row = Panel($"Proj_{id}", parent, CBlack80);
            SetRect(row.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(-28, 100));
            AddBorder(row, CBorderLo, 1f);

            // ไอคอนโครงการ — ไม่มีในชุดอ้างอิง → พื้นดำโปร่ง 80%
            var icon = Panel("i", row.transform, CBlack80);
            SetRect(icon.GetComponent<RectTransform>(), TL, TL, TL, new Vector2(14, -14), new Vector2(72, 72));
            AddBorder(icon, CBorderLo, 1f);

            var nm = Txt("n", row.transform, name, 20, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetRect(nm.rectTransform, TL, TL, TL, new Vector2(100, -12), new Vector2(-330, 28));
            var ds = Txt("d", row.transform, desc, 15, CMuted, TextAnchor.UpperLeft, FontStyle.Normal);
            SetRect(ds.rectTransform, TL, TL, TL, new Vector2(100, -46), new Vector2(-330, 44));

            var btn = SprBtn("R", row.transform, _sprBtnResearch, "วิจัย", 19, out var lbl, out var bg);
            SetRect((RectTransform)btn.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(176, 76));
            string pid = id;
            btn.onClick.AddListener(() => RequestResearch(pid));

            var st = Txt("s", row.transform, "", 13, new Color(0.85f, 0.6f, 0.4f, 1f), TextAnchor.LowerRight, FontStyle.Normal);
            SetRect(st.rectTransform, new Vector2(0.3f, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-200, 8), new Vector2(0, 22));

            return new ProjectRow { id = id, btn = btn, btnBg = bg, btnLabel = lbl, status = st };
        }

        // ─────────── helpers ───────────
        private static readonly Vector2 TL = new Vector2(0, 1);
        private static readonly Vector2 TR = new Vector2(1, 1);

        // โหลดสไปรต์สกินห้องวิจัย (Assets/Resources/LabUI/*) — ไม่มี = null → SprImg/SprBtn ใช้ดำโปร่ง 80% แทน
        private static Sprite Spr(string n) => Resources.Load<Sprite>("LabUI/" + n);

        private GameObject Panel(string name, Transform parent, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = col;
            return go;
        }

        // Image จากสไปรต์ (ไม่มีสไปรต์ = ดำโปร่ง 80% ตามกติกา)
        private Image SprImg(string name, Transform parent, Sprite s)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = s; img.type = Image.Type.Simple; img.preserveAspect = true;
            img.color = s != null ? Color.white : CBlack80;
            img.raycastTarget = false;
            return img;
        }

        // ป้ายต้นทุน — ไม่มีสไปรต์ในชุดอ้างอิง → พื้นดำโปร่ง 80% + ขอบ + ตัวอักษรสี
        private Text CostPill(Transform parent, Vector2 pos, Vector2 size, string text, Color textCol)
        {
            var pill = Panel("Pill", parent, CBlack80);
            SetRect(pill.GetComponent<RectTransform>(), TL, TL, TL, pos, size);
            AddBorder(pill, new Color(textCol.r, textCol.g, textCol.b, 0.55f), 1.5f);
            var t = Txt("v", pill.transform, text, 18, textCol, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform); t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        // ปุ่มสีทึบ (อัปเกรด/จัดวิศวกร/ปิด)
        private Button Btn(string name, Transform parent, string label, int size, Color col, out Image bg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            bg = go.GetComponent<Image>(); bg.color = col;
            var t = Txt("T", go.transform, label, size, CText, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform); t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var b = go.GetComponent<Button>();
            b.transition = Selectable.Transition.None;
            var cb = b.colors; cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f); b.colors = cb;
            UIClickPop.Attach(go);
            return b;
        }

        // ปุ่มสไปรต์ (ฝึก/วิจัย) — สไปรต์เป็นพื้น + ตัวหนังสือสดทับ (tint สไปรต์ผ่าน bg.color ใน Refresh)
        private Button SprBtn(string name, Transform parent, Sprite s, string label, int size, out Text lbl, out Image bg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            bg = go.GetComponent<Image>();
            bg.sprite = s; bg.type = Image.Type.Simple; bg.preserveAspect = false;
            bg.color = s != null ? Color.white : CBlack80;
            lbl = Txt("T", go.transform, label, size, CText, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(lbl.rectTransform); lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            var b = go.GetComponent<Button>();
            b.transition = Selectable.Transition.None;
            var cb = b.colors; cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.75f); b.colors = cb;
            UIClickPop.Attach(go);
            return b;
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
