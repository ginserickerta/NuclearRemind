using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// A3 — Day cycle (§2.3): Day 1 ไม่จับเวลา (tutorial), Day 2–30 จับเวลา dayLength
    /// EndDay → raise OnDayEnded แล้วเริ่มวันถัดไป, clamp ที่ Day 30
    /// </summary>
    public class GameManagerTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private GameManager gameManager;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
            gameManager = NewComponent<GameManager>("GameManager");
            gameManager.dayLength = 90f;
            gameManager.skipDay1Timer = true;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f; // คืนค่า global หลังเทสต์ speed control
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        [Test]
        public void Start_BeginsDay1_Untimed()
        {
            int startedDay = -1;
            bool startedTimed = true;
            eventManager.OnDayStarted += (d, t) => { startedDay = d; startedTimed = t; };

            InvokePrivate(gameManager, "Start");

            Assert.AreEqual(1, gameManager.CurrentDay);
            Assert.IsFalse(gameManager.DayTimerActive, "Day 1 = tutorial ต้องไม่จับเวลา");
            Assert.AreEqual(1, startedDay, "ต้อง raise OnDayStarted(1) ตอนเริ่มเกม");
            Assert.IsFalse(startedTimed, "Day 1 ต้องส่ง timed=false");
        }

        [Test]
        public void RequestEndDay_FromDay1_AdvancesToDay2Timed()
        {
            int endedDay = -1, startedDay = -1;
            bool startedTimed = false;
            eventManager.OnDayEnded += d => endedDay = d;

            InvokePrivate(gameManager, "Start"); // Day 1
            eventManager.OnDayStarted += (d, t) => { startedDay = d; startedTimed = t; };

            gameManager.RequestEndDay();

            Assert.AreEqual(2, gameManager.CurrentDay);
            Assert.IsTrue(gameManager.DayTimerActive, "Day 2 ต้องจับเวลา");
            Assert.AreEqual(90f, gameManager.DayTimeRemaining, 1e-4f, "เวลาวันใหม่ = dayLength");
            Assert.AreEqual(1, endedDay, "ต้อง raise OnDayEnded(1)");
            Assert.AreEqual(2, startedDay, "ต้อง raise OnDayStarted(2)");
            Assert.IsTrue(startedTimed, "Day 2 ต้องส่ง timed=true");
        }

        [Test]
        public void EndDay_ClampsAtDay30_NoDay31()
        {
            InvokePrivate(gameManager, "Start"); // Day 1
            for (int i = 0; i < 40; i++)
                gameManager.RequestEndDay();

            Assert.AreEqual(30, gameManager.CurrentDay, "ต้องหยุดที่ Day 30 ไม่เกิน MaxDay");
            Assert.IsFalse(gameManager.DayTimerActive, "หลังจบ Day 30 timer ต้องปิด");
        }

        [Test]
        public void TutorialComplete_OnDay1_AdvancesToDay2()
        {
            InvokePrivate(gameManager, "Start"); // Day 1

            eventManager.RaiseTutorialComplete();

            Assert.AreEqual(2, gameManager.CurrentDay, "จบ tutorial บน Day 1 → เริ่ม Day 2");
        }

        [Test]
        public void RequestEndDay_AfterVictory_DoesNothing()
        {
            InvokePrivate(gameManager, "Start"); // Day 1
            gameManager.SetState(GameManager.GameState.Victory);

            gameManager.RequestEndDay();

            Assert.AreEqual(1, gameManager.CurrentDay, "หลังชนะแล้วไม่ควรเดินวันต่อ");
        }

        // ---- A4: speed controls ----

        [Test]
        public void SetSpeed_Fast_SetsTimeScaleAndPlaying()
        {
            gameManager.SetSpeed(2f);

            Assert.AreEqual(2f, gameManager.GameSpeed, 1e-4f);
            Assert.AreEqual(2f, Time.timeScale, 1e-4f);
            Assert.AreEqual(GameManager.GameState.Playing, gameManager.CurrentState);
        }

        [Test]
        public void SpeedChangeRequest_ViaEvent_AppliesSpeed()
        {
            eventManager.RaiseSpeedChangeRequested(2f);

            Assert.AreEqual(2f, Time.timeScale, 1e-4f, "GameManager ต้องรับ request ผ่าน event");
            Assert.AreEqual(GameManager.GameState.Playing, gameManager.CurrentState);
        }

        [Test]
        public void Pause_ThenResume_RestoresSelectedSpeed()
        {
            gameManager.SetSpeed(2f); // เร่ง
            gameManager.SetSpeed(0f); // pause

            Assert.AreEqual(GameManager.GameState.Paused, gameManager.CurrentState);
            Assert.AreEqual(0f, Time.timeScale, 1e-4f);
            Assert.AreEqual(2f, gameManager.GameSpeed, 1e-4f, "ความเร็วที่เลือกต้องคงไว้ระหว่าง pause");

            gameManager.SetState(GameManager.GameState.Playing); // resume (เช่นกด Space)

            Assert.AreEqual(2f, Time.timeScale, 1e-4f, "กลับมาเล่นต้องใช้ความเร็วเดิม 2×");
        }

        [Test]
        public void SetSpeed_RaisesOnSpeedChanged()
        {
            float reported = -1f;
            eventManager.OnSpeedChanged += s => reported = s;

            gameManager.SetSpeed(2f);
            Assert.AreEqual(2f, reported, 1e-4f);

            gameManager.SetSpeed(0f);
            Assert.AreEqual(0f, reported, 1e-4f, "pause ต้องรายงาน effective speed = 0");
        }

        [Test]
        public void SetSpeed_AfterVictory_Ignored()
        {
            gameManager.SetState(GameManager.GameState.Victory);

            gameManager.SetSpeed(2f);

            Assert.AreEqual(GameManager.GameState.Victory, gameManager.CurrentState,
                "เกมจบแล้วไม่ควรเปลี่ยนความเร็ว/สถานะ");
        }

        // ---- reflection helpers (เหมือน IntegrationFlowTests) ----

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

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method, $"ไม่พบ method '{methodName}' บน {target.GetType().Name}");
            method.Invoke(target, null);
        }
    }
}
