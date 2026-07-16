using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Research Note (GDD §16/§19 — replaces InfoCardSO's role as the knowledge carrier).
    /// One asset per note — 8 total, data verbatim from docs/NOTES.md (created by
    /// menu NRM > Setup Research Notes). All numbers live in the asset, never in code.
    ///
    /// Deviations from the §16 sketch, both deliberate:
    /// - unlocksBuildings is string[] ids (project uses BuildingData, and the new buildings'
    ///   assets don't exist yet — id gate keeps Sprint 2 self-contained)
    /// - quizzes are NOT referenced here: QUIZZES.md puts linkedNoteId on the quiz side,
    ///   so lookup goes quiz → note (quizIds kept for display only)
    /// </summary>
    [CreateAssetMenu(fileName = "NewResearchNote", menuName = "NRM/Research Note")]
    public class ResearchNoteSO : ScriptableObject
    {
        [Header("Identity")]
        public string noteId;              // deuterium / confinement / nuclear_medicine / ...
        public string title;
        public string category;            // เชื้อเพลิง / เตาปฏิกรณ์ / การแพทย์ / เกษตร / ระบบ / บริหาร

        [Header("Knowledge (NOTES.md — verbatim, ห้ามแต่งใหม่)")]
        [TextArea(4, 14)] public string knowledgeBody;
        [TextArea(1, 3)] public string leadHint;        // shown in research menu before starting

        [Header("Cost (paid ONCE at start — LOCK bug #2)")]
        public int researcherSlots;
        public int daysRequired;
        public int costPower;
        public int costIron;
        public int costLabMat;

        [Header("Gating")]
        public string requiredLead;        // lead that must be unlocked first (lead_map — bug #11)
        public string[] prerequisiteNotes; // e.g. tritium requires deuterium

        [Header("Unlocks")]
        public string[] unlocksBuildings;  // display data (research panel) — the actual gate is BuildingData.requiredNoteId
        public string[] unlocksCommands;
        public string[] quizIds;           // display only — quiz assets link back via linkedNoteId
        public string linkedRecordId;      // record shown alongside (STORY.md), "" = none

        [Header("Completion dialogue (NOTES.md ตารางท้ายไฟล์ — NOT via BarkManager)")]
        public string completionSpeaker;   // Kova / Mira / Dorn
        [TextArea(1, 3)] public string completionLine;
    }
}
