using NUnit.Framework;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// Knowledge Summary ตอนจบเกม (Story Guide §4 true_ending onEnd) — สรุปหัวข้อ Codex ที่ปลดล็อก
    /// ทดสอบ formatter บริสุทธิ์ UIManagerHUD.FormatKnowledgeSummary โดยไม่ต้องมี scene/CodexManager
    /// </summary>
    public class KnowledgeSummaryTests
    {
        [Test]
        public void Format_ListsTopicsWithCount()
        {
            string s = UIManagerHUD.FormatKnowledgeSummary(new[] { "ดิวเทอเรียม", "ALARA", "ฟิวชัน" });
            StringAssert.Contains("(3)", s);
            StringAssert.Contains("ดิวเทอเรียม", s);
            StringAssert.Contains("ALARA", s);
            StringAssert.Contains("ฟิวชัน", s);
            StringAssert.Contains(" · ", s); // คั่นหัวข้อด้วย middot
        }

        [Test]
        public void Format_EmptyWhenNothingUnlocked()
        {
            Assert.AreEqual("", UIManagerHUD.FormatKnowledgeSummary(new string[0]));
            Assert.AreEqual("", UIManagerHUD.FormatKnowledgeSummary(null));
        }
    }
}
