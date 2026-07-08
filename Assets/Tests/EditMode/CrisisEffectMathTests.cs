using NUnit.Framework;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// สูตร pure ของผลกระทบวิกฤต (Story Guide §4) — CrisisEffectMath
    /// deterministic ล้วน: spoil / effectiveWorkers / patientDeaths / riot · ตรวจได้โดยไม่ต้องมี scene/event
    /// </summary>
    public class CrisisEffectMathTests
    {
        // ── Spoil: อาหารเน่าต่อวัน = food × (1 − rate) ──
        [Test]
        public void Spoil_ReducesFoodByRate()
        {
            Assert.AreEqual(85f, CrisisEffectMath.Spoil(100f, 0.15f), 1e-3f, "100 เน่า 15% → 85");
            Assert.AreEqual(50f, CrisisEffectMath.Spoil(100f, 0.5f), 1e-3f);
        }

        [Test]
        public void Spoil_RateZero_IsNoOp()
        {
            Assert.AreEqual(120f, CrisisEffectMath.Spoil(120f, 0f), 1e-3f, "rate 0 = ไม่เน่า (ทางเลือก B ฉายรังสี)");
        }

        [Test]
        public void Spoil_ClampsRate_AndFloorsAtZero()
        {
            Assert.AreEqual(0f, CrisisEffectMath.Spoil(100f, 1f), 1e-3f, "rate 1 → เน่าหมด");
            Assert.AreEqual(0f, CrisisEffectMath.Spoil(100f, 2f), 1e-3f, "rate > 1 clamp เป็น 1 → 0");
            Assert.AreEqual(0f, CrisisEffectMath.Spoil(0f, 0.5f), 1e-3f, "ไม่มีอาหาร → 0");
        }

        // ── EffectiveWorkers: คนงาน − ถูกดึงชั่วคราว ──
        [Test]
        public void EffectiveWorkers_SubtractsBusy_FloorsAtZero()
        {
            Assert.AreEqual(6, CrisisEffectMath.EffectiveWorkers(10, 4));
            Assert.AreEqual(0, CrisisEffectMath.EffectiveWorkers(3, 5), "busy เกินจำนวนคน → 0 ไม่ติดลบ");
            Assert.AreEqual(10, CrisisEffectMath.EffectiveWorkers(10, 0));
        }

        // ── PatientDeaths: round(risk × max(sick, minVulnerable)) — deterministic ──
        [Test]
        public void PatientDeaths_ScalesWithSick()
        {
            Assert.AreEqual(2, CrisisEffectMath.PatientDeaths(0.2f, 10, 5), "0.2 × 10 = 2");
        }

        [Test]
        public void PatientDeaths_UsesMinVulnerable_WhenFewSick()
        {
            Assert.AreEqual(1, CrisisEffectMath.PatientDeaths(0.2f, 0, 5), "ไม่มีคนป่วยระบุ → ใช้ขั้นต่ำ 5 → 0.2 × 5 = 1");
            Assert.AreEqual(2, CrisisEffectMath.PatientDeaths(0.4f, 2, 5), "sick 2 < ขั้นต่ำ 5 → 0.4 × 5 = 2");
            Assert.AreEqual(4, CrisisEffectMath.PatientDeaths(0.5f, 8, 5), "sick 8 > ขั้นต่ำ → 0.5 × 8 = 4");
        }

        // ── Riot: จลาจลเฉพาะเมื่อ Hope < เกณฑ์ ──
        [Test]
        public void Riot_FiresOnlyBelowThreshold()
        {
            var below = CrisisEffectMath.Riot(30f, 40f, 10f, 1);
            Assert.AreEqual(-10f, below.hopeDelta, 1e-3f, "Hope 30 < 40 → จลาจล Hope −10");
            Assert.AreEqual(1, below.deaths);

            var above = CrisisEffectMath.Riot(50f, 40f, 10f, 1);
            Assert.AreEqual(0f, above.hopeDelta, 1e-3f, "Hope 50 ≥ 40 → ไม่จลาจล");
            Assert.AreEqual(0, above.deaths);
        }
    }
}
