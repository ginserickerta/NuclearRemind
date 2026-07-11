using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ข้อมูลหนึ่ง entry ใน Learning Codex (Codex_Spec v8 — 11 entry ปลดจากควิซ)
    /// สร้างเป็น ScriptableObject ต่อ entry — เนื้อหาเขียนเป็นภาษาไทยระดับมัธยม
    /// สถานะปลดล็อก (isUnlocked) ไม่เก็บใน SO — อ่านจาก CodexManager/MetaProgress (§16 ถาวรข้ามรอบ)
    /// ไม่มีฟิลด์ผู้บรรยาย (speaker) — v8 ตัด VESTA ออกจาก Codex
    /// </summary>
    [CreateAssetMenu(fileName = "NewCodexEntry", menuName = "NuclearReMind/CodexEntry")]
    public class CodexEntry : ScriptableObject
    {
        [Header("Identity")]
        public string entryId;         // เช่น "codex_nuclear_fusion"
        public string title;           // หัวข้อไทย (titleTh)
        public string titleEn;         // หัวข้ออังกฤษ เช่น "Nuclear Fusion"
        public QuizCategory category = QuizCategory.Reactor; // 1 ใน 4 — สี pill/แท็บตาม §17
        public string branch;          // (เก่า — คงไว้กันโค้ด/asset เดิมพัง · UI ใหม่ใช้ category)

        [Header("Content")]
        [TextArea(5, 20)]
        public string content;         // bodyText — คำอธิบายจากควิซที่ปลดมัน
        public Sprite illustration;
        public string iconName;        // ไอคอนหน้า entry เช่น "droplet", "atom-2" (UI แปลงเป็นสัญลักษณ์)

        [Header("Unlock")]
        public string unlockedFrom;    // แหล่งที่ปลด โชว์ผู้เล่น เช่น "ควิซ #8" (สเปก §6.2 — hint ตอนล็อก)
        public int researchPointCost;  // (เก่า — spec ใหม่ปลดจากควิซเท่านั้น ทุก entry = 0)
        public string unlockedByEvent; // (เก่า — event ID · spec ใหม่ไม่ใช้ ทุก entry = "")
    }
}
