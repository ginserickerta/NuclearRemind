using System.Text;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: เครื่องมือ Editor ตรวจระบบควิซตอนกด Play — ดูสถานะทุกข้อ
    /// และสั่งเด้งควิซทันทีเพื่อแยกว่าปัญหาอยู่ที่เงื่อนไขปลดล็อกหรือที่ UI
    /// Play-mode diagnostics for the quiz pipeline (GDD §21 / QUIZZES.md).
    ///
    /// A quiz surfaces only after its knowledge has been USED (requiresApplied → QuizAvailability) plus
    /// quizRevealDelayDays, which can be many in-game days into a run. These tools make the two failure
    /// modes distinguishable without playing that far:
    ///   1. "No prompt appeared" because nothing is answerable yet   → Log Quiz States
    ///   2. "No prompt appeared" although one is answerable          → Force Prompt
    ///
    /// Force Prompt raises OnQuizShown directly, so if nothing appears the fault is in the UI path
    /// (EventManager → QuizPromptUI), not in the availability rules.
    /// </summary>
    public static class QuizTools
    {
        private const string Root = "NuclearReMind/Quizzes/";

        // เมนูนี้: พิมพ์สถานะควิซทุกข้อลง Console (ตอบแล้ว/ตอบได้/ยังไม่ปลด)
        [MenuItem(Root + "Log Quiz States", priority = 0)]
        private static void LogStates()
        {
            if (!RequirePlayMode()) return;

            var cq = CodexQuizManager.Instance;
            if (cq == null) { Debug.LogError("[Quiz] ไม่มี CodexQuizManager"); return; }

            var sb = new StringBuilder($"[Quiz] สถานะควิซ — เชี่ยวชาญ {cq.UnlockedCodexCount}/{cq.TotalCodex} · ตอบได้ตอนนี้ {cq.AnswerableCount}\n");
            foreach (var v in cq.GetAll())
            {
                string mark = v.state == QuizState.Earned ? "✔" : v.state == QuizState.Answerable ? "▶" : "·";
                string id = v.quiz != null ? v.quiz.quizId : "?";
                sb.Append("  ").Append(mark).Append(' ').Append(id.PadRight(22))
                  .Append(v.state).Append('\n');
            }
            sb.Append("  (· = ยังไม่ได้ใช้ความรู้นั้นจริง หรือยังไม่ครบ quizRevealDelayDays)");
            Debug.Log(sb.ToString());
        }

        // เมนูนี้: สั่งเด้งควิซข้อแรกที่ตอบได้ตอนนี้ (ทดสอบเส้นทาง UI)
        [MenuItem(Root + "Force Prompt — first answerable", priority = 1)]
        private static void ForceFirstAnswerable()
        {
            if (!RequirePlayMode()) return;

            var cq = CodexQuizManager.Instance;
            if (cq == null) return;
            foreach (var v in cq.GetAll())
                if (v.state == QuizState.Answerable && v.quiz != null)
                {
                    EventManager.Instance?.RaiseQuizShown(v.quiz);
                    Debug.Log($"[Quiz] สั่งเด้ง '{v.quiz.quizId}' แล้ว — ถ้าจอไม่ขึ้น ปัญหาอยู่ที่ UI ไม่ใช่เงื่อนไข");
                    return;
                }
            Debug.LogWarning("[Quiz] ยังไม่มีข้อไหนตอบได้ — ใช้ 'Force Prompt — any quiz' เพื่อทดสอบหน้าตาแผง");
        }

        /// <summary>
        /// Presents a quiz regardless of availability. UI-only smoke test: it bypasses the requiresApplied
        /// gate, so it proves the panel renders — it does NOT prove the gate works.
        /// </summary>
        // เมนูนี้: สั่งเด้งควิซข้อไหนก็ได้โดยข้ามเงื่อนไข (ทดสอบหน้าตาแผงเท่านั้น)
        [MenuItem(Root + "Force Prompt — any quiz (UI test)", priority = 2)]
        private static void ForceAny()
        {
            if (!RequirePlayMode()) return;

            var cq = CodexQuizManager.Instance;
            if (cq == null) return;
            foreach (var v in cq.GetAll())
                if (v.quiz != null && v.state != QuizState.Earned)
                {
                    EventManager.Instance?.RaiseQuizShown(v.quiz);
                    Debug.Log($"[Quiz] เด้ง '{v.quiz.quizId}' (ข้ามเงื่อนไข — ทดสอบหน้าตาแผงเท่านั้น)");
                    return;
                }
            Debug.LogWarning("[Quiz] ตอบครบทุกข้อแล้ว");
        }

        private static bool RequirePlayMode()
        {
            if (Application.isPlaying) return true;
            Debug.LogWarning("[Quiz] ต้องกด Play ก่อน — เครื่องมือนี้อ่านสถานะเกมที่กำลังรันอยู่");
            return false;
        }
    }
}
