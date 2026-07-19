using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Feeds the quiz "requiresApplied" gate (QUIZZES.md) from real gameplay. Every v6.3 quiz is answerable
    /// only after the player has USED the knowledge, tracked in <see cref="MasteryAppliedState"/> and read by
    /// <see cref="QuizAvailability"/>. Pre-cutover only q_tritium_breeding was wired (ZoneBController); this
    /// watcher wires the remaining conditions so all 11 quizzes become reachable in a real playthrough.
    ///
    /// Runs once per day off OnDayEnded, reading the SETTLED state of the other systems (transient signals
    /// — tritium burned, patients healed — are surfaced as ReactorController.LastTritiumConsumed /
    /// WorkerManager.LastMedBayHealed and tolerate a one-day latch lag). Play-only: it is auto-spawned like
    /// the reactor cluster, so EditMode tests (which never run RuntimeInitialize) don't touch quiz state.
    ///
    /// ★ Mapping notes (spec written for a partly-different building set):
    ///  • q_deuterium — the v6.3 "Extractor" is the Water Plant at max level (deuteriumProduction). Staffed
    ///    max-level water plant ⇒ extractor ran.
    ///  • q_nuclear_medicine — needs a Hospital (WorkerManager now wires medBayCapacity from it) so healing
    ///    actually happens; LastMedBayHealed > 0 ⇒ applied.
    ///  • q_mutation / q_food_irradiation — v6.3 has NO Mutation Lab / Co-60 Chamber building. Both quizzes
    ///    share the "irradiation" note, so a working Farm with irradiation researched proxies both. (Flagged
    ///    for design confirmation — this is the one non-literal mapping.)
    /// </summary>
    [DefaultExecutionOrder(60)] // after the reactor/worker day handlers have settled today's state
    public class QuizAppliedWatcher : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnHook()
        {
            AutoSpawn();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
            => AutoSpawn();

        private static void AutoSpawn()
        {
            try
            {
                if (EventManager.Instance == null) return; // MainMenu has no core systems
                if (FindFirstObjectByType<QuizAppliedWatcher>() != null) return;
                new GameObject("QuizAppliedWatcher (auto)").AddComponent<QuizAppliedWatcher>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuizAppliedWatcher] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private void OnEnable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayEnded += HandleDayEnded;
            EventManager.Instance.OnGameOver += HandleGameOver;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayEnded -= HandleDayEnded;
            EventManager.Instance.OnGameOver -= HandleGameOver;
        }

        private void HandleDayEnded(int day)
        {
            var q = CodexQuizManager.Instance;
            if (q == null) return;

            // ── reactor: coils installed, CORE%, tritium fed ──
            var rc = ReactorController.Instance;
            if (rc != null)
            {
                q.SetCoilTypes((rc.ToroidalLv > 0 ? 1 : 0) + (rc.PoloidalLv > 0 ? 1 : 0)); // q_plasma / q_magnetic_pair
                q.SetCoreProgress(rc.Core);                                                 // q_fusion (≥95)
                if (rc.LastTritiumConsumed > 0f) q.MarkTritiumFed();                        // q_dt_fuel
            }

            // ── workers: risk-zone entry + Med Bay heals ──
            var wm = WorkerManager.Instance;
            if (wm != null)
            {
                if (wm.GetWorkers(WorkerJobs.Mine).Count > 0 || wm.GetWorkers(WorkerJobs.ZoneB).Count > 0)
                    q.MarkRiskZoneEntered();          // q_alara
                if (wm.LastMedBayHealed > 0)
                    q.MarkMedBayHealed();             // q_nuclear_medicine
            }

            // ── buildings: deuterium extractor + irradiated agriculture ──
            var reg = BuildingRegistry.Instance;
            if (reg != null)
            {
                var cc = ConstructionController.Instance;
                var wam = WorkerAssignmentManager.Instance;
                bool irradiation = KnowledgeDB.Instance != null && KnowledgeDB.Instance.HasNote("irradiation");

                foreach (var kv in reg.PlacedBuildings)
                {
                    var d = kv.Value;
                    if (d == null) continue;
                    if (cc != null && cc.IsUnderConstruction(kv.Key)) continue;
                    bool staffed = wam == null || wam.GetAssigned(kv.Key) > 0;
                    if (!staffed) continue;

                    // q_deuterium — "the extractor ran" now means the player actually switched extraction
                    // on and it is producing, not merely that a plant reached max level. Asking
                    // DeuteriumExtraction keeps this in step with the production gate in ResourceManager;
                    // hard-coding max level here would silently stop the quiz (and bark V06) firing once
                    // extraction became a researched, opt-in button at a lower level.
                    var dex = DeuteriumExtraction.Instance;
                    if (dex != null && dex.IsExtracting(kv.Key, d, reg.GetLevel(kv.Key)))
                        q.MarkExtractorRan();

                    // q_mutation + q_food_irradiation — irradiation applied to a working Farm (proxy: no
                    // Mutation Lab / Co-60 building exists in v6.3; both share the "irradiation" note)
                    if (irradiation && d.foodProduction > 0f)
                    {
                        q.MarkMutationLab();
                        q.MarkCo60();
                    }
                }
            }

            // advance the reveal clock AFTER today's applied flags are set (quizzes surface a beat later)
            q.AdvanceDay(day);
        }

        // q_clean_energy — reaching a (winning) ending
        private void HandleGameOver(GameEndType endType)
        {
            if (endType == GameEndType.TrueEnding || endType == GameEndType.NormalEnding)
                CodexQuizManager.Instance?.MarkEndingReached();
        }
    }
}
