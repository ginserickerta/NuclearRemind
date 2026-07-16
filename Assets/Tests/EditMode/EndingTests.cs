using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// GDD v6.3 §26 / STORY.md §4 — Endings. State-driven (win/loss checked every day, never waiting
    /// for D30 — bug #15), with day 30 as the only allowed date literal. Win beats a same-day meltdown.
    /// </summary>
    public class EndingTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private GameConfigSO cfg;
        private EndingSystem end;

        [SetUp]
        public void SetUp()
        {
            cfg = ScriptableObject.CreateInstance<GameConfigSO>();
            _spawned.Add(cfg);
            GameConfigSO.OverrideForTest(cfg);

            var evGo = new GameObject("EventManager"); _spawned.Add(evGo);
            evGo.AddComponent<EventManager>();

            var eGo = new GameObject("EndingSystem"); _spawned.Add(eGo);
            end = eGo.AddComponent<EndingSystem>(); end.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) Object.DestroyImmediate(o);
            GameConfigSO.OverrideForTest(null);
        }

        private GameEndType? Eval(int day, float core, float heat, float hope)
        {
            end.Initialize();
            return end.Evaluate(day, core, heat, hope);
        }

        [Test]
        public void TrueEnding_AtCore100_AnyDay()
        {
            Assert.AreEqual(GameEndType.TrueEnding, Eval(5, 100f, 20f, 70f), "★ core 100 → ชนะทันที ไม่รอ D30 (บั๊ก #15)");
        }

        [Test]
        public void Meltdown_AtHeat100()
        {
            Assert.AreEqual(GameEndType.Meltdown, Eval(10, 60f, 100f, 70f));
        }

        [Test]
        public void HopeZero_LosesTheCity()
        {
            Assert.AreEqual(GameEndType.HopeZero, Eval(10, 60f, 20f, 0f));
        }

        [Test]
        public void Day30_NormalVsTimeout_ByCore()
        {
            Assert.AreEqual(GameEndType.NormalEnding, Eval(30, 60f, 20f, 50f), "core 50-99 → Normal");
            Assert.AreEqual(GameEndType.TimeoutLowQ, Eval(30, 40f, 20f, 50f), "core < 50 → Timeout");
        }

        [Test]
        public void ContinuesWhenNothingMet()
        {
            Assert.IsNull(Eval(10, 60f, 20f, 50f), "ยังไม่มีเงื่อนไขจบ → เล่นต่อ");
        }

        [Test]
        public void Win_BeatsSameDayMeltdown()
        {
            Assert.AreEqual(GameEndType.TrueEnding, Eval(29, 100f, 100f, 50f),
                "หอคอยเสร็จกางรับพายุ → ชนะ แม้ HEAT แตะ 100 วันเดียวกัน");
        }

        [Test]
        public void FiresOnce_RaisesGameOver()
        {
            end.Initialize();
            var got = new List<GameEndType>();
            EventManager.Instance.OnGameOver += t => got.Add(t);

            end.Evaluate(5, 100f, 20f, 70f);
            end.Evaluate(6, 100f, 20f, 70f); // already ended → no-op
            Assert.AreEqual(1, got.Count, "ยิง game-over ครั้งเดียว");
            Assert.AreEqual(GameEndType.TrueEnding, got[0]);
            Assert.IsTrue(end.Ended);
        }
    }
}
