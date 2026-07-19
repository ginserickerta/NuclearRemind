using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// ResearchLab_System_Spec — the Water Plant's deuterium button.
    ///
    /// The rule this suite exists to defend: extraction is unlocked by RESEARCH, gated by plant SIZE,
    /// and switched on by the PLAYER. All three, in that order. Before this, a plant simply extracted on
    /// its own once it hit max level — no knowledge required and no choice offered, which broke both
    /// "knowledge is the mechanism" and "no free choices".
    /// </summary>
    public class DeuteriumExtractionTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private GameConfigSO cfg;
        private BuildingRegistry registry;
        private DeuteriumExtraction dex;
        private BuildingData plant;

        private static readonly Vector2Int Cell = new Vector2Int(4, 6);

        [SetUp]
        public void SetUp()
        {
            cfg = ScriptableObject.CreateInstance<GameConfigSO>();
            _spawned.Add(cfg);
            GameConfigSO.OverrideForTest(cfg);
            KnowledgeDB.ResetForTest();

            NewComponent<EventManager>("EventManager");
            registry = NewComponent<BuildingRegistry>("BuildingRegistry");
            registry.allBuildingData = new BuildingData[0];
            registry.maxBuildingLevel = 3;
            dex = NewComponent<DeuteriumExtraction>("DeuteriumExtraction");

            // Water Plant as shipped: 4/day from Lv.2, 8/day at Lv.3
            plant = ScriptableObject.CreateInstance<BuildingData>();
            plant.buildingName = "WaterPlant";
            plant.buildingType = BuildingType.WaterPlant;
            plant.size = Vector2Int.one;
            plant.deuteriumProduction = 8f;
            plant.deuteriumMinLevel = 2;
            plant.deuteriumProductionMinLevel = 4f;
            _spawned.Add(plant);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameConfigSO.OverrideForTest(null);
            KnowledgeDB.ResetForTest();
        }

        private void Research() => KnowledgeDB.Instance.CompleteNote(DeuteriumExtraction.NoteId);

        // ─────────────────────────────────────────
        //  ★ Gate 1 — knowledge
        // ─────────────────────────────────────────

        [Test]
        public void WithoutResearch_TheSwitchRefusesToTurnOn_EvenAtMaxLevel()
        {
            Assert.IsFalse(DeuteriumExtraction.Researched);
            Assert.IsFalse(DeuteriumExtraction.Unlocked(plant, 3), "★ ระดับสูงสุดอย่างเดียวไม่พอ — ต้องวิจัยก่อน");

            dex.SetOn(Cell, true, plant, 3);

            Assert.IsFalse(dex.IsOn(Cell), "กดเปิดไม่ได้ถ้ายังไม่วิจัย");
            Assert.IsFalse(dex.IsExtracting(Cell, plant, 3));
        }

        [Test]
        public void Research_AloneDoesNotExtract_TheSwitchIsStillOff()
        {
            Research();

            Assert.IsTrue(DeuteriumExtraction.Unlocked(plant, 3), "วิจัยแล้ว + ระดับพอ → ปุ่มใช้ได้");
            Assert.IsFalse(dex.IsExtracting(Cell, plant, 3),
                "★ ปลดล็อกแล้วต้องยังไม่สกัดเอง — ผู้เล่นต้องกดเปิดเอง (ไม่มีทางเลือกฟรี)");
        }

        // ─────────────────────────────────────────
        //  ★ Gate 2 — plant size
        // ─────────────────────────────────────────

        [Test]
        public void BelowMinLevel_StaysLocked_HoweverMuchYouHaveResearched()
        {
            Research();

            Assert.AreEqual(0f, DeuteriumExtraction.RateFor(plant, 1), 1e-4f, "Lv.1 สกัดไม่ได้");
            Assert.IsFalse(DeuteriumExtraction.Unlocked(plant, 1));

            dex.SetOn(Cell, true, plant, 1);
            Assert.IsFalse(dex.IsOn(Cell), "โรงเล็กเกินไป → เปิดไม่ได้");
        }

        [Test]
        public void RateStepsUpWithLevel_Lv2Is4_Lv3Is8()
        {
            Assert.AreEqual(4f, DeuteriumExtraction.RateFor(plant, 2), 1e-4f, "Lv.2 = 4/วัน");
            Assert.AreEqual(8f, DeuteriumExtraction.RateFor(plant, 3), 1e-4f, "Lv.3 = 8/วัน (ค่าเดิม ไม่แตะ)");
        }

        [Test]
        public void Lv2_Extracts_WhichIsTheWholePointOfTheChange()
        {
            Research();
            dex.SetOn(Cell, true, plant, 2);

            Assert.IsTrue(dex.IsExtracting(Cell, plant, 2), "★ เดิมต้องรอ Lv.3 — ตอนนี้ Lv.2 สกัดได้แล้ว");
        }

        [Test]
        public void BuildingWithoutTheField_KeepsTheOldMaxLevelBehaviour()
        {
            // deuteriumMinLevel = 0 is the "unset" default, so every other building is untouched.
            var other = ScriptableObject.CreateInstance<BuildingData>();
            other.deuteriumProduction = 5f;
            _spawned.Add(other);

            Assert.AreEqual(3, DeuteriumExtraction.MinLevelFor(other), "ไม่ตั้งค่า → ใช้ระดับสูงสุดเหมือนเดิม");
            Assert.AreEqual(0f, DeuteriumExtraction.RateFor(other, 2), 1e-4f);
            Assert.AreEqual(5f, DeuteriumExtraction.RateFor(other, 3), 1e-4f);
        }

        // ─────────────────────────────────────────
        //  ★ Gate 3 — the player's switch
        // ─────────────────────────────────────────

        [Test]
        public void Toggle_TurnsExtractionOnAndOffAgain()
        {
            Research();

            Assert.IsTrue(dex.Toggle(Cell, plant, 3));
            Assert.IsTrue(dex.IsExtracting(Cell, plant, 3));

            Assert.IsFalse(dex.Toggle(Cell, plant, 3));
            Assert.IsFalse(dex.IsExtracting(Cell, plant, 3), "ปิดได้ — เก็บน้ำไว้ดื่ม/หล่อเย็นตอนวิกฤต");
        }

        [Test]
        public void EachPlantHasItsOwnSwitch()
        {
            Research();
            var other = new Vector2Int(9, 9);

            dex.SetOn(Cell, true, plant, 3);

            Assert.IsTrue(dex.IsOn(Cell));
            Assert.IsFalse(dex.IsOn(other), "สวิตช์แยกรายโรง ไม่ใช่สวิตช์รวมทั้งเมือง");
        }

        [Test]
        public void DemolishingThePlant_ClearsItsSwitch()
        {
            Research();
            dex.SetOn(Cell, true, plant, 3);

            EventManager.Instance.RaiseBuildingRemoved(Cell);

            Assert.IsFalse(dex.IsOn(Cell), "รื้อทิ้งแล้ว สวิตช์ต้องไม่ค้างไว้ให้ของที่สร้างใหม่ตรงนั้น");
        }

        [Test]
        public void LosingLevelOrResearch_StopsExtraction_WithoutLosingTheSwitchSetting()
        {
            Research();
            dex.SetOn(Cell, true, plant, 3);

            // Same switch, a plant that no longer qualifies: it must not produce.
            Assert.IsFalse(dex.IsExtracting(Cell, plant, 1), "ระดับไม่ถึง → ไม่ผลิต");
            Assert.IsTrue(dex.IsOn(Cell), "แต่ค่าที่ผู้เล่นตั้งไว้ยังอยู่ ไม่ต้องมากดใหม่ตอนอัปกลับ");
        }

        // ─────────────────────────────────────────
        //  Notice + save
        // ─────────────────────────────────────────

        [Test]
        public void FinishingTheResearch_TellsThePlayerTheButtonExists()
        {
            var notices = new List<string>();
            EventManager.Instance.OnNotice += n => notices.Add(n);

            EventManager.Instance.RaiseResearchNoteCompleted(DeuteriumExtraction.NoteId);

            Assert.AreEqual(1, notices.Count, "★ ไม่บอก = ฟีเจอร์ล่องหน ผู้เล่นไม่มีทางรู้ว่ามีปุ่ม");
            StringAssert.Contains("สกัดดิวเทอเรียม", notices[0]);

            EventManager.Instance.RaiseResearchNoteCompleted(DeuteriumExtraction.NoteId);
            Assert.AreEqual(1, notices.Count, "แจ้งครั้งเดียวพอ");
        }

        [Test]
        public void OtherResearch_DoesNotAnnounceThisButton()
        {
            var notices = new List<string>();
            EventManager.Instance.OnNotice += n => notices.Add(n);

            EventManager.Instance.RaiseResearchNoteCompleted("confinement");

            Assert.IsEmpty(notices);
        }

        [Test]
        public void SaveLoad_KeepsEverySwitchExactlyWhereThePlayerLeftIt()
        {
            Research();
            var second = new Vector2Int(9, 9);
            dex.SetOn(Cell, true, plant, 3);
            dex.SetOn(second, true, plant, 3);

            var save = new SaveData();
            dex.WriteTo(save);
            dex.ResetForNewRun();
            Assert.IsFalse(dex.IsOn(Cell), "precondition: เคลียร์แล้วจริง");

            dex.RestoreFromSave(save);

            Assert.IsTrue(dex.IsOn(Cell), "โหลดเซฟแล้วโรงที่เปิดไว้ต้องยังเปิด — ไม่งั้นหยุดผลิตเงียบๆ");
            Assert.IsTrue(dex.IsOn(second));
        }

        [Test]
        public void OldSave_WithoutTheField_LeavesEverySwitchOff()
        {
            dex.RestoreFromSave(new SaveData());
            Assert.IsFalse(dex.IsOn(Cell), "เซฟเก่าไม่มีฟิลด์นี้ → ปิดทุกโรง ไม่ใช่เปิดมั่ว");
        }

        // ── helpers ───────────────────────────────

        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var c = go.AddComponent<T>();
            // EditMode never calls Awake/OnEnable for AddComponent — invoke them so the singletons exist.
            TryInvokePrivate(c, "Awake");
            TryInvokePrivate(c, "OnEnable");
            return c;
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
