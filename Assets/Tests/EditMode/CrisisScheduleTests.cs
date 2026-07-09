using NUnit.Framework;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// ★ วันเด้งของวิกฤต 3 ใบ ต้องตรง Victory Loop (GDD v4.0 §14): Day 17 / 20 / 24 · พายุ Day 25
    /// ใช้สตริงเงื่อนไขชุดเดียวกับที่ StorySetup ยัดใส่ StoryBeat (CrisisSchedule) → เทสต์คุมของจริง
    ///
    /// จำลองเตาแบบเดินปกติ: ปลดล็อก Day 11 ที่ CORE% 30 · เฟส 1 ล็อกโหมด Normal → +3%/วัน (§8, §18)
    /// </summary>
    public class CrisisScheduleTests
    {
        // CORE% ตอน "จบวัน" ที่ day (เตายังไม่ปลดล็อกก่อน Day 11) — ตรงกับ CoreTowerManager.AdvanceTurn เดินโหมด Normal
        private static TowerData NormalReactor(int day)
        {
            if (day < CoreTowerManager.UnlockDay) return default;
            return new TowerData
            {
                isUnlocked = true,
                corePercent = CoreTowerManager.StartPercent + 3f * (day - (CoreTowerManager.UnlockDay - 1)),
                coreHeat = 0f,
            };
        }

        private static bool Fires(string trigger, int day, ResourceData res = default, float exposure = 0f)
            => StatCondition.Matches(trigger, day, res, NormalReactor(day), exposure);

        // ── วิกฤต 1 · เสถียรภาพพลาสมา → Day 17 ──

        [Test]
        public void Plasma_FiresExactlyOnDay17_WhenPlayingNormally()
        {
            for (int day = CoreTowerManager.UnlockDay; day < CrisisSchedule.PlasmaDay; day++)
                Assert.IsFalse(Fires(CrisisSchedule.PlasmaTrigger, day),
                    $"Day {day}: CORE% = {NormalReactor(day).corePercent} ยังไม่จบเฟส 1 — วิกฤตพลาสมาต้องยังไม่เด้ง");

            Assert.IsTrue(Fires(CrisisSchedule.PlasmaTrigger, CrisisSchedule.PlasmaDay),
                "Day 17: CORE% แตะ 51 (จบเฟส 1 §8) — วิกฤตพลาสมาต้องเด้งตาม §14");
        }

        [Test]
        public void Plasma_OldQCondition_FiredOnUnlockDay_Regression()
        {
            // q_above_0.3 → CORE% ≥ 30 = ค่าเริ่มต้นตอนปลดล็อก → เงื่อนไขเดิมเด้ง Day 11 (เร็วไป 6 วัน)
            Assert.IsTrue(StatCondition.Matches("q_above_0.3", 11, default, NormalReactor(11)),
                "ยืนยันบั๊กเดิม: q_above_0.3 เป็นจริงตั้งแต่เตาปลดล็อก");
            Assert.IsFalse(StatCondition.Matches("core_above_50", 11, default, NormalReactor(11)),
                "core_above_50 ต้องไม่เป็นจริงตอนปลดล็อก (CORE% 33)");
        }

        [Test]
        public void Plasma_FiresEarly_WhenReactorOverheats()
        {
            // §10: HEAT > 80 = สาเหตุจริง — เร่ง Overdrive จนร้อนก่อนกำหนดต้องเด้งได้ทันที
            var hot = new TowerData { isUnlocked = true, corePercent = 36f, coreHeat = 85f };
            Assert.IsTrue(StatCondition.Matches(CrisisSchedule.PlasmaTrigger, 13, default, hot),
                "HEAT 85 ≥ 80 → วิกฤตพลาสมาเด้งก่อนกำหนดได้ (ผู้เล่นเร่งเตาเอง)");
        }

        // ── วิกฤต 2 · โรครังสี → Day 20 ──

        [Test]
        public void Outbreak_FiresExactlyOnDay20_WhenExposureLow()
        {
            Assert.IsFalse(Fires(CrisisSchedule.OutbreakTrigger, CrisisSchedule.OutbreakDay - 1),
                "Day 19: รังสีสะสมต่ำ — ยังไม่ถึงเพดานวัน");
            Assert.IsTrue(Fires(CrisisSchedule.OutbreakTrigger, CrisisSchedule.OutbreakDay),
                "Day 20: เพดานวันตาม §14 — วิกฤตโรครังสีต้องเด้ง");
        }

        [Test]
        public void Outbreak_FiresEarly_WhenExposureHigh()
        {
            // §10: ส่งคนขุดโซนเสี่ยงมากเกินไป → รังสีสะสมทะลุ 60 ก่อน Day 20
            Assert.IsTrue(Fires(CrisisSchedule.OutbreakTrigger, 15, default, exposure: 65f),
                "รังสีสะสม 65 ≥ 60 → โรครังสีเด้งก่อนกำหนด (ผลจากการขุดหนัก/ไม่ทำตาม ALARA)");
        }

        // ── วิกฤต 3 · เสบียงเน่า → Day 24 ──

        [Test]
        public void Food_DoesNotFireWhenStockpileHitsCap_BeforeDay24()
        {
            // เพดานคลังอาหาร = 500 (ResourceManager.maxFood) ผู้เล่นชนเพดาน ~Day 12
            // เงื่อนไขเดิม food_above_500 ทำให้วิกฤต + ควิซ Q6/Q7 เด้งก่อน Q4/Q5 (สลับลำดับบทเรียน)
            var fullPantry = new ResourceData { food = 500f };
            for (int day = 12; day < CrisisSchedule.FoodDay; day++)
                Assert.IsFalse(Fires(CrisisSchedule.FoodTrigger, day, fullPantry),
                    $"Day {day}: อาหารเต็มเพดาน 500 แต่วิกฤตเสบียงต้องรอ Day {CrisisSchedule.FoodDay} ตาม §14");

            Assert.IsTrue(Fires(CrisisSchedule.FoodTrigger, CrisisSchedule.FoodDay, fullPantry),
                "Day 24: วิกฤตเสบียงต้องเด้งตาม §14");
        }

        [Test]
        public void Food_FiresOnDay24_EvenWithEmptyPantry()
        {
            Assert.IsTrue(Fires(CrisisSchedule.FoodTrigger, CrisisSchedule.FoodDay, new ResourceData { food = 0f }),
                "ผู้เล่นที่คุมอาหารต่ำก็ต้องไม่พลาดเนื้อหา/ควิซ (เพดานวัน)");
        }

        // ── ลำดับบทเรียนต้องเรียงตาม Quiz Master Map (§12): Q2,Q3 → Q4,Q5 → Q6,Q7 → Q8,Q9 ──

        [Test]
        public void CrisisOrder_MatchesVictoryLoop()
        {
            Assert.Less(CrisisSchedule.PlasmaDay, CrisisSchedule.OutbreakDay, "พลาสมา (Q2,Q3) ต้องมาก่อนโรครังสี (Q4,Q5)");
            Assert.Less(CrisisSchedule.OutbreakDay, CrisisSchedule.FoodDay, "โรครังสี (Q4,Q5) ต้องมาก่อนเสบียง (Q6,Q7)");
            Assert.Less(CrisisSchedule.FoodDay, CrisisSchedule.StormDay, "เสบียง (Q6,Q7) ต้องมาก่อนพายุ (Q8,Q9)");
            Assert.AreEqual(CoreTowerManager.StormStartDay, CrisisSchedule.StormDay, "วันพายุต้องตรงกับเตา (§8/§14)");
            Assert.Greater(CrisisSchedule.PlasmaDay, CoreTowerManager.UnlockDay, "วิกฤตเตาต้องเกิดหลังจุดเตา Day 11");
        }
    }
}
