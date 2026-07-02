using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// V4 §11 — ประกาศฉุกเฉิน: ให้แรงงานหล่อเย็น (CoolingLaborBonus) แลก Hope ทันที + ต่อวัน,
    /// ประกาศซ้ำไม่สะสม
    /// </summary>
    public class DecreeManagerTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private EventManager eventManager;
        private DecreeManager decreeManager;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
            decreeManager = NewComponent<DecreeManager>("DecreeManager");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        private DecreeSO MakeDecree(int coolingLabor, float hopeNow, float hopePerDay = 0f)
        {
            var d = ScriptableObject.CreateInstance<DecreeSO>();
            d.id = "D";
            d.coolingLaborGain = coolingLabor;
            d.hopeImmediate = hopeNow;
            d.hopePerDay = hopePerDay;
            _spawned.Add(d);
            return d;
        }

        [Test]
        public void Enact_AddsCoolingBonus_AndLowersHopeImmediately()
        {
            decreeManager.decrees = new[] { MakeDecree(coolingLabor: 12, hopeNow: -15f) };
            float hope = 0f;
            eventManager.OnMoraleDelta += h => hope = h;

            eventManager.RaiseEnactDecreeRequested(0);

            Assert.AreEqual(12, decreeManager.CoolingLaborBonus, "ได้แรงงานหล่อเย็น +12");
            Assert.AreEqual(-15f, hope, 1e-4f, "Hope ดิ่งทันที -15");
        }

        [Test]
        public void Enact_Twice_NotStacked()
        {
            decreeManager.decrees = new[] { MakeDecree(coolingLabor: 12, hopeNow: -15f) };

            eventManager.RaiseEnactDecreeRequested(0);
            eventManager.RaiseEnactDecreeRequested(0); // ประกาศซ้ำ — ไม่ควรสะสม

            Assert.AreEqual(12, decreeManager.CoolingLaborBonus, "ประกาศเดิมซ้ำ → ไม่บวกเพิ่ม");
        }

        [Test]
        public void ActiveDecree_LowersHope_EachDay()
        {
            decreeManager.decrees = new[] { MakeDecree(coolingLabor: 6, hopeNow: -8f, hopePerDay: -3f) };
            eventManager.RaiseEnactDecreeRequested(0);

            float perDay = 0f;
            eventManager.OnMoraleDelta += h => perDay = h;
            eventManager.RaiseDayEnded(26);

            Assert.AreEqual(-3f, perDay, 1e-4f, "ระหว่างใช้ decree → Hope -3/วัน");
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
