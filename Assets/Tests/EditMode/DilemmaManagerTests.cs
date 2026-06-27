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
            eventManager.RaiseDilemmaResolved(crisis, true); // เคลียร์ active
            eventManager.RaiseDayEnded(6);                   // HEAT ยังสูง

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
            eventManager.RaiseDilemmaResolved(crisis, true);

            Assert.Contains((ResourceType.Energy, -300f), deltas, "ต้องหัก Energy ตาม choiceA");
            Assert.Contains((ResourceType.Water, -60f), deltas, "ต้องหัก Water ตาม choiceA");
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
