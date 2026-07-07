using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// Block C — Crisis trigger system: DilemmaManager ประเมินเงื่อนไข day-end ตอน OnDayEnded
    /// (heat_above / food_below / day_reached) + การ apply consequence Energy/Water
    /// </summary>
    public class DilemmaManagerTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private DilemmaManager dilemmaManager;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
            dilemmaManager = NewComponent<DilemmaManager>("DilemmaManager");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        private DilemmaData NewDilemma(string id, string trigger)
        {
            var d = ScriptableObject.CreateInstance<DilemmaData>();
            d.dilemmaId = id;
            d.triggerCondition = trigger;
            _spawned.Add(d);
            return d;
        }

        [Test]
        public void DayEnded_HeatAboveThreshold_TriggersCrisis()
        {
            var crisis = NewDilemma("heat_crisis", "heat_above_70");
            dilemmaManager.dilemmaPool = new[] { crisis };

            DilemmaData triggered = null;
            eventManager.OnDilemmaTriggered += d => triggered = d;

            eventManager.RaiseTowerProgressChanged(new TowerData { coreHeat = 80f });
            eventManager.RaiseDayEnded(5);

            Assert.AreEqual(crisis, triggered, "HEAT 80 ≥ 70 ต้อง trigger crisis ตอนสิ้นวัน");
        }

        [Test]
        public void DayEnded_QAboveThreshold_TriggersCrisis()
        {
            // V4 §10 วิกฤต 1: Q > 0.3 (Q = CORE%/100)
            var crisis = NewDilemma("plasma", "q_above_0.3");
            dilemmaManager.dilemmaPool = new[] { crisis };

            DilemmaData triggered = null;
            eventManager.OnDilemmaTriggered += d => triggered = d;

            eventManager.RaiseTowerProgressChanged(new TowerData { corePercent = 35f });
            eventManager.RaiseDayEnded(17);

            Assert.AreEqual(crisis, triggered, "CORE 35% = Q 0.35 ≥ 0.3 ต้อง trigger");
        }

        [Test]
        public void DayEnded_OrCondition_EitherSideTriggers()
        {
            // "heat_above_80|q_above_0.3" — เข้าเงื่อนไขอย่างใดอย่างหนึ่งก็ trigger (V4 §10)
            var crisis = NewDilemma("plasma", "heat_above_80|q_above_0.3");
            dilemmaManager.dilemmaPool = new[] { crisis };

            DilemmaData triggered = null;
            eventManager.OnDilemmaTriggered += d => triggered = d;

            // heat ต่ำ แต่ Q สูงพอ → trigger จากฝั่งขวา
            eventManager.RaiseTowerProgressChanged(new TowerData { coreHeat = 40f, corePercent = 40f });
            eventManager.RaiseDayEnded(17);

            Assert.AreEqual(crisis, triggered, "Q 0.4 ≥ 0.3 ต้อง trigger แม้ HEAT ต่ำ");
        }

        [Test]
        public void DayEnded_OrCondition_NeitherSide_NoTrigger()
        {
            var crisis = NewDilemma("plasma", "heat_above_80|q_above_0.3");
            dilemmaManager.dilemmaPool = new[] { crisis };

            DilemmaData triggered = null;
            eventManager.OnDilemmaTriggered += d => triggered = d;

            eventManager.RaiseTowerProgressChanged(new TowerData { coreHeat = 40f, corePercent = 20f });
            eventManager.RaiseDayEnded(17);

            Assert.IsNull(triggered, "ทั้ง HEAT และ Q ต่ำกว่าเกณฑ์ → ไม่ trigger");
        }

        [Test]
        public void DayEnded_FoodAboveThreshold_TriggersSpoilage()
        {
            // V4 §10 วิกฤต 3: กักตุนอาหารเกิน 500 → เน่า (เดิม food_below กลับด้าน)
            var crisis = NewDilemma("food_spoil", "food_above_500");
            dilemmaManager.dilemmaPool = new[] { crisis };

            DilemmaData triggered = null;
            eventManager.OnDilemmaTriggered += d => triggered = d;

            eventManager.RaiseResourceChanged(new ResourceData { food = 500f });
            eventManager.RaiseDayEnded(24);

            Assert.AreEqual(crisis, triggered, "food 500 ≥ 500 ต้อง trigger วิกฤตเน่า");
        }

        [Test]
        public void Resolve_WithDeaths_RaisesPopulationDeaths()
        {
            var crisis = NewDilemma("outbreak", "day_reached_20");
            crisis.choiceC_Deaths = 3; // วิกฤต 2·C: เสียชีวิต 3 คน (V4 §10)
            dilemmaManager.dilemmaPool = new[] { crisis };

            DilemmaData triggered = null;
            eventManager.OnDilemmaTriggered += d => triggered = d;
            int deaths = 0;
            eventManager.OnPopulationDeaths += n => deaths = n;

            eventManager.RaiseDayEnded(20);
            eventManager.RaiseDilemmaResolved(triggered, 2); // เลือก C

            Assert.AreEqual(3, deaths, "เลือกทางที่มีคนตาย → raise OnPopulationDeaths(3)");
        }

        [Test]
        public void Resolve_AddsCrisisResolvedHopeBonus()
        {
            var crisis = NewDilemma("any", "day_reached_5");
            crisis.choiceA_HopeChange = -8f;
            dilemmaManager.dilemmaPool = new[] { crisis };

            DilemmaData triggered = null;
            eventManager.OnDilemmaTriggered += d => triggered = d;
            float moraleDelta = float.NaN;
            eventManager.OnMoraleDelta += v => moraleDelta = v;

            eventManager.RaiseDayEnded(5);
            eventManager.RaiseDilemmaResolved(triggered, 0);

            Assert.AreEqual(-8f + dilemmaManager.resolveHopeBonus, moraleDelta, 1e-4f,
                "Hope สุทธิ = ผลของทางเลือก + โบนัสแก้วิกฤตสำเร็จ (V4 §9 +5)");
        }

        [Test]
        public void DayEnded_HeatBelowThreshold_NoTrigger()
        {
            var crisis = NewDilemma("heat_crisis", "heat_above_70");
            dilemmaManager.dilemmaPool = new[] { crisis };

            DilemmaData triggered = null;
            eventManager.OnDilemmaTriggered += d => triggered = d;

            eventManager.RaiseTowerProgressChanged(new TowerData { coreHeat = 50f });
            eventManager.RaiseDayEnded(5);

            Assert.IsNull(triggered, "HEAT 50 < 70 ไม่ควร trigger");
        }

        [Test]
        public void DayEnded_FoodBelowThreshold_TriggersCrisis()
        {
            var crisis = NewDilemma("food_crisis", "food_below_120");
            dilemmaManager.dilemmaPool = new[] { crisis };

            DilemmaData triggered = null;
            eventManager.OnDilemmaTriggered += d => triggered = d;

            eventManager.RaiseResourceChanged(new ResourceData { food = 100f });
            eventManager.RaiseDayEnded(7);

            Assert.AreEqual(crisis, triggered, "food 100 ≤ 120 ต้อง trigger");
        }

        [Test]
        public void DayEnded_DayReached_TriggersCrisis()
        {
            var crisis = NewDilemma("outbreak", "day_reached_18");
            dilemmaManager.dilemmaPool = new[] { crisis };

            DilemmaData triggered = null;
            eventManager.OnDilemmaTriggered += d => triggered = d;

            eventManager.RaiseDayEnded(18);

            Assert.AreEqual(crisis, triggered, "ถึง Day 18 ต้อง trigger");
        }

        [Test]
        public void Crisis_TriggersOnce_NotRepeatedNextDay()
        {
            var crisis = NewDilemma("heat_crisis", "heat_above_70");
            dilemmaManager.dilemmaPool = new[] { crisis };

            int count = 0;
            eventManager.OnDilemmaTriggered += _ => count++;

            eventManager.RaiseTowerProgressChanged(new TowerData { coreHeat = 90f });
            eventManager.RaiseDayEnded(5);
            eventManager.RaiseDilemmaResolved(crisis, 0); // เคลียร์ active (เลือก A)
            eventManager.RaiseDayEnded(6);                // HEAT ยังสูง

            Assert.AreEqual(1, count, "crisis เดิมต้องไม่เด้งซ้ำ");
        }

        [Test]
        public void Resolved_ChoiceA_AppliesEnergyAndWaterDeltas()
        {
            var crisis = NewDilemma("heat_crisis", "heat_above_70");
            crisis.choiceA_EnergyChange = -300f;
            crisis.choiceA_WaterChange = -60f;
            dilemmaManager.dilemmaPool = new[] { crisis };

            var deltas = new List<(ResourceType type, float amt)>();
            eventManager.OnResourceDelta += (t, a) => deltas.Add((t, a));

            eventManager.RaiseTowerProgressChanged(new TowerData { coreHeat = 80f });
            eventManager.RaiseDayEnded(5);
            eventManager.RaiseDilemmaResolved(crisis, 0); // A

            Assert.Contains((ResourceType.Energy, -300f), deltas, "ต้องหัก Energy ตาม choiceA");
            Assert.Contains((ResourceType.Water, -60f), deltas, "ต้องหัก Water ตาม choiceA");
        }

        [Test]
        public void Resolved_ChoiceC_AppliesIronAndHopeDeltas()
        {
            var crisis = NewDilemma("heat_crisis", "heat_above_70");
            crisis.choiceC_IronChange = -250f;
            crisis.choiceC_HopeChange = -12f;
            dilemmaManager.dilemmaPool = new[] { crisis };

            var resDeltas = new List<(ResourceType type, float amt)>();
            float hopeDelta = 0f;
            eventManager.OnResourceDelta += (t, a) => resDeltas.Add((t, a));
            eventManager.OnMoraleDelta += h => hopeDelta = h;

            eventManager.RaiseTowerProgressChanged(new TowerData { coreHeat = 80f });
            eventManager.RaiseDayEnded(5);
            eventManager.RaiseDilemmaResolved(crisis, 2); // C

            Assert.Contains((ResourceType.Iron, -250f), resDeltas, "choice C ต้องหัก Iron");
            // Hope สุทธิ = ของทางเลือก (−12) + โบนัสแก้วิกฤตสำเร็จ (V4 §9: เลือกทางใดก็ได้ +5) = −7
            Assert.AreEqual(-12f + dilemmaManager.resolveHopeBonus, hopeDelta, 1e-4f,
                "choice C ต้องปรับ Hope ของทางเลือก + โบนัสแก้วิกฤต");
        }

        [Test]
        public void Resolved_ChoiceB_ForceIdle_CommandsReactor()
        {
            var tower = NewComponent<CoreTowerManager>("CoreTowerManager");

            var crisis = NewDilemma("outbreak", "day_reached_18");
            crisis.choiceB_ForceReactorIdleDays = 2; // วิกฤต 2·B: สั่งเตา Idle 2 วัน
            dilemmaManager.dilemmaPool = new[] { crisis };

            eventManager.RaiseDayEnded(18);
            eventManager.RaiseDilemmaResolved(crisis, 1); // B

            Assert.AreEqual(2, tower.ForcedIdleDaysRemaining, "choice B ต้องสั่ง ForceIdle(2) ให้เตา");
        }

        // ---- reflection helpers (เหมือน GameManagerTests) ----

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
