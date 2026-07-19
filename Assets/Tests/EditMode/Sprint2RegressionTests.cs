using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// Regression tests for the code-review findings on the Sprint 1/2 work.
    /// Drives WorkerManager's real day-end path (OnDayEnded → HandleDayEnded) through the
    /// event system + ResourceManager, which the unit tests bypassed.
    /// </summary>
    public class Sprint2RegressionTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private GameConfigSO cfg;
        private EventManager events;
        private ResourceManager resources;
        private WorkerManager wm;

        [SetUp]
        public void SetUp()
        {
            cfg = ScriptableObject.CreateInstance<GameConfigSO>();
            _spawned.Add(cfg);
            GameConfigSO.OverrideForTest(cfg);

            events = New<EventManager>("EventManager");
            resources = New<ResourceManager>("ResourceManager"); // Current inits in Awake
            wm = New<WorkerManager>("WorkerManager");            // OnEnable subscribes OnDayEnded
            wm.Initialize(cfg);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) Object.DestroyImmediate(o);
            GameConfigSO.OverrideForTest(null);
        }

        // EditMode never calls Awake/OnEnable for AddComponent, so they have to be invoked by hand -
        // without this ResourceManager.Current is never initialised and every food assertion here is
        // measuring an empty struct. Same idiom as ResearchManagerTests / DataRecoveryTests.
        private T New<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var c = go.AddComponent<T>();
            TryInvokePrivate(c, "Awake");
            TryInvokePrivate(c, "OnEnable");
            return c;
        }

        private static void TryInvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            try { method?.Invoke(target, null); }
            catch (TargetInvocationException) { }
        }

        private void SetFood(float target)
        {
            float delta = target - resources.Current.food;
            events.RaiseResourceDelta(ResourceType.Food, delta);
            Assert.AreEqual(target, resources.Current.food, 0.5f, "precondition: food set");
        }

        // ── Finding #1: food hope must not double-subtract consumption ──
        [Test]
        public void DayEnd_FoodRemains_DoesNotFalselyReportEmpty()
        {
            SetFood(20f);            // 14 workers eat 14 → 6 left (>0, not surplus)
            events.RaiseDayEnded(2);

            var food = wm.Hope.GetCommittedBreakdown().Where(e => e.category == HopeCategory.Food).ToList();
            Assert.IsFalse(food.Any(e => e.sourceKey == "food.empty"),
                "★ #1: ยังเหลืออาหาร 6 → ห้ามรายงาน food.empty (เดิมหักซ้ำ 6−14=−8 → empty ผิด)");
            Assert.IsFalse(food.Any(e => e.sourceKey == "food.surplus"), "6 < pop×3 → ไม่ใช่ surplus เช่นกัน");
        }

        [Test]
        public void DayEnd_FoodTrulyEmpty_StillReportsEmpty()
        {
            SetFood(0f);             // no food → after eating still 0
            events.RaiseDayEnded(2);
            Assert.IsTrue(wm.Hope.GetCommittedBreakdown().Any(e => e.sourceKey == "food.empty"),
                "อาหารหมดจริง → ต้องรายงาน food.empty (fix ต้องไม่กลบเคสจริง)");
        }

        [Test]
        public void DayEnd_FoodSurplus_ReportsSurplus()
        {
            SetFood(200f);           // 200 − 14 = 186 > 14×3=42 → surplus
            events.RaiseDayEnded(2);
            Assert.IsTrue(wm.Hope.GetCommittedBreakdown().Any(e => e.sourceKey == "food.surplus"),
                "อาหารล้น → food.surplus (+3)");
        }

        // ── Sanity: consumption applied once, floors at 0 ──────────────
        [Test]
        public void DayEnd_ConsumesFoodOnce_WaterFloorsAtZero()
        {
            SetFood(50f);
            float foodBefore = resources.Current.food;
            events.RaiseDayEnded(2);
            // 14 alive eat 14 food, once
            Assert.AreEqual(foodBefore - wm.AliveCount, resources.Current.food, 0.5f,
                "หักอาหารครั้งเดียว = จำนวนคน");
            Assert.GreaterOrEqual(resources.Current.water, 0f, "น้ำไม่ติดลบ (floor 0)");
        }
    }
}
