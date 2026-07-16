using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ⚠ ARCHIVED (2026-07-17, v6.3 cutover) — ระบบ dilemma เก่า: ควิซบังคับหลัง resolve (ขัดกฎ 4),
    /// Hope +5 ฟรี + เขียน Hope ตรง (ขัดกฎ 5/8), ระบบ Aethon/Keran (ไม่มีใน v6.3), ForceIdle (วิกฤต 2·B ถูกลบ §13)
    /// แทนที่ด้วย CardManager (§25) · ถูกปิดตอนรันโดย LegacyNarrativeSilencer · เก็บ compile ไว้เพื่อ SaveData/tests
    ///
    /// ตรวจ trigger condition ของ DilemmaData (phase_n_complete, day-end conditions, hope)
    /// แสดง dilemma ผ่าน OnDilemmaTriggered แล้วรอ Popup UI raise OnDilemmaResolved
    /// เพื่อนำผลของ choice ไปปรับ Resource/Hope/ความสัมพันธ์ผ่าน event
    /// </summary>
    public class DilemmaManager : MonoBehaviour
    {
        public static DilemmaManager Instance { get; private set; }

        [Header("Dilemma Pool")]
        public DilemmaData[] dilemmaPool;

        [Header("Morale (V4 §9)")]
        public float resolveHopeBonus = 5f; // แก้วิกฤตสำเร็จ (เลือกทางใดก็ได้) → Hope +5

        public int AethonRelationship { get; private set; }
        public int KeranRelationship { get; private set; }

        private readonly HashSet<string> _triggeredIds = new HashSet<string>();
        private DilemmaData _activeDilemma;

        // วิกฤตที่ StoryDirector สั่ง (Story Guide): ควิซหลัง resolve ให้ StoryDirector จัดคิวเอง
        // (ลำดับบังคับ Outcome → Quiz) · ถ้ามีวิกฤตอื่นแสดงอยู่ → เก็บไว้เล่นต่อทันทีที่จบ
        private bool _activeIsStoryDriven;
        private DilemmaData _pendingStoryDilemma;

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
            EventManager.Instance.OnDilemmaTriggerRequested += HandleDilemmaTriggerRequested;
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
            EventManager.Instance.OnDilemmaTriggerRequested -= HandleDilemmaTriggerRequested;
        }

        // ── วิกฤตจาก StoryDirector (Story Guide) ──────────────────────────
        // เข้า pipeline เดียวกับวิกฤตปกติ (pause/resolve/effects) — ถ้ามีวิกฤตค้างอยู่ให้รอคิว
        private void HandleDilemmaTriggerRequested(DilemmaData dilemma)
        {
            if (dilemma == null) return;

            if (_activeDilemma != null)
            {
                _pendingStoryDilemma = dilemma; // เล่นต่อทันทีที่วิกฤตปัจจุบัน resolve (ท้าย HandleDilemmaResolved)
                return;
            }
            TriggerStoryDriven(dilemma);
        }

        private void TriggerStoryDriven(DilemmaData dilemma)
        {
            Trigger(dilemma);
            _activeIsStoryDriven = _activeDilemma == dilemma;
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

        // ตรรกะประเมินย้ายไป StatCondition (ใช้ร่วมกับ StoryDirector) — พฤติกรรมเดิมทุกอย่าง
        private bool MatchesDayEndCondition(string condition, int day)
            => StatCondition.Matches(condition, day, _resources, _tower);

        private static bool TryThreshold(string condition, string prefix, out float value)
            => StatCondition.TryThreshold(condition, prefix, out value);

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
            int deaths         = Pick(choiceIndex, dilemma.choiceA_Deaths, dilemma.choiceB_Deaths, dilemma.choiceC_Deaths);

            if (foodChange != 0f)   EventManager.Instance.RaiseResourceDelta(ResourceType.Food, foodChange);
            if (energyChange != 0f) EventManager.Instance.RaiseResourceDelta(ResourceType.Energy, energyChange);
            if (waterChange != 0f)  EventManager.Instance.RaiseResourceDelta(ResourceType.Water, waterChange);
            if (ironChange != 0f)   EventManager.Instance.RaiseResourceDelta(ResourceType.Iron, ironChange);

            // V4 §9: แก้วิกฤตสำเร็จ +5 (บวกเพิ่มจากผลเฉพาะของทางเลือก) — ตายให้ PopulationManager หัก −5/คนเอง
            float totalHope = hopeChange + resolveHopeBonus;
            if (totalHope != 0f) EventManager.Instance.RaiseMoraleDelta(totalHope);
            if (deaths > 0) EventManager.Instance.RaisePopulationDeaths(deaths);

            // วิกฤต 2·B (GDD ล่าสุด): บังคับเตาเดิน Idle N วันเพื่อผลิตไอโซโทปการแพทย์จากฟลักซ์นิวตรอน
            if (idleDays > 0)
                CoreTowerManager.Instance?.ForceIdle(idleDays);

            AethonRelationship += aethonChange;
            KeranRelationship += keranChange;
            EventManager.Instance.RaiseRelationshipChanged(AethonRelationship, KeranRelationship);

            TimeManager.Instance?.Resume(PauseReason.CrisisPopup); // ปิด crisis popup → นาฬิกาเดินต่อ

            // V4 §16 (T1.E1): หลัง resolve → เด้งควิซเข้าคิว
            // - ควิซรายทางเลือก (Story Guide quizRef): ทางเลือกที่กดมีรายการของตัวเอง → ใช้แทน linkedQuizIds
            // - วิกฤตจาก StoryDirector: ข้าม — StoryDirector ยิงเองหลังการ์ดบทสรุป (ลำดับ Outcome → Quiz)
            if (!_activeIsStoryDriven && QuizManager.Instance != null)
                QuizManager.Instance.TriggerByIds(dilemma.GetQuizIdsForChoice(choiceIndex));
            _activeIsStoryDriven = false;

            _activeDilemma = null;

            // มีวิกฤตเนื้อเรื่องรอคิวอยู่ → เล่นต่อทันที (StoryDirector รอ OnDilemmaTriggered ของตัวนั้น)
            if (_pendingStoryDilemma != null)
            {
                var next = _pendingStoryDilemma;
                _pendingStoryDilemma = null;
                TriggerStoryDriven(next);
            }
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
