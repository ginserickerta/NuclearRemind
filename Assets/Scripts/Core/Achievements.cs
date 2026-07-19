using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// The three achievements from STORY.md §④. Each is scored the moment the run ends, from the facts
    /// RunStats latched while it was playing.
    ///
    /// They persist across runs like Mastery and Codex do (§④ "ความรู้ที่คุณได้ — ไม่มีวันหาย"), but they
    /// keep their own PlayerPrefs key rather than joining MetaProgress: MetaProgress is the knowledge
    /// bank the game reads back at the start of a run to grant real bonuses, and an achievement grants
    /// nothing. Mixing them would put cosmetic flags on the path that decides gameplay.
    ///
    /// All three are earned by NOT doing something, so none can be awarded mid-run — a Triage card on
    /// the last day still takes "ไม่มีใครต้องเลือก" away. Scoring only at the ending is the point.
    /// </summary>
    public static class Achievements
    {
        public const string NoTriage = "no_triage";
        public const string NoDecree = "no_decree";
        public const string EveryoneHome = "everyone_home";

        private const string PrefsKey = "meta_achievements";

        public static readonly string[] All = { NoTriage, NoDecree, EveryoneHome };

        public static string TitleOf(string id) => id switch
        {
            NoTriage => "ไม่มีใครต้องเลือก",
            NoDecree => "ศักดิ์ศรีของเมือง",
            EveryoneHome => "ทุกคนกลับบ้าน",
            _ => id,
        };

        public static string HowEarned(string id) => id switch
        {
            NoTriage => "จบเกมโดยไม่เจอ Triage",
            NoDecree => "จบเกมโดยไม่ออก Decree",
            EveryoneHome => "ไม่มีใครตายเลย",
            _ => "",
        };

        private static HashSet<string> _unlocked;

        private static HashSet<string> Unlocked
        {
            get
            {
                if (_unlocked != null) return _unlocked;
                _unlocked = new HashSet<string>();
                string raw = PlayerPrefs.GetString(PrefsKey, "");
                if (!string.IsNullOrEmpty(raw))
                    foreach (var id in raw.Split(','))
                        if (!string.IsNullOrEmpty(id)) _unlocked.Add(id);
                return _unlocked;
            }
        }

        public static bool IsUnlocked(string id) => Unlocked.Contains(id);

        /// <summary>
        /// Score the finished run and persist anything newly earned. Returns the ids earned THIS run
        /// (whether or not they were already unlocked before), so the ending screen can list them.
        /// </summary>
        public static List<string> Evaluate(RunStats stats, GameEndType endType)
        {
            var earned = new List<string>();
            if (stats == null) return earned;

            // A lost run is not an achievement run: "ไม่มีใครต้องเลือก" on a city that collapsed on day 9
            // would be a reward for failing early, which is the opposite of what these celebrate.
            if (endType != GameEndType.TrueEnding && endType != GameEndType.NormalEnding) return earned;

            if (!stats.TriageEncountered) earned.Add(NoTriage);
            if (stats.DecreeOption == RunStats.NoDecree) earned.Add(NoDecree);
            if (stats.Deaths == 0) earned.Add(EveryoneHome);

            bool changed = false;
            foreach (var id in earned)
                if (Unlocked.Add(id)) changed = true;
            if (changed) Save();

            return earned;
        }

        private static void Save()
        {
            PlayerPrefs.SetString(PrefsKey, string.Join(",", Unlocked));
            PlayerPrefs.Save();
        }

        /// <summary>Achievement lines for the ending card, or empty when none were earned this run.</summary>
        public static string BuildSummary(IReadOnlyList<string> earnedThisRun)
        {
            if (earnedThisRun == null || earnedThisRun.Count == 0) return "";
            var sb = new StringBuilder("\nAchievements\n");
            foreach (var id in earnedThisRun)
                sb.Append("★ ").Append(TitleOf(id)).Append(" — ").Append(HowEarned(id)).Append('\n');
            return sb.ToString();
        }

        /// <summary>Test hook — clears both the cache and the stored key.</summary>
        public static void ResetForTest()
        {
            _unlocked = null;
            PlayerPrefs.DeleteKey(PrefsKey);
        }
    }
}
