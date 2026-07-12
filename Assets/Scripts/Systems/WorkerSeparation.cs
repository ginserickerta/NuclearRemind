using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// คณิตศาสตร์ "กันคนงานเดินทะลุกัน" (V4 §5) — pure ล้วน ไม่มี scene/component ให้เทสต์ตรวจได้
    ///
    /// ทำไมไม่ใช้ Collider2D/Rigidbody2D:
    ///   คนงานเป็น SpriteRenderer เปล่า ๆ ที่ WorkerView คำนวณ sortingOrder เองทุกเฟรมจาก iso depth
    ///   ถ้าใส่ฟิสิกส์ ตัวจะถูกดันในแกน world (วงกลม) ซึ่งผิดรูปทรงพื้น iso + physics step ไม่ตรงกับเฟรม
    ///   → เลือกวิธี "แก้ตำแหน่งหลังเดิน" (positional correction) แบบ crowd sim ราคาถูกแทน
    ///
    /// ★ กุญแจสำคัญ — พื้น iso ไม่ใช่วงกลม:
    ///   IsoToWorld บีบแกน y ด้วย tileHeight/tileWidth = 0.5 ⇒ วงกลมบนพื้นจริงกลายเป็นวงรี 2:1 บนจอ
    ///   จึงต้องยืด dy กลับด้วย yStretch = tileWidth/tileHeight (= 2) ก่อนวัดระยะ แล้วค่อยบีบกลับตอนคืนค่า
    ///   ถ้าวัดใน world space ตรง ๆ คนที่ยืน "หน้า-หลัง" กันจะถูกผลักแรงเกินจริงเท่าตัว
    ///
    /// วิธีแก้ overlap: ต่างคนต่างถอยคนละ "ครึ่งหนึ่ง" ของระยะที่ซ้อนกัน
    ///   → คู่หนึ่งคู่แยกออกพอดีใน 1 เฟรม โดยไม่ต้องรู้จักกัน (ไม่มี state ร่วม ไม่มีลำดับการอัปเดต)
    ///   ต่างจากการใช้ "แรงผลัก" ที่ต้องสู้กับความเร็วเดิน — ถ้าแรงน้อยกว่าความเร็ว ตัวละครจะเดินทะลุอยู่ดี
    /// </summary>
    public static class WorkerSeparation
    {
        /// <summary>ระยะห่างศูนย์กลางต่ำสุด (world x-units) — ค่ามาตรฐานของสูตร (เทสต์อ้างค่านี้)
        /// · ค่าที่ใช้จริงต่อคนงานตั้งที่ WorkerView.personalRadius (bump ให้ตัวไม่ซ้อน)</summary>
        public const float DefaultRadius = 0.34f;

        /// <summary>ตัวยืดแกน y เริ่มต้น = tileWidth/tileHeight ของกริดมาตรฐาน (1 / 0.5)</summary>
        public const float DefaultYStretch = 2f;

        // มุมทอง — กระจายทิศเมื่อสองตัวซ้อนกัน "สนิท" (d = 0 หาทิศไม่ได้)
        // ใช้ index ของเพื่อนบ้านเป็นตัวกำหนดทิศ → deterministic (เทสต์ได้) และไม่ผลักไปทางเดียวกันหมด
        private const float GoldenAngle = 2.39996323f;

        /// <summary>
        /// ระยะที่ต้องขยับ self ออกจากเพื่อนบ้านที่ซ้อนอยู่ (world units, บวกเข้ากับตำแหน่งได้เลย)
        /// others = ตำแหน่ง world ของคนงานตัวอื่น (ไม่รวม self) · maxCorrection ≤ 0 = ไม่จำกัด
        /// </summary>
        public static Vector3 Correction(Vector3 self, IReadOnlyList<Vector3> others,
                                         float radius, float yStretch, float maxCorrection)
        {
            if (others == null || others.Count == 0 || radius <= 0f) return Vector3.zero;
            if (yStretch <= 0f) yStretch = 1f;

            float r2 = radius * radius;
            float sumX = 0f, sumY = 0f; // สะสมใน ground space (แกน y ยืดแล้ว)

            for (int i = 0; i < others.Count; i++)
            {
                float dx = self.x - others[i].x;
                float dy = (self.y - others[i].y) * yStretch;
                float d2 = dx * dx + dy * dy;
                if (d2 >= r2) continue; // ไม่ซ้อน — ไม่ต้องขยับ

                float dirX, dirY, overlap;
                if (d2 < 1e-10f)
                {
                    // ซ้อนกันสนิท: ไม่มีทิศให้ถอย → แจกทิศตามมุมทอง (ตัวอื่นได้คนละมุม)
                    float a = i * GoldenAngle;
                    dirX = Mathf.Cos(a);
                    dirY = Mathf.Sin(a);
                    overlap = radius;
                }
                else
                {
                    float d = Mathf.Sqrt(d2);
                    dirX = dx / d;
                    dirY = dy / d;
                    overlap = radius - d;
                }

                // ครึ่งเดียว — อีกฝ่ายถอยอีกครึ่งเอง (คู่แยกออกเต็มระยะพอดี)
                sumX += dirX * overlap * 0.5f;
                sumY += dirY * overlap * 0.5f;
            }

            if (sumX == 0f && sumY == 0f) return Vector3.zero;

            // จำกัดระยะต่อเฟรมใน ground space — กันตัวละครกระเด็นตอนคนแออัด
            if (maxCorrection > 0f)
            {
                float mag = Mathf.Sqrt(sumX * sumX + sumY * sumY);
                if (mag > maxCorrection)
                {
                    float k = maxCorrection / mag;
                    sumX *= k;
                    sumY *= k;
                }
            }

            return new Vector3(sumX, sumY / yStretch, 0f); // ground → world (บีบแกน y กลับ)
        }
    }
}
