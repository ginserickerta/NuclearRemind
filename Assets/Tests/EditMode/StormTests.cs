using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// GDD v6.3 §23 — Storm. Pressure climbs from base + escalating boost-days + core + Zone B, plus an
    /// ignition bonus at core ≥ 80; at 100 the storm breaks (once). Sensor predicts arrival from
    /// pressure + last rise.
    /// </summary>
    public class StormTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private GameConfigSO cfg;
        private StormSystem storm;

        [SetUp]
        public void SetUp()
        {
            cfg = ScriptableObject.CreateInstance<GameConfigSO>();
            _spawned.Add(cfg);
            GameConfigSO.OverrideForTest(cfg);

            var evGo = new GameObject("EventManager"); _spawned.Add(evGo);
            evGo.AddComponent<EventManager>();

            var sGo = new GameObject("StormSystem"); _spawned.Add(sGo);
            storm = sGo.AddComponent<StormSystem>(); storm.Initialize(cfg);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) Object.DestroyImmediate(o);
            GameConfigSO.OverrideForTest(null);
        }

        [Test]
        public void BaseRise_FromCoreOnly()
        {
            storm.TickDay(50f, false, false);
            // 0.9 + 0 + (0.5 × 1.1) = 1.45
            Assert.AreEqual(1.45f, storm.LastRise, 1e-3f);
            Assert.AreEqual(1.45f, storm.Pressure, 1e-3f);
        }

        [Test]
        public void BoostDays_EscalateAggression()
        {
            storm.TickDay(50f, true, false);  // boostDays 1 → 0.9+0.30+0.55 = 1.75
            storm.TickDay(50f, true, false);  // boostDays 2 → 0.9+0.60+0.55 = 2.05
            Assert.AreEqual(2, storm.BoostDaysTotal);
            Assert.AreEqual(2.05f, storm.LastRise, 1e-3f, "★ ยิ่ง boost นาน พายุยิ่งก้าวร้าว");
        }

        [Test]
        public void IgnitionAndZoneBBonuses()
        {
            storm.TickDay(80f, false, true);
            // 0.9 + 0 + (0.8×1.1=0.88) + 2.0 (core≥80) + 0.8 (zoneB) = 4.58
            Assert.AreEqual(4.58f, storm.LastRise, 1e-3f);
        }

        [Test]
        public void TriggersAtMax_OnceOnly()
        {
            int fired = 0;
            EventManager.Instance.OnStormTriggered += () => fired++;

            int guard = 0;
            while (!storm.IsStormActive && guard++ < 500) storm.TickDay(90f, true, true);

            Assert.IsTrue(storm.IsStormActive, "แรงดันถึง 100 → พายุมา");
            Assert.AreEqual(cfg.stormPressureMax, storm.Pressure, 1e-3f, "clamp ที่ 100");
            Assert.AreEqual(1, fired, "ยิง event ครั้งเดียว");

            storm.TickDay(90f, true, true);
            Assert.AreEqual(1, fired, "พายุมาแล้วไม่ยิงซ้ำ");
        }

        [Test]
        public void SensorPredictsArrival()
        {
            storm.TickDay(50f, false, false); // pressure 1.45, lastRise 1.45
            int eta = SensorArray.PredictedDaysUntilStorm(storm);
            Assert.AreEqual(Mathf.CeilToInt((cfg.stormPressureMax - storm.Pressure) / storm.LastRise), eta);
            Assert.Greater(eta, 0, "ยังไม่มา → มีวันคาดการณ์");
        }
    }
}
