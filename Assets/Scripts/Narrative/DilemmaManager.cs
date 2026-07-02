using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ตรวจ trigger condition ของ DilemmaData (phase_n_complete, day-end conditions, hope)
    /// แสดง dilemma ผ่าน OnDilemmaTriggered แล้วรอ Popup UI raise OnDilemmaResolved
    /// เพื่อนำผลของ choice ไปปรับ Resource/Hope/ความสัมพันธ์ผ่าน event
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
            EventManager.Instance.OnMoraleChanged += HandleMoraleChanged;
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
            EventManager.Instance.OnMoraleChanged -= HandleMoraleChanged;
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

        // trigger ตามขวัญกำลังใจ: "hope_below_30"
        private void HandleMoraleChanged(float hope)
        {
            foreach (var dilemma in dilemmaPool)
            {
                if (dilemma == null || _triggeredIds.Contains(dilemma.dilemmaId)) continue;

                if (TryThreshold(dilemma.triggerCondition, "hope_below_", out float hb) && hope < hb)
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
            TimeManager.Instance?.Pause(PauseReason.CrisisPopup); // หยุดนาฬิกาวันระหว่างตัดสินใจ (V4 §3)
            EventManager.Instance.RaiseDilemmaTriggered(dilemma);
        }

        // choiceIndex: 0 = A, 1 = B, 2 = C (V4 §10)
        private void HandleDilemmaResolved(DilemmaData dilemma, int choiceIndex)
        {
            if (dilemma != _activeDilemma) return;

            float foodChange   = Pick(choiceIndex, dilemma.choiceA_FoodChange,   dilemma.choiceB_FoodChange,   dilemma.choiceC_FoodChange);
            float energyChange = Pick(choiceIndex, dilemma.choiceA_EnergyChange, dilemma.choiceB_EnergyChange, dilemma.choiceC_EnergyChange);
            float waterChange  = Pick(choiceIndex, dilemma.choiceA_WaterChange,  dilemma.choiceB_WaterChange,  dilemma.choiceC_WaterChange);
            float ironChange   = Pick(choiceIndex, dilemma.choiceA_IronChange,   dilemma.choiceB_IronChange,   dilemma.choiceC_IronChange);
            float hopeChange   = Pick(choiceIndex, dilemma.choiceA_HopeChange,   dilemma.choiceB_HopeChange,   dilemma.choiceC_HopeChange);
            int aethonChange   = Pick(choiceIndex, dilemma.choiceA_AethonRelationChange, dilemma.choiceB_AethonRelationChange, dilemma.choiceC_AethonRelationChange);
            int keranChange    = Pick(choiceIndex, dilemma.choiceA_KeranRelationChange,  dilemma.choiceB_KeranRelationChange,  dilemma.choiceC_KeranRelationChange);
            int idleDays       = Pick(choiceIndex, dilemma.choiceA_ForceReactorIdleDays, dilemma.choiceB_ForceReactorIdleDays, dilemma.choiceC_ForceReactorIdleDays);

            if (foodChange != 0f)   EventManager.Instance.RaiseResourceDelta(ResourceType.Food, foodChange);
            if (energyChange != 0f) EventManager.Instance.RaiseResourceDelta(ResourceType.Energy, energyChange);
            if (waterChange != 0f)  EventManager.Instance.RaiseResourceDelta(ResourceType.Water, waterChange);
            if (ironChange != 0f)   EventManager.Instance.RaiseResourceDelta(ResourceType.Iron, ironChange);
            if (hopeChange != 0f)   EventManager.Instance.RaiseMoraleDelta(hopeChange);

            // วิกฤต 2·B (GDD ล่าสุด): บังคับเตาเดิน Idle N วันเพื่อผลิตไอโซโทปการแพทย์จากฟลักซ์นิวตรอน
            if (idleDays > 0)
                CoreTowerManager.Instance?.ForceIdle(idleDays);

            AethonRelationship += aethonChange;
            KeranRelationship += keranChange;
            EventManager.Instance.RaiseRelationshipChanged(AethonRelationship, KeranRelationship);

            TimeManager.Instance?.Resume(PauseReason.CrisisPopup); // ปิด crisis popup → นาฬิกาเดินต่อ

            // V4 §16 (T1.E1): หลัง resolve → เด้งควิซที่ผูกกับ dilemma นี้เข้าคิว (dilemma implement IQuizTrigger)
            // (ถ้ามีควิซผูกอยู่ QuizManager จะ Pause(QuizPopup) ต่อทันที)
            if (QuizManager.Instance != null)
                QuizManager.Instance.EnqueueQuizzes(dilemma);

            _activeDilemma = null;
        }

        private static float Pick(int i, float a, float b, float c) => i == 0 ? a : (i == 1 ? b : c);
        private static int Pick(int i, int a, int b, int c) => i == 0 ? a : (i == 1 ? b : c);

        private void HandleSaveLoaded(SaveData save)
        {
            AethonRelationship = save.aethonRelationship;
            KeranRelationship = save.keranRelationship;
        }
    }
}
