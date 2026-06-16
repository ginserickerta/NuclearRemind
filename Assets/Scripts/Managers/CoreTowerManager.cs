using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// คุมความก้าวหน้าของ CORE TOWER 3 phase (Foundation / Reactor Core / Fusion Activation)
    /// ทุก tick ถ้ามีอาคารที่ isCoreTowerPart ตรงกับ phase ปัจจุบันอยู่ในเมือง จะเพิ่ม phaseProgress
    /// </summary>
    public class CoreTowerManager : MonoBehaviour
    {
        public static CoreTowerManager Instance { get; private set; }

        [Header("Tick")]
        public float tickInterval = 5f;

        [Header("Progress per tick (ค่าทดสอบ)")]
        public float progressPerTick = 10f;

        [Header("Phase Target (phaseProgress ที่ต้องถึงเพื่อจบ phase นั้น)")]
        public float[] phaseTargets = { 100f, 100f, 100f };

        [Header("Overdrive")]
        public float overdriveEnergyOutputBonus = 0.15f; // +15% energy output
        // TODO: SPEC_NEEDED — Overdrive energy consumption rate per tick
        // TODO: SPEC_NEEDED — Overdrive durability decay rate per tick

        public TowerData Current { get; private set; } = new TowerData
        {
            currentPhase = 0,
            phaseProgress = 0f,
            durability = 100f,
            isOverdriveActive = false
        };

        private float _tickTimer;

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
            EventManager.Instance.OnSaveLoaded      += HandleSaveLoaded;
            EventManager.Instance.OnOverdriveToggled += HandleOverdriveToggled;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnSaveLoaded      -= HandleSaveLoaded;
            EventManager.Instance.OnOverdriveToggled -= HandleOverdriveToggled;
        }

        private void Start()
        {
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

        private void Update()
        {
            if (Current.currentPhase >= phaseTargets.Length)
                return;

            _tickTimer += Time.deltaTime;
            if (_tickTimer < tickInterval)
                return;

            _tickTimer -= tickInterval;
            Tick();
        }

        private void Tick()
        {
            if (!HasActiveCoreTowerPart())
                return;

            var tower = Current;

            if (tower.isOverdriveActive)
            {
                // TODO: SPEC_NEEDED — หัก energy consumption ต่อ tick เมื่อ Overdrive
                // TODO: SPEC_NEEDED — ลด durability ต่อ tick เมื่อ Overdrive
                // EventManager.Instance.RaiseResourceDelta(ResourceType.Energy, -overdriveEnergyConsumptionPerTick);
                // tower.durability -= overdriveDurabilityDecayPerTick;
                // tower.durability = Mathf.Clamp(tower.durability, 0f, 100f);

                if (tower.durability <= 0f)
                {
                    tower.isOverdriveActive = false;
                    Current = tower;
                    EventManager.Instance.RaiseTowerProgressChanged(Current);
                    EventManager.Instance.RaiseGameOver(GameEndType.TowerDestroyed);
                    return;
                }

                EventManager.Instance.RaiseTowerDamaged(tower.durability);
            }

            tower.phaseProgress += progressPerTick;

            if (tower.phaseProgress >= phaseTargets[tower.currentPhase])
            {
                tower.phaseProgress = 0f;
                tower.currentPhase++;
                Current = tower;

                EventManager.Instance.RaiseTowerPhaseComplete(tower.currentPhase);

                if (tower.currentPhase >= phaseTargets.Length)
                    EventManager.Instance.RaiseTowerComplete();
            }
            else
            {
                Current = tower;
            }

            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

        private bool HasActiveCoreTowerPart()
        {
            int requiredPhase = Current.currentPhase + 1;

            foreach (var kvp in BuildingRegistry.Instance.PlacedBuildings)
            {
                // อาคารที่ยังสร้างไม่เสร็จยังไม่นับเป็น active CoreTower part
                if (ConstructionController.Instance != null &&
                    ConstructionController.Instance.IsUnderConstruction(kvp.Key))
                    continue;

                var data = kvp.Value;
                if (data.isCoreTowerPart && (data.towerPhaseRequired == 0 || data.towerPhaseRequired == requiredPhase))
                    return true;
            }

            return false;
        }

        private void HandleOverdriveToggled(bool active)
        {
            var tower = Current;
            tower.isOverdriveActive = active;
            Current = tower;
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

        private void HandleSaveLoaded(SaveData save)
        {
            Current = save.tower;
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }
    }
}
