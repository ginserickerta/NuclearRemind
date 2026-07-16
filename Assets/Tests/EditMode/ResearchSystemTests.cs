using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// GDD v6.3 §19 — Research Queue: ruined lab → repair, pay-once (bug #2),
    /// re-staff floors (bug #9), lead_map coverage (bug #11), soft triggers bound to core,
    /// building gate (no note → no Extractor), completion → knowledge + hope.
    /// </summary>
    public class ResearchSystemTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private GameConfigSO cfg;
        private EventManager events;
        private WorkerManager wm;
        private ResearchLab lab;

        [SetUp]
        public void SetUp()
        {
            cfg = ScriptableObject.CreateInstance<GameConfigSO>();
            _spawned.Add(cfg);
            GameConfigSO.OverrideForTest(cfg);
            KnowledgeDB.ResetForTest();
            KnowledgeDB.Instance.RegisterNotes(BuildCatalog());

            // EventManager first — managers' OnEnable subscribe to it; tests assert on its events
            var evGo = new GameObject("EventManager");
            _spawned.Add(evGo);
            events = evGo.AddComponent<EventManager>();

            var wmGo = new GameObject("WorkerManager");
            _spawned.Add(wmGo);
            wm = wmGo.AddComponent<WorkerManager>();
            wm.Initialize(cfg);

            var labGo = new GameObject("ResearchLab");
            _spawned.Add(labGo);
            lab = labGo.AddComponent<ResearchLab>();
            lab.Initialize(cfg);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) Object.DestroyImmediate(o);
            GameConfigSO.OverrideForTest(null);
            KnowledgeDB.ResetForTest();
        }

        // catalog ในหน่วยความจำ (mirror ResearchNotesSetup — เทสต์ไม่พึ่ง asset ใน Resources)
        private static List<ResearchNoteSO> BuildCatalog()
        {
            ResearchNoteSO Make(string id, string lead, int slots, int days, int p, int fe, int lab, string[] prereq, string[] blds)
            {
                var n = ScriptableObject.CreateInstance<ResearchNoteSO>();
                n.noteId = id; n.title = id; n.requiredLead = lead;
                n.researcherSlots = slots; n.daysRequired = days;
                n.costPower = p; n.costIron = fe; n.costLabMat = lab;
                n.prerequisiteNotes = prereq ?? new string[0];
                n.unlocksBuildings = blds ?? new string[0];
                n.completionSpeaker = "KOVA"; n.completionLine = "done";
                n.knowledgeBody = "body";
                return n;
            }
            return new List<ResearchNoteSO>
            {
                Make("deuterium", "water_analysis", 2, 2, 80, 0, 0, null, new[] { "deuterium_extractor" }),
                Make("confinement", "magnetic_theory", 3, 2, 40, 60, 0, null, new[] { "toroidal_coil", "poloidal_coil" }),
                Make("nuclear_medicine", "radiation_biology", 2, 3, 0, 0, 60, null, new[] { "med_bay", "rad_suit" }),
                Make("irradiation", "food_preservation", 3, 2, 120, 0, 0, null, new[] { "co60_chamber", "mutation_lab" }),
                Make("tritium", "lithium_breeding", 4, 2, 140, 60, 0, new[] { "deuterium" }, new[] { "zone_b" }),
                Make("storm_detection", "storm_detection", 2, 2, 60, 0, 30, null, new[] { "sensor_array" }),
                Make("food_logistics", "food_logistics", 2, 2, 0, 40, 0, null, new[] { "granary" }),
                Make("shift_management", "shift_management", 2, 2, 0, 50, 0, null, new[] { "barracks" }),
            };
        }

        private void RepairLab()
        {
            lab.StartRepair();                  // pay iron (no ResourceManager in test → free)
            foreach (var w in wm.Workers.Take(cfg.repairWorkers)) wm.AssignJob(w, WorkerJobs.Lab);
            for (int i = 0; i < cfg.repairDays; i++) lab.TickDay();
            Assert.IsFalse(lab.IsRuined, "precondition: lab repaired");
        }

        // ── bug #11: lead_map ครบ 8 ใบ ─────────────────────────────
        [Test]
        public void LeadMap_CoversAll8Notes_Bug11()
        {
            var noteIds = KnowledgeDB.Instance.AllNotes.Select(n => n.noteId).ToHashSet();
            Assert.AreEqual(8, noteIds.Count, "ต้องมี 8 note");

            var mappedNotes = KnowledgeDB.LeadMap.Values.ToHashSet();
            foreach (var id in noteIds)
                Assert.Contains(id, mappedNotes.ToList(),
                    $"note '{id}' ไม่มีใน lead_map → ถูกข้ามตลอดเกม (บั๊ก #11)");

            Assert.IsEmpty(KnowledgeDB.Instance.ValidateLeadMap(),
                "ทุก note ต้องมี requiredLead ที่ map กลับหาตัวเองได้");
        }

        // ── ★ Acceptance: ติดขัดถ้าไม่วิจัย ────────────────────────
        // เช็กผ่าน BuildingData.requiredNoteId — เส้นทางเดียวกับที่ PlacementController.IsResearchUnlocked
        // เรียกจริง (KnowledgeDB.IsBuildingUnlocked) → test พิสูจน์ gate ที่ shipped ตรงเป๊ะ
        [Test]
        public void Building_LockedUntilNoteResearched()
        {
            var db = KnowledgeDB.Instance;
            var extractor = MakeBuilding("deuterium");   // Extractor ← ต้องวิจัย deuterium
            var zoneB = MakeBuilding("tritium");         // Zone B ← ต้องวิจัย tritium
            var powerPlant = MakeBuilding("");           // โรงไฟพื้นฐาน — ไม่ผูกวิจัย
            _spawned.Add(extractor); _spawned.Add(zoneB); _spawned.Add(powerPlant);

            Assert.IsFalse(db.IsBuildingUnlocked(extractor),
                "ยังไม่วิจัย deuterium → สร้าง Extractor ไม่ได้ (แก่นของ Sprint 2)");
            Assert.IsFalse(db.IsBuildingUnlocked(zoneB), "ยังไม่วิจัย tritium → Zone B ล็อก");
            Assert.IsTrue(db.IsBuildingUnlocked(powerPlant),
                "อาคารพื้นฐาน requiredNoteId = \"\" → วางได้เสมอ");
            Assert.IsTrue(db.IsBuildingUnlocked((BuildingData)null), "data null → ไม่บล็อก (scene/test เก่า)");

            db.CompleteNote("deuterium");
            Assert.IsTrue(db.IsBuildingUnlocked(extractor), "วิจัยเสร็จ → ปลดล็อก");
            Assert.IsFalse(db.IsBuildingUnlocked(zoneB), "note อื่นเสร็จไม่ปลด Zone B");
        }

        private static BuildingData MakeBuilding(string requiredNoteId)
        {
            var b = ScriptableObject.CreateInstance<BuildingData>();
            b.requiredNoteId = requiredNoteId;
            return b;
        }

        // ── Researchable gate: lead + prerequisites ────────────────
        [Test]
        public void Researchable_RequiresLeadAndPrerequisites()
        {
            var db = KnowledgeDB.Instance;
            var deut = db.GetNote("deuterium");
            var trit = db.GetNote("tritium");

            Assert.IsFalse(db.IsResearchable(deut), "ยังไม่มี lead → วิจัยไม่ได้");
            db.UnlockLead("water_analysis");
            Assert.IsTrue(db.IsResearchable(deut), "มี lead → วิจัยได้");

            db.UnlockLead("lithium_breeding");
            Assert.IsFalse(db.IsResearchable(trit), "tritium ต้องมี prereq deuterium ก่อน");
            db.CompleteNote("deuterium");
            Assert.IsTrue(db.IsResearchable(trit), "ครบ prereq → วิจัย tritium ได้");
        }

        // ── ResearchLab เริ่มเป็นซาก + ซ่อม ────────────────────────
        [Test]
        public void Lab_StartsRuined_RepairsWith2WorkersOver2Days()
        {
            Assert.IsTrue(lab.IsRuined, "★ เริ่มเกมเป็นซาก");

            KnowledgeDB.Instance.UnlockLead("water_analysis");
            Assert.IsFalse(lab.TryStartResearch("deuterium"), "ซากอยู่ → วิจัยไม่ได้");

            lab.StartRepair();
            wm.AssignJob(wm.Workers[0], WorkerJobs.Lab); // มีคนซ่อมแค่ 1 < 2
            lab.TickDay();
            Assert.IsTrue(lab.IsRuined, "คนไม่ครบ 2 → ซ่อมไม่คืบ");

            wm.AssignJob(wm.Workers[1], WorkerJobs.Lab); // ครบ 2
            lab.TickDay();
            lab.TickDay();
            Assert.IsFalse(lab.IsRuined, "2 คน × 2 วัน → ซ่อมเสร็จ");
        }

        // ── bug #2: จ่ายค่าวิจัยครั้งเดียว ─────────────────────────
        [Test]
        public void Research_PaysCostOnce_NotPerDay_Bug2()
        {
            RepairLab();
            KnowledgeDB.Instance.UnlockLead("water_analysis");

            int started = 0;
            EventManager.Instance.OnResearchNoteStarted += _ => started++;
            var paid = new List<(ResourceType, float)>();
            EventManager.Instance.OnResourceDelta += (t, a) => paid.Add((t, a));

            Assert.IsTrue(lab.TryStartResearch("deuterium"));
            int payAtStart = paid.Count;
            Assert.AreEqual(1, payAtStart, "จ่ายครั้งเดียวตอนเริ่ม (P80) — ไม่ใช่ทุกวัน");

            // เดินหลายวันจนวิจัยเสร็จ — ต้องไม่มีการหักซ้ำ
            for (int i = 0; i < 5; i++) lab.TickDay();
            Assert.AreEqual(payAtStart, paid.Count, "★ บั๊ก #2: ห้ามหักค่าวิจัยซ้ำรายวัน");
            Assert.AreEqual(1, started);
        }

        // ── bug #9: re-staff floor farm/water 2, mine 1 ────────────
        [Test]
        public void ReStaff_NeverDrainsFarmWaterBelow2_MineBelow1_Bug9()
        {
            RepairLab();
            KnowledgeDB.Instance.UnlockLead("lithium_breeding");
            KnowledgeDB.Instance.CompleteNote("deuterium");

            // จัดคน: farm 3, water 3, mine 2 → tritium ต้องการ 4 นักวิจัย
            wm.AssignJobCounts(new Dictionary<string, int>
            {
                { WorkerJobs.Farm, 3 }, { WorkerJobs.Water, 3 }, { WorkerJobs.Mine, 2 },
            });
            Assert.IsTrue(lab.TryStartResearch("tritium"));

            for (int i = 0; i < 3; i++) lab.ReStaffForResearch(wm);

            Assert.GreaterOrEqual(wm.GetWorkers(WorkerJobs.Farm).Count, cfg.restaffFloorFarm,
                "★ ห้ามดึงชาวนาต่ำกว่า 2 (บั๊ก #9: อาหารหมด D22)");
            Assert.GreaterOrEqual(wm.GetWorkers(WorkerJobs.Water).Count, cfg.restaffFloorWater,
                "ห้ามดึงคนน้ำต่ำกว่า 2");
            Assert.GreaterOrEqual(wm.GetWorkers(WorkerJobs.Mine).Count, cfg.restaffFloorMine,
                "ห้ามดึงคนเหมืองต่ำกว่า 1");
        }

        [Test]
        public void ReStaff_PullsIdleFirst()
        {
            RepairLab();
            KnowledgeDB.Instance.UnlockLead("water_analysis");
            wm.AssignJobCounts(new Dictionary<string, int> { { WorkerJobs.Mine, 4 } });
            // เหลือ idle 10 คน — deuterium ต้องการ 2 นักวิจัย
            Assert.IsTrue(lab.TryStartResearch("deuterium"));
            lab.ReStaffForResearch(wm);
            Assert.AreEqual(4, wm.GetWorkers(WorkerJobs.Mine).Count, "มี idle พอ → ไม่แตะเหมือง");
            Assert.GreaterOrEqual(wm.GetWorkers(WorkerJobs.Lab).Count, 2);
        }

        // ── progress: จบแล้วปลด note + Hope +6 ─────────────────────
        [Test]
        public void Research_Completes_UnlocksNote_ReportsHope6()
        {
            RepairLab();
            KnowledgeDB.Instance.UnlockLead("water_analysis");
            wm.AssignJobCounts(new Dictionary<string, int> { { WorkerJobs.Lab, 2 } });

            string completedId = null;
            EventManager.Instance.OnResearchNoteCompleted += id => completedId = id;

            Assert.IsTrue(lab.TryStartResearch("deuterium"));
            float hopeBefore = wm.Hope.Current;

            // 2 คนเต็ม eff, slots 2 → staffRatio 1 → progress +1/วัน · daysRequired 2
            lab.TickDay();
            Assert.IsFalse(KnowledgeDB.Instance.HasNote("deuterium"), "วันเดียวยังไม่เสร็จ");
            lab.TickDay();

            Assert.IsTrue(KnowledgeDB.Instance.HasNote("deuterium"), "2 วัน → เสร็จ");
            Assert.AreEqual("deuterium", completedId, "ยิง event ให้ NoteCardPopup");
            var hopeEntry = wm.Hope.GetTodayBreakdown().FirstOrDefault(e => e.sourceKey == "research.complete");
            Assert.AreEqual(cfg.hopeResearchComplete, hopeEntry.value, 1e-3f, "วิจัยสำเร็จ → Hope +6");
        }

        [Test]
        public void Research_UnderstaffedBelowHalf_DoesNotProgress()
        {
            RepairLab();
            KnowledgeDB.Instance.UnlockLead("lithium_breeding");
            KnowledgeDB.Instance.CompleteNote("deuterium");
            // tritium ต้องการ 4 · ให้แค่ 1 คน (ratio 0.25 < 0.5)
            // ★ ต้องไม่มี idle เหลือ + donor ทุกสายอยู่ที่ floor → re-staff ดึงเพิ่มไม่ได้
            //   (power ไม่ใช่ donor — พักคนที่เหลือไว้ตรงนั้น 8 คน)
            wm.AssignJobCounts(new Dictionary<string, int>
            {
                { WorkerJobs.Lab, 1 }, { WorkerJobs.Farm, 2 }, { WorkerJobs.Water, 2 },
                { WorkerJobs.Mine, 1 }, { WorkerJobs.Power, 8 },
            });
            Assert.AreEqual(0, wm.GetWorkers(WorkerJobs.Idle).Count, "precondition: ไม่มี idle");
            Assert.IsTrue(lab.TryStartResearch("tritium"));

            lab.TickDay(); // re-staff ดึงไม่ได้ (idle 0 · donor ที่ floor) → lab ค้าง 1 → ratio 0.25
            Assert.AreEqual(1, wm.GetWorkers(WorkerJobs.Lab).Count, "re-staff ดึงเพิ่มไม่ได้");
            Assert.Less(lab.ActiveJob?.progress ?? 99f, 0.01f, "staffRatio < 0.5 → progress ไม่เดินเลย");
        }

        // ── S7/S8 preempt: ทิ้งงานวิจัยเมื่อวิกฤตคน ─────────────────
        [Test]
        public void EmergencyPreempt_HungryCrisis_DropsActiveJob()
        {
            RepairLab();
            KnowledgeDB.Instance.UnlockLead("water_analysis");
            wm.AssignJobCounts(new Dictionary<string, int> { { WorkerJobs.Lab, 2 } });
            Assert.IsTrue(lab.TryStartResearch("deuterium"));

            // ทำให้หิว 3 คน (hunger > 55)
            for (int i = 0; i < 3; i++) wm.Workers[i].hunger = 60f;
            // RecalcStatus ทำงานใน RunDailyTick — จำลองด้วยการ tick worker วันอด
            wm.RunDailyTick(new WorkerTickContext { foodStock = 0f, rng = new System.Random(1) });
            Assert.GreaterOrEqual(wm.HungryCount, cfg.softHungryCount);

            lab.TickDay();
            Assert.IsNull(lab.ActiveJob, "★ S7: วิกฤตคนหิว → ทิ้งงานวิจัย (progress หาย = ราคาที่จ่าย)");
        }

        // ── Soft trigger ผูก core ไม่ใช่ day ───────────────────────
        [Test]
        public void SoftTrigger_LithiumUnlocksAtCore45_NotByDay()
        {
            var watcher = new SoftTriggerWatcher(cfg);
            var db = KnowledgeDB.Instance;

            watcher.Evaluate(new SoftTriggerState { core = 44f, fuel = 10f }, db);
            Assert.IsFalse(db.HasLead("lithium_breeding"), "core 44 → ยังไม่ปลด");

            watcher.Evaluate(new SoftTriggerState { core = 45f, fuel = 10f }, db);
            Assert.IsTrue(db.HasLead("lithium_breeding"), "core >= 45 → ปลด (ผูก state ไม่ใช่ day)");
        }

        [Test]
        public void SoftTrigger_HeatThreshold_SlidesWithCore()
        {
            var db = KnowledgeDB.Instance;

            // core 0 → เกณฑ์ 35 · core 100 → เกณฑ์ 25
            var low = new SoftTriggerWatcher(cfg);
            low.Evaluate(new SoftTriggerState { core = 0f, heat = 30f, fuel = 10f }, db);
            Assert.IsFalse(db.HasLead("magnetic_theory"), "core 0: heat 30 < เกณฑ์ 35 → ยังไม่ปลด");

            KnowledgeDB.ResetForTest();
            KnowledgeDB.Instance.RegisterNotes(BuildCatalog());
            var hi = new SoftTriggerWatcher(cfg);
            hi.Evaluate(new SoftTriggerState { core = 100f, heat = 30f, fuel = 10f }, KnowledgeDB.Instance);
            Assert.IsTrue(KnowledgeDB.Instance.HasLead("magnetic_theory"),
                "core 100: เกณฑ์ลดเหลือ 25 → heat 30 ปลดได้ (เล่นเก่ง = ได้ Lead เร็ว)");
        }

        [Test]
        public void SoftTrigger_S7S8_FireOnCounts()
        {
            var watcher = new SoftTriggerWatcher(cfg);
            var db = KnowledgeDB.Instance;
            watcher.Evaluate(new SoftTriggerState { core = 50f, fuel = 10f, hungryCount = 3, exhaustedCount = 3 }, db);
            Assert.IsTrue(db.HasLead("food_logistics"), "hungry >= 3 → S7");
            Assert.IsTrue(db.HasLead("shift_management"), "exhausted >= 3 → S8");
        }

        [Test]
        public void Lead_UnlocksOncePerGame()
        {
            var db = KnowledgeDB.Instance;
            Assert.IsTrue(db.UnlockLead("water_analysis"), "ครั้งแรก true");
            Assert.IsFalse(db.UnlockLead("water_analysis"), "ครั้งสอง false (ปลดครั้งเดียว)");
        }
    }
}
