using System.Text;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Per-run statistics for the end-of-run summary (STORY.md §④ "สรุปการเล่นของคุณ").
    ///
    /// Most of the seven spec'd rows are already derivable from the live systems and are pulled on
    /// demand — survivors/deaths from WorkerManager, mastery from MasteryRegistry, records from
    /// DataRecovery. Only three facts vanish the moment they happen and so have to be latched here:
    ///   • the lowest Hope of the run, and the day it bottomed out
    ///   • which Decree option the player took (CardManager keeps the cooldown, not the choice)
    ///   • whether a Triage card was ever put in front of the player
    /// Those three are also what the three achievements are scored on (see Achievements).
    ///
    /// Read-only queries to other systems are direct per the v6.3 exception in CLAUDE.md; nothing here
    /// mutates game state, so no event round-trip is needed.
    /// </summary>
    [DefaultExecutionOrder(-90)]  // after EventManager (-100), before the systems that raise into it
    public class RunStats : MonoBehaviour
    {
        public static RunStats Instance { get; private set; }

        public const int NoDecree = 0;   // option A — "ไม่ออกประกาศ"; B/C (1/2) mean a decree was issued

        /// <summary>Lowest committed Hope this run, and the day it happened. Day 0 = never sampled.</summary>
        public float LowestHope { get; private set; } = float.PositiveInfinity;
        public int LowestHopeDay { get; private set; }

        /// <summary>Option index taken on the Decree card: 0 = none issued, 1 = B, 2 = C.</summary>
        public int DecreeOption { get; private set; } = NoDecree;

        /// <summary>True once a Triage card has been presented — the player had to choose who goes without.</summary>
        public bool TriageEncountered { get; private set; }

        private int _day;
        private bool _subscribed;
        private HopeLedger _hope;   // re-created by WorkerManager.Initialize, so re-resolve each day

        // Self-spawning, same idiom as IntroSequenceController: the tracker has to exist before the
        // first day starts or the run's early Hope samples are lost, and spawning here keeps it out of
        // the scene file (and out of the setup scripts) entirely.
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
                // EventManager lives only in the game scene (the menu destroys it) — this guards MainMenu.
                if (EventManager.Instance == null) return;
                if (FindFirstObjectByType<RunStats>() != null) return;
                new GameObject("RunStats (auto)").AddComponent<RunStats>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RunStats] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable() => TrySubscribe();
        private void Start() => TrySubscribe();

        private void TrySubscribe()
        {
            if (_subscribed || EventManager.Instance == null) return;
            EventManager.Instance.OnDayStarted += HandleDayStarted;
            EventManager.Instance.OnDayEnded += HandleDayEnded;
            EventManager.Instance.OnCrisisCardShown += HandleCardShown;
            EventManager.Instance.OnCrisisCardResolved += HandleCardResolved;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (!_subscribed || EventManager.Instance == null) { _subscribed = false; return; }
            EventManager.Instance.OnDayStarted -= HandleDayStarted;
            EventManager.Instance.OnDayEnded -= HandleDayEnded;
            EventManager.Instance.OnCrisisCardShown -= HandleCardShown;
            EventManager.Instance.OnCrisisCardResolved -= HandleCardResolved;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            UnhookHope();
            _subscribed = false;
        }

        // ── Hope: sample the settled value, never the live running total ──────────────
        // HopeLedger.OnCommitted fires once per day from WorkerManager.CommitDay, after every source
        // has submitted its entry. Sampling Hope.Current from OnDayEnded would race that commit.
        private void HandleDayStarted(int day, bool isTimed)
        {
            _day = day;
            HookHope();
        }

        private void HandleDayEnded(int day) => _day = day;

        private void HookHope()
        {
            var ledger = WorkerManager.Instance != null ? WorkerManager.Instance.Hope : null;
            if (ledger == _hope) return;
            UnhookHope();
            _hope = ledger;
            if (_hope != null) _hope.OnCommitted += HandleHopeCommitted;
        }

        private void UnhookHope()
        {
            if (_hope != null) _hope.OnCommitted -= HandleHopeCommitted;
            _hope = null;
        }

        private void HandleHopeCommitted(float current, float delta)
        {
            if (current >= LowestHope) return;
            LowestHope = current;
            LowestHopeDay = _day;
        }

        // ── Cards ─────────────────────────────────────────────────────────────────────
        // "เจอ Triage" counts the moment the card is shown, not the moment it is answered: the
        // achievement is about never being put in that position at all.
        private void HandleCardShown(CrisisCardSO card)
        {
            if (card != null && card.cardId == CardIds.Triage) TriageEncountered = true;
        }

        private void HandleCardResolved(string cardId, int optionIndex)
        {
            if (cardId == CardIds.Decree) DecreeOption = optionIndex;
        }

        // ── Derived on demand ─────────────────────────────────────────────────────────
        public int TotalWorkers => WorkerManager.Instance != null ? WorkerManager.Instance.Workers.Count : 0;
        public int Survivors => WorkerManager.Instance != null ? WorkerManager.Instance.AliveCount : 0;
        public int Deaths => Mathf.Max(0, TotalWorkers - Survivors);

        public int MasteryEarned => MasteryRegistry.Instance != null ? MasteryRegistry.Instance.EarnedCount : 0;
        public int MasteryTotal => QuizIds.All.Length;

        public int RecordsRecovered => DataRecovery.Instance != null ? DataRecovery.Instance.RecordsRecovered : 0;
        public int RecordsTotal => DataRecovery.Instance != null ? DataRecovery.Instance.TotalRecords : 0;

        /// <summary>
        /// ALARA compliance (STORY.md §④). Deliberately unimplemented: STORY.md asks for the row but
        /// never defines the formula, and CLAUDE.md forbids guessing at values the docs don't specify.
        /// CONFIG.md only defines the per-day Hope entry `alara.compliant` = "Zone B ชุดครบ".
        /// Returns null until the definition is settled; the summary omits the row while it is null.
        /// TODO(story): fill in once the ALARA % definition is agreed, then delete this note.
        /// </summary>
        public float? AlaraCompliance => null;

        // ── Summary block ─────────────────────────────────────────────────────────────

        /// <summary>
        /// The seven-row block from STORY.md §④, aligned in a fixed-width column. Rows whose data is
        /// unavailable this run are dropped rather than printed as a misleading zero.
        /// </summary>
        public string BuildSummary()
        {
            var sb = new StringBuilder();
            Row(sb, "ความรู้ที่ยืนยันแล้ว", $"{MasteryEarned}/{MasteryTotal}");
            Row(sb, "คนที่รอด", $"{Survivors}/{TotalWorkers}");
            Row(sb, "คนที่เสียไป", Deaths.ToString());

            float? alara = AlaraCompliance;
            if (alara.HasValue) Row(sb, "ALARA compliance", $"{Mathf.RoundToInt(alara.Value * 100f)}%");

            Row(sb, "Decree ที่ออก", DecreeLabel());
            Row(sb, "Hope ต่ำสุด", LowestHopeDay > 0
                ? $"{Mathf.RoundToInt(LowestHope)} (Day {LowestHopeDay})"
                : "—");
            Row(sb, "Record ที่กู้คืน", $"{RecordsRecovered}/{RecordsTotal}");
            return sb.ToString();
        }

        private string DecreeLabel() => DecreeOption switch
        {
            1 => "B · เกณฑ์ผู้ป่วยร่วมงาน",
            2 => "C · ดึงแรงงานเด็ก",
            _ => "ไม่มี",
        };

        // Legacy UI Text renders a monospace-ish grid poorly, so pad the label instead of using tabs.
        private const int LabelWidth = 22;
        private static void Row(StringBuilder sb, string label, string value)
            => sb.Append(label.PadRight(LabelWidth)).Append(value).Append('\n');

        // ── Save / load ───────────────────────────────────────────────────────────────
        public void WriteTo(SaveData save)
        {
            if (save == null) return;
            save.statLowestHope = float.IsPositiveInfinity(LowestHope) ? -1f : LowestHope;
            save.statLowestHopeDay = LowestHopeDay;
            save.statDecreeOption = DecreeOption;
            save.statTriageEncountered = TriageEncountered;
        }

        private void HandleSaveLoaded(SaveData save) => RestoreFromSave(save);

        /// <summary>Restore body — public so EditMode tests can drive it without a live subscription.</summary>
        public void RestoreFromSave(SaveData save)
        {
            if (save == null) return;
            // -1 is the "never sampled" sentinel: an old save has no field at all and lands on the
            // field initialiser 0f, which would otherwise read as "Hope bottomed out at zero".
            LowestHope = save.statLowestHope >= 0f ? save.statLowestHope : float.PositiveInfinity;
            LowestHopeDay = save.statLowestHopeDay;
            DecreeOption = save.statDecreeOption;
            TriageEncountered = save.statTriageEncountered;
        }

        /// <summary>Wipe every latched stat — a fresh run starts from nothing.</summary>
        public void ResetForNewRun()
        {
            LowestHope = float.PositiveInfinity;
            LowestHopeDay = 0;
            DecreeOption = NoDecree;
            TriageEncountered = false;
            _day = 0;
        }
    }
}
