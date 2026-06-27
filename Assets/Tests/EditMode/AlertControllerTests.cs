using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// A5 — Alert popup: เด้งเมื่อ resource วิกฤต/หมด หรือ dilemma trigger
    /// debounce ด้วย active-key: alert ชนิดเดียวกันไม่เด้งซ้ำระหว่างยังค้างอยู่
    /// </summary>
    public class AlertControllerTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private AlertController alert;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
            alert = NewComponent<AlertController>("AlertController");

            var containerGO = new GameObject("AlertContainer", typeof(RectTransform));
            _spawned.Add(containerGO);
            alert.alertContainer = containerGO.GetComponent<RectTransform>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        [Test]
        public void ResourceDepleted_ShowsOneAlert()
        {
            eventManager.RaiseResourceDepleted(ResourceType.Food);
            Assert.AreEqual(1, alert.ActiveAlertCount);
        }

        [Test]
        public void SameAlert_Twice_IsDebounced()
        {
            eventManager.RaiseResourceDepleted(ResourceType.Food);
            eventManager.RaiseResourceDepleted(ResourceType.Food);

            Assert.AreEqual(1, alert.ActiveAlertCount, "alert ชนิดเดียวกันต้องไม่เด้งซ้ำระหว่างยังค้างอยู่");
        }

        [Test]
        public void DifferentResources_ShowSeparateAlerts()
        {
            eventManager.RaiseResourceDepleted(ResourceType.Food);
            eventManager.RaiseResourceCritical(ResourceType.Water);

            Assert.AreEqual(2, alert.ActiveAlertCount);
        }

        [Test]
        public void CriticalAndDepleted_SameResource_AreDistinct()
        {
            // critical:Food กับ depleted:Food เป็นคนละ key (เช่น food ลดจากใกล้หมด → หมดจริง)
            eventManager.RaiseResourceCritical(ResourceType.Food);
            eventManager.RaiseResourceDepleted(ResourceType.Food);

            Assert.AreEqual(2, alert.ActiveAlertCount);
        }

        [Test]
        public void DilemmaTriggered_ShowsCrisisAlert()
        {
            var dilemma = ScriptableObject.CreateInstance<DilemmaData>();
            dilemma.dilemmaId = "plasma_instability";
            _spawned.Add(dilemma);

            eventManager.RaiseDilemmaTriggered(dilemma);

            Assert.AreEqual(1, alert.ActiveAlertCount);
        }

        [Test]
        public void BuildResourceAlert_KeysAndLabels()
        {
            var (depKey, depMsg) = AlertController.BuildResourceAlert(ResourceType.Energy, depleted: true);
            Assert.AreEqual("depleted:Energy", depKey);
            StringAssert.Contains("พลังงาน", depMsg);

            var (critKey, _) = AlertController.BuildResourceAlert(ResourceType.Water, depleted: false);
            Assert.AreEqual("critical:Water", critKey);
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
    }
}
