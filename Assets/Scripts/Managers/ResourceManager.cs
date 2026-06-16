using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ติดตามทรัพยากรหลักของเมือง Veltara (ResourceData) — หักต้นทุนเมื่อวางอาคาร
    /// และบวก production ของอาคารที่วางแล้วทุก tick
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        [Header("Max Capacity")]
        public float maxFood = 500f;
        public float maxWater = 400f;
        public float maxRadiationProtection = 200f;
        public float maxEnergy = 300f;
        public int maxWorkers = 100;
        public float maxResearchPoints = 500f;

        [Header("Tick")]
        public float tickInterval = 5f;

        [Header("Critical Threshold (ratio of max)")]
        [Range(0f, 1f)] public float criticalRatio = 0.2f;

        public ResourceData Current { get; private set; } = new ResourceData
        {
            food = 100f,
            water = 80f,
            radiationProtection = 50f,
            energy = 30f,
            workers = 20
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
            EventManager.Instance.OnBuildingPlaced += HandleBuildingPlaced;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
            EventManager.Instance.OnResourceDelta += HandleResourceDelta;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingPlaced -= HandleBuildingPlaced;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            EventManager.Instance.OnResourceDelta -= HandleResourceDelta;
        }

        private void Start()
        {
            EventManager.Instance.RaiseResourceChanged(Current);
        }

        private void Update()
        {
            _tickTimer += Time.deltaTime;
            if (_tickTimer < tickInterval)
                return;

            _tickTimer -= tickInterval;
            Tick();
        }

        private void HandleBuildingPlaced(Cell cell, BuildingData data)
        {
            // materialCost ยังไม่มี ResourceType ของตัวเองใน ResourceData (ตาม Improve data model) — หักเฉพาะ energy/workers ที่มีอยู่
            var c = Current;
            c.energy = Mathf.Max(0f, c.energy - data.energyCost);
            c.workers = Mathf.Max(0, c.workers - data.workerRequired);
            Current = c;

            EventManager.Instance.RaiseResourceChanged(Current);
            CheckThresholds();
        }

        private void HandleSaveLoaded(SaveData save)
        {
            Current = save.resources;
            EventManager.Instance.RaiseResourceChanged(Current);
            CheckThresholds();
        }

        private void HandleResourceDelta(ResourceType type, float amount)
        {
            var c = Current;

            switch (type)
            {
                case ResourceType.Food:
                    c.food = Mathf.Clamp(c.food + amount, 0f, maxFood);
                    break;
                case ResourceType.Water:
                    c.water = Mathf.Clamp(c.water + amount, 0f, maxWater);
                    break;
                case ResourceType.RadiationProtection:
                    c.radiationProtection = Mathf.Clamp(c.radiationProtection + amount, 0f, maxRadiationProtection);
                    break;
                case ResourceType.Energy:
                    c.energy = Mathf.Clamp(c.energy + amount, 0f, maxEnergy);
                    break;
                case ResourceType.Workers:
                    c.workers = Mathf.Clamp(c.workers + Mathf.RoundToInt(amount), 0, maxWorkers);
                    break;
                case ResourceType.ResearchPoints:
                    c.researchPoints = Mathf.Clamp(c.researchPoints + amount, 0f, maxResearchPoints);
                    break;
            }

            Current = c;
            EventManager.Instance.RaiseResourceChanged(Current);
            CheckThresholds();
        }

        private void Tick()
        {
            var c = Current;

            foreach (var data in BuildingRegistry.Instance.PlacedBuildings.Values)
            {
                c.food += data.foodProduction;
                c.water += data.waterProduction;
                c.radiationProtection += data.radiationProtectionBonus;
                c.energy += data.energyProduction;
                c.researchPoints += data.researchPointsPerTick;
            }

            c.food = Mathf.Clamp(c.food, 0f, maxFood);
            c.water = Mathf.Clamp(c.water, 0f, maxWater);
            c.radiationProtection = Mathf.Clamp(c.radiationProtection, 0f, maxRadiationProtection);
            c.energy = Mathf.Clamp(c.energy, 0f, maxEnergy);
            c.workers = Mathf.Clamp(c.workers, 0, maxWorkers);
            c.researchPoints = Mathf.Clamp(c.researchPoints, 0f, maxResearchPoints);

            Current = c;

            EventManager.Instance.RaiseResourceChanged(Current);
            CheckThresholds();
        }

        private void CheckThresholds()
        {
            CheckResource(Current.food, maxFood, ResourceType.Food);
            CheckResource(Current.water, maxWater, ResourceType.Water);
            CheckResource(Current.radiationProtection, maxRadiationProtection, ResourceType.RadiationProtection);
            CheckResource(Current.energy, maxEnergy, ResourceType.Energy);
            CheckResource(Current.workers, maxWorkers, ResourceType.Workers);
        }

        private void CheckResource(float amount, float max, ResourceType type)
        {
            if (amount <= 0f)
                EventManager.Instance.RaiseResourceDepleted(type);
            else if (amount < max * criticalRatio)
                EventManager.Instance.RaiseResourceCritical(type);
        }
    }
}
