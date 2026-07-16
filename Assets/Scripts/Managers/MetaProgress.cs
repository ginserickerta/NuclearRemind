using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// คลังความรู้ถาวร (V4 §9/§16) — Knowledge bank + Codex ที่ปลดล็อกแล้ว "คงข้ามรอบเล่น"
    /// เก็บใน PlayerPrefs · ทรัพยากรอื่นรีเซ็ตทุก restart ยกเว้น 2 ค่านี้
    /// </summary>
    public static class MetaProgress
    {
        private const string KeyKnowledge = "meta_knowledgeBank";
        private const string KeyCodex = "meta_unlockedCodex";
        private const string KeyMastery = "meta_masteryBank"; // v6.3 §21 — quizzes mastered, persistent

        public static int KnowledgeBank { get; private set; }
        public static HashSet<string> UnlockedCodex { get; private set; } = new HashSet<string>();
        /// <summary>★ v6.3 (GDD §16/§21): quizIds mastered — permanent across runs, like UnlockedCodex.</summary>
        public static HashSet<string> MasteryBank { get; private set; } = new HashSet<string>();

        /// <summary>โหลดจาก PlayerPrefs (เรียกตอนเริ่มเกม)</summary>
        public static void Load()
        {
            KnowledgeBank = PlayerPrefs.GetInt(KeyKnowledge, 0);
            UnlockedCodex = ParseSet(PlayerPrefs.GetString(KeyCodex, ""));
            MasteryBank = ParseSet(PlayerPrefs.GetString(KeyMastery, ""));
        }

        private static HashSet<string> ParseSet(string raw)
        {
            var set = new HashSet<string>();
            if (!string.IsNullOrEmpty(raw))
                foreach (var id in raw.Split(','))
                    if (!string.IsNullOrEmpty(id)) set.Add(id);
            return set;
        }

        /// <summary>บันทึกลง PlayerPrefs</summary>
        public static void Save()
        {
            PlayerPrefs.SetInt(KeyKnowledge, KnowledgeBank);
            PlayerPrefs.SetString(KeyCodex, string.Join(",", UnlockedCodex));
            PlayerPrefs.SetString(KeyMastery, string.Join(",", MasteryBank));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// เก็บสถิติรอบนี้ (เรียกตอนจบเกม): Knowledge เก็บค่าสูงสุด, Codex สะสมเพิ่ม แล้ว Save
        /// </summary>
        public static void Capture(int currentKnowledge, IEnumerable<string> unlockedCodex)
        {
            KnowledgeBank = Mathf.Max(KnowledgeBank, currentKnowledge);
            if (unlockedCodex != null)
                foreach (var id in unlockedCodex)
                    if (!string.IsNullOrEmpty(id)) UnlockedCodex.Add(id);
            Save();
        }

        /// <summary>
        /// เพิ่ม Codex id เดี่ยวแล้ว Save ทันที (Codex_Spec §5: ปลดจากควิซ → เข้า MetaProgress → Save()
        /// ไม่รอจบเกม — กันหายถ้าเกม crash/ปิดกลางคัน)
        /// </summary>
        public static void AddCodex(string entryId)
        {
            if (string.IsNullOrEmpty(entryId)) return;
            if (UnlockedCodex.Add(entryId)) Save();
        }

        /// <summary>
        /// เพิ่ม mastery (quizId) แล้ว Save ทันที — ★ ถาวรข้ามรอบเล่น (GDD §21 Persistence).
        /// MasteryRegistry.Grant เรียกเฉพาะตอน Application.isPlaying (กัน EditMode เขียน PlayerPrefs).
        /// </summary>
        public static void AddMastery(string quizId)
        {
            if (string.IsNullOrEmpty(quizId)) return;
            if (MasteryBank.Add(quizId)) Save();
        }

        /// <summary>ล้างคลังความรู้ทั้งหมด (new game+ reset / เทสต์)</summary>
        public static void ResetAll()
        {
            KnowledgeBank = 0;
            UnlockedCodex.Clear();
            MasteryBank.Clear();
            PlayerPrefs.DeleteKey(KeyKnowledge);
            PlayerPrefs.DeleteKey(KeyCodex);
            PlayerPrefs.DeleteKey(KeyMastery);
            PlayerPrefs.Save();
        }

        /// <summary>In-memory clear for EditMode tests — no PlayerPrefs touch (keeps tests hermetic).</summary>
        public static void ClearForTest()
        {
            KnowledgeBank = 0;
            UnlockedCodex.Clear();
            MasteryBank.Clear();
        }
    }
}
