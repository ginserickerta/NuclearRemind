using System.Text;

namespace NuclearReMind
{
    /// <summary>
    /// Crisis-card formatting (CARDS.md). Pure string builders so the option rows — especially the
    /// LOCKED ones — are unit-testable; the F9 dev panel and any uGUI view render these.
    ///
    /// ★ The whole point (CLAUDE.md rule #6 / CARDS.md §implement): a locked option is shown greyed
    /// with 🔒 and the note it needs — NEVER hidden. Hiding it would erase the reason to research.
    /// </summary>
    public static class CrisisCardPanel
    {
        public static string Header(CrisisCardSO card) =>
            card == null ? "" : $"⚠  {card.title}\n{card.description}";

        /// <summary>The character-clash block (may be empty — cards 1/2/3/8 have one voice or none).</summary>
        public static string Dialogue(CrisisCardSO card)
        {
            if (card?.dialogueLines == null || card.dialogueLines.Length == 0) return "";
            var sb = new StringBuilder();
            foreach (var line in card.dialogueLines)
                if (!string.IsNullOrEmpty(line)) sb.AppendLine(line);
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// One option row. Unlocked → "[A] label — effect". Locked → greyed "[C] 🔒 label · ต้องวิจัย
        /// [note]" so the player can see the better path exists. `letter` is A/B/C by index.
        /// </summary>
        public static string OptionRow(int index, CardOption opt, KnowledgeDB db)
        {
            if (opt == null) return "";
            string letter = Letter(index);
            if (opt.IsLocked(db))
                return $"[{letter}] [ล็อก] {opt.label} · ต้องวิจัย \"{NoteTitle(db, opt.requiredNoteId)}\" ก่อน";
            return $"[{letter}] {opt.label}\n      {opt.effectSummary}";
        }

        /// <summary>True if the option should be rendered disabled/greyed (locked).</summary>
        public static bool IsRowLocked(CardOption opt, KnowledgeDB db) => opt != null && opt.IsLocked(db);

        public static string Letter(int index) => index >= 0 && index < 3 ? new[] { "A", "B", "C" }[index] : "?";

        // note id → its Thai title (for the "ต้องวิจัย ..." hint); falls back to the id
        private static string NoteTitle(KnowledgeDB db, string noteId)
        {
            if (string.IsNullOrEmpty(noteId)) return "";
            var note = db?.GetNote(noteId);
            return note != null && !string.IsNullOrEmpty(note.title) ? note.title : noteId;
        }
    }
}
