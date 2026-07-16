using UnityEngine;

namespace NuclearReMind
{
    /// <summary>City state snapshot for one soft-trigger evaluation — injected for headless tests.</summary>
    public struct SoftTriggerState
    {
        public float core;            // CORE% 0-100
        public float fuel;            // deuterium stock
        public float heat;            // reactor HEAT
        public float food;            // food stock
        public float avgRadiation;    // mean radiation over alive workers
        public float maxRadiation;    // AnyWorker(w => w.radiation > x) — pass the max
        public int hungryCount;      // WorkerStatus.Hungry
        public int exhaustedCount;   // WorkerStatus.Exhausted
    }

    /// <summary>
    /// Soft triggers (GDD §19) — unlock research leads from STATE, never from the calendar
    /// (rule #1: no if(day == X)). Thresholds slide with core%: the further the reactor is,
    /// the earlier the game hints — เล่นเก่ง = ได้ Lead เร็ว.
    ///
    /// Pure class; ResearchLab evaluates it once per day. Leads unlock once per game
    /// (KnowledgeDB.UnlockLead already enforces that), so re-evaluating daily is safe.
    /// </summary>
    public class SoftTriggerWatcher
    {
        private readonly GameConfigSO _cfg;

        public SoftTriggerWatcher(GameConfigSO cfg) => _cfg = cfg;

        /// <summary>Evaluate all 7 soft triggers (CONFIG.md 🔒 SOFT TRIGGER — verbatim §19 logic).</summary>
        public void Evaluate(SoftTriggerState s, KnowledgeDB db)
        {
            float p = s.core / 100f;

            // เตาไม่มีเชื้อเพลิง → รู้ว่าต้องหาเชื้อเพลิง (K01 bark คู่กันใน Sprint 5)
            if (s.core < 100f && s.fuel <= 0f)
                db.UnlockLead("water_analysis");

            // ความร้อนไต่ — เกณฑ์ 35 → 25 ตาม core%
            if (s.heat > _cfg.softHeatBase - p * _cfg.softHeatSlope)
                db.UnlockLead("magnetic_theory");

            // มีใครสักคนรังสีสะสมสูง — เกณฑ์ 25 → 19
            if (s.maxRadiation > _cfg.softRadBase - p * _cfg.softRadSlope)
                db.UnlockLead("radiation_biology");

            // เสบียงกอง + รังสีพื้นหลังสูง → ของเน่า
            if (s.food > _cfg.softFoodBase - p * _cfg.softFoodSlope &&
                s.avgRadiation > _cfg.softRad2Base - p * _cfg.softRad2Slope)
                db.UnlockLead("food_preservation");

            // CORE 45 → เริ่มมองเชื้อเพลิงระยะสอง (ประตูชนะ)
            if (s.core >= _cfg.softLithiumCore)
                db.UnlockLead("lithium_breeding");

            // ★ S7/S8 — วิกฤตคน (บั๊ก #12/#13: ค่า 3 มาจาก sim)
            if (s.hungryCount >= _cfg.softHungryCount)
                db.UnlockLead("food_logistics");
            if (s.exhaustedCount >= _cfg.softExhaustedCount)
                db.UnlockLead("shift_management");

            // storm_detection ไม่อยู่ในนี้ — ปลดจาก Record #3 เท่านั้น (STORY.md, Sprint 5)
        }

        /// <summary>Build today's state from live systems (play mode). Reactor fields stay 0 until Sprint 6.</summary>
        public static SoftTriggerState Snapshot()
        {
            var s = new SoftTriggerState();

            var rm = ResourceManager.Instance;
            if (rm != null)
            {
                s.fuel = rm.Current.deuterium;
                s.food = rm.Current.food;
            }

            var ct = CoreTowerManager.Instance;
            if (ct != null)
            {
                s.core = ct.Current.corePercent;
                s.heat = ct.Current.coreHeat;
            }

            var wm = WorkerManager.Instance;
            if (wm != null)
            {
                float sum = 0f, max = 0f;
                int alive = 0;
                foreach (var w in wm.Workers)
                {
                    if (!w.alive) continue;
                    alive++;
                    sum += w.radiation;
                    if (w.radiation > max) max = w.radiation;
                }
                s.avgRadiation = alive > 0 ? sum / alive : 0f;
                s.maxRadiation = max;
                s.hungryCount = wm.HungryCount;
                s.exhaustedCount = wm.ExhaustedCount;
            }

            return s;
        }
    }
}
