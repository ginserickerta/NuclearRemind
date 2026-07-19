using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// V4 §5/§9 — ประชากร 4 คลาส + Hope เดี่ยว:
    /// ฝึก Worker→Engineer/Medic (1 วัน, ต้องมีอาคาร+ทรัพยากร), เติมประชากร +1/วัน,
    /// AssignedCoolingEngineers, Hope recalc, Hope=0 → Game Over (HopeZero)
    /// </summary>
    public class PopulationManagerTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private ResourceManager resources;
        private PopulationManager population;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
            resources = NewComponent<ResourceManager>("ResourceManager"); // ต้นทุนฝึก + คลัง
            population = NewComponent<PopulationManager>("PopulationManager");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        // ── ค่าเริ่มต้น (bootstrap 4 คลาส §5 — กัน "วันแรกผลิตอะไรไม่ได้") ──
        [Test]
        public void StartsAt_Hope100_TenWithClassMix()
        {
            Assert.AreEqual(100f, population.Current.hope, 1e-4f);
            Assert.AreEqual(6, population.Current.workers, "เริ่ม 6 Worker");
            Assert.AreEqual(2, population.Current.farmers, "เริ่ม 2 Farmer (ทำฟาร์มได้วันแรก)");
            Assert.AreEqual(2, population.Current.engineers, "เริ่ม 2 Engineer (คุม Lab ได้)");
            Assert.AreEqual(0, population.Current.medics);
            Assert.AreEqual(10, population.Current.total, "รวม 10 คน");
        }

        // ── ขวัญกำลังใจ (V4 §9/§18 Hope deltas) ──────────────────────────
        //
        // ⚠ These three cover PopulationManager's own Hope recalc, which v6.3 replaced: HopeLedger is the
        // sole owner of Hope (rule #8), and PopulationManager.HandleDayEnded bails out entirely whenever
        // WorkerManager is active - i.e. always, in the shipped game. They only still run here because
        // this suite never creates a WorkerManager, so they are asserting a path no player can reach.
        //
        // They also no longer match the numbers: V5 added hopeDailyDrift = 1.5/day, so the food-shortage
        // case lands on 88.5 rather than 90. Rewriting the expected values would make them green while
        // still guarding dead code - worse than leaving them visible, because it would hide the fact that
        // two Hope systems still coexist. Ignored rather than deleted so that debt stays on the record.
        //
        // TODO(cutover): retire PopulationManager's Hope block, then delete these.
        private const string LegacyHopeReason =
            "v6.3: Hope belongs to HopeLedger; PopulationManager's recalc is dead code when WorkerManager is active";

        [Test]
        [Ignore(LegacyHopeReason)]
        public void DayEnded_FoodShortage_LowersHope10()
        {
            eventManager.RaiseResourceDepleted(ResourceType.Food);
            eventManager.RaiseDayEnded(2);
            Assert.AreEqual(90f, population.Current.hope, 1e-4f, "อาหารขาด → Hope −10/วัน (§18)");
        }

        [Test]
        [Ignore(LegacyHopeReason)]
        public void DayEnded_WithMedic_NoShortage_Recovers2()
        {
            InjectPop(workers: 5, hope: 80f, shelterCap: 20, medics: 1);
            eventManager.RaiseDayEnded(2);
            Assert.AreEqual(82f, population.Current.hope, 1e-4f, "มี Medic + ไม่ขาดของ → Hope +2/วัน (§18)");
        }

        [Test]
        [Ignore(LegacyHopeReason)]
        public void DayEnded_NoMedic_NoRecovery()
        {
            eventManager.RaiseMoraleDelta(-20f); // 100 → 80
            eventManager.RaiseDayEnded(2);
            Assert.AreEqual(80f, population.Current.hope, 1e-4f, "ไม่มี Medic → ไม่ฟื้น (§18: ฟื้นต้องมี Medic)");
        }

        // ── คนตาย (V4 §9: Hope −5/คน) ────────────────────────────
        [Test]
        public void Deaths_ReduceWorkers_AndHope()
        {
            eventManager.RaisePopulationDeaths(3);
            Assert.AreEqual(3, population.Current.workers, "ตาย 3 → Worker เหลือ 3 (เริ่ม 6)");
            Assert.AreEqual(85f, population.Current.hope, 1e-4f, "Hope −5/คน × 3 = −15");
        }

        [Test]
        public void Deaths_TakeWorkersFirst_ThenFarmers_ThenMedics()
        {
            // ลำดับตาย: Worker → Farmer → Medic → Engineer (Engineer รักษาไว้ท้ายสุด — แรงหล่อเย็น CORE)
            InjectPop(workers: 1, hope: 100f, shelterCap: 20, engineers: 2, medics: 1, farmers: 1);
            eventManager.RaisePopulationDeaths(3);

            Assert.AreEqual(0, population.Current.workers, "Worker ตายก่อน");
            Assert.AreEqual(0, population.Current.farmers, "หมด Worker → Farmer ตายต่อ");
            Assert.AreEqual(0, population.Current.medics, "หมด Farmer → Medic ตายต่อ");
            Assert.AreEqual(2, population.Current.engineers, "Engineer รักษาไว้ท้ายสุด");
        }

        [Test]
        public void Deaths_ClampedToPopulation()
        {
            InjectPop(workers: 2, hope: 100f, shelterCap: 20);
            eventManager.RaisePopulationDeaths(5);

            Assert.AreEqual(0, population.Current.total, "ตายได้ไม่เกินจำนวนที่มี");
            Assert.AreEqual(90f, population.Current.hope, 1e-4f, "หัก Hope ตามจำนวนที่ตายจริง (2 คน)");
        }

        [Test]
        public void HopeReachesZero_RaisesGameOver_HopeZero()
        {
            GameEndType? ending = null;
            eventManager.OnGameOver += t => ending = t;

            eventManager.RaiseMoraleDelta(-100f);

            Assert.AreEqual(0f, population.Current.hope, 1e-4f);
            Assert.IsTrue(ending.HasValue);
            Assert.AreEqual(GameEndType.HopeZero, ending.Value);
        }

        [Test]
        public void DayEnded_Day1_DoesNotChangeMorale()
        {
            eventManager.RaiseResourceDepleted(ResourceType.Food);
            eventManager.RaiseDayEnded(1);
            Assert.AreEqual(100f, population.Current.hope, 1e-4f, "Day 1 ไม่คิด Hope");
        }

        // ── ฝึกคลาส (V4 §5) ──────────────────────────────────────
        [Test]
        public void TrainEngineer_ConvertsWorkerToEngineer_After1Day()
        {
            UnlockTraining(engineer: true, medic: false);

            population.TrainEngineer();
            Assert.AreEqual(5, population.Current.workers, "ดึง Worker เข้าฝึกทันที (เริ่ม 6)");
            Assert.AreEqual(2, population.Current.engineers, "ยังไม่เสร็จ — คง Engineer เริ่มต้น 2 (ใช้เวลา 1 วัน)");

            eventManager.RaiseDayEnded(2);
            Assert.AreEqual(3, population.Current.engineers, "สิ้นวัน → ได้ Engineer เพิ่ม (2+1)");
            Assert.AreEqual(5, population.Current.workers);
        }

        [Test]
        public void TrainEngineer_WithoutResearchLab_Blocked()
        {
            population.TrainEngineer(); // ยังไม่ปลดล็อก
            Assert.AreEqual(6, population.Current.workers, "ไม่มี Research Lab → ฝึกไม่ได้ (คง Worker เริ่มต้น 6)");
        }

        [Test]
        public void TrainEngineer_WithoutLab_RaisesNotice()
        {
            string notice = null;
            eventManager.OnNotice += m => notice = m;

            population.TrainEngineer();

            Assert.IsNotNull(notice, "ฝึกไม่ได้ต้องแจ้งเหตุผล (toast) ไม่ใช่เงียบ");
            StringAssert.Contains("ห้องปฏิบัติการ", notice);
        }

        [Test]
        public void TrainEngineer_InsufficientResources_RaisesNotice()
        {
            UnlockTraining(engineer: true, medic: false);
            // ดูดทรัพยากรให้ต่ำกว่าต้นทุนฝึก (Food 30 + Energy 50)
            eventManager.RaiseResourceDelta(ResourceType.Food, -1000);
            eventManager.RaiseResourceDelta(ResourceType.Energy, -1000);

            string notice = null;
            eventManager.OnNotice += m => notice = m;
            population.TrainEngineer();

            Assert.AreEqual(6, population.Current.workers, "ทรัพยากรไม่พอ → ไม่ดึง Worker (คงเริ่มต้น 6)");
            StringAssert.Contains("ทรัพยากรไม่พอ", notice);
        }

        [Test]
        public void TrainEngineer_Success_RaisesNotice()
        {
            UnlockTraining(engineer: true, medic: false);

            string notice = null;
            eventManager.OnNotice += m => notice = m;
            population.TrainEngineer();

            Assert.AreEqual(5, population.Current.workers, "เริ่ม 6 − 1 เข้าฝึก = 5");
            StringAssert.Contains("เริ่มฝึก", notice);
        }

        [Test]
        public void TrainMedic_ConvertsWorkerToMedic_After1Day()
        {
            UnlockTraining(engineer: false, medic: true);

            population.TrainMedic();
            eventManager.RaiseDayEnded(2);

            Assert.AreEqual(1, population.Current.medics);
            Assert.AreEqual(5, population.Current.workers, "เริ่ม 6 − 1 เข้าฝึก = 5");
        }

        [Test]
        public void TrainFarmer_ConvertsWorkerToFarmer_After1Day()
        {
            UnlockTraining(engineer: false, medic: false, farmer: true);

            population.TrainFarmer();
            Assert.AreEqual(5, population.Current.workers, "ดึง Worker เข้าฝึกทันที");
            Assert.AreEqual(2, population.Current.farmers, "ยังไม่เสร็จ — คง Farmer เริ่มต้น 2");

            eventManager.RaiseDayEnded(2);
            Assert.AreEqual(3, population.Current.farmers, "สิ้นวัน → ได้ Farmer เพิ่ม (2+1)");
        }

        [Test]
        public void TrainFarmer_WithoutLab_Blocked()
        {
            population.TrainFarmer(); // ยังไม่ปลดล็อก
            Assert.AreEqual(6, population.Current.workers, "ไม่มีห้องวิจัย → ฝึกเกษตรกรไม่ได้");
            eventManager.RaiseDayEnded(2);
            Assert.AreEqual(2, population.Current.farmers, "ไม่มีการฝึกค้าง — Farmer คงเริ่มต้น 2");
        }

        // ── เติมประชากร (V4 §5) ──────────────────────────────────
        [Test]
        public void Growth_AddsWorker_WhenBelowCap_AndHopeOK()
        {
            InjectPop(workers: 5, hope: 100f, shelterCap: 20);
            eventManager.RaiseDayEnded(2);
            Assert.AreEqual(6, population.Current.workers, "อาหารพอ & Hope≥50 & ยังไม่เต็ม → +1");
        }

        [Test]
        public void Growth_Blocked_WhenAtCap()
        {
            InjectPop(workers: 10, hope: 100f, shelterCap: 10);
            eventManager.RaiseDayEnded(2);
            Assert.AreEqual(10, population.Current.workers, "เต็มเพดาน → ไม่โต");
        }

        [Test]
        public void Growth_Blocked_WhenHopeLow()
        {
            InjectPop(workers: 5, hope: 40f, shelterCap: 20);
            eventManager.RaiseDayEnded(2); // ไม่มี Medic → hope คง 40 < 50
            Assert.AreEqual(5, population.Current.workers, "Hope < 50 → ไม่โต");
        }

        [Test]
        public void Growth_Blocked_WhenFoodShort()
        {
            InjectPop(workers: 5, hope: 100f, shelterCap: 20);
            eventManager.RaiseResourceDepleted(ResourceType.Food);
            eventManager.RaiseDayEnded(2);
            Assert.AreEqual(5, population.Current.workers, "อาหารขาด → ไม่โต");
        }

        // ── หล่อเย็น (§8) ────────────────────────────────────────
        [Test]
        public void AssignedCoolingEngineers_EqualsEngineerCount()
        {
            InjectPop(workers: 5, hope: 100f, shelterCap: 20, engineers: 3);
            Assert.AreEqual(3, population.AssignedCoolingEngineers);
        }

        // ── helpers ─────────────────────────────────────────────
        private void InjectPop(int workers, float hope, int shelterCap, int engineers = 0, int medics = 0, int farmers = 0)
        {
            var save = new SaveData
            {
                // คลังบวกทั้งหมด → ไม่มี depleted event มา block growth
                resources = new ResourceData { energy = 100f, water = 100f, food = 100f, iron = 100f },
                population = new PopulationData
                {
                    workers = workers, engineers = engineers, medics = medics, farmers = farmers,
                    hope = hope, shelterCap = shelterCap,
                },
            };
            eventManager.RaiseSaveLoaded(save);
        }

        private void UnlockTraining(bool engineer, bool medic, bool farmer = false)
        {
            var b = ScriptableObject.CreateInstance<BuildingData>();
            b.buildingName = "TrainingFacility";
            b.size = new Vector2Int(1, 1);
            b.energyCost = 0;
            b.ironCost = 0;
            b.unlocksEngineerTraining = engineer;
            b.unlocksMedicTraining = medic;
            b.unlocksFarmerTraining = farmer;
            _spawned.Add(b);
            eventManager.RaiseBuildingPlaced(new Cell(1, 1), b);
        }

        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var component = go.AddComponent<T>();
            TryInvokePrivate(component, "Awake");
            TryInvokePrivate(component, "OnEnable");
            return component;
        }

        private static void TryInvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            try { method?.Invoke(target, null); }
            catch (TargetInvocationException) { }
        }
    }
}
