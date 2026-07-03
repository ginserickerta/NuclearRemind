using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// V4 §5/§9 — ประชากร 3 คลาส + Hope เดี่ยว:
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

        // ── ค่าเริ่มต้น ──────────────────────────────────────────
        [Test]
        public void StartsAt_Hope100_Workers10()
        {
            Assert.AreEqual(100f, population.Current.hope, 1e-4f);
            Assert.AreEqual(10, population.Current.workers);
            Assert.AreEqual(10, population.Current.total);
            Assert.AreEqual(0, population.Current.engineers);
        }

        // ── ขวัญกำลังใจ (คงพฤติกรรมเดิม) ──────────────────────────
        [Test]
        public void DayEnded_WithShortage_LowersHope()
        {
            eventManager.RaiseResourceDepleted(ResourceType.Food);
            eventManager.RaiseDayEnded(2);
            Assert.AreEqual(95f, population.Current.hope, 1e-4f, "ขาด 1 อย่าง → Hope -5");
        }

        [Test]
        public void DayEnded_NoShortage_RecoversHope()
        {
            eventManager.RaiseMoraleDelta(-20f); // 100 → 80
            eventManager.RaiseDayEnded(2);
            Assert.AreEqual(83f, population.Current.hope, 1e-4f, "ไม่ขาด → Hope +3");
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
            Assert.AreEqual(9, population.Current.workers, "ดึง Worker เข้าฝึกทันที");
            Assert.AreEqual(0, population.Current.engineers, "ยังไม่เสร็จ (ใช้เวลา 1 วัน)");

            eventManager.RaiseDayEnded(2);
            Assert.AreEqual(1, population.Current.engineers, "สิ้นวัน → ได้ Engineer");
            Assert.AreEqual(9, population.Current.workers);
        }

        [Test]
        public void TrainEngineer_WithoutResearchLab_Blocked()
        {
            population.TrainEngineer(); // ยังไม่ปลดล็อก
            Assert.AreEqual(10, population.Current.workers, "ไม่มี Research Lab → ฝึกไม่ได้");
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

            Assert.AreEqual(10, population.Current.workers, "ทรัพยากรไม่พอ → ไม่ดึง Worker");
            StringAssert.Contains("ทรัพยากรไม่พอ", notice);
        }

        [Test]
        public void TrainEngineer_Success_RaisesNotice()
        {
            UnlockTraining(engineer: true, medic: false);

            string notice = null;
            eventManager.OnNotice += m => notice = m;
            population.TrainEngineer();

            Assert.AreEqual(9, population.Current.workers);
            StringAssert.Contains("เริ่มฝึก", notice);
        }

        [Test]
        public void TrainMedic_ConvertsWorkerToMedic_After1Day()
        {
            UnlockTraining(engineer: false, medic: true);

            population.TrainMedic();
            eventManager.RaiseDayEnded(2);

            Assert.AreEqual(1, population.Current.medics);
            Assert.AreEqual(9, population.Current.workers);
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
            eventManager.RaiseDayEnded(2); // 40+3 = 43 < 50
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
        private void InjectPop(int workers, float hope, int shelterCap, int engineers = 0, int medics = 0)
        {
            var save = new SaveData
            {
                // คลังบวกทั้งหมด → ไม่มี depleted event มา block growth
                resources = new ResourceData { energy = 100f, water = 100f, food = 100f, iron = 100f },
                population = new PopulationData
                {
                    workers = workers, engineers = engineers, medics = medics,
                    hope = hope, shelterCap = shelterCap,
                },
            };
            eventManager.RaiseSaveLoaded(save);
        }

        private void UnlockTraining(bool engineer, bool medic)
        {
            var b = ScriptableObject.CreateInstance<BuildingData>();
            b.buildingName = "TrainingFacility";
            b.size = new Vector2Int(1, 1);
            b.energyCost = 0;
            b.ironCost = 0;
            b.unlocksEngineerTraining = engineer;
            b.unlocksMedicTraining = medic;
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
