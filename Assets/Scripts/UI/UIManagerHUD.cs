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
        private float[] _towerPhaseTargets;

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
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnResourceChanged -= HandleResourceChanged;
            EventManager.Instance.OnPopulationChanged -= HandlePopulationChanged;
            EventManager.Instance.OnTowerProgressChanged -= HandleTowerProgressChanged;
            EventManager.Instance.OnRiotStarted -= HandleRiotStarted;
            EventManager.Instance.OnGameOver -= HandleGameOver;
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

            _towerPhaseTargets = CoreTowerManager.Instance.phaseTargets;

            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (riotWarning != null) riotWarning.SetActive(false);
            if (strikeWarning != null) strikeWarning.SetActive(false);
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
            int phaseIndex = Mathf.Clamp(data.currentPhase, 0, _towerPhaseTargets.Length - 1);
            float target = _towerPhaseTargets[phaseIndex];

            if (towerProgressBar != null)
            {
                towerProgressBar.maxValue = target;
                towerProgressBar.value = data.phaseProgress;
            }

            if (towerPhaseText != null)
            {
                int displayPhase = Mathf.Min(data.currentPhase + 1, _towerPhaseTargets.Length);
                towerPhaseText.text = $"CORE TOWER — Phase {displayPhase}/{_towerPhaseTargets.Length}";
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
