using UnityEngine;

namespace NuclearReMind
{
    /// <summary>Who speaks a bark (BARKS.md). Auren = the ▸ Inner Voice (no name shown).</summary>
    public enum BarkSpeaker { Kova, Mira, Dorn, Citizen, Auren }

    /// <summary>
    /// One bark line (GDD §16 / BARKS.md). Bound to STATE not day — the condition itself is code in
    /// BarkConditions (keyed by barkId), so numbers can move without touching the line. This asset
    /// holds only the text + the tuning (priority / cooldown / once).
    ///
    /// Writing rules live in BARKS.md: no intent for machines, no metaphor, no NPC drawing the moral,
    /// one fact per line, numbers over adjectives.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBark", menuName = "NRM/Bark")]
    public class BarkSO : ScriptableObject
    {
        public string barkId;               // K01 / M05 / D03 / C02 ...
        public BarkSpeaker speaker;
        [TextArea(1, 3)] public string text;
        public int priority = 50;           // higher wins when more than one fires
        public int cooldownDays = 2;        // may repeat after this many days
        public bool onceOnly;               // said at most once per game
    }
}
