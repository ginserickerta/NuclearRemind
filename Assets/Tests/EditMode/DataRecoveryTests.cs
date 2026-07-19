using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        /// <summary>
        /// EditMode does not reload the script domain between runs, so MonoBehaviour singletons another
        /// test class created can still be alive in here. That is not hypothetical for this suite: a live
        /// ResourceManager holding 0 iron makes ResearchLab.StartRepair bail (it charges repairIron), and
        /// every test that repairs the lab then fails on the "precondition: repaired" assert - for reasons
        /// that have nothing to do with data recovery. Start from a clean scene instead.
        /// </summary>
        private static void DestroyStray<T>() where T : MonoBehaviour
        {
            foreach (var o in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (o != null) Object.DestroyImmediate(o.gameObject);
        }

        /// <summary>
        /// EditMode does not call Awake/OnEnable for AddComponent, so the `Instance` singletons stay null.
        /// ResearchLab.TickRepair reads WorkerManager.Instance (not our local reference) to count the crew,
        /// so without this the lab could never be repaired and every test here that needs a working lab
        /// failed on the precondition. Same idiom as ResearchManagerTests / ResourceManagerTests.
        /// </summary>
        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var c = go.AddComponent<T>();
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

        [SetUp]
        public void SetUp()
        {
            DestroyStray<DataRecovery>();
            DestroyStray<ResearchLab>();
            DestroyStray<WorkerManager>();
            DestroyStray<ResourceManager>();
            DestroyStray<EventManager>();

            cfg = ScriptableObject.CreateInstance<GameConfigSO>();
            _spawned.Add(cfg);
            GameConfigSO.OverrideForTest(cfg);
            KnowledgeDB.ResetForTest();

            NewComponent<EventManager>("EventManager");

            wm = NewComponent<WorkerManager>("WorkerManager"); wm.Initialize(cfg);
            lab = NewComponent<ResearchLab>("ResearchLab"); lab.Initialize(cfg);

            dr = NewComponent<DataRecovery>("DataRecovery"); dr.Initialize(cfg);
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

            // TickRepair counts the crew through WorkerManager.Instance, so this also asserts that the
            // singleton wiring survived SetUp - it is what broke here before NewComponent existed.
            Assert.IsFalse(lab.IsRuined,
                $"precondition: repaired (paid={lab.RepairPaid} progress={lab.RepairProgress}/{cfg.repairDays})");

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
