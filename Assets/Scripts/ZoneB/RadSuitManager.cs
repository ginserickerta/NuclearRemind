using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: ชุดกันรังสี — คราฟต์จากวัสดุแล็บ ปลดล็อกเมื่อวิจัย nuclear_medicine
    /// ใส่แล้วลดรังสีที่คนงานรับเหลือ ×0.4 — เปลี่ยนโซน B จาก "ตายใน ~3 วัน" เป็นหมุนเวียนคนได้จริง
    /// ชุดเป็น "กองกลาง": ทุกเช้าคนงานโซน B ตามจำนวนชุดที่มีจะได้ใส่ก่อนโดนรังสีตอนจบวัน
    ///
    /// Rad Suits (GDD §22 / CONFIG.md 🔒 RADIATION). Crafted from labMat, unlocked by the
    /// nuclear_medicine research (unlocksCommands craft_radsuit). A suit cuts a worker's dose ×0.4
    /// (WorkerManager applies radSuitMult when Worker.hasRadSuit), which is what turns Zone B from a
    /// ~3-day death sentence into a survivable rotation — the difference between the Death Spiral
    /// running away and being managed.
    ///
    /// Suits are a pool: on each day-start the first SuitsMade Zone B workers wear one, so the dose
    /// reduction is in place BEFORE WorkerManager applies radiation at day-end.
    /// </summary>
    public class RadSuitManager : MonoBehaviour
    {
        public static RadSuitManager Instance { get; private set; }

        private GameConfigSO _cfg;
        public int SuitsMade { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Initialize(GameConfigSO.Instance);
        }

        /// <summary>Bootstrap — also the EditMode-test entry point.</summary>
        public void Initialize(GameConfigSO cfg)
        {
            _cfg = cfg;
            SuitsMade = 0;
        }

        private void OnEnable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayStarted += HandleDayStarted;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayStarted -= HandleDayStarted;
        }

        // Equip at day-start so hasRadSuit is set before WorkerManager applies radiation at day-end.
        private void HandleDayStarted(int day, bool timed) => EquipZoneBWorkers();

        /// <summary>Whether crafting is allowed yet (nuclear_medicine gates the recipe).</summary>
        public bool CanCraft => KnowledgeDB.Instance.HasNote("nuclear_medicine") && SuitsMade < _cfg.suitTargetCount;

        // [TH] คราฟต์ชุด 1 ตัว: จ่ายวัสดุแล็บ — ล้มเหลวถ้ายังไม่วิจัย / ครบโควตา / ของไม่พอ (ไม่มีของฟรี)
        /// <summary>
        /// Craft one suit: pay suit_cost labMat. Returns false if not researched, at the target count,
        /// or labMat can't cover it. (public for the F9 panel / tests.)
        /// </summary>
        public bool CraftSuit()
        {
            if (!KnowledgeDB.Instance.HasNote("nuclear_medicine")) return false;
            if (SuitsMade >= _cfg.suitTargetCount) return false;

            var rm = ResourceManager.Instance;
            if (rm != null && rm.Current.labMat < _cfg.suitCostLabMat) return false;

            EventManager.Instance?.RaiseResourceDelta(ResourceType.LabMat, -_cfg.suitCostLabMat);
            SuitsMade++;
            EquipZoneBWorkers();
            return true;
        }

        // [TH] แจกชุดให้คนงานโซน B ตามจำนวนชุดที่มี — คนนอกโซนไม่ได้ใส่ (ชุดใช้เฉพาะในโซน)
        /// <summary>First SuitsMade Zone B workers wear a suit; everyone else has none (public for tests).</summary>
        public void EquipZoneBWorkers()
        {
            var wm = WorkerManager.Instance;
            if (wm == null) return;
            int worn = 0;
            foreach (var w in wm.Workers)
            {
                if (!w.alive) continue;
                if (w.job == WorkerJobs.ZoneB)
                    w.hasRadSuit = worn++ < SuitsMade;
                else
                    w.hasRadSuit = false; // suits are worn in the zone, not carried around
            }
        }
    }
}
