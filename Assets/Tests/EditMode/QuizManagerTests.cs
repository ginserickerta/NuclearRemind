using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// T1.T — QuizManager (V4 §12/§16): ตอบถูก +8 / ผิด +3 Knowledge ผ่าน RaiseResourceDelta,
    /// กันถามซ้ำ (_answered), เข้าคิวถามทีละข้อ (_pending), ปลดล็อก Codex ด้วย codexUnlockId,
    /// และ pause/resume เกมผ่าน GameManager.SetState (เฟส 1 · ดู Gap G2/G6)
    /// </summary>
    public class QuizManagerTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private ResourceManager resources;
        private CodexManager codex;
        private GameManager gameManager;
        private TimeManager time;
        private QuizManager quiz;

        // บันทึกควิซที่ถูกแสดง (OnQuizShown) และผลตอบ (OnQuizAnswered) ผ่าน event กลาง
        private List<QuizQuestionSO> _shown;
        private List<KeyValuePair<string, bool>> _answeredEvents;

        [SetUp]
        public void SetUp()
        {
            // EventManager ต้องมาก่อน — Manager อื่น subscribe EventManager.Instance ตอน OnEnable
            eventManager = NewComponent<EventManager>("EventManager");
            resources = NewComponent<ResourceManager>("ResourceManager");
            codex = NewComponent<CodexManager>("CodexManager");
            gameManager = NewComponent<GameManager>("GameManager");
            time = NewComponent<TimeManager>("TimeManager"); // ควิซ pause นาฬิกาวันผ่าน TimeManager (เฟส 5)
            quiz = NewComponent<QuizManager>("QuizManager");

            // ตั้ง array ว่างแล้ว rebuild lookup (Awake อาจ build dict จาก allQuizzes/allCodexEntries)
            ConfigureCodex();
            Configure();

            _shown = new List<QuizQuestionSO>();
            _answeredEvents = new List<KeyValuePair<string, bool>>();
            eventManager.OnQuizShown += q => _shown.Add(q);
            eventManager.OnQuizAnswered += (id, correct) =>
                _answeredEvents.Add(new KeyValuePair<string, bool>(id, correct));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        // ---- ตอบถูก +8 / ตอบผิด +3 (RaiseResourceDelta → ResourceManager.Current.knowledge) ----

        [Test]
        public void SubmitCorrectAnswer_GrantsRewardKnowledge()
        {
            // codexUnlockId = "" → ได้เฉพาะรางวัลแบน +8 (ไม่มี codex +2 · Gap G1)
            Configure(MakeQuiz("Q1", correctIndex: 1, reward: 8));
            quiz.TriggerByIds("Q1");

            quiz.SubmitAnswer(_shown[0].correctIndex);

            Assert.AreEqual(8f, resources.Current.knowledge, 1e-4f, "ตอบถูก → Knowledge +8");
            Assert.AreEqual(1, _answeredEvents.Count, "ตอบเสร็จต้อง raise OnQuizAnswered หนึ่งครั้ง");
            Assert.AreEqual("Q1", _answeredEvents[0].Key);
            Assert.IsTrue(_answeredEvents[0].Value, "ตอบถูก → OnQuizAnswered(correct=true)");
        }

        [Test]
        public void SubmitWrongAnswer_GrantsFlatThreeKnowledge()
        {
            Configure(MakeQuiz("Q1", correctIndex: 1, reward: 8));
            quiz.TriggerByIds("Q1");

            var shown = _shown[0];
            int wrongIndex = (shown.correctIndex + 1) % shown.options.Length;
            quiz.SubmitAnswer(wrongIndex);

            Assert.AreEqual(3f, resources.Current.knowledge, 1e-4f, "ตอบผิด → Knowledge +3 (แบน)");
            Assert.IsFalse(_answeredEvents[0].Value, "ตอบผิด → OnQuizAnswered(correct=false)");
        }

        // ---- กันถามซ้ำ (_answered / AlreadyAnswered) ----

        [Test]
        public void AnsweredQuiz_IsNotAskedAgain()
        {
            Configure(MakeQuiz("Q1", correctIndex: 0));
            quiz.TriggerByIds("Q1");
            quiz.SubmitAnswer(_shown[0].correctIndex);

            Assert.IsTrue(quiz.AlreadyAnswered("Q1"), "ตอบแล้วต้องถูกจำใน _answered");

            int shownBefore = _shown.Count;
            quiz.TriggerByIds("Q1"); // trigger ซ้ำ — ต้องไม่เด้งอีก

            Assert.AreEqual(shownBefore, _shown.Count, "ควิซที่ตอบแล้วต้องไม่ถูกถามซ้ำ");
        }

        [Test]
        public void UnansweredQuiz_ReportsNotAnswered()
        {
            Configure(MakeQuiz("Q1", correctIndex: 0));

            Assert.IsFalse(quiz.AlreadyAnswered("Q1"), "ยังไม่ตอบ → AlreadyAnswered = false");
        }

        // ---- เข้าคิวถามทีละข้อ (_pending) ----

        [Test]
        public void MultipleTriggeredQuizzes_AreAskedOneAtATime()
        {
            Configure(MakeQuiz("Q1", correctIndex: 0), MakeQuiz("Q2", correctIndex: 1));

            quiz.TriggerByIds("Q1", "Q2");
            Assert.AreEqual(1, _shown.Count, "ต้องแสดงทีละข้อ — ข้อแรกก่อน");
            Assert.AreEqual("Q1", _shown[0].id);

            quiz.SubmitAnswer(_shown[0].correctIndex); // ตอบข้อแรก → เด้งข้อถัดไปในคิว
            Assert.AreEqual(2, _shown.Count, "ตอบข้อแรกแล้วต้องเด้งข้อถัดไปจากคิว");
            Assert.AreEqual("Q2", _shown[1].id);

            quiz.SubmitAnswer(_shown[1].correctIndex);
        }

        [Test]
        public void EnqueueQuizzes_ShowsLinkedQuizFromTrigger()
        {
            var q = MakeQuiz("Q1", correctIndex: 2);
            Configure(); // allQuizzes ไม่จำเป็น — trigger ส่ง SO ตรง ๆ ผ่าน IQuizTrigger

            quiz.EnqueueQuizzes(new FakeTrigger(q));

            Assert.AreEqual(1, _shown.Count, "EnqueueQuizzes ต้องเข้าคิวและเด้งควิซที่ผูกไว้");
            Assert.AreSame(q, _shown[0]);
        }

        // ---- ปลดล็อก Codex ด้วย codexUnlockId (V4 §12) ----

        [Test]
        public void CodexLinkedQuiz_UnlocksCodexOnAnswer()
        {
            // ตอบถูก codex-linked: +8 (ควิซ) +2 (CodexManager.Unlock) = +10 Knowledge (Gap G6)
            ConfigureCodex(MakeCodexEntry("Core_01"));
            Configure(MakeQuiz("Q1", correctIndex: 0, reward: 8, codexUnlockId: "Core_01"));

            quiz.TriggerByIds("Q1");
            quiz.SubmitAnswer(_shown[0].correctIndex);

            Assert.IsTrue(codex.IsUnlocked("Core_01"), "codexUnlockId ที่ไม่ว่าง → ต้องปลดล็อก Codex");
            Assert.AreEqual(10f, resources.Current.knowledge, 1e-4f, "ถูก +8 (ควิซ) +2 (codex) = 10");
        }

        // ---- GetById (ใช้โดย DilemmaData.GetLinkedQuizzes) ----

        [Test]
        public void GetById_ReturnsMatchingQuizOrNull()
        {
            var q1 = MakeQuiz("Q1", correctIndex: 0);
            var q2 = MakeQuiz("Q2", correctIndex: 0);
            Configure(q1, q2);

            Assert.AreSame(q1, quiz.GetById("Q1"));
            Assert.AreSame(q2, quiz.GetById("Q2"));
            Assert.IsNull(quiz.GetById("Q999"), "id ที่ไม่มี → คืน null");
        }

        // ---- pause/resume ผ่าน GameManager (เฟส 1 · Gap G2) ----

        [Test]
        public void ShowingQuiz_PausesDayClock_ResumesAfterLastAnswer()
        {
            Configure(MakeQuiz("Q1", correctIndex: 0));

            quiz.TriggerByIds("Q1");
            Assert.IsTrue(time.IsPaused(PauseReason.QuizPopup),
                "เปิดควิซต้องหยุดนาฬิกาวัน (เฟส 5: TimeManager.Pause(QuizPopup))");

            quiz.SubmitAnswer(_shown[0].correctIndex);
            Assert.IsFalse(time.IsPaused(PauseReason.QuizPopup),
                "ตอบข้อสุดท้ายในคิวแล้วต้องปลด pause");
        }

        // ---- factory helpers ----

        // ---- Q1: ย้าย ownership ไป StoryDirector (beat "deuterium_ignition") ----
        // StoryDirector เล่น InfoCard/Record ก่อนแล้วค่อยเด้ง Q1 (กฎเหล็ก Story Guide: ความรู้มาก่อนควิซ)
        // — เทสต์ latch ฝั่ง director อยู่ใน StoryDirectorTests.DeuteriumBeat_FiresOnFirstDeuteriumOnly

        [Test]
        public void FirstDeuterium_DoesNotAutoTriggerQ1()
        {
            Configure(MakeQuiz("Q1", correctIndex: 0));

            eventManager.RaiseResourceDelta(ResourceType.Deuterium, 5f);

            Assert.AreEqual(0, _shown.Count,
                "QuizManager ต้องไม่ยิง Q1 เอง — StoryBeat deuterium_ignition เป็นเจ้าของ (ความรู้ก่อนควิซ)");
        }

        private QuizQuestionSO MakeQuiz(string id, int correctIndex, int reward = 8,
            string codexUnlockId = "", QuizCategory category = QuizCategory.Reactor)
        {
            var q = ScriptableObject.CreateInstance<QuizQuestionSO>();
            q.id = id;
            q.category = category;
            q.question = $"คำถาม {id}";
            q.options = new[] { "ก", "ข", "ค" };
            q.correctIndex = correctIndex;
            q.rewardKnowledge = reward;
            q.explainText = $"คำอธิบาย {id}";
            q.speaker = "VESTA";
            q.codexUnlockId = codexUnlockId;
            _spawned.Add(q);
            return q;
        }

        private CodexEntry MakeCodexEntry(string entryId)
        {
            var e = ScriptableObject.CreateInstance<CodexEntry>();
            e.entryId = entryId;
            e.title = entryId;
            e.branch = "Core";
            e.content = "เนื้อหา";
            e.unlockedByEvent = "";
            _spawned.Add(e);
            return e;
        }

        // IQuizTrigger จำลอง (แทน DilemmaData/CrisisSO ที่ผูกควิซไว้ — V4 §16)
        private class FakeTrigger : IQuizTrigger
        {
            private readonly QuizQuestionSO[] _quizzes;
            public FakeTrigger(params QuizQuestionSO[] quizzes) => _quizzes = quizzes;
            public QuizQuestionSO[] GetLinkedQuizzes() => _quizzes;
        }

        // ตั้ง allQuizzes แล้ว rebuild lookup ผ่าน Awake (เผื่อ QuizManager build dict จาก array)
        private void Configure(params QuizQuestionSO[] quizzes)
        {
            quiz.allQuizzes = quizzes;
            TryInvokePrivate(quiz, "Awake");
        }

        private void ConfigureCodex(params CodexEntry[] entries)
        {
            codex.allCodexEntries = entries;
            TryInvokePrivate(codex, "Awake");
        }

        // ---- reflection helpers (เหมือน ResourceManagerTests/PopulationManagerTests) ----

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
