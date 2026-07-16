using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Research Lead (GDD §19) — the "clue" that makes one note researchable.
    /// Leads unlock ONCE per game (unlike cards which repeat on cooldown).
    /// Sources: SoftTriggerWatcher (state-bound) or Data Recovery records (Sprint 5).
    ///
    /// The runtime lead→note map (bug #11: must cover all 8 notes) is enforced by
    /// KnowledgeDB.BuildLeadMap — these assets are optional display/data carriers;
    /// the map itself never depends on asset wiring so a missing asset can't skip a note.
    /// </summary>
    [CreateAssetMenu(fileName = "NewResearchLead", menuName = "NRM/Research Lead")]
    public class ResearchLeadSO : ScriptableObject
    {
        public string leadId;              // water_analysis / magnetic_theory / ...
        public string noteId;              // the note this lead opens
        [TextArea(1, 3)] public string hintText;   // shown when the lead pops (NOTES.md lead hint)
    }
}
