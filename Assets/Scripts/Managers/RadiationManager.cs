using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ความเสี่ยงรังสีสะสมของคนงาน (Story Guide §4 — วิกฤตโรครังสี trigger เดิม "ZoneA_workers>threshold")
    ///
    /// เกมนี้ไม่มีระบบวางคนงานบนแมป (PopulationData เก็บแค่จำนวนรวม) จึงจำลอง "Zone A" ด้วย
    /// ค่า exposure สะสมทั้งเมือง แทนการนับคนงานในโซนบนกริด:
    ///   ทุกสิ้นวัน (เฉพาะหลังเตาเดินเครื่อง = ยุคนิวเคลียร์ Phase 3) →
    ///     exposure += max(0, แหล่งรังสี − การป้องกัน)
    ///   แหล่ง    = Mine × ต่อเหมือง + เตา + (HEAT สูง)   ← ขุดแร่/เตาปล่อยรังสี
    ///   ป้องกัน  = RadiationShelter × ค่าลด + Medic × ค่าลด ← หลัก ALARA (ผู้เล่นเลือกลดความเสี่ยง)
    ///
    /// เมื่อ exposure ข้าม threshold → beat crisis_radiation_disease ยิง
    /// (StatCondition "exposure_above_60") · fallback "day_reached_23" บน beat กันพลาดเนื้อหา
    ///
    /// ★ ผล: วิกฤตกลายเป็น "ผลจากการเลือก" (ขุดหนัก/ไม่ป้องกัน = มาเร็ว · ทำตาม ALARA = ช้า/เลี่ยง)
    ///   ตรงเจตนา guide มากกว่าวันตายตัว และตอกย้ำความรู้ ALARA ที่วิกฤตนั้นสอน
    /// ค่า tuning ตั้งต้น — ปรับด้วย playtest (สูตรแยกเป็น ComputeDailyExposure ให้เทสต์ตรวจได้)
    /// </summary>
    public class RadiationManager : MonoBehaviour
    {
        public static RadiationManager Instance { get; private set; }

        [Header("แหล่งรังสี — สะสม/วัน (เฉพาะเมื่อเตาเดินเครื่องแล้ว)")]
        public float exposurePerMinePerDay = 3f;   // เหมืองแร่แต่ละแห่ง (คนงานขุดแร่รับรังสี)
        public float exposurePerReactorDay = 2f;   // เตาฟิวชันเดินเครื่อง (ฟลักซ์นิวตรอน)
        public float heatExposureThreshold = 80f;  // HEAT ≥ ค่านี้ = รั่วมากขึ้น
        public float heatExposureBonus = 3f;

        [Header("การป้องกัน — ลด/วัน (หลัก ALARA)")]
        public float shelterMitigationPerDay = 6f; // ที่หลบภัยรังสีแต่ละหลัง
        public float medicMitigationPerDay = 2f;   // แพทย์แต่ละคน (เฝ้าระวัง/กันรังสีให้กลุ่มเสี่ยง)

        /// <summary>ค่าเสี่ยงรังสีสะสมปัจจุบัน (StatCondition "exposure_above_X" เทียบกับค่านี้)</summary>
        public float CurrentExposure { get; private set; }

        // snapshot อ่านผ่าน event (ไม่ direct reference manager อื่น — ล้อ StoryDirector/DilemmaManager)
        private TowerData _tower;
        private PopulationData _population;

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
            EventManager.Instance.OnDayEnded += HandleDayEnded;
            EventManager.Instance.OnTowerProgressChanged += HandleTowerProgressChanged;
            EventManager.Instance.OnPopulationChanged += HandlePopulationChanged;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayEnded -= HandleDayEnded;
            EventManager.Instance.OnTowerProgressChanged -= HandleTowerProgressChanged;
            EventManager.Instance.OnPopulationChanged -= HandlePopulationChanged;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
        }

        private void HandleTowerProgressChanged(TowerData data) => _tower = data;
        private void HandlePopulationChanged(PopulationData data) => _population = data;

        // สะสมความเสี่ยงรังสีทุกสิ้นวัน — เฉพาะเมื่อเตาเดินเครื่องแล้ว (ก่อนจุดเตา = ยังไม่มีแหล่งรังสีนัยสำคัญ)
        private void HandleDayEnded(int day)
        {
            if (!_tower.isUnlocked) return;

            int mines = 0, shelters = 0;
            var registry = BuildingRegistry.Instance;
            if (registry != null)
            {
                foreach (var kvp in registry.PlacedBuildings)
                {
                    if (kvp.Value == null) continue;
                    if (kvp.Value.buildingType == BuildingType.Mine) mines++;
                    else if (kvp.Value.buildingType == BuildingType.RadiationShelter) shelters++;
                }
            }

            float daily = ComputeDailyExposure(mines, shelters, _population.medics, _tower.coreHeat);
            if (daily <= 0f) return;

            CurrentExposure += daily;
            EventManager.Instance.RaiseRadiationExposureChanged(CurrentExposure);
        }

        /// <summary>
        /// เพิ่มค่าเสี่ยงรังสีสะสมจากทางเลือกวิกฤต (Story Guide §4 Plasma B — "วิศวกร 2 คนได้รับรังสีเกิน")
        /// ทำให้ radSickRisk มีผลจริง: ดัน exposure เข้าใกล้ threshold ของ beat โรครังสีได้ (CrisisEffectManager สั่ง)
        /// </summary>
        public void AddExposure(float amount)
        {
            if (amount <= 0f) return;
            CurrentExposure += amount;
            EventManager.Instance.RaiseRadiationExposureChanged(CurrentExposure);
        }

        /// <summary>
        /// รังสีสะสมของวันนี้ = max(0, แหล่ง − ป้องกัน) — pure ให้เทสต์ตรวจ balance ได้โดยไม่ต้องมี scene/event
        /// </summary>
        public float ComputeDailyExposure(int mines, int shelters, int medics, float coreHeat)
        {
            float sources = mines * exposurePerMinePerDay + exposurePerReactorDay
                          + (coreHeat >= heatExposureThreshold ? heatExposureBonus : 0f);
            float mitigation = shelters * shelterMitigationPerDay + medics * medicMitigationPerDay;
            return Mathf.Max(0f, sources - mitigation);
        }

        private void HandleSaveLoaded(SaveData save)
        {
            CurrentExposure = save.radiationExposure;
            _tower = save.tower;
            _population = save.population;
            EventManager.Instance.RaiseRadiationExposureChanged(CurrentExposure);
        }
    }
}
