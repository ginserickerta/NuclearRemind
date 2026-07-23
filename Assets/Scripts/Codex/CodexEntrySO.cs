using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: การ์ดความรู้ถาวรหนึ่งใบในสารานุกรม Codex (มี 11 ใบ คู่ 1:1 กับควิซใน QUIZZES.md)
    /// [TH] ปลดล็อกด้วยการตอบควิซของมันถูกเท่านั้น — สถานะปลดล็อกไม่เก็บใน SO นี้ (อยู่ MetaProgress ข้ามรอบเล่น)
    /// [TH] เป็นคนละ type กับ Data/CodexEntry.cs (ระบบเก่า) — อยู่ร่วมกันจนกว่าจะ cutover
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
        public string bodyText;         // == the unlocking quiz's explanation (CODEX.md §7) · [TH] เนื้อความรู้ = คำอธิบายของควิซที่ปลดใบนี้
        public string iconName;         // Tabler icon hint (droplet, atom, ...)

        [Header("Unlock")]
        public string unlockedFromQuiz; // ★ quizId that unlocks this — never "Day X" · [TH] ปลดจากควิซเท่านั้น ห้ามผูกกับเลขวัน
    }
}
