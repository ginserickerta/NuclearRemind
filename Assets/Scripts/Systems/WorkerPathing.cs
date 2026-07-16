using System;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// คณิตศาสตร์ "กันคนงานเดินทะลุอาคาร" (V4 §5) — pure ล้วน ไม่มี scene/component ให้เทสต์ตรวจได้
    ///
    /// ทำไมไม่ใช้ Collider2D + Physics2D:
    ///   GridManager.Cell.isOccupied เป็นแหล่งความจริงของ "ช่องไหนมีของตั้งอยู่" อยู่แล้ว —
    ///   PlacementController (วาง) · PrePlacedBuilding (CORE TOWER/อนุสรณ์) · OreDepositManager (แหล่งแร่)
    ///   เขียนเข้ามาครบ และ GridManager เคลียร์ให้ตอนทุบ/โหลดเซฟ
    ///   ถ้าเพิ่ม collider จะกลายเป็นข้อมูล "เดินไม่ได้" สองชุดที่ต้องซิงก์กันเอง (ผิดกฎแหล่งความจริงเดียว)
    ///   และ isOccupied = ฐานอาคารพอดี ไม่ใช่กรอบสี่เหลี่ยมรอบ sprite ที่ลอยเลยฐานขึ้นไป
    ///
    /// วิธี: "ไถลตามกำแพง" (wall slide) — ถ้าก้าวตรงไปชนอาคาร ให้ลองก้าวเฉพาะแกน col แล้วแกน row
    ///   ⇒ คนงานลื่นไถลอ้อมมุมอาคารแทนที่จะจมเข้าไป โดยไม่ต้องคำนวณเส้นทางล่วงหน้า
    ///
    /// ★ ข้อจำกัดที่ต้องรู้: นี่ไม่ใช่ pathfinding (A*) — มันมองแค่ก้าวถัดไปก้าวเดียว
    ///   ถ้าอาคารเรียงเป็นกำแพงยาวหรือเป็นรูปตัว U คนงานจะไปติดหน้ากำแพง ไม่อ้อมกลับ
    ///   ผังเมืองเกมนี้อาคารวางกระจาย จึงพอ — ถ้าวันหนึ่งไม่พอ ค่อยเปลี่ยน Step() เป็น A* จุดเดียว
    /// </summary>
    public static class WorkerPathing
    {
        /// <summary>ช่องกริดที่พิกัด iso ทศนิยมนี้ยืนอยู่ — ปัดแบบเดียวกับ GridManager.WorldToIso</summary>
        public static Vector2Int CellOf(Vector2 iso) =>
            new Vector2Int(Mathf.RoundToInt(iso.x), Mathf.RoundToInt(iso.y));

        /// <summary>
        /// ตำแหน่ง iso ใหม่หลังพยายามก้าวจาก from ไป to โดยไม่ทะลุอาคาร
        /// isBlocked   = ช่องนี้เดินผ่านไม่ได้ (isOccupied หรือนอกกริด) · null = ไม่เช็ค
        /// crossBlocked = การก้าว (fromCell → cell) นี้ถูก "ขอบ" กั้นไหม เช่นรั้วโซน B (ต้องอ้อมไปประตู) · null = ไม่เช็ค
        ///   ต่างจาก isBlocked ตรงที่ขึ้นกับ "คู่ช่อง" ไม่ใช่สภาพของช่องปลายทางเดี่ยว ๆ
        /// </summary>
        public static Vector2 Step(Vector2 from, Vector2 to, Func<Vector2Int, bool> isBlocked,
            Func<Vector2Int, Vector2Int, bool> crossBlocked = null)
        {
            if (isBlocked == null && crossBlocked == null) return to;

            var fromCell = CellOf(from);

            // ช่องปลายทางเดินไม่ได้ (มีของตั้ง/นอกกริด) หรือก้าวนี้ข้ามขอบที่ถูกกั้น (รั้วโซน)
            bool Blocked(Vector2Int cell) =>
                (isBlocked != null && isBlocked(cell)) ||
                (crossBlocked != null && crossBlocked(fromCell, cell));

            if (!Blocked(CellOf(to))) return to; // ทางโล่ง

            // ★ ยืนอยู่ในช่องต้องห้ามอยู่แล้ว (เช่นถูกดันเข้าไป/อาคารถูกสร้างทับ) → ปล่อยให้เดินออกได้
            //   เช็คเฉพาะ isBlocked (การข้ามขอบต้องมีคู่ from→to เสมอ ไม่ใช่สภาพช่องเดียว) กันคนงานติดในตึกตลอดกาล
            if (isBlocked != null && isBlocked(fromCell)) return to;

            // ชนแล้ว — ลองไถลทีละแกน (แกนที่ไม่ชนจะพาอ้อมมุม/ไถลตามรั้วไปเอง)
            var slideCol = new Vector2(to.x, from.y);
            if (!Blocked(CellOf(slideCol))) return slideCol;

            var slideRow = new Vector2(from.x, to.y);
            if (!Blocked(CellOf(slideRow))) return slideRow;

            return from; // มุมอับ — ยืนนิ่งดีกว่าจมเข้าอาคาร/ทะลุรั้ว
        }
    }
}
