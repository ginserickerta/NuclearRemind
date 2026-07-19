using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// RecordCardUI must never freeze the clock for a card the player cannot dismiss.
    ///
    /// The day-9 hang: Record #1 lands at the end of day 9 (DataRecovery ticks 14/day passive against a
    /// target of 100, so 14×8 = 112 clears it), the card paused time before checking anything, and its
    /// RecordCardCanvas was inactive in the scene — so nothing drew, the buttons could not be clicked,
    /// and PauseReason.StoryCard was held for the rest of the run with no UI to explain it.
    /// </summary>
    public class RecordCardUITests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private EventManager events;
        private TimeManager time;
        private RecordCardUI card;
        private GameObject overlay;
        private Button ack;

        [SetUp]
        public void SetUp()
        {
            events = NewComponent<EventManager>("EventManager");
            time = NewComponent<TimeManager>("TimeManager");

            var host = new GameObject("RecordCardUI");
            _spawned.Add(host);
            card = host.AddComponent<RecordCardUI>();

            overlay = NewGO("RecordOverlay");
            card.overlayPanel = overlay;

            var btnGO = NewGO("AckButton");
            btnGO.transform.SetParent(overlay.transform, false);
            ack = btnGO.AddComponent<Button>();
            card.ackButton = ack;

            TryInvokePrivate(card, "Awake");
            TryInvokePrivate(card, "OnEnable");
            overlay.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ── the hang ──────────────────────────────────────────────

        [Test]
        public void DeadCanvas_DoesNotStopTheClock()
        {
            // Reproduce the scene as it shipped: the overlay's parent canvas is inactive, so the card
            // can be told to show and still never appear.
            var deadCanvas = NewGO("RecordCardCanvas");
            overlay.transform.SetParent(deadCanvas.transform, false);
            deadCanvas.SetActive(false);

            events.RaiseStoryRecordShown(MakeRecord("record_01"));

            Assert.IsFalse(time.IsPaused(PauseReason.StoryCard),
                "★ การ์ดขึ้นไม่ได้ → ห้ามหยุดเวลา (นี่คือบั๊กวันที่ 9)");
            Assert.IsFalse(card.IsShowing);
        }

        [Test]
        public void DeadCanvas_StillArchivesTheRecord()
        {
            var deadCanvas = NewGO("RecordCardCanvas");
            overlay.transform.SetParent(deadCanvas.transform, false);
            deadCanvas.SetActive(false);

            RecordCardSO archived = null;
            events.OnRecordArchiveRequested += r => archived = r;
            int dismissed = 0;
            events.OnStoryCardDismissed += () => dismissed++;

            var rec = MakeRecord("record_01");
            events.RaiseStoryRecordShown(rec);

            Assert.AreSame(rec, archived, "แสดงไม่ได้ก็ต้องไม่ทำบันทึกหาย — เก็บเข้าแผงแทน");
            Assert.AreEqual(1, dismissed, "ต้องบอกคนที่รออยู่ว่าการ์ดจบแล้ว ไม่งั้นคิวค้าง");
        }

        [Test]
        public void NoDismissButton_DoesNotStopTheClock()
        {
            // A visible card with no working way out traps the player just as thoroughly.
            card.ackButton = null;
            card.archiveButton = null;

            events.RaiseStoryRecordShown(MakeRecord("record_01"));

            Assert.IsFalse(time.IsPaused(PauseReason.StoryCard), "ไม่มีปุ่มปิด → ห้ามหยุดเวลา");
        }

        [Test]
        public void DisabledButton_CountsAsNoWayOut()
        {
            ack.interactable = false;

            events.RaiseStoryRecordShown(MakeRecord("record_01"));

            Assert.IsFalse(time.IsPaused(PauseReason.StoryCard), "ปุ่มกดไม่ได้ = ไม่มีทางออก");
        }

        // ── the healthy path still works ──────────────────────────

        [Test]
        public void LiveCard_StopsTheClock_AndResumesOnDismiss()
        {
            events.RaiseStoryRecordShown(MakeRecord("record_01"));

            Assert.IsTrue(card.IsShowing, "การ์ดขึ้นได้ → ต้องโชว์");
            Assert.IsTrue(time.IsPaused(PauseReason.StoryCard), "การ์ดค้างอยู่ → เวลาต้องหยุดให้อ่าน");
            Assert.IsTrue(overlay.activeSelf);

            card.Acknowledge();

            Assert.IsFalse(card.IsShowing);
            Assert.IsFalse(time.IsPaused(PauseReason.StoryCard), "กดปิด → เวลาเดินต่อ");
        }

        [Test]
        public void ArchiveButton_AloneIsEnough()
        {
            card.ackButton = null;
            var btnGO = NewGO("ArchiveButton");
            btnGO.transform.SetParent(overlay.transform, false);
            card.archiveButton = btnGO.AddComponent<Button>();

            events.RaiseStoryRecordShown(MakeRecord("record_01"));

            Assert.IsTrue(time.IsPaused(PauseReason.StoryCard), "มีปุ่มเดียวก็ปิดได้ → หยุดเวลาได้");
        }

        // ── helpers ───────────────────────────────────────────────

        private RecordCardSO MakeRecord(string id)
        {
            var r = ScriptableObject.CreateInstance<RecordCardSO>();
            r.recordId = id;
            r.archiveTitle = id;
            r.authorLabel = "Dr. Elara Vane";
            r.bodyTH = "เนื้อหา";
            _spawned.Add(r);
            return r;
        }

        private GameObject NewGO(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            _spawned.Add(go);
            return go;
        }

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
