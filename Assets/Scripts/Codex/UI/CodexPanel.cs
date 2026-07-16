namespace NuclearReMind
{
    /// <summary>
    /// Codex panel formatting (CODEX.md §6). Pure string builders so the header count and the
    /// locked/unlocked rows are unit-testable; the F9 dev panel and any future uGUI view render
    /// these. Locked entries show "??? + 🔒" and are NEVER hidden — the player must see how much
    /// is left to collect (replay motivation, same principle as locked card options — CODEX.md §6).
    /// </summary>
    public static class CodexPanel
    {
        /// <summary>Header — "ปลดล็อกแล้ว x / 11" (always x/total, never per-category).</summary>
        public static string CountLine(int unlocked, int total) =>
            $"คลังความรู้ (Codex)   ปลดล็อกแล้ว {unlocked} / {total}";

        /// <summary>Left-list row: "● Title" when unlocked, "🔒 ? ? ?" when locked (never blank/hidden).</summary>
        public static string EntryRow(CodexEntrySO e, bool unlocked)
        {
            if (e == null) return "";
            return unlocked ? $"● {e.titleTh} · {e.titleEn}" : "🔒 ? ? ?";
        }

        /// <summary>Right-detail for an unlocked entry.</summary>
        public static string DetailUnlocked(CodexEntrySO e) =>
            e == null ? "" : $"{e.titleTh}\n[{CategoryLabel(e.category)}]\n\n{e.bodyText}";

        /// <summary>Right-detail for a locked entry — a hint pointing at the quiz that unlocks it.</summary>
        public static string DetailLocked(QuizQuestionSO quiz) =>
            $"ยังไม่ปลดล็อก — ปลดได้จากควิซ [{(quiz != null ? Topic(quiz) : "?")}]";

        /// <summary>Thai tab label per category (CODEX.md §6 filter bar).</summary>
        public static string CategoryLabel(QuizCategory c)
        {
            switch (c)
            {
                case QuizCategory.Reactor:     return "เตา/ฟิวชัน";
                case QuizCategory.Medical:     return "แพทย์/รังสี";
                case QuizCategory.Agriculture: return "เกษตร";
                case QuizCategory.Ethics:      return "จริยธรรม";
                default:                       return "";
            }
        }

        private static string Topic(QuizQuestionSO q) =>
            !string.IsNullOrEmpty(q.topicTitle) ? q.topicTitle : q.quizId;
    }
}
