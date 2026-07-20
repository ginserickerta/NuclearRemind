using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// HUD หลัก: resource bars, CORE TOWER progress, Hope meter (V4 §9)
    /// อัปเดต real-time ผ่าน EventManager เท่านั้น (OnResourceChanged/OnPopulationChanged/OnTowerProgressChanged ฯลฯ)
    /// Canvas layout (Slider/Text จริง) ต่อ reference ใน Inspector โดยทีม Editor (Day 7 [คน])
    /// </summary>
    public class UIManagerHUD : MonoBehaviour
    {
        public static UIManagerHUD Instance { get; private set; }

        [System.Serializable]
        public class ResourceBarUI
        {
            public Slider bar;
            public Text valueText;
            public Image fillImage;
        }

        [Header("Resource Bars — หลอดทุกแถว: ตันที่ display* แล้วตัวเลขวิ่งต่อได้ถึง cap 9999")]
        public ResourceBarUI foodBar;      // "x / 500" (cap จริง — เกินเสี่ยงวิกฤตเน่า)
        public ResourceBarUI waterBar;
        public ResourceBarUI ironBar;
        public ResourceBarUI energyBar;
        public ResourceBarUI deuteriumBar; // เชื้อเพลิงฟิวชัน (สกัดจากน้ำ — พาร์ทหน้า)
        public ResourceBarUI tritiumBar;   // เชื้อเพลิงฟิวชัน (ขุดจากแหล่งแร่โซน B)

        [Header("Day Cycle")]
        public Text dayText;    // เลขวันเท่านั้น (เช่น "12") — "DAY"/"/30" มากับสไปรต์แผ่นวัน (day_plate)
        public Text timerText;  // นับถอยหลังแยกเฟส "วางแผน 0:28" / "เดินเครื่อง 0:54" (Day 1 = "—")

        [Header("Day Phase (V4 §3) — แถบเวลาแบ่ง Planning|Live + แบนเนอร์เข้า Live")]
        public Slider planningSegBar;   // ช่วงซ้าย (30s) — เต็มระหว่าง Planning
        public Slider liveSegBar;       // ช่วงขวา (60s) — เต็มระหว่าง Live
        public GameObject livePhaseBanner; // ป้ายกลางจอ "เริ่มเดินเครื่อง!" ตอนเข้า Live
        public float bannerSeconds = 1.6f;
        public Color planningColor = new Color(0.28f, 0.55f, 0.92f); // ฟ้า = วางแผน
        public Color liveColor = new Color(0.93f, 0.52f, 0.18f);     // ส้ม = เดินเครื่อง

        private float _bannerTimer;

        [Header("Speed Controls")]
        public Button pauseButton;   // ⏸ → 0×
        public Button normalButton;  // 1×
        public Button fastButton;    // 2×
        public Color speedSelectedColor = new Color(0.3f, 0.7f, 1f);
        public Color speedIdleColor = Color.white;

        [Header("CORE TOWER Progress")]
        public Slider towerProgressBar;
        public Text towerPhaseText;
        public Button toroidalButton; // อัป Toroidal Coils (+หล่อเย็น) — V4 §6
        public Button poloidalButton; // ติดตั้ง Poloidal Coils (กัน micro-damage)

        [Header("Population / Morale (Hope)")]
        public Slider hopeBar;
        public Text hopeText;
        public Text populationText; // legacy บรรทัดเดียว (คงไว้กัน wire เดิมพัง — ไม่สร้างแล้ว ใช้แถว icon ด้านล่างแทน)

        [Header("Population rows (icon ต่อคลาส — wire โดย HUDCanvasSetup) เรียง รวม/worker/engineer/medic/farmer")]
        public Text popTotalText;     // ประชากรรวม  total/shelterCap
        public Text popWorkerText;    // worker (+ ว่าง N)
        public Text popEngineerText;  // engineer
        public Text popMedicText;     // medic
        public Text popFarmerText;    // farmer
        public Button trainEngineerButton; // ฝึก Worker → Engineer (V4 §5)
        public Button trainMedicButton;    // ฝึก Worker → Medic
        public Button trainFarmerButton;   // ฝึก Worker → Farmer
        public Button decree1Button;       // ประกาศฉุกเฉิน 1 (V4 §11)
        public Button decree2Button;       // ประกาศฉุกเฉิน 2

        [Header("Knowledge (V4 §16)")]
        public Slider knowledgeBar;   // 0–100
        public Text knowledgeText;    // "Knowledge: 42 / 100 · Aware"

        [Header("Game Over / Victory")]
        public GameObject gameOverPanel;
        public Text gameOverText;
        public Button restartButton; // เริ่มใหม่ (V4 §14) — คลังความรู้คงอยู่

        [Header("Bar Colors")]
        public Color normalColor = Color.green;
        public Color criticalColor = Color.yellow;
        public Color depletedColor = Color.red;

        // cap จริง 9999 (V4 §4 "แทบไม่จำกัด") ใช้เป็นฐานหลอดไม่ได้ (หลอดจะแบนทั้งเกม)
        // → หลอดตันที่ค่าเหล่านี้: เกินแล้ว Slider clamp เต็มค้าง แต่ตัวเลขวิ่งต่อปกติ
        [Header("Display Reference (ฐานความยาวหลอด — เกินแล้วหลอดเต็มค้าง ตัวเลขวิ่งต่อ)")]
        public float displayFood = 500f;       // = cap จริง
        public float displayWater = 2000f;
        public float displayIron = 2000f;
        public float displayEnergy = 2000f;
        public float displayDeuterium = 500f;  // สเกลเชื้อเพลิง (คลังใช้จริงหลักร้อย)
        public float displayTritium = 500f;

        private float _maxFood;
        private float _critFood, _critWater, _critIron, _critEnergy; // สีเตือนใช้ค่าสัมบูรณ์เดียวกับ alert

        private static readonly string[] PhaseNames = { "Locked", "Cold Assembly", "Plasma Ramp", "Ignition" };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnResourceChanged += HandleResourceChanged;
            EventManager.Instance.OnPopulationChanged += HandlePopulationChanged;
            EventManager.Instance.OnMoraleChanged += HandleMoraleChanged;
            EventManager.Instance.OnTowerProgressChanged += HandleTowerProgressChanged;
            EventManager.Instance.OnGameOver += HandleGameOver;
            EventManager.Instance.OnDayStarted += HandleDayStarted;
            EventManager.Instance.OnDayPhaseChanged += HandleDayPhaseChanged;
            EventManager.Instance.OnSpeedChanged += HandleSpeedChanged;
            EventManager.Instance.OnKnowledgeChanged += HandleKnowledgeChanged;
            EventManager.Instance.OnWorkerPoolChanged += HandleWorkerPoolChanged;
            HookWorkerManager();
        }

        // WorkerManager auto-spawns AfterSceneLoad, so it may not exist yet at OnEnable — Start() retries.
        // Guarded by a flag because both entry points can run for the same enable cycle.
        private bool _workersHooked;

        private void HookWorkerManager()
        {
            if (_workersHooked || WorkerManager.Instance == null) return;
            WorkerManager.Instance.OnWorkersChanged += RefreshPopulationText;
            _workersHooked = true;
        }

        private void OnDisable()
        {
            if (_workersHooked && WorkerManager.Instance != null)
                WorkerManager.Instance.OnWorkersChanged -= RefreshPopulationText;
            _workersHooked = false;

            if (EventManager.Instance == null) return;
            EventManager.Instance.OnResourceChanged -= HandleResourceChanged;
            EventManager.Instance.OnPopulationChanged -= HandlePopulationChanged;
            EventManager.Instance.OnMoraleChanged -= HandleMoraleChanged;
            EventManager.Instance.OnTowerProgressChanged -= HandleTowerProgressChanged;
            EventManager.Instance.OnGameOver -= HandleGameOver;
            EventManager.Instance.OnDayStarted -= HandleDayStarted;
            EventManager.Instance.OnDayPhaseChanged -= HandleDayPhaseChanged;
            EventManager.Instance.OnSpeedChanged -= HandleSpeedChanged;
            EventManager.Instance.OnKnowledgeChanged -= HandleKnowledgeChanged;
            EventManager.Instance.OnWorkerPoolChanged -= HandleWorkerPoolChanged;
        }

        private void Start()
        {
            // อ่าน config (max capacity / phase targets) จาก manager อื่นครั้งเดียวตอนเริ่ม เพื่อ normalize bar
            // เป็นการอ่านค่า config ที่ตั้งไว้ใน Inspector ของ manager นั้น ๆ ไม่ใช่การเรียก method ข้าม manager
            var rm = ResourceManager.Instance;
            _maxFood = rm.maxFood;
            _critFood = rm.criticalFood;     // สีเตือนตรงกับ alert "ใกล้หมด" เป๊ะ (ค่าสัมบูรณ์)
            _critWater = rm.criticalWater;
            _critIron = rm.criticalIron;
            _critEnergy = rm.criticalEnergy;

            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (livePhaseBanner != null) livePhaseBanner.SetActive(false);

            // WorkerManager exists by now even if it didn't at OnEnable; paint the real count once so the
            // HUD never shows the stale PopulationData defaults on day 1.
            HookWorkerManager();
            RefreshPopulationText();

            // sync หลอด Knowledge ครั้งแรก: OnKnowledgeChanged raise เฉพาะตอนมี delta → ค่าเริ่มเกม (startKnowledge)
            // ไม่ถูก raise → Slider ค้างค่า default ของ Unity (value=1,max=1 = เต็มหลอด) ทั้งที่ค่าเป็น 0
            HandleKnowledgeChanged(rm.Current.knowledge);

            // ปุ่มความเร็ว → raise request ให้ GameManager จัดการ (ไม่เรียก GameManager ตรง)
            if (pauseButton != null)  pauseButton.onClick.AddListener(() => EventManager.Instance.RaiseSpeedChangeRequested(0f));
            if (normalButton != null) normalButton.onClick.AddListener(() => EventManager.Instance.RaiseSpeedChangeRequested(1f));
            if (fastButton != null)   fastButton.onClick.AddListener(() => EventManager.Instance.RaiseSpeedChangeRequested(2f));

            // ปุ่มฝึกคลาส → raise request ให้ PopulationManager (V4 §5)
            if (trainEngineerButton != null) trainEngineerButton.onClick.AddListener(() => EventManager.Instance.RaiseTrainEngineerRequested());
            if (trainMedicButton != null)    trainMedicButton.onClick.AddListener(() => EventManager.Instance.RaiseTrainMedicRequested());
            if (trainFarmerButton != null)   trainFarmerButton.onClick.AddListener(() => EventManager.Instance.RaiseTrainFarmerRequested());

            // ปุ่ม Coils → raise request ให้ CoreTowerManager (V4 §6)
            if (toroidalButton != null) toroidalButton.onClick.AddListener(() => EventManager.Instance.RaiseUpgradeToroidalRequested());
            if (poloidalButton != null) poloidalButton.onClick.AddListener(() => EventManager.Instance.RaiseInstallPoloidalRequested());

            // ปุ่มประกาศฉุกเฉิน → raise ให้ DecreeManager (V4 §11)
            if (decree1Button != null) decree1Button.onClick.AddListener(() => EventManager.Instance.RaiseEnactDecreeRequested(0));
            if (decree2Button != null) decree2Button.onClick.AddListener(() => EventManager.Instance.RaiseEnactDecreeRequested(1));

            // ปุ่มเริ่มใหม่ (V4 §14) — ซ่อนจนจบเกม
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(() => GameManager.Instance?.Restart());
                restartButton.gameObject.SetActive(false);
            }
        }

        private void HandleSpeedChanged(float speed)
        {
            TintSpeedButton(pauseButton,  Mathf.Approximately(speed, 0f));
            TintSpeedButton(normalButton, Mathf.Approximately(speed, 1f));
            TintSpeedButton(fastButton,   speed >= 2f);
        }

        private void TintSpeedButton(Button button, bool selected)
        {
            if (button == null || button.image == null) return;
            button.image.color = selected ? speedSelectedColor : speedIdleColor;
        }

        private int _day = 1;

        private void HandleDayStarted(int day, bool timed)
        {
            _day = day;
            if (dayText != null)
                dayText.text = day.ToString(); // เลขวันอย่างเดียว — "DAY"/"/30" อยู่ในสไปรต์แผ่นวันแล้ว

            if (!timed) // Day 1 tutorial — ไม่จับเวลา
            {
                if (timerText != null) { timerText.text = "—"; timerText.color = planningColor; }
                SetSeg(planningSegBar, 0f);
                SetSeg(liveSegBar, 0f);
            }
            // วันที่จับเวลา: OnDayPhaseChanged(Planning) จะตามมา แล้ว Update เขียน timer/แถบเฟสเอง
        }

        // เข้าเฟสใหม่ (V4 §3): Planning วางแผน → Live เดินเครื่อง · เข้า Live → เด้งแบนเนอร์กลางจอ
        private void HandleDayPhaseChanged(GameManager.DayPhase phase)
        {
            if (phase == GameManager.DayPhase.Live && livePhaseBanner != null)
            {
                livePhaseBanner.SetActive(true);
                _bannerTimer = bannerSeconds;
            }
        }

        // อ่านสถานะเวลา/เฟสแบบ read-only query ต่อเฟรม (เทียบเท่าการอ่าน config ของ manager อื่น)
        private void Update()
        {
            UpdateLiveBanner(); // เฟดแบนเนอร์เข้า Live (unscaled — ไม่โดน pause)

            if (timerText == null || GameManager.Instance == null) return;
            var gm = GameManager.Instance;
            if (!gm.DayTimerActive) return; // คงข้อความล่าสุด (เช่น "—" ของ Day 1)

            bool planning = gm.CurrentDayPhase == GameManager.DayPhase.Planning;
            string label = planning ? "วางแผน" : "เดินเครื่อง";
            Color col = planning ? planningColor : liveColor;

            // นับถอยหลัง "แยกเฟส" (Planning 0:30→0:00 / Live 1:00→0:00) — ceil กันโชว์ 0:00 คร่อมรอยต่อ
            float t = gm.PhaseTimeRemaining;
            int total = Mathf.CeilToInt(t);
            int m = total / 60, s = total % 60;

            // §15: มีเหตุหยุดนาฬิกา (วางอาคาร/ทุบ/ควิซ/วิกฤต) → บอกผู้เล่นชัด ๆ ว่าเวลาหยุด ไม่ใช่บั๊ก
            bool clockPaused = TimeManager.Instance != null && !TimeManager.Instance.IsRunning;
            timerText.text = clockPaused ? $"หยุด · {label} {m}:{s:00}" : $"{label} {m}:{s:00}";
            timerText.color = clockPaused ? Color.gray : col;

            // แถบเวลาแบ่งเฟส: ช่วงที่ผ่านไปของเฟสปัจจุบันเติมขึ้น, อีกช่วงว่าง/เต็มตามลำดับ
            float frac = gm.PhaseDuration > 0f ? 1f - t / gm.PhaseDuration : 0f;
            if (planning) { SetSeg(planningSegBar, frac); SetSeg(liveSegBar, 0f); }
            else          { SetSeg(planningSegBar, 1f);   SetSeg(liveSegBar, frac); }
        }

        private void UpdateLiveBanner()
        {
            if (livePhaseBanner == null || _bannerTimer <= 0f) return;
            _bannerTimer -= Time.unscaledDeltaTime;
            if (_bannerTimer <= 0f) livePhaseBanner.SetActive(false);
        }

        private static void SetSeg(Slider bar, float value)
        {
            if (bar != null) bar.value = Mathf.Clamp01(value);
        }

        private void HandleResourceChanged(ResourceData data)
        {
            SetBar(foodBar, data.food, displayFood, _maxFood, _critFood); // อาหารโชว์ "x / 500" (cap จริง)
            SetBar(waterBar, data.water, displayWater, 0f, _critWater);
            SetBar(ironBar, data.iron, displayIron, 0f, _critIron);
            SetBar(energyBar, data.energy, displayEnergy, 0f, _critEnergy);
            SetBar(deuteriumBar, data.deuterium, displayDeuterium, 0f, 0f); // เชื้อเพลิงไม่มีเกณฑ์เตือน
            SetBar(tritiumBar, data.tritium, displayTritium, 0f, 0f);
        }

        // หลอดตันที่ displayRef (Slider clamp เอง) · capText > 0 → โชว์ "x / cap" ไม่งั้นตัวเลขล้วน
        private void SetBar(ResourceBarUI ui, float amount, float displayRef, float capText, float critical)
        {
            if (ui == null) return;

            if (ui.bar != null)
            {
                ui.bar.gameObject.SetActive(true); // เผื่อโดนซ่อนไว้จากดีไซน์ตัวเลขล้วนรอบก่อน
                ui.bar.maxValue = displayRef;
                ui.bar.value = amount;             // เกิน displayRef → หลอดเต็มค้าง ตัวเลขวิ่งต่อ
            }

            if (ui.valueText != null)
                ui.valueText.text = capText > 0f
                    ? $"{Mathf.RoundToInt(amount)} / {Mathf.RoundToInt(capText)}"
                    : $"{Mathf.RoundToInt(amount)}";

            if (ui.fillImage != null)
            {
                if (amount <= 0f)
                    ui.fillImage.color = depletedColor;
                else if (critical > 0f && amount < critical)
                    ui.fillImage.color = criticalColor;
                else
                    ui.fillImage.color = normalColor;
            }
        }

        private PopulationData _lastPop;
        private int _idleWorkers = -1; // -1 = ยังไม่รู้ (ไม่มี WorkerAssignmentManager) → ไม่โชว์

        private void HandlePopulationChanged(PopulationData data)
        {
            _lastPop = data;
            // ★ v6.3: HopeLedger (via WorkerManager) is the sole owner of Hope and drives the bar through
            //   OnMoraleChanged (HandleMoraleChanged below). The legacy PopulationData.hope defaults to 100
            //   and would latch the HUD at 100 — so only read it when the ledger isn't live (pre-cutover).
            if (WorkerManager.Instance == null)
                SetHopeDisplay(data.hope);

            RefreshPopulationText();
        }

        // v6.3 Hope source: WorkerManager broadcasts the ledger value here — at start (hope_start = 70) and
        // after every end-of-day commit (research +6, memorial +2, worker/food/water penalties, …).
        private void HandleMoraleChanged(float hope) => SetHopeDisplay(hope);

        private void SetHopeDisplay(float hope)
        {
            if (hopeBar != null)
            {
                hopeBar.maxValue = 100f;
                hopeBar.value = hope;
            }
            if (hopeText != null)
                hopeText.text = $"Hope: {Mathf.RoundToInt(hope)}";
        }

        // idle pool (V4 §5) — คนงานว่างที่ยังไม่ถูก assign อาคาร
        private void HandleWorkerPoolChanged(int idle, int total)
        {
            _idleWorkers = idle;
            RefreshPopulationText();
        }

        private void RefreshPopulationText()
        {
            // ★ v6.3: WorkerManager owns population now — PopulationData is the retired v4.1 record and
            //   nothing writes to it any more, so it latches at its start values (the HUD showed "10/80"
            //   forever: 10 = legacy start count, 80 = the old additive shelter formula). Same fix shape as
            //   Hope above: read the live system when it exists, fall back to the record when it doesn't.
            var wm = WorkerManager.Instance;
            int total = wm != null ? wm.AliveCount : _lastPop.total;
            int cap   = wm != null ? wm.ShelterCap : _lastPop.shelterCap;
            int crew  = wm != null ? total : _lastPop.workers; // v5.2 killed classes — everyone is a worker

            // legacy บรรทัดเดียว (เผื่อยัง wire อยู่บางซีน — ปกติ null หลังรัน Setup HUD ใหม่)
            if (populationText != null)
            {
                string idlePart = _idleWorkers >= 0 ? $"  ·  ว่าง {_idleWorkers}" : "";
                populationText.text = $"ประชากร {total}/{cap}  ·  W{crew} E{_lastPop.engineers} M{_lastPop.medics} F{_lastPop.farmers}{idlePart}";
            }

            // แถว icon ต่อคลาส (เรียงบนลงล่าง: รวม → worker → engineer → medic → farmer)
            if (popTotalText != null)  popTotalText.text  = $"{total}/{cap}";
            if (popWorkerText != null) popWorkerText.text = _idleWorkers >= 0
                                                              ? $"{crew}  ·  ว่าง {_idleWorkers}"
                                                              : $"{crew}";
            if (popEngineerText != null) popEngineerText.text = $"{_lastPop.engineers}";
            if (popMedicText != null)    popMedicText.text    = $"{_lastPop.medics}";
            if (popFarmerText != null)   popFarmerText.text   = $"{_lastPop.farmers}";
        }

        // Knowledge 0–100 + ป้าย tier (Novice/Aware/Skilled/Expert) — อ่าน tier จาก ResourceManager (config-style query)
        // OnKnowledgeChanged raise เฉพาะตอนมี delta; ค่าเริ่มต้น "Knowledge: 0 / 100 · Novice" ตั้งไว้โดย HUDCanvasSetup (Gap G8)
        private void HandleKnowledgeChanged(float knowledge)
        {
            if (knowledgeBar != null)
            {
                knowledgeBar.maxValue = 100f;
                knowledgeBar.value = knowledge;
            }

            if (knowledgeText != null)
            {
                string tier = ResourceManager.Instance != null
                    ? ResourceManager.Instance.Tier.ToString()
                    : "Novice";
                knowledgeText.text = $"Knowledge: {Mathf.RoundToInt(knowledge)} / 100 · {tier}";
            }
        }

        private void HandleTowerProgressChanged(TowerData data)
        {
            // แถบแสดง CORE% (0–100) โดยตรง
            if (towerProgressBar != null)
            {
                towerProgressBar.maxValue = 100f;
                towerProgressBar.value = data.corePercent;
            }

            if (towerPhaseText != null)
            {
                if (!data.isUnlocked)
                    towerPhaseText.text = "CORE TOWER — ล็อก (Day 11)";
                else
                {
                    int p = Mathf.Clamp(data.currentPhase, 0, PhaseNames.Length - 1);
                    towerPhaseText.text = $"CORE {data.corePercent:0}% · {PhaseNames[p]} · HEAT {data.coreHeat:0}/{data.heatCap:0}";
                }
            }
        }

        private void HandleGameOver(GameEndType endType)
        {
            if (gameOverPanel == null) return;

            gameOverPanel.SetActive(true);
            if (restartButton != null) restartButton.gameObject.SetActive(true);

            if (gameOverText == null) return;

            // การ์ดสรุปจบเกม — ข้อความ Story Guide §4 ENDINGS (💀/🌥 นอก BMP legacy Text วาดไม่ได้ — ไม่ใช้)
            string msg = endType switch
            {
                GameEndType.TrueEnding =>
                    "ภารกิจสำเร็จ — แสงแรกของโลกใหม่\n" +
                    "โล่พลาสมากางรับคลื่นรังสีไว้ทั้งหมด เสียงพายุเงียบลง\n" +
                    "ปฏิกิริยาฟิวชันถึงจุดเสถียร Q = 1.0 · Veltara ปลอดภัย\n" +
                    "ต้นไม้ต้นแรกผลิใบในดินที่เคยเป็นพิษ\n\n" +
                    "Kova: นายทำได้ วิศวกร ที่พวกเราทั้งทีมทำไม่สำเร็จ\n" +
                    "▸ ความคิด: Elara... ทุกคน ผมส่งรายงานไม่ทันในวันนั้น แต่คราวนี้ผมส่งมันถึงแล้ว\n" +
                    "พลังงานสะอาด ไม่ได้เกิดจากความสมบูรณ์แบบ แต่เกิดจากคนที่กล้ารับผิดชอบมัน",
                GameEndType.NormalEnding =>
                    "ภารกิจสำเร็จบางส่วน — เมืองที่ยังต้องซ่อม\n" +
                    "เตาติด แต่ไม่ถึงจุดเสถียรเต็มที่ โล่พลาสมากางได้ครึ่งเดียว\n" +
                    "พายุผ่านไป แต่เมืองบาดเจ็บ\n\n" +
                    "Kova: มันไม่เพอร์เฟกต์ แต่เราก็ยังอยู่\n" +
                    "▸ ความคิด: ยังไม่จบ ยังมีงานให้ทำอีกมาก แต่เมืองนี้ยังมีพรุ่งนี้",
                GameEndType.HopeZero     => "GAME OVER — ขวัญเมืองหมด\nHope = 0 · ประชาชนหมดศรัทธา เมืองล่มสลาย",
                GameEndType.Meltdown     => "GAME OVER — เตาหลอมละลาย\nHEAT ≥ 100 · เร่งเครื่องเกินกำลังหล่อเย็น",
                GameEndType.TimeoutLowQ  => "GAME OVER — หมดเวลา\nหมดเวลา 30 วัน แต่ CORE ยังไม่ถึง 100%",
                _ => ""
            };

            // สถิติย่อ (V4 §14 Defeat Summary): วัน · Q · Knowledge
            float q = CoreTowerManager.Instance != null ? CoreTowerManager.Instance.Q : 0f;
            int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 0;
            int knowledge = ResourceManager.Instance != null
                ? Mathf.RoundToInt(ResourceManager.Instance.Current.knowledge) : 0;

            // สรุปการเล่น (STORY.md §④) + Achievements — RunStats latches what the live systems forget
            string runSummary = "";
            string achievements = "";
            if (RunStats.Instance != null)
            {
                runSummary = "\nสรุปการเล่นของคุณ\n" + RunStats.Instance.BuildSummary();
                achievements = Achievements.BuildSummary(Achievements.Evaluate(RunStats.Instance, endType));
            }

            // Knowledge Summary (Story Guide §4 true_ending onEnd): สรุปหัวข้อความรู้/Codex ที่ปลดล็อกรอบนี้
            gameOverText.text = $"{msg}\n\nวันที่ {day} · Q {q:0.00} · Knowledge {knowledge}\n" +
                                runSummary + achievements + "\n" +
                                CollectKnowledgeSummary() +
                                "ความรู้ที่คุณได้ — ไม่มีวันหาย เริ่มใหม่แล้วไปให้ไกลกว่าเดิม";
        }

        // รวมชื่อหัวข้อ Codex ที่ปลดล็อกแล้ว เรียงตามลำดับนิยามใน allCodexEntries (คงที่)
        private static string CollectKnowledgeSummary()
        {
            var codex = CodexManager.Instance;
            if (codex == null || codex.allCodexEntries == null) return "";
            var titles = new List<string>();
            foreach (var e in codex.allCodexEntries)
                if (e != null && codex.IsUnlocked(e.entryId) && !string.IsNullOrEmpty(e.title))
                    titles.Add(e.title);
            return FormatKnowledgeSummary(titles);
        }

        /// <summary>จัดรูปสรุปคลังความรู้ (pure — ให้เทสต์ตรวจได้) · ว่าง = ยังไม่ปลดหัวข้อใด</summary>
        public static string FormatKnowledgeSummary(IReadOnlyList<string> unlockedTitles)
        {
            if (unlockedTitles == null || unlockedTitles.Count == 0) return "";
            return $"▸ คลังความรู้ที่ปลดล็อก ({unlockedTitles.Count}):\n{string.Join(" · ", unlockedTitles)}\n\n";
        }
    }
}
