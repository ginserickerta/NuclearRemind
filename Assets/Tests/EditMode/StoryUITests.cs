using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// UI เนื้อเรื่องเฟส 3 (Story Guide §2):
    /// - CardUIController: ตั้ง/ปลด CardUIAvailable · โชว์การ์ด + หยุดนาฬิกา · Dismiss คืนนาฬิกา + raise event
    /// - integration กับ StoryDirector: การ์ดค้างจนกด ปิด → ใบถัดไปเด้งต่อ
    /// - RecordsPanelController: ลิสต์บันทึกที่กู้คืนจาก StoryDirector
    /// - MemorialPanelController: เปิดเฉพาะคลิกบน footprint ตึกอนุสรณ์ + เสียงในใจครั้งเดียว
    /// </summary>
    public class StoryUITests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private TimeManager timeManager;

        [SetUp]
        public void SetUp()
        {
            StoryDirector.CardUIAvailable = false;
            eventManager = NewComponent<EventManager>("EventManager");
            timeManager = NewComponent<TimeManager>("TimeManager");
        }

        [TearDown]
        public void TearDown()
        {
            StoryDirector.CardUIAvailable = false;
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        // ── CardUIController ──────────────────────────────────

        [Test]
        public void CardUI_TogglesAvailabilityFlag_OnEnableDisable()
        {
            var card = NewCardUI();
            Assert.IsTrue(StoryDirector.CardUIAvailable, "OnEnable ต้องตั้ง flag ให้ director รอกดปิดการ์ด");

            TryInvokePrivate(card, "OnDisable");
            Assert.IsFalse(StoryDirector.CardUIAvailable, "OnDisable ต้องปลด flag — director กลับไป degrade เป็น toast");
        }

        [Test]
        public void InfoCardShown_ShowsPanel_AndPausesClock()
        {
            var card = NewCardUI();

            var info = ScriptableObject.CreateInstance<InfoCardSO>();
            info.title = "เชื้อเพลิงที่ซ่อนอยู่ในน้ำ";
            info.bodyTH = "ในน้ำทะเลทุกหยดมีดิวเทอเรียมปนอยู่";
            info.buttonLabel = "เข้าใจแล้ว";
            _spawned.Add(info);

            eventManager.RaiseStoryInfoShown(info);

            Assert.IsTrue(card.IsShowing);
            Assert.IsTrue(card.overlayPanel.activeSelf, "การ์ดต้องเด้งกลางจอ");
            Assert.AreEqual("เชื้อเพลิงที่ซ่อนอยู่ในน้ำ", card.titleText.text);
            Assert.AreEqual("ในน้ำทะเลทุกหยดมีดิวเทอเรียมปนอยู่", card.bodyText.text);
            Assert.AreEqual("เข้าใจแล้ว", card.dismissLabel.text);
            Assert.IsTrue(timeManager.IsPaused(PauseReason.StoryCard), "เปิดการ์ด → นาฬิกาวันหยุด (อ่านโดยไม่เสียเวลาเกม)");
        }

        [Test]
        public void Dismiss_HidesPanel_ResumesClock_RaisesDismissedOnce()
        {
            var card = NewCardUI();
            var info = ScriptableObject.CreateInstance<InfoCardSO>();
            info.title = "x";
            _spawned.Add(info);
            eventManager.RaiseStoryInfoShown(info);

            int dismissed = 0;
            eventManager.OnStoryCardDismissed += () => dismissed++;

            card.Dismiss();
            Assert.IsFalse(card.IsShowing);
            Assert.IsFalse(card.overlayPanel.activeSelf);
            Assert.IsFalse(timeManager.IsPaused(PauseReason.StoryCard), "ปิดการ์ด → นาฬิกาเดินต่อ");
            Assert.AreEqual(1, dismissed, "ต้องบอก StoryDirector ให้เดินลำดับต่อ");

            card.Dismiss(); // กดซ้ำตอนไม่มีการ์ด
            Assert.AreEqual(1, dismissed, "ไม่มีการ์ดค้าง → ไม่ raise ซ้ำ");
        }

        [Test]
        public void RecordCardShown_UsesArchiveTitle_AuthorAndButtonLabel()
        {
            var card = NewCardUI();

            var record = ScriptableObject.CreateInstance<RecordCardSO>();
            record.recordId = "elara_01";
            record.archiveTitle = "บันทึก #01 — เชื้อเพลิงในน้ำ";
            record.authorLabel = "Dr. Elara Vane";
            record.bodyTH = "วันนี้เราสกัดดิวเทอเรียมชุดแรกได้";
            _spawned.Add(record);

            eventManager.RaiseStoryRecordShown(record);

            Assert.AreEqual("บันทึก #01 — เชื้อเพลิงในน้ำ", card.titleText.text);
            StringAssert.Contains("Dr. Elara Vane", card.kickerText.text);
            Assert.AreEqual("รับทราบ · เก็บเข้าแผง Records", card.dismissLabel.text, "ใช้ default ของ RecordCardSO");
        }

        [Test]
        public void OutcomeShown_ShowsBody_WithoutTitle()
        {
            var card = NewCardUI();

            eventManager.RaiseStoryOutcomeShown("[ระบบ] ขดลวดซ่อมเสร็จ — เตาเดินต่อ");

            Assert.IsTrue(card.IsShowing);
            Assert.AreEqual("[ระบบ] ขดลวดซ่อมเสร็จ — เตาเดินต่อ", card.bodyText.text);
            Assert.IsFalse(card.titleText.gameObject.activeSelf, "การ์ดบทสรุปไม่มีหัวเรื่อง");
        }

        // ── Integration: StoryDirector + CardUI จริง ───────────

        [Test]
        public void Integration_DirectorWaitsForDismiss_ThenShowsNextCard()
        {
            var director = NewComponent<StoryDirector>("StoryDirector");
            var card = NewCardUI(); // OnEnable → CardUIAvailable = true

            var beat = ScriptableObject.CreateInstance<StoryBeatSO>();
            beat.beatId = "b_seq";
            beat.triggerType = StoryTriggerType.OnDay;
            beat.triggerParam = "2";
            var record = ScriptableObject.CreateInstance<RecordCardSO>();
            record.recordId = "r1";
            record.archiveTitle = "บันทึก r1";
            beat.record = record;
            var info = ScriptableObject.CreateInstance<InfoCardSO>();
            info.title = "ความรู้หลังบันทึก";
            beat.infoCard = info;
            _spawned.Add(beat); _spawned.Add(record); _spawned.Add(info);
            director.beats = new[] { beat };

            eventManager.RaiseDayStarted(2, true);
            Assert.IsTrue(card.IsShowing);
            Assert.AreEqual("บันทึก r1", card.titleText.text, "ใบแรก: การ์ดบันทึก (ลำดับบังคับ record ก่อน info)");

            card.Dismiss();
            Assert.IsTrue(card.IsShowing, "กดปิด record → info ต้องเด้งต่อทันที");
            Assert.AreEqual("ความรู้หลังบันทึก", card.titleText.text);
            Assert.IsTrue(timeManager.IsPaused(PauseReason.StoryCard), "การ์ดใบถัดไปต้องหยุดนาฬิกาต่อ (Resume เก่าไม่ลบ Pause ใหม่)");

            card.Dismiss();
            Assert.IsFalse(card.IsShowing, "จบ beat → ไม่มีการ์ดค้าง");
            Assert.IsFalse(timeManager.IsPaused(PauseReason.StoryCard));
        }

        // ── RecordsPanelController ────────────────────────────

        [Test]
        public void RecordsPanel_Toggle_ListsArchivedRecords()
        {
            var director = NewComponent<StoryDirector>("StoryDirector");

            var beat = ScriptableObject.CreateInstance<StoryBeatSO>();
            beat.beatId = "b_rec";
            beat.triggerType = StoryTriggerType.OnDay;
            beat.triggerParam = "3";
            var record = ScriptableObject.CreateInstance<RecordCardSO>();
            record.recordId = "elara_01";
            record.archiveTitle = "บันทึก #01 — เชื้อเพลิงในน้ำ";
            beat.record = record;
            _spawned.Add(beat); _spawned.Add(record);
            director.beats = new[] { beat };

            eventManager.RaiseDayStarted(3, true); // ไม่มี Card UI → auto-advance + archive

            var records = NewComponent<RecordsPanelController>("RecordsPanelController");
            records.recordsPanel = NewGO("RecordsPanel");
            records.recordsPanel.SetActive(false);
            var list = NewGO("RecordList");
            records.entryListParent = list.transform;
            var template = NewGO("Template");
            template.transform.SetParent(list.transform, false);
            template.AddComponent<Image>();
            template.AddComponent<Button>();
            var tmplLabel = NewText("Label");
            tmplLabel.transform.SetParent(template.transform, false);
            template.SetActive(false);
            records.entryButtonTemplate = template;
            records.detailTitle = NewText("DetailTitle");
            records.detailAuthor = NewText("DetailAuthor");
            records.detailBody = NewText("DetailBody");

            records.Toggle();
            Assert.IsTrue(records.recordsPanel.activeSelf);

            // นับปุ่มที่เปิดใช้ (template ยัง inactive)
            var activeButtons = new List<GameObject>();
            foreach (Transform child in list.transform)
                if (child.gameObject.activeSelf) activeButtons.Add(child.gameObject);

            Assert.AreEqual(1, activeButtons.Count, "บันทึกที่กู้แล้ว 1 ใบ → ปุ่ม 1 ปุ่ม");
            Assert.AreEqual("บันทึก #01 — เชื้อเพลิงในน้ำ", activeButtons[0].GetComponentInChildren<Text>().text);

            records.ShowRecord(record);
            Assert.AreEqual("บันทึก #01 — เชื้อเพลิงในน้ำ", records.detailTitle.text);
        }

        // ── MemorialPanelController ───────────────────────────

        [Test]
        public void Memorial_OpensOnFootprintOnly_InnerVoiceOnce()
        {
            NewComponent<GridManager>("GridManager");
            NewComponent<BuildingRegistry>("BuildingRegistry");

            var building = ScriptableObject.CreateInstance<BuildingData>();
            building.buildingName = "อนุสรณ์";
            building.buildingType = BuildingType.Memorial;
            building.size = new Vector2Int(2, 2);
            _spawned.Add(building);
            eventManager.RaiseBuildingPlaced(GridManager.Instance.GetCell(3, 4), building);

            var memorial = ScriptableObject.CreateInstance<MemorialSO>();
            memorial.headerTH = "เพื่อจดจำทีมสร้างหอคอย — Veltara Core Project";
            memorial.names = new[] { "ELARA VANE — Lead Reactor Physicist" };
            memorial.innerVoiceOnFirstOpen = "ชื่อนี้...";
            _spawned.Add(memorial);

            var mem = NewComponent<MemorialPanelController>("MemorialPanelController");
            mem.memorialData = memorial;
            mem.panel = NewGO("MemorialPanel");
            mem.panel.SetActive(false);
            mem.headerText = NewText("Header");
            mem.namesText = NewText("Names");

            int notices = 0;
            eventManager.OnNotice += _ => notices++;

            Assert.IsFalse(mem.TryOpenAtCell(new Vector2Int(10, 10)), "คลิกนอกตึก → ไม่เปิด");
            Assert.IsFalse(mem.panel.activeSelf);

            Assert.IsTrue(mem.TryOpenAtCell(new Vector2Int(4, 5)), "มุมขวาล่างของ footprint 2×2 จาก (3,4)");
            Assert.IsTrue(mem.panel.activeSelf);
            StringAssert.Contains("ELARA VANE", mem.namesText.text, "ปมเรื่อง: ชื่อ Elara อยู่ในรายชื่อ");
            Assert.AreEqual(1, notices, "เสียงในใจโชว์ตอนเปิดครั้งแรก");

            mem.Close();
            Assert.IsFalse(mem.panel.activeSelf);

            Assert.IsTrue(mem.TryOpenAtCell(new Vector2Int(3, 4)));
            Assert.AreEqual(1, notices, "เปิดซ้ำ → เสียงในใจไม่โชว์อีก (ครั้งเดียวต่อรอบเล่น)");
        }

        // ── infrastructure ────────────────────────────────────

        private CardUIController NewCardUI()
        {
            var card = NewComponent<CardUIController>("CardUIController");
            card.overlayPanel = NewGO("StoryCardPanel");
            card.overlayPanel.SetActive(false);
            card.kickerText = NewText("Kicker");
            card.titleText = NewText("Title");
            card.bodyText = NewText("Body");
            card.dismissLabel = NewText("DismissLabel");
            return card;
        }

        private GameObject NewGO(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private Text NewText(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            _spawned.Add(go);
            return go.AddComponent<Text>();
        }

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
