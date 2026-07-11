using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// กันคนงานเดินทะลุกัน (V4 §5) — WorkerSeparation.Correction
    /// ตรวจ: ไม่ซ้อน=ไม่ขยับ · ซ้อน=ถอยครึ่งระยะ · พื้น iso ยืดแกน y · ซ้อนสนิท · เพดานต่อเฟรม
    /// </summary>
    public class WorkerSeparationTests
    {
        private const float R = WorkerSeparation.DefaultRadius; // 0.34
        private const float Y = WorkerSeparation.DefaultYStretch; // 2
        private const float Tol = 1e-4f;

        private static List<Vector3> At(params Vector3[] p) => new List<Vector3>(p);
        private static Vector3 Correct(Vector3 self, List<Vector3> others, float max = 0f)
            => WorkerSeparation.Correction(self, others, R, Y, max);

        [Test]
        public void NoNeighbors_NoCorrection()
        {
            Assert.AreEqual(Vector3.zero, Correct(Vector3.zero, At()));
            Assert.AreEqual(Vector3.zero, WorkerSeparation.Correction(Vector3.zero, null, R, Y, 0f));
        }

        [Test]
        public void NeighborBeyondRadius_NoCorrection()
        {
            // ห่างตามแกน x เกินรัศมี → ไม่ต้องขยับ
            Assert.AreEqual(Vector3.zero, Correct(Vector3.zero, At(new Vector3(R + 0.01f, 0f, 0f))));
        }

        [Test]
        public void NeighborExactlyAtRadius_NoCorrection()
        {
            // แตะขอบพอดี = ยังไม่ซ้อน (เงื่อนไข d2 >= r2) — กันสั่นตอนยืนชิดกันพอดี
            Assert.AreEqual(Vector3.zero, Correct(Vector3.zero, At(new Vector3(R, 0f, 0f))));
        }

        [Test]
        public void OverlappingNeighbor_PushesAwayByHalfOverlap()
        {
            // เพื่อนอยู่ทางซ้าย 0.1 → ซ้อนกัน (R−0.1) → ตัวเราถอยขวาครึ่งหนึ่ง (อีกฝ่ายถอยอีกครึ่ง)
            var c = Correct(Vector3.zero, At(new Vector3(-0.1f, 0f, 0f)));
            Assert.AreEqual((R - 0.1f) * 0.5f, c.x, Tol, "ต้องถอยครึ่งระยะที่ซ้อน");
            Assert.AreEqual(0f, c.y, Tol);
        }

        [Test]
        public void PairSeparates_ExactlyByOverlap_WhenBothApplyTheirHalf()
        {
            // คู่หนึ่งคู่: ต่างคนต่างถอยครึ่ง → ระยะสุดท้าย = R พอดี (แยกออกใน 1 เฟรม ไม่ต้องรู้จักกัน)
            var a = new Vector3(0f, 0f, 0f);
            var b = new Vector3(0.2f, 0f, 0f);
            var na = Correct(a, At(b));
            var nb = Correct(b, At(a));
            float finalDist = ((b + nb) - (a + na)).x;
            Assert.AreEqual(R, finalDist, Tol, "หลังต่างคนต่างถอยครึ่ง ต้องห่างกันเท่ารัศมีพอดี");
        }

        [Test]
        public void SymmetricNeighbors_CancelOut()
        {
            // ถูกขนาบซ้าย-ขวาเท่ากัน → แรงหักล้าง ไม่ขยับ (ปล่อยให้อีกสองตัวถอยเอง)
            var c = Correct(Vector3.zero, At(new Vector3(-0.1f, 0f, 0f), new Vector3(0.1f, 0f, 0f)));
            Assert.AreEqual(0f, c.x, Tol);
            Assert.AreEqual(0f, c.y, Tol);
        }

        // ── พื้น iso: แกน y ถูกบีบครึ่ง ⇒ ระยะจริงบนพื้น = dy × 2 ──

        [Test]
        public void IsoStretch_VerticalNeighbor_MeasuredOnGroundNotScreen()
        {
            // ห่างแกน y เพียง 0.18 (บนจอดูใกล้ < R) แต่บนพื้นจริง = 0.36 > R ⇒ ไม่ซ้อน ไม่ต้องขยับ
            // ถ้าวัดใน world space ตรง ๆ (ไม่ยืด y) จะเผลอผลักออก — นี่คือบั๊กที่เทสต์นี้กัน
            var c = Correct(Vector3.zero, At(new Vector3(0f, 0.18f, 0f)));
            Assert.AreEqual(Vector3.zero, c, "0.18 × 2 = 0.36 > รัศมี → ไม่ใช่การซ้อน");
        }

        [Test]
        public void IsoStretch_CorrectionIsSquashedBackToWorldSpace()
        {
            // ซ้อนกันตามแกน y: ground dy = 0.2 → ถอย ground (R−0.2)/2 → world y หารด้วย 2 กลับ
            var c = Correct(Vector3.zero, At(new Vector3(0f, -0.1f, 0f)));
            float expectedGround = (R - 0.2f) * 0.5f;
            Assert.AreEqual(expectedGround / Y, c.y, Tol, "ค่าที่คืนต้องอยู่ใน world space (บีบ y กลับแล้ว)");
            Assert.AreEqual(0f, c.x, Tol);
        }

        // ── เคสมุม: ซ้อนกันสนิท / เพดานต่อเฟรม ──

        [Test]
        public void ExactOverlap_PushesDeterministically_AndNotZero()
        {
            var c1 = Correct(Vector3.zero, At(Vector3.zero));
            var c2 = Correct(Vector3.zero, At(Vector3.zero));
            Assert.AreNotEqual(Vector3.zero, c1, "ซ้อนสนิทต้องแยกออก ไม่ใช่ค้างทับกัน");
            Assert.AreEqual(c1, c2, "ต้อง deterministic (ไม่ใช้ Random) — ไม่งั้นภาพสั่นทุกเฟรม");
        }

        [Test]
        public void ExactOverlap_ManyWorkers_GetDifferentDirections()
        {
            // ทุกตัวซ้อนที่จุดเดียวกัน: ถ้าแจกทิศเดียวกันหมด กองนี้จะเคลื่อนไปด้วยกันโดยไม่แยกออก
            var c0 = WorkerSeparation.Correction(Vector3.zero, At(Vector3.zero), R, Y, 0f);
            var c1 = WorkerSeparation.Correction(Vector3.zero, At(Vector3.zero, Vector3.zero), R, Y, 0f);
            Assert.AreNotEqual(c0, c1, "เพื่อนบ้านคนที่ 2 ต้องได้ทิศต่างจากคนแรก (มุมทอง)");
        }

        [Test]
        public void MaxCorrection_ClampsPerFrameDistance()
        {
            // ซ้อนสนิท → ถอยเต็ม R/2 = 0.17 แต่เพดาน 0.05 ⇒ ขยับได้แค่ 0.05 (วัดใน ground space)
            var c = Correct(Vector3.zero, At(Vector3.zero), max: 0.05f);
            float ground = Mathf.Sqrt(c.x * c.x + (c.y * Y) * (c.y * Y));
            Assert.AreEqual(0.05f, ground, Tol, "ต้องไม่กระเด็นเกินเพดานต่อเฟรม");
        }

        [Test]
        public void ZeroRadius_DisablesSystem()
        {
            Assert.AreEqual(Vector3.zero, WorkerSeparation.Correction(Vector3.zero, At(Vector3.zero), 0f, Y, 0f));
        }
    }
}
