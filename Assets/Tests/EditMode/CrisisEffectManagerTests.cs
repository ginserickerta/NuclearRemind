using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// CrisisEffectManager (Story Guide §4) — subscribe OnDilemmaResolved แล้วแปลงเป็นกลไกจริง
    /// ตรวจ: yield ×2 · spoilRate induced/stop · efficiency debuff หมดอายุ · busy workers คืนกำลัง ·
    ///       hopePerDay ยิงครบวัน · exposure+sick จาก Plasma B · riot · save round-trip + back-compat
    /// </summary>
    public class CrisisEffectManagerTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private EventManager eventManager;
        private CrisisEffectManager crisis;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager"); // ต้องมาก่อน — CrisisEffectManager.OnEnable subscribe
            crisis = NewComponent<CrisisEffectManager>("CrisisEffectManager");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
            _spawned.Clear();
        }

        private DilemmaData MakeDilemma(string id = "Crisis_Test")
        {
            var d = ScriptableObject.CreateInstance<DilemmaData>();
            d.dilemmaId = id;
            _spawned.Add(d);
            return d;
        }

        // ── Food A: ผลผลิตอาหาร ×2 ถาวร ──
        [Test]
        public void FoodA_YieldPct_DoublesFoodMultiplier()
        {
            var d = MakeDilemma();
            d.choiceA_Effects = new CrisisChoiceEffects { foodYieldPct = 1.0f };
            eventManager.RaiseDilemmaResolved(d, 0);
            Assert.AreEqual(2f, crisis.FoodYieldMultiplier, 1e-4f, "yield +100% → ×2");
        }

        // ── Food: วิกฤตเหนี่ยวนำการเน่า (A/C ยังเน่า) · B ฉายรังสีหยุดเน่า ──
        [Test]
        public void Food_InducedSpoilage_AndChoiceBStopsIt()
        {
            var d = MakeDilemma("Crisis_FoodShortage");
            d.inducedSpoilRatePerDay = 0.15f;
            d.choiceA_Effects = new CrisisChoiceEffects();                    // A: ไม่มี stopSpoilage
            d.choiceB_Effects = new CrisisChoiceEffects { stopSpoilage = true };

            eventManager.RaiseDilemmaResolved(d, 0);                          // เลือก A → เน่าตาม induced
            Assert.AreEqual(0.15f, crisis.FoodSpoilRatePerDay, 1e-4f);

            eventManager.RaiseDilemmaResolved(d, 1);                          // เลือก B → หยุดเน่า
            Assert.AreEqual(0f, crisis.FoodSpoilRatePerDay, 1e-4f);
        }

        // ── Food C: ประสิทธิภาพผลิต −50% แล้วคืนปกติเมื่อครบวัน ──
        [Test]
        public void FoodC_EfficiencyDebuff_RevertsAfterDays()
        {
            var d = MakeDilemma();
            d.choiceC_Effects = new CrisisChoiceEffects { workerEfficiencyPct = -0.5f, efficiencyDays = 3 };
            eventManager.RaiseDilemmaResolved(d, 2);
            Assert.AreEqual(0.5f, crisis.WorkerEfficiencyMultiplier, 1e-4f, "งานช้าลง 50%");

            eventManager.RaiseDayEnded(1);
            eventManager.RaiseDayEnded(2);
            Assert.AreEqual(0.5f, crisis.WorkerEfficiencyMultiplier, 1e-4f, "ยังไม่ครบ 3 วัน");

            eventManager.RaiseDayEnded(3);
            Assert.AreEqual(1f, crisis.WorkerEfficiencyMultiplier, 1e-4f, "ครบ 3 วัน → คืนปกติ");
        }

        // ── Busy workers: หักกำลังผลิตชั่วคราวแล้วคืนเมื่อครบวัน ──
        [Test]
        public void BusyWorkers_DecrementsAndReleases()
        {
            var d = MakeDilemma();
            d.choiceA_Effects = new CrisisChoiceEffects { busyWorkers = 4, busyDays = 2 };
            eventManager.RaiseDilemmaResolved(d, 0);
            Assert.AreEqual(4, crisis.BusyWorkers);

            eventManager.RaiseDayEnded(1);
            Assert.AreEqual(4, crisis.BusyWorkers, "ยังไม่ครบ 2 วัน");
            eventManager.RaiseDayEnded(2);
            Assert.AreEqual(0, crisis.BusyWorkers, "ครบ 2 วัน → คืนกำลัง");
        }

        // ── Decree B: Hope ดิ่งต่อวันครบจำนวนวันแล้วหยุด (hopePerDay) ──
        [Test]
        public void DecreeB_HopePerDay_FiresExactlyNDays()
        {
            var d = MakeDilemma("Crisis_DecreeEmergency");
            d.choiceB_Effects = new CrisisChoiceEffects { hopePerDay = -3f, hopePerDayDays = 4 };
            eventManager.RaiseDilemmaResolved(d, 1);

            int count = 0; float last = 0f;
            eventManager.OnMoraleDelta += x => { last = x; count++; };
            for (int day = 1; day <= 6; day++) eventManager.RaiseDayEnded(day);

            Assert.AreEqual(4, count, "ยิง -3 พอดี 4 วันแล้วหยุด");
            Assert.AreEqual(-3f, last, 1e-4f);
        }

        // ── Plasma B: ดัน exposure + เพิ่มคนป่วย ("วิศวกร 2 คนได้รับรังสีเกิน") ──
        [Test]
        public void PlasmaB_InjectsExposureAndSick()
        {
            var rad = NewComponent<RadiationManager>("RadiationManager");
            int sick = -1;
            eventManager.OnPopulationSickInjected += n => sick = n;

            var d = MakeDilemma("Crisis_PlasmaInstability");
            d.choiceB_Effects = new CrisisChoiceEffects { radExposureInjected = 25f, sickInjected = 2 };
            eventManager.RaiseDilemmaResolved(d, 1);

            Assert.AreEqual(25f, rad.CurrentExposure, 1e-4f, "exposure ถูกดันเข้า RadiationManager");
            Assert.AreEqual(2, sick, "วิศวกร 2 คนได้รับรังสีเกิน");
        }

        // ── Food C: จลาจลเมื่อ Hope ต่ำ (deterministic) ──
        [Test]
        public void FoodC_Riot_FiresWhenHopeLow()
        {
            var pop = NewComponent<PopulationManager>("PopulationManager");
            // ตั้ง Hope = 30 (< เกณฑ์ 40) ผ่านการโหลดเซฟ
            eventManager.RaiseSaveLoaded(new SaveData
            {
                population = new PopulationData { workers = 10, hope = 30f, shelterCap = 10 }
            });

            float moraleDelta = 0f; int deaths = 0;
            eventManager.OnMoraleDelta += x => moraleDelta += x;
            eventManager.OnPopulationDeaths += n => deaths += n;

            var d = MakeDilemma("Crisis_FoodShortage");
            d.choiceC_Effects = new CrisisChoiceEffects { riotRisk = true };
            eventManager.RaiseDilemmaResolved(d, 2);

            Assert.AreEqual(-10f, moraleDelta, 1e-4f, "จลาจล → Hope −10");
            Assert.AreEqual(1, deaths, "จลาจล → ตาย 1 คน");
            Assert.IsNotNull(pop);
        }

        // ── Save round-trip: state กลับมาครบหลังโหลด ──
        [Test]
        public void SaveLoad_RestoresState()
        {
            eventManager.RaiseSaveLoaded(new SaveData
            {
                foodYieldMultiplier = 2f,
                foodSpoilRatePerDay = 0.1f,
                workerEfficiencyMultiplier = 0.5f,
                workerEfficiencyDaysRemaining = 3,
                busyWorkerCounts = new List<int> { 4 },
                busyWorkerDays = new List<int> { 2 },
                hopeDrainPerDay = new List<float> { -3f },
                hopeDrainDays = new List<int> { 4 },
            });

            Assert.AreEqual(2f, crisis.FoodYieldMultiplier, 1e-4f);
            Assert.AreEqual(0.1f, crisis.FoodSpoilRatePerDay, 1e-4f);
            Assert.AreEqual(0.5f, crisis.WorkerEfficiencyMultiplier, 1e-4f);
            Assert.AreEqual(4, crisis.BusyWorkers);
        }

        // ── Back-compat: เซฟเก่า (default) → ไม่มี effect (multiplier = 1, ไม่เน่า) ──
        [Test]
        public void OldSave_DefaultsToNoEffect()
        {
            // จำลอง state ค้างก่อน แล้วโหลดเซฟเก่าที่ไม่มี field วิกฤต
            eventManager.RaiseDilemmaResolved(MakeDilemmaWith(new CrisisChoiceEffects { foodYieldPct = 1.0f }), 0);
            Assert.AreEqual(2f, crisis.FoodYieldMultiplier, 1e-4f);

            eventManager.RaiseSaveLoaded(new SaveData()); // default: multiplier 1, rate 0, list ว่าง

            Assert.AreEqual(1f, crisis.FoodYieldMultiplier, 1e-4f, "เซฟเก่า → yield 1 (ไม่ทำให้ผลผลิตเป็น 0)");
            Assert.AreEqual(1f, crisis.WorkerEfficiencyMultiplier, 1e-4f);
            Assert.AreEqual(0f, crisis.FoodSpoilRatePerDay, 1e-4f);
            Assert.AreEqual(0, crisis.BusyWorkers);
        }

        private DilemmaData MakeDilemmaWith(CrisisChoiceEffects aEffects)
        {
            var d = MakeDilemma();
            d.choiceA_Effects = aEffects;
            return d;
        }

        // ── harness (เหมือน DecreeManagerTests): AddComponent + เรียก Awake/OnEnable ผ่าน reflection ──
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
