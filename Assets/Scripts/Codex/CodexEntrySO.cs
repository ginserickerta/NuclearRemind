using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Codex entry (GDD §21 / CODEX.md) — one permanent knowledge card, unlocked by answering its
    /// quiz correctly. 11 total, 1:1 with QUIZZES.md. Unlock state is NOT stored here — it lives in
    /// MetaProgress.UnlockedCodex (persistent across runs) and is read through CodexQuizManager.
    ///
    /// Distinct type from the legacy Data/CodexEntry.cs, which the old event-driven CodexManager
    /// still uses; the v6.3 quiz-driven path uses THIS type, so the two coexist until cutover.
    /// (No speaker field — v8 removed VESTA from the Codex. No isUnlocked — that's state, not data.)
    /// </summary>
    [CreateAssetMenu(fileName = "NewCodexEntrySO", menuName = "NRM/Codex Entry")]
    public class CodexEntrySO : ScriptableObject
    {
        [Header("Identity")]
        public string entryId;          // codex_deuterium ...
        public string titleTh;
        public string titleEn;
        public QuizCategory category;   // Reactor / Medical / Agriculture / Ethics (tab + colour)

        [Header("Content")]
        [TextArea(4, 10)]
        public string bodyText;         // == the unlocking quiz's explanation (CODEX.md §7)
        public string iconName;         // Tabler icon hint (droplet, atom, ...)

        [Header("Unlock")]
        public string unlockedFromQuiz; // ★ quizId that unlocks this — never "Day X"
    }
}
