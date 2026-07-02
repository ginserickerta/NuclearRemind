using NUnit.Framework;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// V4 §9 — MetaProgress คลังความรู้ถาวร: Knowledge เก็บค่าสูงสุด, Codex สะสม,
    /// round-trip ผ่าน PlayerPrefs, ResetAll ล้างได้
    /// </summary>
    public class MetaProgressTests
    {
        [SetUp]
        public void SetUp() => MetaProgress.ResetAll();

        [TearDown]
        public void TearDown() => MetaProgress.ResetAll();

        [Test]
        public void Capture_KeepsMaxKnowledge()
        {
            MetaProgress.Capture(50, null);
            Assert.AreEqual(50, MetaProgress.KnowledgeBank);

            MetaProgress.Capture(30, null); // ต่ำกว่า → ไม่ลด
            Assert.AreEqual(50, MetaProgress.KnowledgeBank);

            MetaProgress.Capture(80, null); // สูงกว่า → อัป
            Assert.AreEqual(80, MetaProgress.KnowledgeBank);
        }

        [Test]
        public void Capture_AccumulatesCodex()
        {
            MetaProgress.Capture(0, new[] { "a", "b" });
            MetaProgress.Capture(0, new[] { "b", "c" });

            Assert.AreEqual(3, MetaProgress.UnlockedCodex.Count);
            Assert.IsTrue(MetaProgress.UnlockedCodex.Contains("a"));
            Assert.IsTrue(MetaProgress.UnlockedCodex.Contains("c"));
        }

        [Test]
        public void SaveLoad_RoundTrip_ViaPlayerPrefs()
        {
            MetaProgress.Capture(42, new[] { "codex_x" }); // Capture เรียก Save ให้แล้ว
            MetaProgress.Load();                            // โหลดกลับจาก PlayerPrefs

            Assert.AreEqual(42, MetaProgress.KnowledgeBank);
            Assert.IsTrue(MetaProgress.UnlockedCodex.Contains("codex_x"));
        }

        [Test]
        public void ResetAll_ClearsBank()
        {
            MetaProgress.Capture(10, new[] { "y" });
            MetaProgress.ResetAll();

            Assert.AreEqual(0, MetaProgress.KnowledgeBank);
            Assert.AreEqual(0, MetaProgress.UnlockedCodex.Count);
        }
    }
}
