using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Editor tools for the cross-run knowledge bank (MetaProgress + Achievements).
    ///
    /// Why this exists: the bank is deliberately permanent (STORY.md §④ "ความรู้ที่คุณได้ — ไม่มีวันหาย"),
    /// and Capture() keeps the maximum, so it only ever goes up. That is right for players and wrong for
    /// testing — after a few playtests the run no longer starts at GameConfigSO.startKnowledge, it starts
    /// at startKnowledge + bank, which silently makes the game easier every session and makes balance
    /// checks meaningless. Reset puts the machine back to a first-time player's state.
    ///
    /// It lives in PlayerPrefs, not in the scene or a save file, so nothing in the project can clear it.
    /// </summary>
    public static class MetaProgressTools
    {
        private const string MenuRoot = "NuclearReMind/Meta Progress/";

        [MenuItem(MenuRoot + "Show Current", priority = 100)]
        public static void ShowCurrent()
        {
            MetaProgress.Load();
            EditorUtility.DisplayDialog("คลังความรู้ถาวร (MetaProgress)", Describe(), "ปิด");
        }

        [MenuItem(MenuRoot + "Reset — เล่นใหม่แบบผู้เล่นครั้งแรก", priority = 101)]
        public static void ResetAll()
        {
            MetaProgress.Load();

            bool wipe = EditorUtility.DisplayDialog(
                "ล้างคลังความรู้ถาวร?",
                "กำลังจะลบของทั้งหมดนี้ทิ้ง:\n\n" + Describe() +
                "\nหลังล้าง เกมจะเริ่มที่ Knowledge " + StartKnowledge() +
                " เหมือนผู้เล่นที่เพิ่งเปิดเกมครั้งแรก\n\n" +
                "กู้คืนไม่ได้ — ค่าพวกนี้อยู่ใน PlayerPrefs ไม่ได้อยู่ใน git",
                "ล้างเลย", "ยกเลิก");

            if (!wipe) return;

            MetaProgress.ResetAll();
            Achievements.ResetAll();   // separate PlayerPrefs key — a MetaProgress wipe alone misses these
            Debug.Log("[MetaProgress] ล้างคลังความรู้ถาวรแล้ว — Knowledge bank / Codex / Mastery / Achievements = 0");
        }

        private static string Describe()
        {
            MetaProgress.Load();
            return
                $"Knowledge bank : {MetaProgress.KnowledgeBank}   (บวกเพิ่มจากค่าเริ่มต้น {StartKnowledge()} ทุกรอบ)\n" +
                $"Codex ที่ปลดแล้ว : {MetaProgress.UnlockedCodex.Count}\n" +
                $"Mastery ที่ได้   : {MetaProgress.MasteryBank.Count}/{QuizIds.All.Length}\n" +
                $"Achievements    : {Achievements.UnlockedCount}/{Achievements.All.Length}\n";
        }

        // Read the real config rather than repeating the number (rule #2: values live in one place).
        private static string StartKnowledge()
        {
            var cfg = GameConfigSO.Instance;
            return cfg != null ? Mathf.RoundToInt(cfg.startKnowledge).ToString() : "?";
        }
    }
}
