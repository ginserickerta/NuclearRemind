using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// StoryDirector (Story Guide §1/§3):
    /// - trigger: OnDay / OnBuildingBuilt / Deuterium latch / OnStatThreshold — ยิงครั้งเดียว
    /// - ลำดับบังคับ record → info → crisis → outcome → quiz
    /// - วิกฤตผ่าน DilemmaManager (request) + ควิซรายทางเลือกหลัง afterText
    /// - โหลดเซฟ: beat ที่ยิงแล้วไม่ยิงซ้ำ + กู้แผง Records
    /// ทุกเทสต์รันแบบไม่มี Card UI (CardUIAvailable = false) → การ์ด auto-advance
    /// </summary>
    public class StoryDirectorTests
    {
        // StoryDirector is archived (Scripts/Narrative/_archive) and disabled at runtime by
        // LegacyNarrativeSilencer; it stays compiled only for SaveData and these tests. The two Ignored
        // cases below cover the card-sequencing it used to own, which CardUIController no longer serves.
        private const string RetiredBeatOrderReason =
            "v6.3 cutover: the forced record→info card sequence retired with StoryDirector. Records now " +
            "flow DataRecovery → RecordFlowBridge → RecordCardUI; crisis cards are CardManager (GDD §25).";

        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private StoryDirector director;

        [SetUp]
        public void SetUp()
        {
            StoryDirector.CardUIAvailable = false; // กัน state static รั่วข้ามเทสต์
            eventManager = NewComponent<EventManager>("EventManager");
            NewComponent<TimeManager>("TimeManager");
            director = NewComponent<StoryDirector>("StoryDirector");
        }

        [TearDown]
        public void TearDown()
        {
            StoryDirector.CardUIAvailable = false;
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        // ── helpers ────────────────────────────────────────────

        private StoryBeatSO NewBeat(string id, StoryTriggerType type, string param)
        {
            var beat = ScriptableObject.CreateInstance<StoryBeatSO>();
            beat.beatId = id;
            beat.triggerType = type;
            beat.triggerParam = param;
            _spawned.Add(beat);
            return beat;
        }

        private RecordCardSO NewRecord(string recordId)
        {
            var r = ScriptableObject.CreateInstance<RecordCardSO>();
            r.recordId = recordId;
            r.archiveTitle = $"บันทึก {recordId}";
            _spawned.Add(r);
            return r;
        }

        private InfoCardSO NewInfo(string title)
        {
            var info = ScriptableObject.CreateInstance<InfoCardSO>();
            info.title = title;
            _spawned.Add(info);
            return info;
        }

        private void SetBeats(params StoryBeatSO[] beats) => director.beats = beats;

        // ── Trigger: OnDay ────────────────────────────────────

        [Test]
        public void OnDayBeat_FiresOnMatchingDay_Once()
        {
            SetBeats(NewBeat("b_day6", StoryTriggerType.OnDay, "6"));

            eventManager.RaiseDayStarted(5, true);
            Assert.IsEmpty(director.FiredBeatIds, "ยังไม่ถึงวัน → ไม่ยิง");

            eventManager.RaiseDayStarted(6, true);
            CollectionAssert.AreEqual(new[] { "b_day6" }, director.FiredBeatIds);

            eventManager.RaiseDayStarted(6, true); // วันซ้ำ (เช่นโหลดเซฟกลางวัน)
            Assert.AreEqual(1, director.FiredBeatIds.Count, "beat ยิงครั้งเดียวต่อรอบเล่น");
        }

        // ── Trigger: OnBuildingBuilt ──────────────────────────

        [Test]
        public void BuildingBeat_FiresOnMatchingBuildingNameOnly()
        {
            SetBeats(NewBeat("b_lab", StoryTriggerType.OnBuildingBuilt, "ห้องปฏิบัติการ"));

            var farm = ScriptableObject.CreateInstance<BuildingData>();
            farm.buildingName = "ฟาร์ม";
            _spawned.Add(farm);
            eventManager.RaiseConstructionComplete(new Vector2Int(1, 1), farm);
            Assert.IsEmpty(director.FiredBeatIds, "ตึกไม่ตรงชื่อ → ไม่ยิง");

            var lab = ScriptableObject.CreateInstance<BuildingData>();
            lab.buildingName = "ห้องปฏิบัติการ";
            _spawned.Add(lab);
            eventManager.RaiseConstructionComplete(new Vector2Int(2, 2), lab);
            CollectionAssert.AreEqual(new[] { "b_lab" }, director.FiredBeatIds);
        }

        // ── logLines: daily = ปล่อยวันละบรรทัด · ไม่ daily = โชว์รวดเดียว ──

        [Test]
        public void LogLines_NotDaily_AllShownImmediately()
        {
            var beat = NewBeat("b_power", StoryTriggerType.OnDay, "2");
            beat.logLines = new[] { "[ระบบ] ไฟมา", "Kova: ดี" };
            beat.logLinesDaily = false;
            SetBeats(beat);

            var notices = new List<string>();
            eventManager.OnNotice += notices.Add;
            eventManager.RaiseDayStarted(2, true);

            Assert.Contains("[ระบบ] ไฟมา", notices, "บรรทัดแรกโชว์ทันที");
            Assert.Contains("Kova: ดี", notices, "ไม่ daily → บรรทัดถัดไปโชว์วันเดียวกัน ไม่ค้างคิว");
        }

        [Test]
        public void LogLines_Daily_SpreadOneLinePerDay()
        {
            var beat = NewBeat("b_foreshadow", StoryTriggerType.OnDay, "20");
            beat.logLines = new[] { "ลาง 1", "ลาง 2" };
            beat.logLinesDaily = true;
            SetBeats(beat);

            var notices = new List<string>();
            eventManager.OnNotice += notices.Add;

            eventManager.RaiseDayStarted(20, true);
            Assert.Contains("ลาง 1", notices);
            CollectionAssert.DoesNotContain(notices, "ลาง 2", "daily → บรรทัด 2 รอวันถัดไป");

            eventManager.RaiseDayStarted(21, true);
            Assert.Contains("ลาง 2", notices, "วันถัดไปปล่อยบรรทัดถัดไป");
        }

        // ── Trigger: Deuterium latch ──────────────────────────

        [Test]
        public void DeuteriumBeat_FiresOnFirstDeuteriumOnly()
        {
            SetBeats(NewBeat("b_deu", StoryTriggerType.OnDeuteriumExtracted, ""));

            eventManager.RaiseResourceChanged(new ResourceData { deuterium = 0f });
            Assert.IsEmpty(director.FiredBeatIds);

            eventManager.RaiseResourceChanged(new ResourceData { deuterium = 5f });
            eventManager.RaiseResourceChanged(new ResourceData { deuterium = 10f });
            Assert.AreEqual(1, director.FiredBeatIds.Count, "latch — ยิงเฉพาะครั้งแรกที่ได้ Deuterium");
        }

        // ── Trigger: OnStatThreshold (จบวัน) ───────────────────

        [Test]
        public void StatThresholdBeat_EvaluatedAtDayEnd_WithOrSyntax()
        {
            SetBeats(NewBeat("b_plasma", StoryTriggerType.OnStatThreshold, "heat_above_80|q_above_0.3"));

            eventManager.RaiseTowerProgressChanged(new TowerData { coreHeat = 50f, corePercent = 10f });
            eventManager.RaiseDayEnded(17);
            Assert.IsEmpty(director.FiredBeatIds, "ยังไม่เข้าเงื่อนไขไหนเลย");

            eventManager.RaiseTowerProgressChanged(new TowerData { coreHeat = 50f, corePercent = 35f }); // Q 0.35
            eventManager.RaiseDayEnded(18);
            CollectionAssert.AreEqual(new[] { "b_plasma" }, director.FiredBeatIds, "เข้า OR ขาใดขาหนึ่ง → ยิง");
        }

        // ── ลำดับการเล่น: record → info → notice quiz ──────────

        // TODO(cutover): the record→info guarantee belongs to RecordFlowBridge/RecordCardUI now.
        [Test, Ignore(RetiredBeatOrderReason)]
        public void Beat_PlaysRecordThenInfo_InForcedOrder()
        {
            var beat = NewBeat("b_seq", StoryTriggerType.OnDay, "2");
            beat.record = NewRecord("elara_01");
            beat.infoCard = NewInfo("เชื้อเพลิงที่ซ่อนอยู่ในน้ำ");
            SetBeats(beat);

            var sequence = new List<string>();
            eventManager.OnStoryRecordShown += r => sequence.Add($"record:{r.recordId}");
            eventManager.OnStoryInfoShown += i => sequence.Add($"info:{i.title}");

            eventManager.RaiseDayStarted(2, true);

            CollectionAssert.AreEqual(
                new[] { "record:elara_01", "info:เชื้อเพลิงที่ซ่อนอยู่ในน้ำ" },
                sequence, "ลำดับบังคับ: record ก่อน info (Story Guide §3)");
        }

        [Test]
        public void RecordBeat_ArchivesRecord_AndRaisesEvent()
        {
            var beat = NewBeat("b_rec", StoryTriggerType.OnDay, "3");
            beat.record = NewRecord("elara_02");
            SetBeats(beat);

            RecordCardSO archived = null;
            eventManager.OnRecordArchived += r => archived = r;

            eventManager.RaiseDayStarted(3, true);

            Assert.AreSame(beat.record, archived, "การ์ดบันทึกต้อง raise OnRecordArchived");
            CollectionAssert.AreEqual(new[] { "elara_02" }, director.ArchivedRecordIds);
        }

        // ── การ์ดค้างรอผู้เล่นกดปิด เมื่อมี Card UI ────────────────

        // TODO(cutover): re-assert "one card at a time" against CardManager (CardSystemTests already does).
        [Test, Ignore(RetiredBeatOrderReason)]
        public void WithCardUI_WaitsForDismiss_BeforeNextCard()
        {
            StoryDirector.CardUIAvailable = true;

            var beat = NewBeat("b_wait", StoryTriggerType.OnDay, "2");
            beat.record = NewRecord("r1");
            beat.infoCard = NewInfo("ความรู้");
            SetBeats(beat);

            var sequence = new List<string>();
            eventManager.OnStoryRecordShown += r => sequence.Add("record");
            eventManager.OnStoryInfoShown += i => sequence.Add("info");

            eventManager.RaiseDayStarted(2, true);
            CollectionAssert.AreEqual(new[] { "record" }, sequence, "info ต้องยังไม่โชว์จนกว่าจะกดปิด record");

            eventManager.RaiseStoryCardDismissed();
            CollectionAssert.AreEqual(new[] { "record", "info" }, sequence);
        }

        // ── วิกฤตครบวง: request → resolve → afterText → ควิซรายทางเลือก ──

        [Test]
        public void CrisisBeat_FullFlow_OutcomeThenPerChoiceQuiz()
        {
            NewComponent<DilemmaManager>("DilemmaManager").dilemmaPool = new DilemmaData[0];

            var quizManager = NewComponent<QuizManager>("QuizManager");
            var quizB = ScriptableObject.CreateInstance<QuizQuestionSO>();
            quizB.id = "Q_B";
            quizB.options = new[] { "ก", "ข", "ค" };
            _spawned.Add(quizB);
            quizManager.allQuizzes = new[] { quizB };

            var crisis = ScriptableObject.CreateInstance<DilemmaData>();
            crisis.dilemmaId = "story_crisis";
            crisis.choiceB_AfterText = "[ระบบ] ขดลวดซ่อมเสร็จ";
            crisis.choiceB_QuizIds = new[] { "Q_B" };
            _spawned.Add(crisis);

            var beat = NewBeat("b_crisis", StoryTriggerType.OnDay, "17");
            beat.crisis = crisis;
            SetBeats(beat);

            DilemmaData shown = null;
            string outcomeText = null;
            QuizQuestionSO quizShown = null;
            eventManager.OnDilemmaTriggered += d => shown = d;
            eventManager.OnStoryOutcomeShown += t => outcomeText = t;
            eventManager.OnQuizShown += q => quizShown = q;

            eventManager.RaiseDayStarted(17, true);
            Assert.AreSame(crisis, shown, "วิกฤตของ beat ต้องเข้า pipeline DilemmaManager (popup โชว์)");
            Assert.IsNull(quizShown, "ควิซต้องยังไม่เด้งก่อนผู้เล่นเลือก");

            eventManager.RaiseDilemmaResolved(crisis, 1); // ผู้เล่นเลือก B

            Assert.AreEqual("[ระบบ] ขดลวดซ่อมเสร็จ", outcomeText, "afterText ของทางเลือก B");
            Assert.AreSame(quizB, quizShown, "ควิซรายทางเลือก B เด้งหลัง Outcome (ลำดับ Story Guide)");
        }

        // ── Save / Load ───────────────────────────────────────

        [Test]
        public void SaveLoaded_FiredBeats_DoNotRefire_AndRecordsRestored()
        {
            var beat = NewBeat("b_day6", StoryTriggerType.OnDay, "6");
            beat.record = NewRecord("elara_01");
            SetBeats(beat);

            var save = new SaveData
            {
                firedStoryBeats = new List<string> { "b_day6" },
                archivedRecords = new List<string> { "elara_01" },
            };
            eventManager.RaiseSaveLoaded(save);

            CollectionAssert.AreEqual(new[] { "b_day6" }, director.FiredBeatIds, "กู้รายการ beat จากเซฟ");
            CollectionAssert.AreEqual(new[] { "elara_01" }, director.ArchivedRecordIds, "กู้แผง Records จากเซฟ");

            eventManager.RaiseDayStarted(6, true);
            Assert.AreEqual(1, director.FiredBeatIds.Count, "beat ที่ยิงแล้วในเซฟต้องไม่ยิงซ้ำ");
        }

        [Test]
        public void SaveLoaded_OldSaveWithoutStoryFields_IsSafe()
        {
            SetBeats(NewBeat("b_day6", StoryTriggerType.OnDay, "6"));

            var oldSave = new SaveData { firedStoryBeats = null, archivedRecords = null }; // เซฟรุ่นเก่า
            Assert.DoesNotThrow(() => eventManager.RaiseSaveLoaded(oldSave));

            eventManager.RaiseDayStarted(6, true);
            Assert.AreEqual(1, director.FiredBeatIds.Count, "หลังโหลดเซฟเก่า ระบบยังทำงานปกติ");
        }

        // ── infrastructure ────────────────────────────────────

        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var component = go.AddComponent<T>();
            TryInvokePrivate(component, "Awake");
            TryInvokePrivate(component, "OnEnable");
            return component;
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
