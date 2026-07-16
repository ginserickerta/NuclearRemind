using System;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
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

        /// <summary>Locked while its required note isn't completed (KnowledgeDB is the single gate).</summary>
        public bool IsLocked(KnowledgeDB db) =>
            !string.IsNullOrEmpty(requiredNoteId) && (db == null || !db.HasNote(requiredNoteId));
    }
}
