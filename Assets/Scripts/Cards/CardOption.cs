using System;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: ตัวเลือก 1 ข้อบนการ์ดวิกฤต — ตัวเลือกที่ดีจะถูกล็อก 🔒 จนกว่าจะวิจัย Note ที่กำหนด
    /// [TH] กติกาข้อ 6: ตัวเลือกที่ล็อกต้อง "โชว์ให้เห็นว่ามีอยู่" (จางลง + 🔒) ห้ามซ่อน — นี่คือแรงจูงใจให้วิจัย
    /// One choice on a crisis card (GDD §16/§25 / CARDS.md). A "good" option is gated behind a
    /// research note: while requiredNoteId names a not-yet-completed note the option is LOCKED —
    /// shown greyed with 🔒 and the note name, never hidden (CLAUDE.md rule #6, CARDS.md §implement).
    /// That visible-but-unreachable option is exactly what makes research feel worth doing.
    /// </summary>
    [Serializable]
    public class CardOption
    {
        public string label;                 // "เร่งไฟเข้าระบบหล่อเย็น"
        [TextArea(1, 3)] public string effectSummary;  // "พลังงาน −250 · HEAT −8 · Hope −4"
        [TextArea(1, 2)] public string afterText;      // closing line, "" = silent (CARDS.md #7A — intentional)
        public string requiredNoteId;        // "" = always available; else locked until that note is done
        public CardEffect effect = new CardEffect();

        /// <summary>
        /// [TH] เช็คว่ายังล็อกอยู่ไหม — ล็อกตราบใดที่ Note ที่ต้องใช้ยังวิจัยไม่เสร็จ (KnowledgeDB เป็นผู้ตัดสินเดียว)
        /// Locked while its required note isn't completed (KnowledgeDB is the single gate).
        /// </summary>
        public bool IsLocked(KnowledgeDB db) =>
            !string.IsNullOrEmpty(requiredNoteId) && (db == null || !db.HasNote(requiredNoteId));
    }
}
