using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ติดตามจำนวนประชากรและ Trust ของเมือง Veltara (PopulationData)
    /// ตรวจ demand ทุก 10 วินาที — Trust ลดเมื่อมีทรัพยากรขาด, Trust ต่ำกว่า threshold → worker strike
    /// </summary>
    public class PopulationManager : MonoBehaviour
    {
        public static PopulationManager Instance { get; private set; }

        [Header("Tick")]
        public float demandCheckInterval = 10f;

        [Header("Trust Tuning")]
        public float trustDecayPerDepletedResource = 2f;
        public float trustRecoveryPerCheck = 1f;
        public float strikeThreshold = 20f;

        public PopulationData Current { get; private set; } = new PopulationData
        {
            total = 50,
            trust = 70f,
            isOnStrike = false
        };

        private float _demandTimer;
        private readonly HashSet<ResourceType> _depletedResources = new HashSet<ResourceType>();

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
            EventManager.Instance.OnResourceDepleted += HandleResourceDepleted;
            EventManager.Instance.OnResourceChanged += HandleResourceChanged;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnResourceDepleted -= HandleResourceDepleted;
            EventManager.Instance.OnResourceChanged -= HandleResourceChanged;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
        }

        private void Start()
        {
            EventManager.Instance.RaiseTrustChanged(Current.trust);
            EventManager.Instance.RaisePopulationChanged(Current);
        }

        private void Update()
        {
            _demandTimer += Time.deltaTime;
            if (_demandTimer < demandCheckInterval)
                return;

            _demandTimer -= demandCheckInterval;
            EvaluateDemand();
        }

        private void HandleResourceDepleted(ResourceType type)
        {
            _depletedResources.Add(type);
        }

        private void HandleResourceChanged(ResourceData data)
        {
            if (data.food > 0f) _depletedResources.Remove(ResourceType.Food);
            if (data.water > 0f) _depletedResources.Remove(ResourceType.Water);
            if (data.radiationProtection > 0f) _depletedResources.Remove(ResourceType.RadiationProtection);
            if (data.energy > 0f) _depletedResources.Remove(ResourceType.Energy);
            if (data.workers > 0) _depletedResources.Remove(ResourceType.Workers);
        }

        private void EvaluateDemand()
        {
            var pop = Current;
            float previousTrust = pop.trust;

            pop.trust += _depletedResources.Count > 0
                ? -trustDecayPerDepletedResource * _depletedResources.Count
                : trustRecoveryPerCheck;
            pop.trust = Mathf.Clamp(pop.trust, 0f, 100f);

            bool wasOnStrike = pop.isOnStrike;
            pop.isOnStrike = pop.trust < strikeThreshold;

            Current = pop;

            if (!Mathf.Approximately(pop.trust, previousTrust))
                EventManager.Instance.RaiseTrustChanged(pop.trust);

            EventManager.Instance.RaisePopulationChanged(pop);

            if (pop.isOnStrike && !wasOnStrike)
                EventManager.Instance.RaiseWorkerStrike();

            if (pop.trust <= 0f)
                EventManager.Instance.RaiseRiotStarted();
        }

        private void HandleSaveLoaded(SaveData save)
        {
            Current = save.population;
            EventManager.Instance.RaiseTrustChanged(Current.trust);
            EventManager.Instance.RaisePopulationChanged(Current);
        }
    }
}
