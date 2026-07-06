using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// Data layer ของระบบเนื้อเรื่อง (Story Guide §2):
    /// - StoryBeatSO.GetLinkedQuizzes กรอง null/ว่าง
    /// - DilemmaData.GetQuizIdsForChoice: รายการรายทางเลือกชนะ · ว่าง/นอกช่วง fallback ไป linkedQuizIds
    /// - DilemmaData.GetAfterText คืนบทหลังเลือกถูกตัว
    /// </summary>
    public class StoryDataTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        private T NewSO<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _spawned.Add(so);
            return so;
        }

        // ── StoryBeatSO ────────────────────────────────────────

        [Test]
        public void StoryBeat_NoQuiz_ReturnsEmpty()
        {
            var beat = NewSO<StoryBeatSO>();
            beat.quiz = null;
            Assert.IsEmpty(beat.GetLinkedQuizzes(), "ไม่มีควิซ → array ว่าง (ไม่ null)");

            beat.quiz = new QuizQuestionSO[0];
            Assert.IsEmpty(beat.GetLinkedQuizzes());
        }

        [Test]
        public void StoryBeat_FiltersNullQuizEntries()
        {
            var beat = NewSO<StoryBeatSO>();
            var q1 = NewSO<QuizQuestionSO>();
            var q2 = NewSO<QuizQuestionSO>();
            beat.quiz = new[] { q1, null, q2 };

            var result = beat.GetLinkedQuizzes();
            Assert.AreEqual(2, result.Length, "ช่อง null ใน quiz array ต้องถูกกรองทิ้ง");
            Assert.AreSame(q1, result[0]);
            Assert.AreSame(q2, result[1]);
        }

        // ── DilemmaData: ควิซรายทางเลือก (quizRef) ────────────────

        private static DilemmaData NewDilemma()
        {
            var d = ScriptableObject.CreateInstance<DilemmaData>();
            d.linkedQuizIds = new[] { "Q_SHARED" };
            return d;
        }

        [Test]
        public void QuizIdsForChoice_PerChoiceList_Wins()
        {
            var d = NewDilemma();
            _spawned.Add(d);
            d.choiceA_QuizIds = new[] { "Q_A" };
            d.choiceB_QuizIds = new[] { "Q_B1", "Q_B2" };

            CollectionAssert.AreEqual(new[] { "Q_A" }, d.GetQuizIdsForChoice(0),
                "ทางเลือก A มีรายการของตัวเอง → ใช้แทน linkedQuizIds");
            CollectionAssert.AreEqual(new[] { "Q_B1", "Q_B2" }, d.GetQuizIdsForChoice(1));
        }

        [Test]
        public void QuizIdsForChoice_EmptyPerChoice_FallsBackToLinked()
        {
            var d = NewDilemma();
            _spawned.Add(d);
            d.choiceC_QuizIds = new string[0]; // ว่าง = ไม่กำหนด

            CollectionAssert.AreEqual(new[] { "Q_SHARED" }, d.GetQuizIdsForChoice(2),
                "ทางเลือกไม่มีรายการของตัวเอง → fallback ไป linkedQuizIds (พฤติกรรมเดิม)");
            CollectionAssert.AreEqual(new[] { "Q_SHARED" }, d.GetQuizIdsForChoice(0));
        }

        [Test]
        public void QuizIdsForChoice_OutOfRange_FallsBackToLinked()
        {
            var d = NewDilemma();
            _spawned.Add(d);
            CollectionAssert.AreEqual(new[] { "Q_SHARED" }, d.GetQuizIdsForChoice(99));
            CollectionAssert.AreEqual(new[] { "Q_SHARED" }, d.GetQuizIdsForChoice(-1));
        }

        // ── DilemmaData: afterText ────────────────────────────

        [Test]
        public void AfterText_ReturnsPerChoiceText()
        {
            var d = NewDilemma();
            _spawned.Add(d);
            d.choiceA_AfterText = "[ระบบ] สนามแม่เหล็กเสถียร";
            d.choiceB_AfterText = "[ระบบ] ขดลวดซ่อมเสร็จใน 2 วัน";

            Assert.AreEqual("[ระบบ] สนามแม่เหล็กเสถียร", d.GetAfterText(0));
            Assert.AreEqual("[ระบบ] ขดลวดซ่อมเสร็จใน 2 วัน", d.GetAfterText(1));
            Assert.IsTrue(string.IsNullOrEmpty(d.GetAfterText(2)), "ทางเลือกไม่มีบท → ว่าง");
            Assert.IsNull(d.GetAfterText(99), "นอกช่วง → null");
        }

        // ── ค่า default ของการ์ด ────────────────────────────────

        [Test]
        public void Cards_HaveNonEmptyDefaultButtonLabels()
        {
            Assert.IsFalse(string.IsNullOrEmpty(NewSO<InfoCardSO>().buttonLabel),
                "InfoCard ต้องมีป้ายปุ่ม default — การ์ดไม่มีปุ่มจะปิดไม่ได้");
            Assert.IsFalse(string.IsNullOrEmpty(NewSO<RecordCardSO>().buttonLabel));
        }
    }
}
