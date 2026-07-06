using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// V4 §15/§16 — TimeManager pause-reason stack: IsRunning เมื่อไม่มีเหตุหยุด,
    /// เหตุหยุดซ้อนกันได้ (Placement + QuizPopup), เดินต่อเมื่อปลดครบ
    /// </summary>
    public class TimeManagerTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private TimeManager time;

        [SetUp]
        public void SetUp()
        {
            time = NewComponent<TimeManager>("TimeManager");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        [Test]
        public void IsRunning_TrueByDefault()
        {
            Assert.IsTrue(time.IsRunning, "ไม่มีเหตุหยุด → นาฬิกาเดิน");
        }

        [Test]
        public void Pause_StopsClock()
        {
            time.Pause(PauseReason.Placement);
            Assert.IsFalse(time.IsRunning, "มีเหตุหยุด → นาฬิกาหยุด");
            Assert.IsTrue(time.IsPaused(PauseReason.Placement));
        }

        [Test]
        public void StackedPauses_NeedAllResumed()
        {
            time.Pause(PauseReason.Placement);
            time.Pause(PauseReason.QuizPopup);
            Assert.AreEqual(2, time.ActivePauseCount);

            time.Resume(PauseReason.Placement);
            Assert.IsFalse(time.IsRunning, "ยังเหลือเหตุ QuizPopup → ยังหยุด");

            time.Resume(PauseReason.QuizPopup);
            Assert.IsTrue(time.IsRunning, "ปลดครบทุกเหตุ → เดินต่อ");
        }

        [Test]
        public void Pause_Idempotent_SameReasonTwice()
        {
            time.Pause(PauseReason.Placement);
            time.Pause(PauseReason.Placement); // ซ้ำ — HashSet กันซ้ำ
            time.Resume(PauseReason.Placement);
            Assert.IsTrue(time.IsRunning, "เหตุเดียวกดซ้ำ → ปลดครั้งเดียวก็พอ");
        }

        [Test]
        public void DemolishMode_Toggle_PausesAndResumesClock()
        {
            var em = NewComponent<EventManager>("EventManager");
            NewComponent<DemolitionController>("DemolitionController");

            em.RaiseDemolishModeToggled(true);
            Assert.IsTrue(time.IsPaused(PauseReason.Demolition), "เข้าโหมดทุบ → หยุดนาฬิกาวัน (เจตนา §15)");

            em.RaiseDemolishModeToggled(false);
            Assert.IsTrue(time.IsRunning, "ออกจากโหมดทุบ → นาฬิกาเดินต่อ");
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
