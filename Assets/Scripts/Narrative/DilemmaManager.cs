using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ตรวจ trigger condition ของ DilemmaData (เช่น "phase_1_complete", "trust_below_40")
    /// แสดง dilemma ผ่าน OnDilemmaTriggered แล้วรอ Popup UI raise OnDilemmaResolved
    /// เพื่อนำผลของ choice ไปปรับ Resource/Trust/ความสัมพันธ์ผ่าน event
    /// </summary>
    public class DilemmaManager : MonoBehaviour
    {
        public static DilemmaManager Instance { get; private set; }

        [Header("Dilemma Pool")]
        public DilemmaData[] dilemmaPool;

        public int AethonRelationship { get; private set; }
        public int KeranRelationship { get; private set; }

        private readonly HashSet<string> _triggeredIds = new HashSet<string>();
        private DilemmaData _activeDilemma;

        // state ล่าสุดสำหรับประเมิน crisis ตอน OnDayEnded (อ่านผ่าน event ไม่ direct reference manager อื่น)
        private ResourceData _resources;
        private TowerData _tower;

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
            EventManager.Instance.OnTowerPhaseComplete += HandleTowerPhaseComplete;
            EventManager.Instance.OnTrustChanged += HandleTrustChanged;
            EventManager.Instance.OnDilemmaResolved += HandleDilemmaResolved;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
            EventManager.Instance.OnDayEnded += HandleDayEnded;
            EventManager.Instance.OnResourceChanged += HandleResourceChanged;
            EventManager.Instance.OnTowerProgressChanged += HandleTowerProgressChanged;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnTowerPhaseComplete -= HandleTowerPhaseComplete;
            EventManager.Instance.OnTrustChanged -= HandleTrustChanged;
            EventManager.Instance.OnDilemmaResolved -= HandleDilemmaResolved;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            EventManager.Instance.OnDayEnded -= HandleDayEnded;
            EventManager.Instance.OnResourceChanged -= HandleResourceChanged;
            EventManager.Instance.OnTowerProgressChanged -= HandleTowerProgressChanged;
        }

        private void HandleResourceChanged(ResourceData data) => _resources = data;
        private void HandleTowerProgressChanged(TowerData data) => _tower = data;

        // ── Crisis trigger: ประเมินทุกสิ้นวัน (C1) ─────────────────────────
        private void HandleDayEnded(int day)
        {
            if (_activeDilemma != null) return;

            foreach (var dilemma in dilemmaPool)
            {
                if (dilemma == null || _triggeredIds.Contains(dilemma.dilemmaId)) continue;
                if (MatchesDayEndCondition(dilemma.triggerCondition, day))
                {
                    Trigger(dilemma);
                    return; // ครั้งละ 1 crisis
                }
            }
        }

        private bool MatchesDayEndCondition(string condition, int day)
        {
            if (TryThreshold(condition, "heat_above_",   out float h)) return _tower.coreHeat   >= h;
            if (TryThreshold(condition, "food_below_",   out float fb)) return _resources.food   <= fb;
            if (TryThreshold(condition, "food_above_",   out float fa)) return _resources.food   >= fa;
            if (TryThreshold(condition, "energy_below_", out float e)) return _resources.energy  <= e;
            if (TryThreshold(condition, "water_below_",  out float w)) return _resources.water   <= w;
            if (TryThreshold(condition, "day_reached_",  out float d)) return day               >= d;
            return false;
        }

        private static bool TryThreshold(string condition, string prefix, out float value)
        {
            value = 0f;
            return !string.IsNullOrEmpty(condition)
                && condition.StartsWith(prefix)
                && float.TryParse(condition.Substring(prefix.Length), out value);
        }

        private void HandleTowerPhaseComplete(int phase)
        {
            TryTrigger($"phase_{phase}_complete");
        }

        private void HandleTrustChanged(float trust)
        {
            foreach (var dilemma in dilemmaPool)
            {
                if (dilemma == null || _triggeredIds.Contains(dilemma.dilemmaId)) continue;
                if (!dilemma.triggerCondition.StartsWith("trust_below_")) continue;

                string suffix = dilemma.triggerCondition.Substring("trust_below_".Length);
                if (int.TryParse(suffix, out int threshold) && trust < threshold)
                {
                    Trigger(dilemma);
                    return;
                }
            }
        }

        private void TryTrigger(string condition)
        {
            foreach (var dilemma in dilemmaPool)
            {
                if (dilemma == null || _triggeredIds.Contains(dilemma.dilemmaId)) continue;
                if (dilemma.triggerCondition != condition) continue;

                Trigger(dilemma);
                return;
            }
        }

        private void Trigger(DilemmaData dilemma)
        {
            if (_activeDilemma != null) return;

            _activeDilemma = dilemma;
            _triggeredIds.Add(dilemma.dilemmaId);
            EventManager.Instance.RaiseDilemmaTriggered(dilemma);
        }

        private void HandleDilemmaResolved(DilemmaData dilemma, bool choiceA)
        {
            if (dilemma != _activeDilemma) return;

            float foodChange = choiceA ? dilemma.choiceA_FoodChange : dilemma.choiceB_FoodChange;
            float energyChange = choiceA ? dilemma.choiceA_EnergyChange : dilemma.choiceB_EnergyChange;
            float waterChange = choiceA ? dilemma.choiceA_WaterChange : dilemma.choiceB_WaterChange;
            float trustChange = choiceA ? dilemma.choiceA_TrustChange : dilemma.choiceB_TrustChange;
            int aethonChange = choiceA ? dilemma.choiceA_AethonRelationChange : dilemma.choiceB_AethonRelationChange;
            int keranChange = choiceA ? dilemma.choiceA_KeranRelationChange : dilemma.choiceB_KeranRelationChange;

            if (foodChange != 0f)
                EventManager.Instance.RaiseResourceDelta(ResourceType.Food, foodChange);

            if (energyChange != 0f)
                EventManager.Instance.RaiseResourceDelta(ResourceType.Energy, energyChange);

            if (waterChange != 0f)
                EventManager.Instance.RaiseResourceDelta(ResourceType.Water, waterChange);

            if (trustChange != 0f)
                EventManager.Instance.RaiseTrustDelta(trustChange);

            AethonRelationship += aethonChange;
            KeranRelationship += keranChange;
            EventManager.Instance.RaiseRelationshipChanged(AethonRelationship, KeranRelationship);

            _activeDilemma = null;
        }

        private void HandleSaveLoaded(SaveData save)
        {
            AethonRelationship = save.aethonRelationship;
            KeranRelationship = save.keranRelationship;
        }
    }
}
