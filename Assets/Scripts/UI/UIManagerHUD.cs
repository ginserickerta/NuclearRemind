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

        [Header("Resource Bars")]
        public ResourceBarUI foodBar;
        public ResourceBarUI waterBar;
        public ResourceBarUI ironBar;
        public ResourceBarUI energyBar;

        [Header("Day Cycle")]
        public Text dayText;    // "DAY 12 / 30"
        public Text timerText;  // นับถอยหลัง "1:30" (Day 1 = "—")

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
        public Text populationText;
        public Button trainEngineerButton; // ฝึก Worker → Engineer (V4 §5)
        public Button trainMedicButton;    // ฝึก Worker → Medic
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

        private float _maxFood, _maxWater, _maxIron, _maxEnergy, _criticalRatio;

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
            EventManager.Instance.OnTowerProgressChanged += HandleTowerProgressChanged;
            EventManager.Instance.OnGameOver += HandleGameOver;
            EventManager.Instance.OnDayStarted += HandleDayStarted;
            EventManager.Instance.OnDayPhaseChanged += HandleDayPhaseChanged;
            EventManager.Instance.OnSpeedChanged += HandleSpeedChanged;
            EventManager.Instance.OnKnowledgeChanged += HandleKnowledgeChanged;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnResourceChanged -= HandleResourceChanged;
            EventManager.Instance.OnPopulationChanged -= HandlePopulationChanged;
            EventManager.Instance.OnTowerProgressChanged -= HandleTowerProgressChanged;
            EventManager.Instance.OnGameOver -= HandleGameOver;
            EventManager.Instance.OnDayStarted -= HandleDayStarted;
            EventManager.Instance.OnDayPhaseChanged -= HandleDayPhaseChanged;
            EventManager.Instance.OnSpeedChanged -= HandleSpeedChanged;
            EventManager.Instance.OnKnowledgeChanged -= HandleKnowledgeChanged;
        }

        private void Start()
        {
            // อ่าน config (max capacity / phase targets) จาก manager อื่นครั้งเดียวตอนเริ่ม เพื่อ normalize bar
            // เป็นการอ่านค่า config ที่ตั้งไว้ใน Inspector ของ manager นั้น ๆ ไม่ใช่การเรียก method ข้าม manager
            var rm = ResourceManager.Instance;
            _maxFood = rm.maxFood;
            _maxWater = rm.maxWater;
            _maxIron = rm.maxIron;
            _maxEnergy = rm.maxEnergy;
            _criticalRatio = rm.criticalRatio;

            if (gameOverPanel != null) gameOverPanel.SetActive(false);

            // ปุ่มความเร็ว → raise request ให้ GameManager จัดการ (ไม่เรียก GameManager ตรง)
            if (pauseButton != null)  pauseButton.onClick.AddListener(() => EventManager.Instance.RaiseSpeedChangeRequested(0f));
            if (normalButton != null) normalButton.onClick.AddListener(() => EventManager.Instance.RaiseSpeedChangeRequested(1f));
            if (fastButton != null)   fastButton.onClick.AddListener(() => EventManager.Instance.RaiseSpeedChangeRequested(2f));

            // ปุ่มฝึกคลาส → raise request ให้ PopulationManager (V4 §5)
            if (trainEngineerButton != null) trainEngineerButton.onClick.AddListener(() => EventManager.Instance.RaiseTrainEngineerRequested());
            if (trainMedicButton != null)    trainMedicButton.onClick.AddListener(() => EventManager.Instance.RaiseTrainMedicRequested());

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
                dayText.text = $"DAY {day} / {GameManager.MaxDay}";

            // Day ที่ไม่จับเวลา (Day 1 tutorial) — ตั้งข้อความครั้งเดียว, Update จะไม่เขียนทับ
            if (timerText != null && !timed)
                timerText.text = "—";
        }

        // แสดงเฟสภายในวัน (V4 §3): Planning วางแผน → Live เดินเครื่อง
        private void HandleDayPhaseChanged(GameManager.DayPhase phase)
        {
            if (dayText == null) return;
            string label = phase == GameManager.DayPhase.Planning ? "วางแผน" : "เดินเครื่อง";
            dayText.text = $"DAY {_day} / {GameManager.MaxDay} · {label}";
        }

        // อ่าน DayTimeRemaining แบบ read-only query ต่อเฟรม (เทียบเท่าการอ่าน config/registry ของ manager อื่น)
        // ไม่ใช่การเรียก method เปลี่ยนสถานะข้าม manager
        private void Update()
        {
            if (timerText == null || GameManager.Instance == null) return;
            if (!GameManager.Instance.DayTimerActive) return; // คงข้อความล่าสุด (เช่น "—" ของ Day 1)

            float t = GameManager.Instance.DayTimeRemaining;
            int m = Mathf.FloorToInt(t / 60f);
            int s = Mathf.FloorToInt(t % 60f);

            // §15: มีเหตุหยุดนาฬิกา (วางอาคาร/ทุบ/ควิซ/วิกฤต) → บอกผู้เล่นชัด ๆ ว่าเวลาหยุด ไม่ใช่บั๊ก
            bool clockPaused = TimeManager.Instance != null && !TimeManager.Instance.IsRunning;
            timerText.text = clockPaused ? $"หยุด · {m}:{s:00}" : $"{m}:{s:00}";
        }

        private void HandleResourceChanged(ResourceData data)
        {
            SetBar(foodBar, data.food, _maxFood);
            SetBar(waterBar, data.water, _maxWater);
            SetBar(ironBar, data.iron, _maxIron);
            SetBar(energyBar, data.energy, _maxEnergy);
        }

        private void SetBar(ResourceBarUI ui, float amount, float max)
        {
            if (ui == null) return;

            if (ui.bar != null)
            {
                ui.bar.maxValue = max;
                ui.bar.value = amount;
            }

            if (ui.valueText != null)
                ui.valueText.text = $"{Mathf.RoundToInt(amount)} / {Mathf.RoundToInt(max)}";

            if (ui.fillImage != null)
            {
                if (amount <= 0f)
                    ui.fillImage.color = depletedColor;
                else if (amount < max * _criticalRatio)
                    ui.fillImage.color = criticalColor;
                else
                    ui.fillImage.color = normalColor;
            }
        }

        private void HandlePopulationChanged(PopulationData data)
        {
            if (hopeBar != null)
            {
                hopeBar.maxValue = 100f;
                hopeBar.value = data.hope;
            }
            if (hopeText != null)
                hopeText.text = $"Hope: {Mathf.RoundToInt(data.hope)}";

            if (populationText != null)
                populationText.text = $"ประชากร {data.total}/{data.shelterCap}  ·  W{data.workers} E{data.engineers} M{data.medics}";
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
                GameEndType.TimeoutLowQ  => "GAME OVER — หมดเวลา\nหมดเวลา 30 วัน แต่ค่า Q ยังไม่ถึงเป้า",
                _ => ""
            };

            // สถิติย่อ (V4 §14 Defeat Summary): วัน · Q · Knowledge
            float q = CoreTowerManager.Instance != null ? CoreTowerManager.Instance.Q : 0f;
            int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 0;
            int knowledge = ResourceManager.Instance != null
                ? Mathf.RoundToInt(ResourceManager.Instance.Current.knowledge) : 0;

            gameOverText.text = $"{msg}\n\nวันที่ {day} · Q {q:0.00} · Knowledge {knowledge}\n" +
                                "ความรู้ที่คุณได้ — ไม่มีวันหาย เริ่มใหม่แล้วไปให้ไกลกว่าเดิม";
        }
    }
}
