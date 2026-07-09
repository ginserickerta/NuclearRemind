using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// พาเนลลอยเหนืออาคารที่เมาส์ชี้ (hover) — โชว์ชื่อ/ระดับ/ผลผลิต/ค่าอัปเกรด + ปุ่ม "อัปเกรด" ที่คลิกได้
    /// การอัปเกรดยิงผ่าน EventManager.RaiseUpgradeBuildingRequested (BuildingRegistry เป็นคนทำจริง)
    /// อ่านสถานะ (ระดับ/อาคารที่วาง/ทรัพยากร/ตัวคูณผลผลิต) แบบ read-only query จาก manager กลาง
    ///
    /// ซ่อนตัวเองระหว่างโหมดวาง/ทุบ เพื่อกันคลิกชนกัน · คงพาเนลไว้เมื่อเมาส์อยู่บนตัวพาเนล (จะได้กดปุ่มทัน)
    /// </summary>
    public class BuildingUpgradeUI : MonoBehaviour
    {
        public static BuildingUpgradeUI Instance { get; private set; }

        [Header("Panel (wire โดย HUDCanvasSetup)")]
        public GameObject panel;
        public RectTransform panelRect;
        public Text nameText;      // ชื่ออาคาร
        public Text levelText;     // "Lv.1  ●○○"
        public Text productionText;// "⚡12→36 /วัน"
        public Text costText;      // "อัปเกรด: ⛏40  ⚡0"  (แดงถ้าไม่พอ / เขียวถ้าเต็ม)
        public Text hintText;      // ปลดล็อกเชื้อเพลิงฟิวชันที่ L3 ฯลฯ
        public Button upgradeButton;
        public Text upgradeButtonLabel;

        [Header("Worker assignment (V4 §5) — wire โดย HUDCanvasSetup")]
        public Text workerText;      // "คนงาน 2/3   ว่าง 4"
        public Button minusButton;   // −1 คืนคนสู่ pool
        public Button plusButton;    // +1 ดึงคนจาก pool มาประจำ

        [Header("Construction progress (V4 §5) — แถบก่อสร้างในแผง (บาร์ลอย world-space ถูกถอดแล้ว)")]
        public Slider constructionBar; // โชว์เฉพาะตอนอาคารกำลังก่อสร้าง (ตำแหน่งเดียวกับปุ่มอัปเกรด)

        [Header("Behaviour")]
        [Tooltip("ยกพาเนลขึ้นเหนือฐานอาคารกี่หน่วย world (สูงพอให้พ้นตัวตึก)")]
        public float worldYOffset = 1.2f;
        [Tooltip("หน่วงก่อนซ่อน เมื่อเมาส์ออกจากทั้งอาคารและพาเนล (กันกระพริบตอนเลื่อนเมาส์ขึ้นไปกดปุ่ม)")]
        public float hideGrace = 0.18f;

        [Header("Colors — อ่านออกบนพื้น panel สว่าง (light theme)")]
        public Color affordColor = new Color(0.72f, 0.5f, 0.1f);    // ค่าอัปเกรด (อำพันเข้ม)
        public Color cantAffordColor = new Color(0.8f, 0.22f, 0.22f);
        public Color maxColor = new Color(0.2f, 0.55f, 0.33f);      // เต็มระดับ (เขียวเข้ม)

        // runtime
        private Camera _cam;
        private Vector2Int _currentCell;
        private bool _shown;
        private float _hideTimer;
        private bool _placing;
        private bool _demolishing;

        private static readonly StringBuilder _sb = new StringBuilder(64);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _cam = Camera.main;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnResourceChanged     += HandleResourceChanged;
            EventManager.Instance.OnBuildingUpgraded     += HandleBuildingUpgraded;
            EventManager.Instance.OnBuildingRemoved      += HandleBuildingRemoved;
            EventManager.Instance.OnBuildingSelected     += HandleBuildingSelected;
            EventManager.Instance.OnDemolishModeToggled  += HandleDemolishModeToggled;
            EventManager.Instance.OnWorkerAssignmentChanged += HandleWorkerChanged;
            EventManager.Instance.OnWorkerPoolChanged    += HandleWorkerPoolChanged;
            EventManager.Instance.OnConstructionProgressChanged += HandleConstructionProgress;
            EventManager.Instance.OnConstructionComplete += HandleConstructionCompleted;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnResourceChanged     -= HandleResourceChanged;
            EventManager.Instance.OnBuildingUpgraded     -= HandleBuildingUpgraded;
            EventManager.Instance.OnBuildingRemoved      -= HandleBuildingRemoved;
            EventManager.Instance.OnBuildingSelected     -= HandleBuildingSelected;
            EventManager.Instance.OnDemolishModeToggled  -= HandleDemolishModeToggled;
            EventManager.Instance.OnWorkerAssignmentChanged -= HandleWorkerChanged;
            EventManager.Instance.OnWorkerPoolChanged    -= HandleWorkerPoolChanged;
            EventManager.Instance.OnConstructionProgressChanged -= HandleConstructionProgress;
            EventManager.Instance.OnConstructionComplete -= HandleConstructionCompleted;
        }

        private void Start()
        {
            if (upgradeButton != null)
                upgradeButton.onClick.AddListener(OnUpgradeClicked);
            if (plusButton != null)
                plusButton.onClick.AddListener(OnAssignPlus);
            if (minusButton != null)
                minusButton.onClick.AddListener(OnAssignMinus);
            Hide();
        }

        // ───────────────────────── hover loop ─────────────────────────
        private void Update()
        {
            if (panel == null || BuildingRegistry.Instance == null || InputManager.Instance == null)
                return;

            // โหมดวาง/ทุบ → ซ่อน ไม่ให้คลิกชนกับ ghost/ค้อน
            if (_placing || _demolishing)
            {
                if (_shown) Hide();
                return;
            }

            // คีย์ลัด Q/E: ลด/เพิ่มคนงานให้อาคารที่กำลัง hover (เทียบเท่าปุ่ม −/+) — WAM clamp เองถ้าเกิน/ไม่มี idle
            if (_shown)
            {
                if (Input.GetKeyDown(KeyCode.Q)) EventManager.Instance.RaiseWorkerAssignRequested(_currentCell, -1);
                else if (Input.GetKeyDown(KeyCode.E)) EventManager.Instance.RaiseWorkerAssignRequested(_currentCell, +1);
            }

            // เมาส์อยู่บนตัวพาเนล → คงไว้ (ปล่อยให้ปุ่มรับคลิก) ไม่ต้องคำนวณ cell ใหม่
            if (_shown && RectTransformUtility.RectangleContainsScreenPoint(panelRect, Input.mousePosition, null))
            {
                _hideTimer = hideGrace;
                return;
            }

            Vector2Int cell = InputManager.Instance.GetMouseGridPosition();
            if (BuildingRegistry.Instance.PlacedBuildings.TryGetValue(cell, out var data) && data != null)
            {
                _hideTimer = hideGrace;
                if (!_shown || cell != _currentCell)
                {
                    _currentCell = cell;
                    _shown = true;
                    panel.SetActive(true);
                    Populate();
                }
                PositionOverBuilding();
                return;
            }

            // ไม่อยู่บนอาคารหรือพาเนล → นับถอยหลังแล้วซ่อน (เผื่อช่องว่างตอนเลื่อนขึ้นไปกดปุ่ม)
            if (_shown)
            {
                _hideTimer -= Time.unscaledDeltaTime;
                if (_hideTimer <= 0f) Hide();
            }
        }

        private void PositionOverBuilding()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null || panelRect == null) return;

            Vector3 world = GridManager.Instance.IsoToWorld(_currentCell.x, _currentCell.y)
                          + new Vector3(0f, worldYOffset, 0f);
            Vector3 screen = _cam.WorldToScreenPoint(world);
            if (screen.z < 0f) return; // อยู่หลังกล้อง — อย่าเด้งไปอีกฝั่งจอ
            panelRect.position = screen; // overlay canvas: position = พิกัดจอ (พิกัด pivot bottom-center)
        }

        // ───────────────────────── populate ─────────────────────────
        private void Populate()
        {
            if (!BuildingRegistry.Instance.PlacedBuildings.TryGetValue(_currentCell, out var data) || data == null)
            {
                Hide();
                return;
            }

            // กำลังก่อสร้าง → แผงความคืบหน้า (แถบก่อสร้างย้ายจาก world-space มาอยู่ที่นี่)
            if (ConstructionController.Instance != null &&
                ConstructionController.Instance.IsUnderConstruction(_currentCell))
            {
                PopulateConstruction(data);
                return;
            }

            // แหล่งแร่ = ภูมิประเทศ ไม่มีระดับ/อัปเกรด — แผงแยกของตัวเอง
            if (data.isOreNode)
            {
                PopulateOreNode(data);
                return;
            }

            if (constructionBar != null) constructionBar.gameObject.SetActive(false);

            int level = BuildingRegistry.Instance.GetLevel(_currentCell);
            int maxLevel = BuildingRegistry.Instance.maxBuildingLevel;
            bool isMax = level >= maxLevel;

            if (nameText != null) nameText.text = data.buildingName;
            if (levelText != null) levelText.text = $"Lv.{level}  {LevelDots(level, maxLevel)}";
            if (productionText != null) productionText.text = BuildActualProductionString(data, level);
            if (hintText != null) hintText.text = BuildHint(data, level, maxLevel, isMax);

            if (isMax)
            {
                if (costText != null)
                {
                    costText.text = "ระดับสูงสุดแล้ว ✔";
                    costText.color = maxColor;
                }
                if (upgradeButton != null) upgradeButton.gameObject.SetActive(false);
            }
            else
            {
                int ironCost = data.upgradeIronCost * level;
                int energyCost = data.upgradeEnergyCost * level;
                bool afford = CanAfford(ironCost, energyCost);

                if (costText != null)
                {
                    _sb.Clear();
                    _sb.Append("อัปเกรด: ⛏").Append(ironCost);
                    if (energyCost > 0) _sb.Append("  ⚡").Append(energyCost);
                    costText.text = _sb.ToString();
                    costText.color = afford ? affordColor : cantAffordColor;
                }
                if (upgradeButton != null)
                {
                    upgradeButton.gameObject.SetActive(true);
                    upgradeButton.interactable = afford;
                }
                if (upgradeButtonLabel != null)
                    upgradeButtonLabel.text = afford ? "⬆ อัปเกรด" : "แร่/พลังงานไม่พอ";
            }

            UpdateWorkerRow(data);
        }

        // แผงตอนก่อสร้าง: แถบคืบหน้า + สเปกที่จะได้เมื่อเสร็จ — จ่ายคนเร่งได้ (แถวคนงานเดิม)
        private void PopulateConstruction(BuildingData data)
        {
            var cc = ConstructionController.Instance;
            int progress = cc.GetProgress(_currentCell);
            int total = cc.GetTotalTicks(_currentCell);

            if (nameText != null) nameText.text = data.buildingName;
            if (levelText != null) levelText.text = $"🔨 กำลังก่อสร้าง {progress}/{total}";
            if (productionText != null)
                productionText.text = $"เมื่อเสร็จ: {BuildProductionString(data, 1, true)}"; // สเปก L1 (ไม่มีลูกศรอัป)
            if (costText != null) costText.text = "";
            if (hintText != null) hintText.text = "คนงานมากขึ้น = สร้างเร็วขึ้น (Q/E)";
            if (upgradeButton != null) upgradeButton.gameObject.SetActive(false);

            if (constructionBar != null)
            {
                constructionBar.gameObject.SetActive(true);
                constructionBar.maxValue = Mathf.Max(1, total);
                constructionBar.value = progress;
            }

            UpdateWorkerRow(data);
        }

        // แผงแหล่งแร่ (V4 §5): ผลขุดจริงตอนนี้ + โควตาวันนี้ + ป้ายความเสี่ยงโซน B — ไม่มีอัปเกรด/ระดับ
        private void PopulateOreNode(BuildingData data)
        {
            bool risky = data.oreExposurePerWorkerDay > 0f;
            var ore = OreDepositManager.Instance;
            float quota = ore != null ? ore.GetDailyQuota(_currentCell) : 0f;
            float tritQuota = ore != null ? ore.GetDailyTritiumQuota(_currentCell) : 0f;

            if (constructionBar != null) constructionBar.gameObject.SetActive(false);

            if (nameText != null) nameText.text = data.buildingName;
            if (levelText != null) levelText.text = "แหล่งแร่ธรรมชาติ";
            if (productionText != null)
            {
                var wam = WorkerAssignmentManager.Instance;
                int assigned = wam != null ? wam.GetAssigned(_currentCell) : 0;
                if (assigned == 0)
                {
                    productionText.text = tritQuota > 0f
                        ? $"⏸ ไม่มีคนขุด — โควตาวันนี้ ⛏{quota:0} ⚛{tritQuota:0}"
                        : $"⏸ ไม่มีคนขุด — โควตาวันนี้ ⛏{quota:0}";
                }
                else
                {
                    // ผลขุดจริง = โควตา × กำลังคน × ตัวคูณวิกฤต (สูตรเดียวกับ ResourceManager ore branch)
                    float scale = CurrentOutputScale(data.workerRequired, assigned);
                    productionText.text = tritQuota > 0f
                        ? $"⛏ ตอนนี้ {quota * scale:0}/วัน (โควตา {quota:0}) · ⚛ {tritQuota * scale:0}/วัน"
                        : $"⛏ ตอนนี้ {quota * scale:0}/วัน (โควตา {quota:0})";
                }
            }
            if (costText != null)
            {
                costText.text = risky ? "☢ พื้นที่เสี่ยงรังสี" : "ปลอดภัย · ใกล้เมือง";
                costText.color = risky ? cantAffordColor : maxColor;
            }
            if (hintText != null)
                hintText.text = risky
                    ? $"☢ รังสีสะสม +{data.oreExposurePerWorkerDay:0.#}/คน/วัน · เสี่ยงป่วย {data.oreSickChancePerWorkerDay:P0}/คน/วัน"
                    : "";
            if (upgradeButton != null) upgradeButton.gameObject.SetActive(false);

            UpdateWorkerRow(data); // ปุ่ม −/+ และ Q/E ทำงานปกติ (โหนดอยู่ใน registry)
        }

        // แถวจัดสรรคนงาน (V4 §5): "<คลาส> assigned/required   ว่าง idle" + ปุ่ม +/−
        // idle นับเฉพาะ pool ของคลาสที่อาคารต้องใช้ (Lab=Engineer, Hospital=Medic, Farm=Farmer ฯลฯ)
        private void UpdateWorkerRow(BuildingData data)
        {
            var wam = WorkerAssignmentManager.Instance;
            int required = Mathf.Max(0, data.workerRequired);
            int assigned = wam != null ? wam.GetAssigned(_currentCell) : 0;
            int idle = wam != null ? wam.IdleOfClass(data.requiredClass) : 0;
            bool hasRow = required > 0;
            string label = ClassLabel(data.requiredClass);

            if (workerText != null)
                workerText.text = hasRow
                    ? $"{label} {assigned}/{required}   ว่าง {idle}"
                    : "👷 ไม่ต้องใช้คนงาน";

            if (minusButton != null)
            {
                minusButton.gameObject.SetActive(hasRow);
                minusButton.interactable = hasRow && assigned > 0;
            }
            if (plusButton != null)
            {
                plusButton.gameObject.SetActive(hasRow);
                plusButton.interactable = hasRow && assigned < required && idle > 0;
            }
        }

        // ป้ายคลาส + emoji (แถวจัดสรร) — Worker ใช้คำเดิม "คนงาน" กันสับสน asset ที่ไม่ได้ตั้งคลาส
        private static string ClassLabel(WorkerClass c)
        {
            switch (c)
            {
                case WorkerClass.Engineer: return "🔧 วิศวกร";
                case WorkerClass.Medic:    return "⚕ แพทย์";
                case WorkerClass.Farmer:   return "🌾 เกษตรกร";
                default:                   return "👷 คนงาน";
            }
        }

        private static string LevelDots(int level, int maxLevel)
        {
            _sb.Clear();
            for (int i = 1; i <= maxLevel; i++)
                _sb.Append(i <= level ? '●' : '○');
            return _sb.ToString();
        }

        // ผลผลิต/วันที่เต็มกำลังคน: base × ตัวคูณระดับ (ปัจจุบัน → ระดับถัดไป)
        private static string BuildProductionString(BuildingData data, int level, bool isMax)
        {
            float cur = ResourceManager.LevelMultiplier(level);
            float next = isMax ? cur : ResourceManager.LevelMultiplier(level + 1);

            _sb.Clear();
            AppendProd(data.foodProduction, "🌿", cur, next, isMax);
            AppendProd(data.waterProduction, "💧", cur, next, isMax);
            AppendProd(data.energyProduction, "⚡", cur, next, isMax);
            AppendProd(data.ironProduction, "⛏", cur, next, isMax);
            if (_sb.Length == 0) return "—";
            _sb.Append("/วัน");
            return _sb.ToString();
        }

        private static void AppendProd(float baseVal, string emoji, float curMul, float nextMul, bool isMax)
        {
            if (baseVal <= 0f) return;
            if (_sb.Length > 0) _sb.Append("  ");
            _sb.Append(emoji).Append(Mathf.RoundToInt(baseVal * curMul));
            if (!isMax) _sb.Append('→').Append(Mathf.RoundToInt(baseVal * nextMul));
        }

        // ─── ผลผลิตจริงตอนนี้ (GDD §6 — โชว์ตามคนที่ใส่จริง ไม่ใช่สเปกตอนคนครบ) ───

        /// <summary>
        /// กำลังผลิตรวม = กำลังคน × busy × ประสิทธิภาพ (mirror ResourceManager.ComputeProductionDelta
        /// แบบ read-only query · ไม่จำลอง upkeep gate — แสดงตามกำลังคนพอ)
        /// </summary>
        private float CurrentOutputScale(int required, int assigned)
        {
            float workerScale = required > 0 ? Mathf.Clamp01((float)assigned / required) : 1f;

            var crisis = CrisisEffectManager.Instance;
            var wam = WorkerAssignmentManager.Instance;
            int totalAssigned = wam != null ? wam.TotalAssigned : 0;
            int busy = crisis != null ? crisis.BusyWorkers : 0;
            float busyFactor = totalAssigned > 0
                ? (float)CrisisEffectMath.EffectiveWorkers(totalAssigned, busy) / totalAssigned
                : 1f;
            float efficiency = crisis != null ? crisis.WorkerEfficiencyMultiplier : 1f;

            return workerScale * busyFactor * efficiency;
        }

        // "ตอนนี้: 🌿24 💧12 /วัน" — เฉพาะชนิดที่อาคารนี้ผลิตจริง · ไม่มีคน → หยุดผลิต
        private string BuildActualProductionString(BuildingData data, int level)
        {
            var wam = WorkerAssignmentManager.Instance;
            int required = data.workerRequired;
            int assigned = wam != null ? wam.GetAssigned(_currentCell) : 0;
            if (required > 0 && assigned == 0) return "⏸ หยุดผลิต — ไม่มีคนงานประจำ";

            float scale = CurrentOutputScale(required, assigned) * ResourceManager.LevelMultiplier(level);
            float foodYield = CrisisEffectManager.Instance != null
                ? CrisisEffectManager.Instance.FoodYieldMultiplier : 1f;

            _sb.Clear();
            AppendActual(data.foodProduction * scale * foodYield, "🌿");
            AppendActual(data.waterProduction * scale, "💧");
            AppendActual(data.energyProduction * scale, "⚡");
            AppendActual(data.ironProduction * scale, "⛏");

            // เชื้อเพลิงฟิวชันผลิตเฉพาะระดับสูงสุด (ไม่คูณตัวคูณระดับ — ตาม ComputeProductionDelta)
            if (BuildingRegistry.Instance != null && level >= BuildingRegistry.Instance.maxBuildingLevel)
            {
                float fuelScale = CurrentOutputScale(required, assigned);
                AppendActual(data.deuteriumProduction * fuelScale, "D");
                AppendActual(data.tritiumProduction * fuelScale, "⚛");
            }

            if (_sb.Length == 0) return "—";
            _sb.Insert(0, "ตอนนี้: ").Append(" /วัน");
            return _sb.ToString();
        }

        private static void AppendActual(float val, string emoji)
        {
            if (val <= 0f) return;
            if (_sb.Length > 0) _sb.Append("  ");
            _sb.Append(emoji).Append(Mathf.RoundToInt(val));
        }

        // เชื้อเพลิงฟิวชันผลิตเฉพาะเมื่อถึงระดับสูงสุด (จุดขายวิทยาศาสตร์ NSC)
        private static string BuildHint(BuildingData data, int level, int maxLevel, bool isMax)
        {
            bool makesFuel = data.deuteriumProduction > 0f || data.tritiumProduction > 0f;
            if (!makesFuel) return "";
            if (isMax) return "⚛ ผลิตเชื้อเพลิงฟิวชันแล้ว";
            if (level + 1 >= maxLevel) return $"⚛ อัปเป็น L{maxLevel} → ปลดล็อกเชื้อเพลิงฟิวชัน";
            return "";
        }

        private static bool CanAfford(int ironCost, int energyCost)
        {
            var rm = ResourceManager.Instance;
            if (rm == null) return true; // ไม่มี manager (เช่นในเทสต์) → ไม่บล็อก
            return rm.Current.iron >= ironCost && rm.Current.energy >= energyCost;
        }

        // ───────────────────────── actions / events ─────────────────────────
        private void OnUpgradeClicked()
        {
            if (!_shown) return;
            EventManager.Instance.RaiseUpgradeBuildingRequested(_currentCell);
            // ไม่ต้องซ่อน — BuildingRegistry จะ raise OnBuildingUpgraded กลับมาให้ refresh (ระดับ/ค่าใหม่)
        }

        private void OnAssignPlus()
        {
            if (_shown) EventManager.Instance.RaiseWorkerAssignRequested(_currentCell, +1);
        }

        private void OnAssignMinus()
        {
            if (_shown) EventManager.Instance.RaiseWorkerAssignRequested(_currentCell, -1);
        }

        private void HandleWorkerChanged(Vector2Int cell, int count)
        {
            if (_shown) Populate(); // คน/ปุ่ม +/− อัปเดตสด (assigned อาจเปลี่ยนหลายอาคารตอน reconcile)
        }

        private void HandleWorkerPoolChanged(int idle, int total)
        {
            if (_shown) Populate(); // idle เปลี่ยน → ปุ่ม + อาจเปิด/ปิด
        }

        private void HandleResourceChanged(ResourceData _)
        {
            if (_shown) Populate(); // อัปเดตปุ่ม afford/สีค่าอัปเกรดสด ๆ ตามคลัง
        }

        private void HandleBuildingUpgraded(Vector2Int cell, int newLevel)
        {
            if (_shown && cell == _currentCell) Populate();
        }

        private void HandleConstructionProgress(Vector2Int cell, int progress)
        {
            if (_shown && cell == _currentCell) Populate(); // แถบ/เลขคืบหน้าในแผงขยับสด
        }

        private void HandleConstructionCompleted(Vector2Int cell, BuildingData _)
        {
            if (_shown && cell == _currentCell) Populate(); // สลับจากแผงก่อสร้าง → แผงอาคารปกติทันที
        }

        private void HandleBuildingRemoved(Vector2Int cell)
        {
            if (_shown && cell == _currentCell) Hide();
        }

        private void HandleBuildingSelected(BuildingData data)
        {
            _placing = data != null; // เลือกอาคารเพื่อวาง = อยู่โหมดวาง
        }

        private void HandleDemolishModeToggled(bool active)
        {
            _demolishing = active;
        }

        private void Hide()
        {
            _shown = false;
            if (panel != null) panel.SetActive(false);
        }
    }
}
