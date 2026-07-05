using NUnit.Framework;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// V4 §12 — สับตำแหน่งตัวเลือกควิซ (กันผู้เล่นจำตำแหน่งข้อถูก):
    /// MakeShuffledIndices ต้องคืน permutation ครบทุก index ไม่ซ้ำไม่หาย
    /// </summary>
    public class QuizShuffleTests
    {
        [Test]
        public void MakeShuffledIndices_IsCompletePermutation()
        {
            for (int round = 0; round < 20; round++) // สุ่มหลายรอบ — ทุกรอบต้องเป็น permutation ที่ถูกต้อง
            {
                var indices = QuizPopupController.MakeShuffledIndices(3);

                Assert.AreEqual(3, indices.Length);
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, indices,
                    "ต้องมีครบทุก index ไม่ซ้ำไม่หาย (แค่สลับลำดับ)");
            }
        }

        [Test]
        public void MakeShuffledIndices_EmptyAndSingle()
        {
            Assert.AreEqual(0, QuizPopupController.MakeShuffledIndices(0).Length);
            CollectionAssert.AreEqual(new[] { 0 }, QuizPopupController.MakeShuffledIndices(1));
        }
    }
}
