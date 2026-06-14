using System.Collections.Generic;
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
        private readonly Dictionary<Vector2Int, BuildingData> _placedBuildings = new Dictionary<Vector2Int, BuildingData>();

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
            EventManager.Instance.OnBuildingRemoved += HandleBuildingRemoved;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingPlaced -= HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved -= HandleBuildingRemoved;
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
            _placedBuildings[new Vector2Int(cell.col, cell.row)] = data;

            // materialCost ยังไม่มี ResourceType ของตัวเองใน ResourceData (ตาม Improve data model) — หักเฉพาะ energy/workers ที่มีอยู่
            var c = Current;
            c.energy = Mathf.Max(0f, c.energy - data.energyCost);
            c.workers = Mathf.Max(0, c.workers - data.workerRequired);
            Current = c;

            EventManager.Instance.RaiseResourceChanged(Current);
            CheckThresholds();
        }

        private void HandleBuildingRemoved(Vector2Int position)
        {
            _placedBuildings.Remove(position);
        }

        private void Tick()
        {
            var c = Current;

            foreach (var data in _placedBuildings.Values)
            {
                c.food += data.foodProduction;
                c.water += data.waterProduction;
                c.radiationProtection += data.radiationProtectionBonus;
                c.energy += data.energyProduction;
            }

            c.food = Mathf.Clamp(c.food, 0f, maxFood);
            c.water = Mathf.Clamp(c.water, 0f, maxWater);
            c.radiationProtection = Mathf.Clamp(c.radiationProtection, 0f, maxRadiationProtection);
            c.energy = Mathf.Clamp(c.energy, 0f, maxEnergy);
            c.workers = Mathf.Clamp(c.workers, 0, maxWorkers);

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
