using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// GDD v6.3 §24 / STORY.md — Data Recovery. Idle researchers decrypt Elara's logs faster; each
    /// recovered record pre-unlocks a research lead (the real power-up). A ruined lab decrypts nothing.
    /// </summary>
    public class DataRecoveryTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private GameConfigSO cfg;
        private WorkerManager wm;
        private ResearchLab lab;
        private DataRecovery dr;

        [SetUp]
        public void SetUp()
        {
            cfg = ScriptableObject.CreateInstance<GameConfigSO>();
            _spawned.Add(cfg);
            GameConfigSO.OverrideForTest(cfg);
            KnowledgeDB.ResetForTest();

            var evGo = new GameObject("EventManager"); _spawned.Add(evGo);
            evGo.AddComponent<EventManager>();

            var wmGo = new GameObject("WorkerManager"); _spawned.Add(wmGo);
            wm = wmGo.AddComponent<WorkerManager>(); wm.Initialize(cfg);

            var labGo = new GameObject("ResearchLab"); _spawned.Add(labGo);
            lab = labGo.AddComponent<ResearchLab>(); lab.Initialize(cfg);

            var drGo = new GameObject("DataRecovery"); _spawned.Add(drGo);
            dr = drGo.AddComponent<DataRecovery>(); dr.Initialize(cfg);
            dr.RegisterCatalog(BuildRecords());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) Object.DestroyImmediate(o);
            GameConfigSO.OverrideForTest(null);
            KnowledgeDB.ResetForTest();
        }

        private List<RecordCardSO> BuildRecords()
        {
            RecordCardSO R(string id, string lead)
            {
                var r = ScriptableObject.CreateInstance<RecordCardSO>();
                r.recordId = id; r.unlocksLead = lead; r.archiveTitle = id; r.bodyTH = "body";
                _spawned.Add(r);
                return r;
            }
            return new List<RecordCardSO>
            {
                R("record_01", "water_analysis"),
                R("record_02", "magnetic_theory"),
                R("record_03", "storm_detection"),
                R("record_final", "lithium_breeding"),
            };
        }

        // repair the lab and leave `labWorkers` assigned to it
        private void RepairLab(int labWorkers)
        {
            lab.StartRepair();
            for (int i = 0; i < cfg.repairWorkers; i++) wm.AssignJob(wm.Workers[i], WorkerJobs.Lab);
            for (int i = 0; i < cfg.repairDays; i++) lab.TickDay();
            Assert.IsFalse(lab.IsRuined, "precondition: repaired");

            // set desired lab headcount
            foreach (var w in wm.GetWorkers(WorkerJobs.Lab).ToList()) wm.AssignJob(w, WorkerJobs.Idle);
            for (int i = 0; i < labWorkers; i++) wm.AssignJob(wm.Workers[i], WorkerJobs.Lab);
        }

        [Test]
        public void RuinedLab_NoProgress()
        {
            Assert.IsTrue(lab.IsRuined);
            dr.TickDay();
            Assert.AreEqual(0f, dr.Progress, 1e-4f, "ซากวิจัย → ถอดรหัสไม่ได้");
            Assert.AreEqual(0, dr.RecordsRecovered);
        }

        [Test]
        public void Records_UnlockLeadsInOrder()
        {
            RepairLab(2); // 2 idle researchers → rate 14 + 20 = 34/day

            for (int i = 0; i < 3; i++) dr.TickDay();      // 34·68·102 → record_01
            Assert.AreEqual(1, dr.RecordsRecovered);
            Assert.IsTrue(KnowledgeDB.Instance.HasLead("water_analysis"), "★ record #1 → ปลด lead ล่วงหน้า");

            for (int i = 0; i < 3; i++) dr.TickDay();      // next 100 → record_02
            Assert.AreEqual(2, dr.RecordsRecovered);
            Assert.IsTrue(KnowledgeDB.Instance.HasLead("magnetic_theory"), "record #2 → magnetic_theory");
        }

        [Test]
        public void SaveLoaded_RestoresProgressAndReplaysLeads()
        {
            dr.RestoreFromSave(new SaveData { dataRecoveryProgress = 42f, dataRecoveryRecords = 3 });

            Assert.AreEqual(42f, dr.Progress, 1e-4f);
            Assert.AreEqual(3, dr.RecordsRecovered);
            Assert.AreEqual(3, dr.Recovered.Count(), "แผง Records ต้องเห็นครบ 3 ใบหลังโหลด");

            // ★ KnowledgeDB ไม่มี persistence ของตัวเอง — ถ้าไม่ replay จะเสีย Sensor Array ทั้งรอบ
            Assert.IsTrue(KnowledgeDB.Instance.HasLead("water_analysis"));
            Assert.IsTrue(KnowledgeDB.Instance.HasLead("magnetic_theory"));
            Assert.IsTrue(KnowledgeDB.Instance.HasLead("storm_detection"), "★ record #3 → Sensor Array ต้องรอด");
            Assert.IsFalse(KnowledgeDB.Instance.HasLead("lithium_breeding"), "ใบที่ 4 ยังไม่กู้ → ห้ามปลด");
        }

        [Test]
        public void SaveLoaded_OldSaveWithoutRecordFields_IsFreshRun()
        {
            Assert.DoesNotThrow(() => dr.RestoreFromSave(new SaveData()));
            Assert.AreEqual(0f, dr.Progress, 1e-4f);
            Assert.AreEqual(0, dr.RecordsRecovered, "เซฟเก่า = ยังไม่กู้ใบไหน (CLAUDE.md default rule)");
        }

        [Test]
        public void SaveLoaded_ReplayIsIdempotent_NoDoubleUnlock()
        {
            int raised = 0;
            EventManager.Instance.OnLeadUnlocked += _ => raised++;

            var save = new SaveData { dataRecoveryProgress = 10f, dataRecoveryRecords = 2 };
            dr.RestoreFromSave(save);
            dr.RestoreFromSave(save);                     // โหลดซ้ำรอบสอง

            Assert.AreEqual(2, raised, "UnlockLead idempotent → ห้าม raise ซ้ำ");
            Assert.AreEqual(2, dr.RecordsRecovered);
        }

        [Test]
        public void IdleResearchers_SpeedUpRecovery()
        {
            RepairLab(0);                                  // repaired, no lab workers → passive only
            dr.TickDay();
            Assert.AreEqual(cfg.dataRecoveryPassive, dr.Progress, 1e-4f, "0 คน → passive 14/วัน");

            dr.Initialize(cfg);                            // reset progress (lab stays repaired)
            for (int i = 0; i < 3; i++) wm.AssignJob(wm.Workers[i], WorkerJobs.Lab); // 3 idle researchers
            dr.TickDay();
            Assert.AreEqual(cfg.dataRecoveryPassive + 3 * cfg.dataRecoveryRate, dr.Progress, 1e-4f,
                "★ เผื่อนักวิจัยว่าง = ถอดรหัสเร็วกว่า");
        }

        [Test]
        public void RaisesRecordRecoveredEvent()
        {
            RepairLab(2);
            RecordCardSO got = null;
            EventManager.Instance.OnRecordRecovered += r => got = r;
            for (int i = 0; i < 3; i++) dr.TickDay();
            Assert.IsNotNull(got, "กู้สำเร็จ → ยิง event ให้การ์ดเด้ง");
            Assert.AreEqual("record_01", got.recordId);
        }
    }
}
