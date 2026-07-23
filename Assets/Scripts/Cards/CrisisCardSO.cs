using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: asset การ์ดวิกฤต 1 ใบ — เก็บข้อความ (หัวเรื่อง, บทตัวละครเถียงกัน, ตัวเลือก 2–3 ข้อ) + cooldown
    /// [TH] เงื่อนไข trigger เป็นโค้ดใน CardTriggers ผูกกับ state จริง (heat, hunger ฯลฯ) ไม่ใช่วันที่ (กติกาข้อ 1)
    /// Crisis Card (GDD §16/§25 / CARDS.md) — a state-triggered decision with 2–3 options, some
    /// locked behind research. dialogueLines carry the character-clash script (cards 4/5/6/7 have
    /// two NPCs arguing in one box, CARDS.md §Conflict); order is fixed, never shuffle.
    ///
    /// Trigger + cooldown are read by CardManager; the trigger CONDITION itself is code in
    /// CardTriggers (state-bound, never `if (day == X)` — CLAUDE.md rule #1). This asset holds the
    /// text and the tunables.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCrisisCard", menuName = "NRM/Crisis Card")]
    public class CrisisCardSO : ScriptableObject
    {
        [Header("Identity")]
        public string cardId;               // heat / sick / spoil / hunger / ... (CardIds)
        public string title;                // "ความร้อนเกินพิกัด"
        [TextArea(2, 4)] public string description;

        [Header("Character clash (CARDS.md — fixed order, may be empty)")]
        [TextArea(1, 2)] public string[] dialogueLines;  // "Kova: ...", "Mira: ...", "Dorn: ..."

        [Header("Trigger tunables")]
        public int cooldownDays = 4;        // may re-fire after this many days (CONFIG.md 🔒 CARDS)
        public bool onceOnly;               // ★ Decree — fires at most once per game

        [Header("Options (fixed order — index 0 = A, 1 = B, 2 = C)")]
        public CardOption[] options;
    }
}
