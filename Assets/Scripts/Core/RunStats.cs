using System.Text;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: เก็บสถิติประจำรอบเล่นสำหรับหน้าสรุปจบเกม — จับค่าที่หายไปถ้าไม่บันทึกทันที
    /// (Hope ต่ำสุด + วันที่เกิด · ตัวเลือก Decree ที่ผู้เล่นเลือก · เคยเจอการ์ด Triage ไหม · วันทำงานใน Zone B)
    /// ส่วนที่เหลือ (คนรอด/ตาย, Mastery, Records) ดึงสดจากระบบอื่นตอนสรุป — ใช้ตัดสิน Achievements ด้วย
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

        /// <summary>Worker-days spent inside Zone B, and how many of those were spent wearing a suit.</summary>
        public int ZoneBWorkerDays { get; private set; }
        public int ZoneBSuitedDays { get; private set; }

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

        // สร้างตัวเองอัตโนมัติตอนโหลดซีนเกม (ต้องมีก่อนวันแรกเริ่ม ไม่งั้นค่า Hope ช่วงต้นหาย)
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

        // สมัครฟัง event กลาง (เริ่มวัน/จบวัน/การ์ด/โหลดเซฟ) — กันสมัครซ้ำด้วย _subscribed
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

        private void HandleDayEnded(int day)
        {
            _day = day;
            SampleAlara();
        }

        /// <summary>
        /// [TH] เก็บตัวอย่าง ALARA วันละครั้ง: คนใน Zone B วันนี้กี่คน ใส่ชุดกันรังสีกี่คน (วัดก่อนรังสีลงและก่อนสับเวร)
        /// One ALARA sample per day: of the workers standing in Zone B right now, how many are wearing a
        /// suit? This runs at execution order -90, i.e. BEFORE WorkerManager (-50) applies the day's
        /// radiation and before ZoneBController (-35) rotates anyone out — so it sees the crew as it
        /// actually worked the day, not the survivors. Suits were fitted at day start by RadSuitManager.
        /// </summary>
        private void SampleAlara()
        {
            var wm = WorkerManager.Instance;
            if (wm == null) return;
            foreach (var w in wm.GetWorkers(WorkerJobs.ZoneB))
            {
                if (!w.alive) continue;
                ZoneBWorkerDays++;
                if (w.hasRadSuit) ZoneBSuitedDays++;
            }
        }

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

        // จด Hope ต่ำสุดใหม่ทุกครั้งที่ค่าหลัง commit ต่ำกว่าที่เคยเห็น
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
        // ค่าที่ดึงสดจากระบบอื่นตอนสรุปจบเกม (คนรอด/ตาย · Mastery · Records)
        public int TotalWorkers => WorkerManager.Instance != null ? WorkerManager.Instance.Workers.Count : 0;
        public int Survivors => WorkerManager.Instance != null ? WorkerManager.Instance.AliveCount : 0;
        public int Deaths => Mathf.Max(0, TotalWorkers - Survivors);

        public int MasteryEarned => MasteryRegistry.Instance != null ? MasteryRegistry.Instance.EarnedCount : 0;
        public int MasteryTotal => QuizIds.All.Length;

        public int RecordsRecovered => DataRecovery.Instance != null ? DataRecovery.Instance.RecordsRecovered : 0;
        public int RecordsTotal => DataRecovery.Instance != null ? DataRecovery.Instance.TotalRecords : 0;

        /// <summary>
        /// [TH] สัดส่วนวันทำงาน Zone B ที่ใส่ชุดกันรังสี — วัด "การป้องกัน" ไม่ใช่ผลลัพธ์ · null ถ้าไม่เคยส่งใครเข้า
        /// ALARA compliance (STORY.md §④) — the share of Zone B worker-days that were spent in a suit.
        ///
        /// STORY.md names the row but never defines the maths, so this follows the only definition the
        /// game already had: CONFIG.md scores the Hope entry `alara.compliant` as "Zone B ชุดครบ". The
        /// measure is protection, not outcome — ALARA is about shielding people you send in, so a player
        /// who researches nuclear_medicine and crafts suits first scores well even if nobody happens to
        /// fall ill, and a player who sends crews in bare scores badly even if the Med Bay saves them.
        ///
        /// Null when nobody ever entered Zone B: there is no compliance to report, and printing 0% (or
        /// 100%) would both be lies about a decision the player never faced.
        /// </summary>
        public float? AlaraCompliance =>
            ZoneBWorkerDays > 0 ? (float?)ZoneBSuitedDays / ZoneBWorkerDays : null;

        // ── Summary block ─────────────────────────────────────────────────────────────

        /// <summary>
        /// [TH] สร้างข้อความสรุป 7 บรรทัดสำหรับหน้าจบเกม (ความรู้/คนรอด/ALARA/Decree/Hope ต่ำสุด/Record)
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
            Row(sb, "ALARA compliance", alara.HasValue
                ? $"{Mathf.RoundToInt(alara.Value * 100f)}%"
                : "— (ไม่เคยส่งคนเข้า Zone B)");

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
        // เขียนสถิติที่จับไว้ลง SaveData ตอนเซฟ
        public void WriteTo(SaveData save)
        {
            if (save == null) return;
            save.statLowestHope = float.IsPositiveInfinity(LowestHope) ? -1f : LowestHope;
            save.statLowestHopeDay = LowestHopeDay;
            save.statDecreeOption = DecreeOption;
            save.statTriageEncountered = TriageEncountered;
            save.statZoneBWorkerDays = ZoneBWorkerDays;
            save.statZoneBSuitedDays = ZoneBSuitedDays;
        }

        private void HandleSaveLoaded(SaveData save) => RestoreFromSave(save);

        // กู้สถิติกลับจากเซฟ (-1 = ยังไม่เคยวัด Hope — กันเซฟเก่าอ่านเพี้ยนเป็น 0)
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
            ZoneBWorkerDays = save.statZoneBWorkerDays;
            ZoneBSuitedDays = save.statZoneBSuitedDays;
        }

        // ล้างสถิติทั้งหมดเมื่อเริ่มรอบเล่นใหม่
        /// <summary>Wipe every latched stat — a fresh run starts from nothing.</summary>
        public void ResetForNewRun()
        {
            LowestHope = float.PositiveInfinity;
            LowestHopeDay = 0;
            DecreeOption = NoDecree;
            TriageEncountered = false;
            ZoneBWorkerDays = 0;
            ZoneBSuitedDays = 0;
            _day = 0;
        }
    }
}
