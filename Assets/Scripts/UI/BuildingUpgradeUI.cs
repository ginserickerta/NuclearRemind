using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// แผง Status อาคารแบบเต็ม (ธีมมืด-เขียวอุตสาหกรรม) — เปิดด้วยคลิกอาคาร (ปักหมุดกลางจอ) ยกเว้น Core Tower/Memorial
    ///   header: ไอคอน + ชื่อ + Lv + ปุ่ม ✕ · body: สไปรต์อาคาร + คำอธิบาย + กล่องผลิต/คนงาน + กล่องระดับปัจจุบัน
    ///   อัปเกรด: การ์ด Lv1..Lv3 (ผลผลิตต่อระดับ + ปัจจุบัน/เสร็จ/ล็อก) · แถวต้นทุนอัปเกรด + ปุ่มอัปเกรด + เตือนไม่พอ
    /// UI ทั้งหมดสร้างเองตอนรันไทม์ (ไม่ต้อง wire ใน editor) · ยิงคำสั่งผ่าน EventManager · อ่านสถานะ read-only
    /// hover อาคาร = ป้ายชื่อเล็ก ๆ · คลิก = เปิดแผง · ปิดด้วย Esc / ✕ / คลิกพื้นว่าง / คลิกอาคารเดิมซ้ำ
    /// </summary>
    public class BuildingUpgradeUI : MonoBehaviour, GameUIStack.IPanel
    {
        public static BuildingUpgradeUI Instance { get; private set; }

        [Header("Legacy fields (คงไว้กัน wire เดิมใน scene พัง — ไม่ใช้แล้ว UI สร้าง runtime)")]
        public GameObject panel;
        public RectTransform panelRect;
        public Text nameText, levelText, productionText, costText, hintText, upgradeButtonLabel, workerText;
        public Button upgradeButton, minusButton, plusButton;
        public Slider constructionBar;

        [Header("HUD")]
        [Tooltip("ตำแหน่งแผงเทียบกึ่งกลางจอ (px, อิง 1920×1080)")]
        public Vector2 hudAnchoredPosition = Vector2.zero;
        [Tooltip("ขนาดแผง (px, อิง 1920×1080)")]
        public Vector2 panelSize = new Vector2(1180, 840);

        // ── ธีมสี (มืด-เขียว) ──
        static readonly Color CBackdrop = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color CPanel    = new Color(0.098f, 0.117f, 0.106f, 0.985f);
        static readonly Color CInset    = new Color(0.137f, 0.160f, 0.145f, 1f);
        static readonly Color CInset2   = new Color(0.078f, 0.094f, 0.086f, 1f);
        static readonly Color CBorder   = new Color(0.28f, 0.36f, 0.29f, 1f);
        static readonly Color CText     = new Color(0.85f, 0.88f, 0.82f, 1f);
        static readonly Color CMuted    = new Color(0.55f, 0.62f, 0.52f, 1f);
        static readonly Color CAccent   = new Color(0.56f, 0.85f, 0.45f, 1f); // เขียวเน้น (ค่า/ไอคอน)
        static readonly Color CWarn     = new Color(0.86f, 0.36f, 0.30f, 1f); // แดง (เตือน/ไม่พอ)
        static readonly Color CGold     = new Color(0.80f, 0.86f, 0.48f, 1f); // ระดับปัจจุบัน
        static readonly Color CBtn      = new Color(0.20f, 0.42f, 0.24f, 1f);
        static readonly Color CBtnDim   = new Color(0.24f, 0.26f, 0.24f, 1f);
        static readonly Color CClose    = new Color(0.55f, 0.17f, 0.16f, 1f);

        // runtime state
        private Vector2Int _currentCell;
        private bool _shown, _placing, _demolishing;
        private Font _font;

        // built UI refs
        private GameObject _root, _backdrop;
        private RectTransform _rootRect;
        private Image _iconImg, _spriteImg;
        private Text _nameTxt, _headLvTxt, _descTxt, _hintTxt;
        private Text _prodLabelTxt, _prodValTxt, _workerValTxt, _bigLvTxt, _maxLvTxt, _extractTxt;
        private GameObject _extractRow;
        private Button _workerMinus, _workerPlus;
        private Text _upTitleTxt;
        private LevelCard[] _cards;
        private GameObject _reqRow, _lvBox;
        private Text _reqTitleTxt, _reqEnergyTxt, _reqIronTxt, _reqWorkerTxt, _reqTimeTxt, _warnTxt;
        private Button _upgradeBtn; private Text _upgradeBtnTxt;
        private Button _closeBtn;
        private bool _authoring; // true = กำลัง bake เป็น prefab → parent ใต้ตัวเอง (ไม่ใช่ canvas)

        // hover nameplate
        private GameObject _nameplate; private RectTransform _nameplateRect; private Text _nameplateText;

        private static readonly StringBuilder _sb = new StringBuilder(64);

        private struct LevelCard { public GameObject root; public Outline frame; public Text lv; public Image sprite; public Text output; public Text status; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _font = LoadFont();
        }

        private void OnEnable()
        {
            var e = EventManager.Instance;
            e.OnResourceChanged     += HandleResourceChanged;
            e.OnBuildingUpgraded    += HandleBuildingUpgraded;
            e.OnBuildingRemoved     += HandleBuildingRemoved;
            e.OnBuildingSelected    += HandleBuildingSelected;
            e.OnDemolishModeToggled += HandleDemolishModeToggled;
            e.OnWorkerAssignmentChanged += HandleWorkerChanged;
            e.OnWorkerPoolChanged   += HandleWorkerPoolChanged;
            e.OnConstructionProgressChanged += HandleConstructionProgress;
            e.OnConstructionComplete += HandleConstructionCompleted;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            var e = EventManager.Instance;
            e.OnResourceChanged     -= HandleResourceChanged;
            e.OnBuildingUpgraded    -= HandleBuildingUpgraded;
            e.OnBuildingRemoved     -= HandleBuildingRemoved;
            e.OnBuildingSelected    -= HandleBuildingSelected;
            e.OnDemolishModeToggled -= HandleDemolishModeToggled;
            e.OnWorkerAssignmentChanged -= HandleWorkerChanged;
            e.OnWorkerPoolChanged   -= HandleWorkerPoolChanged;
            e.OnConstructionProgressChanged -= HandleConstructionProgress;
            e.OnConstructionComplete -= HandleConstructionCompleted;
        }

        private void Start()
        {
            if (panel != null) panel.SetActive(false); // ซ่อนแผงเก่าจาก HUDCanvasSetup ทิ้ง

            // authored prefab (แก้ layout ด้วยตาใน Editor) วางที่ Resources/BuildingUI/BuildingStatusPanel
            // มี → instantiate + bind ref จาก BuildingPanelRefs · ไม่มี/เพี้ยน → สร้างสด fallback เหมือนเดิม
            var prefab = Resources.Load<GameObject>("BuildingUI/BuildingStatusPanel");
            if (prefab != null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
                var inst = Instantiate(prefab);
                if (canvas != null) inst.transform.SetParent(canvas.transform, false);
                var refs = inst.GetComponent<BuildingPanelRefs>();
                if (refs != null) { BindFromRefs(refs); HookListeners(); }
                else { Destroy(inst); BuildPanel(); }
            }
            else BuildPanel();

            Hide();
        }

        // ─────────────────── click-to-open + hover nameplate ───────────────────
        private void Update()
        {
            if (BuildingRegistry.Instance == null || InputManager.Instance == null) return;

            if (_placing || _demolishing) { HideNameplate(); if (_shown) Hide(); return; }

            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            // ตรวจอาคารใต้เมาส์ (footprint) แยก 2 ระดับ:
            //   overBuilding = มีอาคารใด ๆ อยู่ใต้เมาส์ → ใช้โชว์ "ป้ายชื่อ hover" ทุกอาคาร (รวม Core Tower/Memorial/Lab)
            //   openable     = อาคารทั่วไปที่เปิดแผงนี้ได้ (Core/Memorial/Lab ถูก exclude เพราะมีแผงเฉพาะของตัวเอง)
            Vector2Int hoverOrigin = default; BuildingData hoverData = null;
            bool overBuilding = !overUI && BuildingRegistry.Instance.TryGetBuildingAt(
                    InputManager.Instance.GetMouseGridPosition(), out hoverOrigin, out hoverData) && hoverData != null;
            bool openable = overBuilding && !IsExcluded(hoverData);

            if (Input.GetMouseButtonDown(0) && !overUI)
            {
                if (openable)
                {
                    if (_shown && hoverOrigin == _currentCell) Hide();
                    else OpenAt(hoverOrigin);
                }
                else if (_shown) Hide();
            }

            // Esc จัดการรวมที่ GameUIStack (ผ่าน PauseMenuController) — ไม่เช็คเองแล้ว
            if (_shown && Input.GetKeyDown(KeyCode.Q)) EventManager.Instance.RaiseWorkerAssignRequested(_currentCell, -1);
            else if (_shown && Input.GetKeyDown(KeyCode.E)) EventManager.Instance.RaiseWorkerAssignRequested(_currentCell, +1);

            if (_shown && !BuildingRegistry.Instance.PlacedBuildings.ContainsKey(_currentCell)) Hide();

            // โหนดแร่/ไซต์ก่อสร้าง: refresh ต่อเฟรมให้ % ขุด/เวลาเดินสด (Refresh ปกติยิงตาม event เท่านั้น)
            if (_shown && BuildingRegistry.Instance.PlacedBuildings.TryGetValue(_currentCell, out var curData)
                && curData != null
                && (curData.isOreNode
                    || (ConstructionController.Instance != null && ConstructionController.Instance.IsUnderConstruction(_currentCell))))
                Refresh();

            // ป้ายชื่อ hover — โชว์ทุกอาคาร (รวม Core Tower/Memorial/Lab) ยกเว้นตอนแผงนี้เปิดค้างที่อาคารเดิม
            if (overBuilding && !(_shown && hoverOrigin == _currentCell)) ShowNameplate(hoverData.buildingName);
            else HideNameplate();
        }

        private void OpenAt(Vector2Int origin)
        {
            _currentCell = origin;
            _shown = true;
            if (_backdrop != null) { UIPopIn.Ensure(_backdrop); _backdrop.SetActive(true); } // backdrop เป็นแม่ของ _root → เปิดทั้งชุด
            GameUIStack.Push(this); // ขึ้นบนสุด + ลงทะเบียน (บล็อก Pause / Esc=ปิด)
            Refresh();
        }

        private void Hide()
        {
            _shown = false;
            if (_backdrop != null) _backdrop.SetActive(false); // ปิดทั้งชุด (ไม่งั้น backdrop บังคลิกทั้งจอ)
            GameUIStack.Pop(this);
        }

        // ── GameUIStack (แผงปิดได้: Esc=ปิดเหมือน ✕ · กติกากลางใน PauseMenuController) ──
        bool GameUIStack.IPanel.ClosableByEscape => true;
        void GameUIStack.IPanel.BringToFront() => GameUIStack.RaiseToTop(_backdrop);
        void GameUIStack.IPanel.CloseFromStack() => Hide();

        // Laboratory มีป๊อปอัพเฉพาะ (LabPanelUI — ฝึก/วิจัย/จัดคน/อัปเกรดครบในนั้น) — exclude กันเปิดซ้อน
        // แบบเดียวกับ CoreTower (CoreTowerPanelUI) และ Memorial (MemorialPanelController)
        private static bool IsExcluded(BuildingData d)
            => d == null || d.buildingType == BuildingType.CoreTower || d.isCoreTowerPart
               || d.buildingType == BuildingType.Memorial
               || d.buildingType == BuildingType.Laboratory;

        // ═══════════════════════════ POPULATE ═══════════════════════════
        private void Refresh()
        {
            if (_root == null) return;
            if (!BuildingRegistry.Instance.PlacedBuildings.TryGetValue(_currentCell, out var data) || data == null)
            { Hide(); return; }

            int level = BuildingRegistry.Instance.GetLevel(_currentCell);
            int maxLv = BuildingRegistry.Instance.maxBuildingLevel;
            bool isMax = level >= maxLv;
            bool building = ConstructionController.Instance != null &&
                            ConstructionController.Instance.IsUnderConstruction(_currentCell);

            // header
            var (primEmoji, primLabel, primBase) = PrimaryOutput(data);
            // ใช้ sprite ตามระดับปัจจุบัน (realtime) — เช่น โรงไฟฟ้า Lv.3 โชว์ภาพ Lv.3 ที่พรีวิวซ้ายบน
            var curSprite = data.SpriteForLevel(level);
            if (_iconImg != null) { _iconImg.sprite = curSprite; _iconImg.enabled = curSprite != null; }
            if (_nameTxt != null) _nameTxt.text = data.buildingName;
            if (_headLvTxt != null) _headLvTxt.text = $"Lv. {level}";
            if (_descTxt != null) _descTxt.text = string.IsNullOrEmpty(data.description)
                                                ? "อาคารในเมือง Veltara" : data.description;
            if (_hintTxt != null) _hintTxt.text = data.isOreNode ? "แหล่งแร่ธรรมชาติ (ไม่มีระดับ)" : "";
            if (_spriteImg != null) { _spriteImg.sprite = curSprite; _spriteImg.enabled = curSprite != null; }

            // ผลิต + คนงาน (ค่าจริงตามคนที่ประจำ)
            int required = BuildingRegistry.Instance.WorkersRequired(_currentCell);
            int assigned = WorkerAssignmentManager.Instance != null ? WorkerAssignmentManager.Instance.GetAssigned(_currentCell) : 0;
            int idle = WorkerAssignmentManager.Instance != null ? WorkerAssignmentManager.Instance.IdleOfClass(data.requiredClass) : 0;

            // ═══ โหนดแร่: โหมด "งานขุดมีเวลา" — ซ่อนส่วนอัปเกรด/ระดับ · โชว์แร่ + สถานะขุด ═══
            bool ore = data.isOreNode;
            if (_upTitleTxt != null) _upTitleTxt.gameObject.SetActive(!ore);
            if (_reqRow != null) _reqRow.SetActive(!ore);
            if (_lvBox != null) _lvBox.SetActive(!ore);
            if (ore)
            {
                if (_headLvTxt != null) _headLvTxt.text = ""; // โหนดแร่ไม่มีระดับ
                foreach (var c in _cards) if (c.root != null) c.root.SetActive(false);

                var om = OreDepositManager.Instance;
                float oreIron = om != null ? om.GetIronPayload(_currentCell) : 0f;
                float oreTrit = om != null ? om.GetTritiumPayload(_currentCell) : 0f;

                if (_prodLabelTxt != null) _prodLabelTxt.text = "⛏ แร่ในแหล่ง";
                if (_prodValTxt != null)
                {
                    _prodValTxt.text = oreTrit > 0f
                        ? $"{Mathf.RoundToInt(oreIron)} เหล็ก · {Mathf.RoundToInt(oreTrit)} ทริเทียม"
                        : $"{Mathf.RoundToInt(oreIron)} เหล็ก";
                    _prodValTxt.color = CAccent;
                }

                if (_workerValTxt != null) _workerValTxt.text = $"{assigned} / {required} คนขุด";
                if (_workerMinus != null) _workerMinus.interactable = assigned > 0;
                if (_workerPlus  != null) _workerPlus.interactable  = assigned < required && idle > 0;

                if (_extractRow != null)
                {
                    _extractRow.SetActive(true);
                    if (_extractTxt != null)
                    {
                        if (assigned <= 0)
                            _extractTxt.text = "⛏ ใส่คนงานเพื่อเริ่มขุด (ยิ่งหลายคน ยิ่งเร็ว)";
                        else if (om != null && om.IsWalking(_currentCell))
                            _extractTxt.text = $"🚶 คนงานกำลังเดินไปแหล่งแร่... (~{Mathf.CeilToInt(om.GetWalkRemaining(_currentCell))} วิ)";
                        else
                        {
                            float prog = om != null ? om.GetMineProgress01(_currentCell) : 0f;
                            float remain = om != null ? om.GetMineSecondsRemaining(_currentCell, assigned) : 0f;
                            _extractTxt.text = $"⏳ กำลังขุด {Mathf.RoundToInt(prog * 100f)}% · เหลือ ~{Mathf.CeilToInt(remain)} วิ";
                        }
                    }
                }
                return; // โหนดแร่ — จบตรงนี้ (ไม่มีระดับ/การ์ด/ต้นทุนอัปเกรด)
            }

            float actual = building ? 0f : ActualPrimaryOutput(data, level, primBase, required, assigned);
            // ระหว่างก่อสร้าง: โชว์ป้ายสถานะ 3 ระยะ (ใส่คนงาน → เดินมา → กำลังสร้าง%) แทนค่าผลิต
            if (_prodLabelTxt != null) _prodLabelTxt.text = building ? "🏗 สถานะก่อสร้าง" : $"{primEmoji} {primLabel} ที่ผลิต";
            if (_prodValTxt != null)
            {
                if (building)
                {
                    var cc = ConstructionController.Instance;
                    if (assigned <= 0)
                        _prodValTxt.text = "⛏ ใส่คนงานเพื่อเริ่มสร้าง";
                    else if (cc != null && cc.IsWalking(_currentCell))
                        _prodValTxt.text = $"🚶 คนงานกำลังเดินมาสร้าง... (~{Mathf.CeilToInt(cc.GetWalkRemaining(_currentCell))} วิ)";
                    else if (cc != null)
                    {
                        int total = cc.GetTotalTicks(_currentCell);
                        int prog = cc.GetProgress(_currentCell);
                        int pct = total > 0 ? Mathf.Clamp(Mathf.RoundToInt(100f * prog / total), 0, 100) : 0;
                        _prodValTxt.text = $"🏗 กำลังสร้าง {pct}%";
                    }
                    else _prodValTxt.text = "กำลังสร้าง";
                    _prodValTxt.color = (assigned <= 0) ? CWarn : CAccent;
                }
                else
                {
                    _prodValTxt.text = $"{Mathf.RoundToInt(actual)} / วัน";
                    _prodValTxt.color = (assigned == 0 && required > 0) ? CWarn : CAccent;
                }
            }
            // เพดานคนที่ใส่ได้ = EffectiveCap (ตามระดับ + ระหว่างสร้างรับผู้สร้าง ≥1) — แหล่งความจริงเดียวกับตอนกด +
            // เดิม gate ด้วย required (=WorkersForLevel) → Habitat (workerRequired 0) ใส่ผู้สร้างไม่ได้ระหว่างสร้าง (บั๊ก #4)
            int workerCap = WorkerAssignmentManager.Instance != null
                ? WorkerAssignmentManager.Instance.EffectiveCap(_currentCell, data) : required;
            if (_workerValTxt != null)
                _workerValTxt.text = workerCap > 0
                    ? $"{assigned} / {workerCap} คน" + (building && required <= 0 ? " (ผู้สร้าง)" : "")
                    : "ไม่ต้องใช้";
            if (_workerMinus != null) _workerMinus.interactable = assigned > 0;
            if (_workerPlus  != null) _workerPlus.interactable  = assigned < workerCap && idle > 0;

            // โรงน้ำ §4: บรรทัดสกัดดิวเทอเรียม (กินน้ำ) — เฉพาะอาคารที่มี deuteriumProduction
            if (_extractRow != null)
            {
                bool isExtractor = data.deuteriumProduction > 0f;
                _extractRow.SetActive(isExtractor);
                if (isExtractor && _extractTxt != null)
                {
                    if (level < BuildingRegistry.Instance.maxBuildingLevel)
                        _extractTxt.text = "🧪 สกัดดิวเทอเรียม: ปลดล็อกที่ Lv.3";
                    else if (required > 0 && assigned == 0)
                        _extractTxt.text = "🧪 สกัดดิวเทอเรียม: ต้องมีคนงานประจำ";
                    else
                    {
                        float wScale = required > 0 ? Mathf.Clamp01((float)assigned / required) : 1f;
                        float ratio = ResourceManager.Instance != null ? ResourceManager.Instance.deuteriumWaterPerUnit : 25f;
                        float d = data.deuteriumProduction * wScale;
                        _extractTxt.text = $"🧪 สกัดดิวเทอเรียม +{Mathf.RoundToInt(d)}/วัน (ใช้น้ำ {Mathf.RoundToInt(d * ratio)}/วัน)";
                    }
                }
            }

            // ระดับปัจจุบัน
            if (_bigLvTxt != null) _bigLvTxt.text = level.ToString();
            if (_maxLvTxt != null) _maxLvTxt.text = $"สูงสุด Lv {maxLv}";

            // การ์ดอัปเกรด
            for (int i = 0; i < _cards.Length; i++)
            {
                int lv = i + 1;
                var c = _cards[i];
                if (c.root != null) c.root.SetActive(lv <= maxLv);
                if (lv > maxLv) continue;
                if (c.lv != null) c.lv.text = $"Lv.{lv}";
                // การ์ดแต่ละใบโชว์ sprite ของระดับตัวเอง (Lv.1/2/3 = ภาพคนละแบบ)
                var cardSprite = data.SpriteForLevel(lv);
                if (c.sprite != null) { c.sprite.sprite = cardSprite; c.sprite.enabled = cardSprite != null; }
                if (c.output != null) c.output.text = primBase > 0f
                    ? $"{primEmoji}+{Mathf.RoundToInt(primBase * ResourceManager.LevelMultiplier(lv))} /วัน" : "—";
                bool current = lv == level, done = lv < level;
                if (c.status != null)
                {
                    c.status.text = current ? "ปัจจุบัน" : done ? "✔" : "🔒";
                    c.status.color = current ? CGold : done ? CAccent : CMuted;
                }
                if (c.frame != null) c.frame.effectColor = current ? CGold : (done ? CBorder : new Color(0.22f,0.24f,0.22f,1f));
            }

            // แถวต้นทุน + ปุ่มอัปเกรด
            if (isMax)
            {
                if (_reqTitleTxt != null) _reqTitleTxt.text = "ระดับสูงสุดแล้ว ✔";
                SetReq(_reqEnergyTxt, "—"); SetReq(_reqIronTxt, "—"); SetReq(_reqWorkerTxt, "—"); SetReq(_reqTimeTxt, "—");
                if (_warnTxt != null) _warnTxt.gameObject.SetActive(false);
                if (_upgradeBtn != null) { _upgradeBtn.interactable = false; _upgradeBtnTxt.text = "สูงสุดแล้ว"; }
            }
            else
            {
                int nextLv = level + 1;
                int ironCost = data.upgradeIronCost * level;
                int energyCost = data.upgradeEnergyCost * level;
                int nextWorkers = data.WorkersForLevel(nextLv);
                bool afford = CanAfford(ironCost, energyCost);

                if (_reqTitleTxt != null) _reqTitleTxt.text = $"ความต้องการสำหรับ Lv {nextLv}";
                SetReqCol(_reqEnergyTxt, energyCost > 0 ? energyCost.ToString() : "—",
                          energyCost == 0 || (ResourceManager.Instance == null || ResourceManager.Instance.Current.energy >= energyCost));
                SetReqCol(_reqIronTxt, ironCost.ToString(),
                          ResourceManager.Instance == null || ResourceManager.Instance.Current.iron >= ironCost);
                SetReq(_reqWorkerTxt, $"{assigned} / {nextWorkers}");
                SetReq(_reqTimeTxt, "ทันที");
                if (_warnTxt != null) _warnTxt.gameObject.SetActive(!afford);
                if (_upgradeBtn != null)
                {
                    _upgradeBtn.interactable = afford;
                    if (_upgradeBtn.image != null) _upgradeBtn.image.color = afford ? CBtn : CBtnDim;
                    if (_upgradeBtnTxt != null) _upgradeBtnTxt.text = "⬆ อัปเกรด";
                }
            }
        }

        private void SetReq(Text t, string s) { if (t != null) { t.text = s; t.color = CText; } }
        private void SetReqCol(Text t, string s, bool ok) { if (t != null) { t.text = s; t.color = ok ? CText : CWarn; } }

        // resource หลักของอาคาร (emoji, label, ผลผลิตฐาน/วันที่ L1 เต็มคน)
        private static (string, string, float) PrimaryOutput(BuildingData d)
        {
            if (d.energyProduction > 0f) return ("⚡", "ไฟฟ้า", d.energyProduction);
            if (d.waterProduction  > 0f) return ("💧", "น้ำ", d.waterProduction);
            if (d.foodProduction   > 0f) return ("🌿", "อาหาร", d.foodProduction);
            if (d.knowledgeProduction > 0f) return ("📖", "ความรู้", d.knowledgeProduction);
            if (d.ironProduction   > 0f || d.isOreNode) return ("⛏", "แร่เหล็ก", d.ironProduction);
            return ("⚙", "ผลผลิต", 0f);
        }

        private float ActualPrimaryOutput(BuildingData d, int level, float primBase, int required, int assigned)
        {
            if (required > 0 && assigned == 0) return 0f;
            float workerScale = required > 0 ? Mathf.Clamp01((float)assigned / required) : 1f;
            var crisis = CrisisEffectManager.Instance;
            var wam = WorkerAssignmentManager.Instance;
            int total = wam != null ? wam.TotalAssigned : 0;
            int busy = crisis != null ? crisis.BusyWorkers : 0;
            float busyF = total > 0 ? (float)CrisisEffectMath.EffectiveWorkers(total, busy) / total : 1f;
            float eff = crisis != null ? crisis.WorkerEfficiencyMultiplier : 1f;

            float lvlMul = ResourceManager.LevelMultiplier(level);
            float foodYield = (d.foodProduction > 0f && crisis != null) ? crisis.FoodYieldMultiplier : 1f;
            return primBase * workerScale * busyF * lvlMul * eff * foodYield;
        }

        private static bool CanAfford(int iron, int energy)
        {
            var rm = ResourceManager.Instance;
            if (rm == null) return true;
            return rm.Current.iron >= iron && rm.Current.energy >= energy;
        }

        // ─────────────── actions ───────────────
        private void OnUpgrade() { if (_shown) EventManager.Instance.RaiseUpgradeBuildingRequested(_currentCell); }
        private void OnPlus()    { if (_shown) EventManager.Instance.RaiseWorkerAssignRequested(_currentCell, +1); }
        private void OnMinus()   { if (_shown) EventManager.Instance.RaiseWorkerAssignRequested(_currentCell, -1); }

        private void HandleWorkerChanged(Vector2Int c, int n)      { if (_shown) Refresh(); }
        private void HandleWorkerPoolChanged(int idle, int total)  { if (_shown) Refresh(); }
        private void HandleResourceChanged(ResourceData _)         { if (_shown) Refresh(); }
        private void HandleBuildingUpgraded(Vector2Int c, int lv)  { if (_shown && c == _currentCell) Refresh(); }
        private void HandleConstructionProgress(Vector2Int c, int p){ if (_shown && c == _currentCell) Refresh(); }
        private void HandleConstructionCompleted(Vector2Int c, BuildingData _){ if (_shown && c == _currentCell) Refresh(); }
        private void HandleBuildingRemoved(Vector2Int c)           { if (_shown && c == _currentCell) Hide(); }
        private void HandleBuildingSelected(BuildingData d)        { _placing = d != null; }
        private void HandleDemolishModeToggled(bool a)            { _demolishing = a; }

        // ═══════════════════════════ BUILD UI (runtime) ═══════════════════════════
        private void BuildPanel()
        {
            var canvas = panel != null ? panel.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
            // bake (authored) → parent ใต้ตัวเอง (prefab เก็บ subtree ได้) · runtime สร้างสด → ใต้ canvas เหมือนเดิม
            Transform parent = _authoring ? transform
                             : (canvas != null ? canvas.transform : (panel != null ? panel.transform.parent : transform));

            // backdrop มืดโปร่งเต็มจอ (กันคลิกทะลุ + โฟกัส)
            _backdrop = Panel("BuildingStatusBackdrop", parent, CBackdrop);
            Stretch(_backdrop.GetComponent<RectTransform>());
            var bdBtn = _backdrop.AddComponent<Button>(); bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            // root panel
            _root = Panel("BuildingStatusPanel", _backdrop.transform, CPanel);
            _rootRect = _root.GetComponent<RectTransform>();
            _rootRect.anchorMin = _rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            _rootRect.pivot = new Vector2(0.5f, 0.5f);
            _rootRect.sizeDelta = panelSize;
            _rootRect.anchoredPosition = hudAnchoredPosition;
            AddBorder(_root, CBorder, 3f);
            // _root มี Image (พื้น) เป็น raycast target อยู่แล้ว → คลิกในแผงไม่ทะลุไปโดน backdrop (ไม่ปิด)

            float W = panelSize.x, H = panelSize.y, pad = 22f;

            // ===== HEADER =====
            _iconImg = Img("HdrIcon", _root.transform, CAccent);
            SetRect(_iconImg.rectTransform, new Vector2(0,1),new Vector2(0,1),new Vector2(0,1), new Vector2(pad+2, -pad-2), new Vector2(46,46));
            _iconImg.preserveAspect = true;

            _nameTxt = Txt("HdrName", _root.transform, "อาคาร", 34, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetRect(_nameTxt.rectTransform, new Vector2(0,1),new Vector2(0,1),new Vector2(0,1), new Vector2(pad+58, -pad), new Vector2(520,44));

            _headLvTxt = Txt("HdrLv", _root.transform, "Lv. 1", 28, CGold, TextAnchor.UpperLeft, FontStyle.Bold);
            SetRect(_headLvTxt.rectTransform, new Vector2(0,1),new Vector2(0,1),new Vector2(0,1), new Vector2(pad+340, -pad), new Vector2(160,40));

            _descTxt = Txt("HdrDesc", _root.transform, "", 17, CMuted, TextAnchor.UpperLeft);
            SetRect(_descTxt.rectTransform, new Vector2(0,1),new Vector2(0,1),new Vector2(0,1), new Vector2(pad+58, -pad-42), new Vector2(560,26));

            _hintTxt = Txt("HdrHint", _root.transform, "", 15, CAccent, TextAnchor.UpperLeft);
            SetRect(_hintTxt.rectTransform, new Vector2(0,1),new Vector2(0,1),new Vector2(0,1), new Vector2(pad+58, -pad-66), new Vector2(560,22));

            _closeBtn = Btn("CloseBtn", _root.transform, "✕", 26, CClose);
            SetRect(((RectTransform)_closeBtn.transform), new Vector2(1,1),new Vector2(1,1),new Vector2(1,1), new Vector2(-pad, -pad), new Vector2(52,52));
            _closeBtn.onClick.AddListener(Hide);

            // ===== BODY (สไปรต์ | ข้อมูล | ระดับปัจจุบัน) =====
            float bodyTop = -110f, bodyH = 300f;
            // left sprite box
            var spriteBox = Panel("SpriteBox", _root.transform, CInset2);
            SetRect(spriteBox.GetComponent<RectTransform>(), new Vector2(0,1),new Vector2(0,1),new Vector2(0,1), new Vector2(pad, bodyTop), new Vector2(360, bodyH));
            AddBorder(spriteBox, CBorder, 2f);
            _spriteImg = Img("BuildingSprite", spriteBox.transform, Color.white);
            SetRect(_spriteImg.rectTransform, new Vector2(0.5f,0.5f),new Vector2(0.5f,0.5f),new Vector2(0.5f,0.5f), Vector2.zero, new Vector2(300,260));
            _spriteImg.preserveAspect = true;

            // middle info box
            var infoBox = Panel("InfoBox", _root.transform, CInset);
            SetRect(infoBox.GetComponent<RectTransform>(), new Vector2(0,1),new Vector2(0,1),new Vector2(0,1), new Vector2(pad+380, bodyTop), new Vector2(W-360-320-pad*2-24, bodyH));
            AddBorder(infoBox, CBorder, 2f);
            float infoW = W-360-320-pad*2-24;

            // ผลิต row
            var prodRow = Panel("ProdRow", infoBox.transform, CInset2);
            SetRect(prodRow.GetComponent<RectTransform>(), new Vector2(0,1),new Vector2(1,1),new Vector2(0.5f,1), new Vector2(0,-16), new Vector2(-28,54));
            _prodLabelTxt = Txt("ProdLabel", prodRow.transform, "⚡ ไฟฟ้า ที่ผลิต", 20, CText, TextAnchor.MiddleLeft);
            SetRect(_prodLabelTxt.rectTransform, new Vector2(0,0),new Vector2(0.6f,1),new Vector2(0,0.5f), new Vector2(16,0), Vector2.zero);
            _prodValTxt = Txt("ProdVal", prodRow.transform, "0 / วัน", 22, CAccent, TextAnchor.MiddleRight, FontStyle.Bold);
            SetRect(_prodValTxt.rectTransform, new Vector2(0.55f,0),new Vector2(1,1),new Vector2(1,0.5f), new Vector2(-16,0), Vector2.zero);

            // คนงาน row
            var wRow = Panel("WorkerRow", infoBox.transform, CInset2);
            SetRect(wRow.GetComponent<RectTransform>(), new Vector2(0,1),new Vector2(1,1),new Vector2(0.5f,1), new Vector2(0,-78), new Vector2(-28,54));
            var wLabel = Txt("WLabel", wRow.transform, "👤 คนงานที่ใช้", 20, CText, TextAnchor.MiddleLeft);
            SetRect(wLabel.rectTransform, new Vector2(0,0),new Vector2(0.5f,1),new Vector2(0,0.5f), new Vector2(16,0), Vector2.zero);
            _workerMinus = Btn("WMinus", wRow.transform, "−", 24, new Color(0.55f,0.24f,0.22f));
            SetRect((RectTransform)_workerMinus.transform, new Vector2(1,0.5f),new Vector2(1,0.5f),new Vector2(1,0.5f), new Vector2(-150,0), new Vector2(40,40));
            _workerMinus.onClick.AddListener(OnMinus);
            _workerValTxt = Txt("WVal", wRow.transform, "0 / 0 คน", 20, CAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(_workerValTxt.rectTransform, new Vector2(1,0.5f),new Vector2(1,0.5f),new Vector2(1,0.5f), new Vector2(-96,0), new Vector2(110,40));
            _workerPlus = Btn("WPlus", wRow.transform, "+", 24, CBtn);
            SetRect((RectTransform)_workerPlus.transform, new Vector2(1,0.5f),new Vector2(1,0.5f),new Vector2(1,0.5f), new Vector2(-24,0), new Vector2(40,40));
            _workerPlus.onClick.AddListener(OnPlus);

            // สกัดดิวเทอเรียม row (เฉพาะโรงน้ำ §4) — กินน้ำแปลงเป็นเชื้อเพลิงฟิวชัน
            _extractRow = Panel("ExtractRow", infoBox.transform, CInset2);
            SetRect(_extractRow.GetComponent<RectTransform>(), new Vector2(0,1),new Vector2(1,1),new Vector2(0.5f,1), new Vector2(0,-140), new Vector2(-28,54));
            _extractTxt = Txt("ExtractVal", _extractRow.transform, "", 17, CGold, TextAnchor.MiddleLeft);
            SetRect(_extractTxt.rectTransform, new Vector2(0,0),new Vector2(1,1),new Vector2(0.5f,0.5f), new Vector2(16,0), new Vector2(-28,0));

            // right level box
            _lvBox = Panel("LevelBox", _root.transform, CInset);
            SetRect(_lvBox.GetComponent<RectTransform>(), new Vector2(1,1),new Vector2(1,1),new Vector2(1,1), new Vector2(-pad, bodyTop), new Vector2(300, bodyH));
            AddBorder(_lvBox, CBorder, 2f);
            var lvTitle = Txt("LvTitle", _lvBox.transform, "ระดับปัจจุบัน", 20, CText, TextAnchor.UpperCenter, FontStyle.Bold);
            SetRect(lvTitle.rectTransform, new Vector2(0,1),new Vector2(1,1),new Vector2(0.5f,1), new Vector2(0,-16), new Vector2(-20,28));
            _bigLvTxt = Txt("BigLv", _lvBox.transform, "1", 90, CGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(_bigLvTxt.rectTransform, new Vector2(0.5f,0.5f),new Vector2(0.5f,0.5f),new Vector2(0.5f,0.5f), new Vector2(0,10), new Vector2(180,140));
            _maxLvTxt = Txt("MaxLv", _lvBox.transform, "สูงสุด Lv 3", 18, CMuted, TextAnchor.LowerCenter);
            SetRect(_maxLvTxt.rectTransform, new Vector2(0,0),new Vector2(1,0),new Vector2(0.5f,0), new Vector2(0,18), new Vector2(-20,26));

            // ===== UPGRADE CARDS =====
            _upTitleTxt = Txt("UpTitle", _root.transform, "⬆ อัปเกรด", 22, CAccent, TextAnchor.UpperLeft, FontStyle.Bold);
            SetRect(_upTitleTxt.rectTransform, new Vector2(0,1),new Vector2(0,1),new Vector2(0,1), new Vector2(pad, bodyTop-bodyH-18), new Vector2(300,30));

            int maxCards = 3;
            _cards = new LevelCard[maxCards];
            float cardW = 190f, cardH = 210f, cardTop = bodyTop-bodyH-56, gap = 40f;
            for (int i = 0; i < maxCards; i++)
            {
                var card = new LevelCard();
                card.root = Panel($"Card{i+1}", _root.transform, CInset2);
                SetRect(card.root.GetComponent<RectTransform>(), new Vector2(0,1),new Vector2(0,1),new Vector2(0,1),
                        new Vector2(pad + i*(cardW+gap), cardTop), new Vector2(cardW, cardH));
                card.frame = AddBorder(card.root, CBorder, 2f);
                card.lv = Txt("Lv", card.root.transform, $"Lv.{i+1}", 22, CText, TextAnchor.UpperCenter, FontStyle.Bold);
                SetRect(card.lv.rectTransform, new Vector2(0,1),new Vector2(1,1),new Vector2(0.5f,1), new Vector2(0,-10), new Vector2(-10,28));
                card.sprite = Img("Sp", card.root.transform, Color.white);
                SetRect(card.sprite.rectTransform, new Vector2(0.5f,1),new Vector2(0.5f,1),new Vector2(0.5f,1), new Vector2(0,-42), new Vector2(110,100));
                card.sprite.preserveAspect = true;
                card.output = Txt("Out", card.root.transform, "+0 /วัน", 18, CAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
                SetRect(card.output.rectTransform, new Vector2(0,0),new Vector2(1,0),new Vector2(0.5f,0), new Vector2(0,48), new Vector2(-10,26));
                card.status = Txt("St", card.root.transform, "🔒", 18, CMuted, TextAnchor.LowerCenter);
                SetRect(card.status.rectTransform, new Vector2(0,0),new Vector2(1,0),new Vector2(0.5f,0), new Vector2(0,14), new Vector2(-10,28));
                _cards[i] = card;
            }

            // ===== REQUIREMENT ROW + UPGRADE BUTTON =====
            _reqRow = Panel("ReqRow", _root.transform, CInset);
            SetRect(_reqRow.GetComponent<RectTransform>(), new Vector2(0,0),new Vector2(1,0),new Vector2(0.5f,0), new Vector2(0,pad), new Vector2(-pad*2, 130));
            AddBorder(_reqRow, CBorder, 2f);

            _reqTitleTxt = Txt("ReqTitle", _reqRow.transform, "ความต้องการสำหรับ Lv 2", 18, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetRect(_reqTitleTxt.rectTransform, new Vector2(0,1),new Vector2(0.6f,1),new Vector2(0,1), new Vector2(18,-12), new Vector2(0,26));

            // three cost items
            _reqEnergyTxt = CostItem(_reqRow.transform, "⚡", "พลังงาน", 20);
            _reqIronTxt   = CostItem(_reqRow.transform, "🧱", "วัสดุ", 250);
            _reqWorkerTxt = CostItem(_reqRow.transform, "👷", "คนงาน", 480);
            _reqTimeTxt   = Txt("ReqTime", _reqRow.transform, "ทันที", 18, CText, TextAnchor.MiddleLeft);
            var timeLabel = Txt("TimeLabel", _reqRow.transform, "⏱ เวลาอัปเกรด", 16, CMuted, TextAnchor.MiddleLeft);
            SetRect(timeLabel.rectTransform, new Vector2(0.55f,0),new Vector2(0.55f,0),new Vector2(0,0), new Vector2(20,60), new Vector2(180,24));
            SetRect(_reqTimeTxt.rectTransform, new Vector2(0.55f,0),new Vector2(0.55f,0),new Vector2(0,0), new Vector2(20,30), new Vector2(180,26));

            _warnTxt = Txt("Warn", _reqRow.transform, "⚠ ทรัพยากรไม่เพียงพอ", 15, CWarn, TextAnchor.MiddleLeft);
            SetRect(_warnTxt.rectTransform, new Vector2(0.55f,0),new Vector2(0.55f,0),new Vector2(0,0), new Vector2(20,6), new Vector2(260,22));

            var upBtnGo = Btn("UpgradeBtn", _reqRow.transform, "⬆ อัปเกรด", 24, CBtn);
            _upgradeBtn = upBtnGo; _upgradeBtnTxt = upBtnGo.GetComponentInChildren<Text>();
            SetRect((RectTransform)upBtnGo.transform, new Vector2(1,0.5f),new Vector2(1,0.5f),new Vector2(1,0.5f), new Vector2(-20,0), new Vector2(240,84));
            upBtnGo.onClick.AddListener(OnUpgrade);
        }

        // ═══════════════════════════ AUTHORED PREFAB (instantiate แทน build) ═══════════════════════════
        // เรียกจาก editor baker: build แผงใต้ตัวเอง แล้ว copy ref ลง BuildingPanelRefs (component logic ถูกลบตอน bake เหลือแค่ visual+refs)
        public void BuildForBake()
        {
            _authoring = true;
            _font = LoadFont();
            BuildPanel();
        }

        // bake → เก็บ ref ทุกชิ้นลงตัวถือ (holder อยู่บน prefab · runtime อ่านกลับ)
        public void CopyRefsTo(BuildingPanelRefs r)
        {
            r.root = _root; r.backdrop = _backdrop;
            r.iconImg = _iconImg; r.spriteImg = _spriteImg;
            r.nameTxt = _nameTxt; r.headLvTxt = _headLvTxt; r.descTxt = _descTxt; r.hintTxt = _hintTxt;
            r.prodLabelTxt = _prodLabelTxt; r.prodValTxt = _prodValTxt; r.workerValTxt = _workerValTxt;
            r.bigLvTxt = _bigLvTxt; r.maxLvTxt = _maxLvTxt; r.extractTxt = _extractTxt; r.extractRow = _extractRow;
            r.workerMinus = _workerMinus; r.workerPlus = _workerPlus; r.upTitleTxt = _upTitleTxt; r.closeBtn = _closeBtn;
            r.reqRow = _reqRow; r.lvBox = _lvBox;
            r.reqTitleTxt = _reqTitleTxt; r.reqEnergyTxt = _reqEnergyTxt; r.reqIronTxt = _reqIronTxt;
            r.reqWorkerTxt = _reqWorkerTxt; r.reqTimeTxt = _reqTimeTxt; r.warnTxt = _warnTxt;
            r.upgradeBtn = _upgradeBtn; r.upgradeBtnTxt = _upgradeBtnTxt;
            if (_cards != null)
            {
                r.cards = new BuildingPanelRefs.CardRef[_cards.Length];
                for (int i = 0; i < _cards.Length; i++)
                    r.cards[i] = new BuildingPanelRefs.CardRef {
                        root = _cards[i].root, frame = _cards[i].frame, lv = _cards[i].lv,
                        sprite = _cards[i].sprite, output = _cards[i].output, status = _cards[i].status };
            }
        }

        // runtime → อ่าน ref จาก holder (prefab instance) เข้าฟิลด์ของ component logic ในซีน
        private void BindFromRefs(BuildingPanelRefs r)
        {
            _root = r.root; _backdrop = r.backdrop;
            _rootRect = _root != null ? _root.GetComponent<RectTransform>() : null;
            _iconImg = r.iconImg; _spriteImg = r.spriteImg;
            _nameTxt = r.nameTxt; _headLvTxt = r.headLvTxt; _descTxt = r.descTxt; _hintTxt = r.hintTxt;
            _prodLabelTxt = r.prodLabelTxt; _prodValTxt = r.prodValTxt; _workerValTxt = r.workerValTxt;
            _bigLvTxt = r.bigLvTxt; _maxLvTxt = r.maxLvTxt; _extractTxt = r.extractTxt; _extractRow = r.extractRow;
            _workerMinus = r.workerMinus; _workerPlus = r.workerPlus; _upTitleTxt = r.upTitleTxt; _closeBtn = r.closeBtn;
            _reqRow = r.reqRow; _lvBox = r.lvBox;
            _reqTitleTxt = r.reqTitleTxt; _reqEnergyTxt = r.reqEnergyTxt; _reqIronTxt = r.reqIronTxt;
            _reqWorkerTxt = r.reqWorkerTxt; _reqTimeTxt = r.reqTimeTxt; _warnTxt = r.warnTxt;
            _upgradeBtn = r.upgradeBtn; _upgradeBtnTxt = r.upgradeBtnTxt;
            if (r.cards != null)
            {
                _cards = new LevelCard[r.cards.Length];
                for (int i = 0; i < r.cards.Length; i++)
                {
                    var cr = r.cards[i];
                    _cards[i] = new LevelCard { root = cr.root, frame = cr.frame, lv = cr.lv,
                                                sprite = cr.sprite, output = cr.output, status = cr.status };
                }
            }
            else _cards = new LevelCard[0];
        }

        // ผูก onClick ใหม่ — listener ที่ AddListener ในโค้ดไม่ถูก serialize ลง prefab จึงต้องผูกซ้ำเมื่อ instantiate
        private void HookListeners()
        {
            if (_backdrop != null) { var b = _backdrop.GetComponent<Button>(); if (b != null) b.onClick.AddListener(Hide); }
            if (_closeBtn != null) _closeBtn.onClick.AddListener(Hide);
            if (_workerMinus != null) _workerMinus.onClick.AddListener(OnMinus);
            if (_workerPlus  != null) _workerPlus.onClick.AddListener(OnPlus);
            if (_upgradeBtn  != null) _upgradeBtn.onClick.AddListener(OnUpgrade);
        }

        // cost item (ไอคอน+ป้าย ด้านบน · ค่า ด้านล่าง) คืน Text ของ "ค่า"
        private Text CostItem(Transform parent, string emoji, string label, float x)
        {
            var lbl = Txt($"C_{label}", parent, $"{emoji} {label}", 16, CMuted, TextAnchor.MiddleLeft);
            SetRect(lbl.rectTransform, new Vector2(0,0),new Vector2(0,0),new Vector2(0,0), new Vector2(x,60), new Vector2(150,24));
            var val = Txt($"V_{label}", parent, "0", 22, CText, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetRect(val.rectTransform, new Vector2(0,0),new Vector2(0,0),new Vector2(0,0), new Vector2(x,26), new Vector2(150,28));
            return val;
        }

        // ─────────── nameplate (hover) ───────────
        private void ShowNameplate(string name)
        {
            EnsureNameplate();
            if (_nameplateText.text != name) _nameplateText.text = name;
            if (!_nameplate.activeSelf) _nameplate.SetActive(true);
            _nameplateRect.position = (Vector2)Input.mousePosition + new Vector2(0f, 26f);
        }
        private void HideNameplate() { if (_nameplate != null && _nameplate.activeSelf) _nameplate.SetActive(false); }

        private void EnsureNameplate()
        {
            if (_nameplate != null) return;
            var canvas = GetComponentInParent<Canvas>() ?? (_root != null ? _root.GetComponentInParent<Canvas>() : null);
            Transform parent = canvas != null ? canvas.transform : transform;
            _nameplate = new GameObject("BuildingNameplate", typeof(RectTransform), typeof(Image));
            _nameplate.transform.SetParent(parent, false);
            _nameplateRect = _nameplate.GetComponent<RectTransform>();
            _nameplateRect.pivot = new Vector2(0.5f, 0f);
            var bg = _nameplate.GetComponent<Image>(); bg.color = new Color(0.08f,0.09f,0.12f,0.86f); bg.raycastTarget = false;
            var fit = _nameplate.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var pad = _nameplate.AddComponent<HorizontalLayoutGroup>(); pad.padding = new RectOffset(14,14,6,6); pad.childAlignment = TextAnchor.MiddleCenter;
            var t = new GameObject("Text", typeof(RectTransform), typeof(Text));
            t.transform.SetParent(_nameplate.transform, false);
            _nameplateText = t.GetComponent<Text>();
            _nameplateText.font = _font; _nameplateText.fontSize = 22; _nameplateText.color = Color.white;
            _nameplateText.alignment = TextAnchor.MiddleCenter; _nameplateText.raycastTarget = false;
            _nameplateText.horizontalOverflow = HorizontalWrapMode.Overflow; _nameplateText.verticalOverflow = VerticalWrapMode.Overflow;
            _nameplate.transform.SetAsLastSibling(); _nameplate.SetActive(false);
        }

        // ─────────── UI helpers ───────────
        private GameObject Panel(string name, Transform parent, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = col;
            return go;
        }
        private Image Img(string name, Transform parent, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = col;
            return img;
        }
        private Text Txt(string name, Transform parent, string text, int size, Color col, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font; t.text = text; t.fontSize = size; t.color = col; t.alignment = anchor; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
        private Button Btn(string name, Transform parent, string label, int size, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = col;
            var t = Txt("T", go.transform, label, size, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform);
            t.raycastTarget = false;
            var b = go.GetComponent<Button>();
            var cb = b.colors; cb.disabledColor = new Color(0.4f,0.4f,0.4f,0.6f); b.colors = cb;
            return b;
        }
        // ขอบ = Outline บน Image พื้นทึบของตัว panel เอง (พื้นทึบบังกลาง เหลือขอบ w รอบๆ เป็นเส้นกรอบสะอาด)
        private Outline AddBorder(GameObject target, Color col, float w)
        {
            var ol = target.AddComponent<Outline>();
            ol.effectColor = col;
            ol.effectDistance = new Vector2(w, w);
            ol.useGraphicAlpha = false;
            return ol;
        }
        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
        // anchor min/max/pivot + anchoredPos + sizeDelta
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
