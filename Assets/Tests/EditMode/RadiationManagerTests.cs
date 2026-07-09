using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// ระบบ Zone A / โรครังสี (Story Guide §4) — RadiationManager สะสม exposure + StatCondition "exposure_above_"
    /// ตรวจ: สูตรรังสีต่อวัน (แหล่ง − ป้องกัน, clamp 0) · เงื่อนไข exposure_above_ · backward-compat (ไม่ส่ง exposure)
    /// </summary>
    public class RadiationManagerTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            // EventManager ต้องมาก่อน — RadiationManager.OnEnable subscribe event ตอน AddComponent
            NewComponent<EventManager>("EventManager");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
            _spawned.Clear();
        }

        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go.AddComponent<T>();
        }

        // ── สูตรรังสีต่อวัน (default tuning: Mine 3 · เตา 2 · HEAT≥80 +3 · Shelter −6 · Medic −2) ──

        [Test]
        public void DailyExposure_MineAndReactor_Accumulates()
        {
            var rm = NewComponent<RadiationManager>("RadiationManager");
            // 1 Mine (3) + เตา (2) + HEAT ต่ำ (0) − ไม่มีป้องกัน = 5
            Assert.AreEqual(5f, rm.ComputeDailyExposure(1, 0, 0, 0f), 0.01f);
        }

        [Test]
        public void DailyExposure_HighHeat_AddsBonus()
        {
            var rm = NewComponent<RadiationManager>("RadiationManager");
            float cool = rm.ComputeDailyExposure(2, 0, 0, 0f);   // HEAT ต่ำ
            float hot  = rm.ComputeDailyExposure(2, 0, 0, 90f);  // HEAT ≥ 80 → +3
            Assert.Greater(hot, cool, "HEAT สูงต้องเพิ่ม exposure");
            Assert.AreEqual(cool + 3f, hot, 0.01f);
        }

        [Test]
        public void DailyExposure_ShelterAndMedic_MitigatePerAlara()
        {
            var rm = NewComponent<RadiationManager>("RadiationManager");
            // 3 Mine (9) + เตา (2) = 11 · − Shelter (6) − Medic (2) = 3
            Assert.AreEqual(11f, rm.ComputeDailyExposure(3, 0, 0, 0f), 0.01f);
            Assert.AreEqual(5f, rm.ComputeDailyExposure(3, 1, 0, 0f), 0.01f);
            Assert.AreEqual(3f, rm.ComputeDailyExposure(3, 1, 1, 0f), 0.01f);
        }

        [Test]
        public void DailyExposure_ClampedAtZero_WhenWellProtected()
        {
            var rm = NewComponent<RadiationManager>("RadiationManager");
            // เตา (2) เท่านั้น − Shelter (6) = ติดลบ → clamp 0 (ผู้เล่นทำตาม ALARA เลี่ยงวิกฤตได้)
            Assert.AreEqual(0f, rm.ComputeDailyExposure(0, 1, 0, 0f), 0.01f);
            Assert.AreEqual(0f, rm.ComputeDailyExposure(1, 2, 3, 0f), 0.01f);
        }

        [Test]
        public void DailyExposure_Hospital_MitigatesLikeShelter()
        {
            var rm = NewComponent<RadiationManager>("RadiationManager");
            // 3 Mine (9) + เตา (2) = 11 · − Hospital ประจำครบ 1 แห่ง (6) = 5 (GDD §6)
            Assert.AreEqual(11f, rm.ComputeDailyExposure(3, 0, 0, 0f, hospitals: 0), 0.01f);
            Assert.AreEqual(5f, rm.ComputeDailyExposure(3, 0, 0, 0f, hospitals: 1), 0.01f);
        }

        // ── StatCondition "exposure_above_" (เงื่อนไข trigger ของ beat crisis_radiation_disease) ──

        [Test]
        public void StatCondition_ExposureAbove_MatchesThreshold()
        {
            Assert.IsTrue(StatCondition.Matches("exposure_above_60", 5, default, default, 70f), "70 ≥ 60 ต้อง true");
            Assert.IsFalse(StatCondition.Matches("exposure_above_60", 5, default, default, 50f), "50 < 60 ต้อง false");
        }

        [Test]
        public void StatCondition_ExposureOrDayFallback_FiresOnEither()
        {
            // เงื่อนไขจริงของ beat crisis_radiation_disease (GDD §10 + เพดานวัน §14 = Day 20)
            string cond = CrisisSchedule.OutbreakTrigger;
            Assert.IsFalse(StatCondition.Matches(cond, 10, default, default, 30f), "exposure 30<60 + day 10<20 → false");
            Assert.IsTrue(StatCondition.Matches(cond, 10, default, default, 80f), "exposure 80≥60 → true (มาเร็วถ้าประมาท)");
            Assert.IsTrue(StatCondition.Matches(cond, CrisisSchedule.OutbreakDay, default, default, 30f),
                "day 20 → true (เพดานวันกันพลาดเนื้อหา)");
        }

        [Test]
        public void StatCondition_ExposureAbove_DefaultsFalse_ForLegacyCallers()
        {
            // caller เดิมที่ไม่ส่ง radiationExposure → default 0 → exposure_above_* เป็น false เสมอ (backward-compat)
            Assert.IsFalse(StatCondition.Matches("exposure_above_60", 5, default, default),
                "ไม่ส่ง exposure (เซ็นเจอร์เดิม 4 อาร์กิวเมนต์) ต้องไม่ยิง");
        }
    }
}
