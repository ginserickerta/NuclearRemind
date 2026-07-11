using NUnit.Framework;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// อนิเมชันอาคาร idle loop (V4 §16) — SpriteAnimationMath.FrameIndex
    /// ตรวจ: เดินเฟรมตาม fps · วนลูป · เคสมุม (เฟรมเดียว/ไม่มีเฟรม/fps≤0/เวลา 0)
    /// </summary>
    public class SpriteAnimationMathTests
    {
        [Test]
        public void AdvancesWithTime_AtGivenFps()
        {
            // 4 เฟรม @ 6 fps → เฟรมเปลี่ยนทุก 1/6 วิ
            Assert.AreEqual(0, SpriteAnimationMath.FrameIndex(0.00f, 6f, 4));
            Assert.AreEqual(0, SpriteAnimationMath.FrameIndex(0.16f, 6f, 4)); // ยังไม่ครบ 1/6
            Assert.AreEqual(1, SpriteAnimationMath.FrameIndex(0.17f, 6f, 4)); // ข้าม 1/6 แล้ว
            Assert.AreEqual(2, SpriteAnimationMath.FrameIndex(0.34f, 6f, 4));
            Assert.AreEqual(3, SpriteAnimationMath.FrameIndex(0.51f, 6f, 4));
        }

        [Test]
        public void Loops_AfterLastFrame()
        {
            // เฟรม 4 (index 4) ต้องวนกลับเป็น 0 · ไม่ใช่ค้างเฟรมสุดท้ายหรือหลุด array
            Assert.AreEqual(0, SpriteAnimationMath.FrameIndex(4f / 6f + 0.001f, 6f, 4));
            Assert.AreEqual(1, SpriteAnimationMath.FrameIndex(5f / 6f + 0.001f, 6f, 4));
        }

        [Test]
        public void ManyLoops_StaysInRange()
        {
            // เล่นนาน ๆ index ต้องไม่หลุดขอบ [0,frameCount) เสมอ
            for (float t = 0f; t < 100f; t += 0.37f)
            {
                int idx = SpriteAnimationMath.FrameIndex(t, 8f, 5);
                Assert.GreaterOrEqual(idx, 0);
                Assert.Less(idx, 5);
            }
        }

        [Test]
        public void SingleFrame_AlwaysZero()
        {
            Assert.AreEqual(0, SpriteAnimationMath.FrameIndex(0f, 6f, 1));
            Assert.AreEqual(0, SpriteAnimationMath.FrameIndex(99f, 6f, 1));
        }

        [Test]
        public void NoFrames_ReturnsMinusOne()
        {
            // -1 = สัญญาณ "ไม่มีเฟรม" → SpriteFrameAnimator ไม่แตะ SpriteRenderer
            Assert.AreEqual(-1, SpriteAnimationMath.FrameIndex(1f, 6f, 0));
            Assert.AreEqual(-1, SpriteAnimationMath.FrameIndex(1f, 6f, -3));
        }

        [Test]
        public void ZeroOrNegativeFps_HoldsFrameZero()
        {
            // fps ≤ 0 ต้องไม่หารศูนย์/ไม่เล่น — ค้างเฟรม 0 (กันอนิเมชันบ้าเมื่อ asset ตั้ง fps ผิด)
            Assert.AreEqual(0, SpriteAnimationMath.FrameIndex(5f, 0f, 4));
            Assert.AreEqual(0, SpriteAnimationMath.FrameIndex(5f, -6f, 4));
        }

        [Test]
        public void ZeroTime_IsFrameZero()
        {
            Assert.AreEqual(0, SpriteAnimationMath.FrameIndex(0f, 6f, 4));
        }
    }
}
