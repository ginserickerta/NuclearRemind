using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// ResearchLab_Spec §3 — โครงการวิจัย 3 อันของห้องวิจัย (ResearchManager):
    /// gate ห้องวิจัย+ของครบ, รักษาป่วย ≤N (ยาไอโซโทป), restore จากเซฟ,
    /// และ CORE TOWER ต้องรอวิจัย "core_tower" ก่อนปลด (Day 11 อย่างเดียวไม่พอเมื่อมี ResearchManager)
    /// </summary>
    public class ResearchManagerTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private ResourceManager resources;
        private ResearchManager research;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
            resources = NewComponent<ResourceManager>("ResourceManager");
            research = NewComponent<ResearchManager>("ResearchManager");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go.AddComponent<T>();
        }

        // ── gate พื้นฐาน: ไม่มีห้องวิจัยในเมือง → วิจัยไม่ได้ + แจ้งเหตุผล + ไม่หักของ ──
        [Test]
        public void Research_WithoutLab_BlockedWithNotice()
        {
            float ironBefore = resources.Current.iron;
            string notice = null;
            eventManager.OnNotice += m => notice = m;

            eventManager.RaiseResearchRequested(ResearchManager.ProjectSeeds);

            Assert.IsFalse(research.SeedsDone, "ไม่มีห้องวิจัย → วิจัยต้องไม่สำเร็จ");
            Assert.IsNotNull(notice, "วิจัยไม่ได้ต้องแจ้งเหตุผล (toast)");
            StringAssert.Contains("ห้องวิจัย", notice);
            Assert.AreEqual(ironBefore, resources.Current.iron, 1e-4f, "ถูกบล็อก → ห้ามหักเหล็ก");
        }

        [Test]
        public void CanResearch_UnknownProject_False()
        {
            Assert.IsFalse(research.CanResearch("no_such_project", out string reason));
            StringAssert.Contains("ไม่รู้จัก", reason);
        }

        // ── restore จากเซฟ (SaveData.research* → สถานะกลับครบ) ──
        [Test]
        public void SaveLoaded_RestoresResearchFlags()
        {
            eventManager.RaiseSaveLoaded(new SaveData
            {
                researchSeedsDone = true,
                researchCoreUnlockDone = true,
                researchIsotopePending = true,
            });

            Assert.IsTrue(research.SeedsDone);
            Assert.IsTrue(research.CoreUnlockDone);
            Assert.IsTrue(research.IsotopePending);
            Assert.IsFalse(research.IsotopeDone, "ยัง pending — ไม่ใช่ done");
            Assert.IsTrue(research.IsDone(ResearchManager.ProjectSeeds));
            Assert.IsTrue(research.IsDone(ResearchManager.ProjectCoreTower));
        }

        // ── ยาไอโซโทป: รักษาป่วยสูงสุด N (OnPopulationSickCured → PopulationManager) ──
        [Test]
        public void SickCured_ReducesSick_UpToCount()
        {
            var population = NewComponent<PopulationManager>("PopulationManager");
            eventManager.RaisePopulationSickSet(10);
            Assert.AreEqual(10, population.Current.sick);

            eventManager.RaisePopulationSickCured(3);
            Assert.AreEqual(7, population.Current.sick, "รักษา 3 จาก 10 → เหลือ 7");

            eventManager.RaisePopulationSickCured(15);
            Assert.AreEqual(0, population.Current.sick, "รักษาเกินจำนวนป่วย → clamp ที่ 0");
        }

        // ── CORE TOWER: มี ResearchManager แต่ยังไม่วิจัย → Day 11 ไม่ปลด · วิจัยแล้ว → ปลด ──
        [Test]
        public void CoreTower_Day11_WaitsForCoreResearch()
        {
            var tower = NewComponent<CoreTowerManager>("CoreTowerManager");

            eventManager.RaiseDayStarted(CoreTowerManager.UnlockDay, false);
            Assert.IsFalse(tower.Current.isUnlocked, "ยังไม่วิจัย core_tower → Day 11 ต้องไม่ปลดเตา");

            // วิจัยแล้ว (ผ่านเซฟ — เลี่ยง dependency GameManager/ห้องวิจัยจริง)
            eventManager.RaiseSaveLoaded(new SaveData { researchCoreUnlockDone = true });
            eventManager.RaiseDayStarted(CoreTowerManager.UnlockDay, false);
            Assert.IsTrue(tower.Current.isUnlocked, "วิจัยแล้ว + ถึงวัน → ปลดเตา");
        }

        // ── เมล็ดพันธุ์: วิจัยสำเร็จ → CrisisEffectManager.FoodYieldMultiplier เพิ่มตามโบนัส ──
        [Test]
        public void SeedsCompleted_RaisesFoodYieldMultiplier()
        {
            var crisis = NewComponent<CrisisEffectManager>("CrisisEffectManager");
            float before = crisis.FoodYieldMultiplier;

            eventManager.RaiseResearchCompleted(ResearchManager.ProjectSeeds);

            Assert.AreEqual(before + research.seedsFoodYieldBonus, crisis.FoodYieldMultiplier, 1e-4f,
                "วิจัยเมล็ดพันธุ์ → ผลผลิตอาหารคูณเพิ่มตาม seedsFoodYieldBonus (+100%)");
        }
    }
}
