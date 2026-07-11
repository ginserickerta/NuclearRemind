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

        public static int KnowledgeBank { get; private set; }
        public static HashSet<string> UnlockedCodex { get; private set; } = new HashSet<string>();

        /// <summary>โหลดจาก PlayerPrefs (เรียกตอนเริ่มเกม)</summary>
        public static void Load()
        {
            KnowledgeBank = PlayerPrefs.GetInt(KeyKnowledge, 0);

            UnlockedCodex = new HashSet<string>();
            string raw = PlayerPrefs.GetString(KeyCodex, "");
            if (!string.IsNullOrEmpty(raw))
                foreach (var id in raw.Split(','))
                    if (!string.IsNullOrEmpty(id)) UnlockedCodex.Add(id);
        }

        /// <summary>บันทึกลง PlayerPrefs</summary>
        public static void Save()
        {
            PlayerPrefs.SetInt(KeyKnowledge, KnowledgeBank);
            PlayerPrefs.SetString(KeyCodex, string.Join(",", UnlockedCodex));
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

        /// <summary>ล้างคลังความรู้ทั้งหมด (new game+ reset / เทสต์)</summary>
        public static void ResetAll()
        {
            KnowledgeBank = 0;
            UnlockedCodex.Clear();
            PlayerPrefs.DeleteKey(KeyKnowledge);
            PlayerPrefs.DeleteKey(KeyCodex);
            PlayerPrefs.Save();
        }
    }
}
