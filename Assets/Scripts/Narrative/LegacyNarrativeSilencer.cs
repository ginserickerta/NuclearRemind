using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ★ v6.3 cutover (Narrative): retires the legacy story pipeline at runtime.
    ///
    /// The scene still carries StoryDirector + DilemmaManager (their sources now live under
    /// Narrative/_archive — kept compiled so scene GUIDs, SaveData fields and EditMode tests stay
    /// valid), but GDD v6.3 replaces them wholesale:
    ///   • crises   → CardManager (8 state-triggered CrisisCardSO, GDD §25 — never the calendar)
    ///   • records  → DataRecovery (DataRecovery.progress unlocks Elara records + leads, §24)
    ///   • barks    → BarkManager / InnerVoiceDirector (BARKS.md, state-bound)
    ///   • quizzes  → Codex (opt-in, §21 — never forced after a crisis)
    ///
    /// Disabling the two scene components makes their OnDisable unsubscribe from EventManager, so
    /// no legacy beat / day-scheduled crisis (Day 17/20/24/25) ever fires again. Same pattern as
    /// slice 4's CrisisCardPanelUI → DilemmaPopupController. EditMode tests are unaffected
    /// (RuntimeInitializeOnLoadMethod does not run there).
    /// </summary>
    public static class LegacyNarrativeSilencer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            Silence();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
            => Silence();

        private static void Silence()
        {
            var director = Object.FindFirstObjectByType<StoryDirector>();
            if (director != null && director.enabled)
            {
                director.enabled = false;
                Debug.Log("[LegacyNarrativeSilencer] StoryDirector disabled (v6.3 cutover — CardManager/DataRecovery own the story now)");
            }

            var dilemmas = Object.FindFirstObjectByType<DilemmaManager>();
            if (dilemmas != null && dilemmas.enabled)
            {
                dilemmas.enabled = false;
                Debug.Log("[LegacyNarrativeSilencer] DilemmaManager disabled (v6.3 cutover — crisis cards replace dilemmas)");
            }
        }
    }
}
