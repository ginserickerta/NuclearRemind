using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Day-based quest guidance shown by QuestPanelController (Day 2-30).
    /// Pure data: designers add/edit entries in the Inspector — which days pop a quest
    /// lives in this asset, never as day numbers in code.
    /// Day 1 is owned by TutorialManager, so entries start at day 2.
    /// </summary>
    [CreateAssetMenu(menuName = "NuclearReMind/Quest Schedule", fileName = "QuestSchedule")]
    public class QuestScheduleSO : ScriptableObject
    {
        [System.Serializable]
        public class DayQuest
        {
            [Min(2)] public int day = 2;                      // game day this quest pops up
            public string title = "";                          // short headline after "ภารกิจ Day X — "
            [TextArea(1, 4)] public List<string> tasks = new List<string>(); // bullet lines
        }

        public List<DayQuest> entries = new List<DayQuest>();

        /// <summary>Pure lookup shared by controller + tests. Null when the day has no quest.</summary>
        public static DayQuest FindForDay(List<DayQuest> list, int day)
        {
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].day == day) return list[i];
            return null;
        }

        public DayQuest ForDay(int day) => FindForDay(entries, day);
    }
}
