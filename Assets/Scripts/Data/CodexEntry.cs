using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ข้อมูลหนึ่ง entry ใน Learning Codex
    /// สร้างเป็น ScriptableObject ต่อ entry — เนื้อหาเขียนเป็นภาษาไทยระดับมัธยม
    /// </summary>
    [CreateAssetMenu(fileName = "NewCodexEntry", menuName = "NuclearReMind/CodexEntry")]
    public class CodexEntry : ScriptableObject
    {
        [Header("Identity")]
        public string entryId;
        public string title;
        public string branch;          // "Core" | "Agriculture" | "Medical" | "Environment"

        [Header("Content")]
        [TextArea(5, 20)]
        public string content;         // ภาษาไทย เขียนให้มัธยมอ่านได้
        public Sprite illustration;

        [Header("Unlock")]
        public int researchPointCost;  // 0 = unlock อัตโนมัติตาม unlockedByEvent
        public string unlockedByEvent; // event ID เช่น "phase_1_complete", "" = ต้องซื้อด้วย RP
    }
}
