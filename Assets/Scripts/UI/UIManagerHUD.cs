using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// HUD หลัก: resource bars, CORE TOWER progress, trust meter
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
        public ResourceBarUI radiationProtectionBar;
        public ResourceBarUI energyBar;
        public ResourceBarUI workersBar;

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

        [Header("Population / Trust")]
        public Slider trustBar;
        public Text trustText;
        public Text populationText;
        public GameObject strikeWarning;

        [Header("Game Over / Victory")]
        public GameObject gameOverPanel;
        public Text gameOverText;
        public GameObject riotWarning;

        [Header("Bar Colors")]
        public Color normalColor = Color.green;
        public Color criticalColor = Color.yellow;
        public Color depletedColor = Color.red;

        private float _maxFood, _maxWater, _maxRadiationProtection, _maxEnergy, _maxWorkers, _criticalRatio;

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
            EventManager.Instance.OnRiotStarted += HandleRiotStarted;
            EventManager.Instance.OnGameOver += HandleGameOver;
            EventManager.Instance.OnDayStarted += HandleDayStarted;
            EventManager.Instance.OnSpeedChanged += HandleSpeedChanged;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnResourceChanged -= HandleResourceChanged;
            EventManager.Instance.OnPopulationChanged -= HandlePopulationChanged;
            EventManager.Instance.OnTowerProgressChanged -= HandleTowerProgressChanged;
            EventManager.Instance.OnRiotStarted -= HandleRiotStarted;
            EventManager.Instance.OnGameOver -= HandleGameOver;
            EventManager.Instance.OnDayStarted -= HandleDayStarted;
            EventManager.Instance.OnSpeedChanged -= HandleSpeedChanged;
        }

        private void Start()
        {
            // อ่าน config (max capacity / phase targets) จาก manager อื่นครั้งเดียวตอนเริ่ม เพื่อ normalize bar
            // เป็นการอ่านค่า config ที่ตั้งไว้ใน Inspector ของ manager นั้น ๆ ไม่ใช่การเรียก method ข้าม manager
            var rm = ResourceManager.Instance;
            _maxFood = rm.maxFood;
            _maxWater = rm.maxWater;
            _maxRadiationProtection = rm.maxRadiationProtection;
            _maxEnergy = rm.maxEnergy;
            _maxWorkers = rm.maxWorkers;
            _criticalRatio = rm.criticalRatio;

            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (riotWarning != null) riotWarning.SetActive(false);
            if (strikeWarning != null) strikeWarning.SetActive(false);

            // ปุ่มความเร็ว → raise request ให้ GameManager จัดการ (ไม่เรียก GameManager ตรง)
            if (pauseButton != null)  pauseButton.onClick.AddListener(() => EventManager.Instance.RaiseSpeedChangeRequested(0f));
            if (normalButton != null) normalButton.onClick.AddListener(() => EventManager.Instance.RaiseSpeedChangeRequested(1f));
            if (fastButton != null)   fastButton.onClick.AddListener(() => EventManager.Instance.RaiseSpeedChangeRequested(2f));
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

        private void HandleDayStarted(int day, bool timed)
        {
            if (dayText != null)
                dayText.text = $"DAY {day} / {GameManager.MaxDay}";

            // Day ที่ไม่จับเวลา (Day 1 tutorial) — ตั้งข้อความครั้งเดียว, Update จะไม่เขียนทับ
            if (timerText != null && !timed)
                timerText.text = "—";
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
            timerText.text = $"{m}:{s:00}";
        }

        private void HandleResourceChanged(ResourceData data)
        {
            SetBar(foodBar, data.food, _maxFood);
            SetBar(waterBar, data.water, _maxWater);
            SetBar(radiationProtectionBar, data.radiationProtection, _maxRadiationProtection);
            SetBar(energyBar, data.energy, _maxEnergy);
            SetBar(workersBar, data.workers, _maxWorkers);
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
            if (trustBar != null)
            {
                trustBar.maxValue = 100f;
                trustBar.value = data.trust;
            }

            if (trustText != null)
                trustText.text = $"Trust: {Mathf.RoundToInt(data.trust)}%";

            if (populationText != null)
                populationText.text = $"Population: {data.total}";

            if (strikeWarning != null)
                strikeWarning.SetActive(data.isOnStrike);
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

        private void HandleRiotStarted()
        {
            if (riotWarning != null)
                riotWarning.SetActive(true);
        }

        private void HandleGameOver(GameEndType endType)
        {
            if (gameOverPanel == null) return;

            gameOverPanel.SetActive(true);

            if (gameOverText == null) return;

            gameOverText.text = endType switch
            {
                GameEndType.Win => "CORE TOWER สำเร็จ! Veltara ได้รับพลังงานสะอาดอีกครั้ง",
                GameEndType.ResourceDepleted => "ทรัพยากรหมด — เมือง Veltara ล่มสลาย",
                GameEndType.TrustCollapsed => "ความเชื่อมั่นล่มสลาย — ประชาชนก่อกบฏ",
                _ => ""
            };
        }
    }
}
