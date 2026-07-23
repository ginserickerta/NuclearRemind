using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: ระบบ "กู้คืนบันทึก" ของ Dr. Elara Vane 4 ใบ — ห้องแล็บถอดรหัสสะสมทีละวัน
    /// [TH] กู้ได้แต่ละใบจะปลด LEAD วิจัยล่วงหน้าให้ (เร็วขึ้น 4-6 วัน) = รางวัลเกมเพลย์จริง ไม่ใช่แค่เนื้อเรื่อง
    /// [TH] อัตรา = 14 (passive) + 10 ต่อนักวิจัยว่าง · ×1.3 ที่แล็บ L2 (ค่าจาก CONFIG.md) · ข้ามได้ ไม่บังคับชนะ
    /// Data Recovery (GDD §24 / STORY.md) — Dr. Elara Vane's 4 logs, decrypted passively as the lab
    /// runs. Each recovered record pre-unlocks a research LEAD (record_01→water_analysis,
    /// record_02→magnetic_theory, record_03→storm_detection→Sensor Array, record_final→lithium_breeding),
    /// so a player who keeps researchers idle gets leads 4–6 days early — a real power-up, not just lore.
    ///
    /// Rate = passive 14 + 10 per idle researcher, ×1.3 at lab L2 (CONFIG.md). Records are a shortcut,
    /// never a requirement (STORY.md §3): skip them and you still win, just later.
    /// </summary>
    // -25: after ResearchLab (-40) / WorkerManager (-50) so it reads today's lab staffing.
    [DefaultExecutionOrder(-25)]
    public class DataRecovery : MonoBehaviour
    {
        public static DataRecovery Instance { get; private set; }

        // Recovery order (STORY.md) — record_final is 4th despite the id sorting oddly.
        private static readonly string[] Order = { "record_01", "record_02", "record_03", "record_final" };

        private GameConfigSO _cfg;
        private readonly Dictionary<string, RecordCardSO> _catalog = new Dictionary<string, RecordCardSO>();
        private bool _catalogLoaded;

        public float Progress { get; private set; }
        public int RecordsRecovered { get; private set; }   // 0..4
        public int TotalRecords => Order.Length;

        /// <summary>
        /// [TH] true = แล็บกำลังถอดรหัสบันทึกใบถัดไปอยู่ — ผู้เล่นต้องกดเริ่มเองทีละใบ (ไม่รันอัตโนมัติ)
        /// [TH] และ reset ตัวเองเมื่อกู้ใบนั้นสำเร็จ ให้แต่ละใบเป็นการตัดสินใจของผู้เล่นเสมอ
        /// True while the lab is working on the next record. Decryption used to run on its own the moment
        /// the lab was standing — the player never chose it and often never noticed it — so it is now
        /// started deliberately, one record at a time, the same way research is (owner's call 2026-07-21;
        /// STORY.md §3 updated to match). Clears itself when a record lands, so each one is its own decision.
        /// </summary>
        public bool IsDecoding { get; private set; }

        /// <summary>[TH] ผู้เล่นกดปุ่ม "ถอดรหัส" ได้ตอนนี้ไหม — เงื่อนไขชุดเดียวกับที่ TickDay จะปฏิเสธ
        /// Can the player press "ถอดรหัส" right now? Mirrors the conditions TickDay would refuse on.</summary>
        public bool CanStartDecoding
        {
            get
            {
                if (IsDecoding || RecordsRecovered >= Order.Length) return false;
                var lab = ResearchLab.Instance;
                return lab != null && !lab.IsRuined;
            }
        }

        /// <summary>[TH] เริ่มถอดรหัสบันทึกใบถัดไป — คืน false ถ้าแล็บพัง/ไม่เหลือบันทึกให้กู้
        /// Begin decrypting the next record. Returns false when the lab is ruined or nothing is left.</summary>
        public bool StartDecoding()
        {
            if (!CanStartDecoding) return false;
            IsDecoding = true;
            return true;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Initialize(GameConfigSO.Instance);
        }

        /// <summary>[TH] ตั้งค่าเริ่มต้น (progress = 0, ยังไม่กู้สักใบ) — เป็นทางเข้าของ EditMode test ด้วย
        /// Bootstrap — also the EditMode-test entry point.</summary>
        public void Initialize(GameConfigSO cfg)
        {
            _cfg = cfg;
            Progress = 0f;
            RecordsRecovered = 0;
        }

        private bool _subscribed;

        // auto-spawn AfterSceneLoad → OnEnable may run while EventManager.Instance is still null (build),
        // so retry in Start like ResearchManager — a silent drop here would stop recovery for the whole run.
        private void OnEnable() => TrySubscribe();
        private void Start() => TrySubscribe();

        private void TrySubscribe()
        {
            if (_subscribed || EventManager.Instance == null) return;
            EventManager.Instance.OnDayEnded += HandleDayEnded;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (!_subscribed || EventManager.Instance == null) { _subscribed = false; return; }
            EventManager.Instance.OnDayEnded -= HandleDayEnded;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            _subscribed = false;
        }

        /// <summary>
        /// Restore from a save. Replays the LEAD unlocks for every record already recovered — KnowledgeDB
        /// keeps no persistence of its own, so without this the player loses storm_detection (and the
        /// Sensor Array with it) on load. UnlockLead is idempotent, so replaying is safe.
        /// Deliberately does NOT route through UnlockNextRecord: that would re-pop all four record cards.
        /// </summary>
        private void HandleSaveLoaded(SaveData save) => RestoreFromSave(save);

        /// <summary>[TH] กู้สถานะจากเซฟ — replay การปลด LEAD ของบันทึกทุกใบที่กู้ไปแล้ว (KnowledgeDB ไม่ persist เอง)
        /// [TH] ตั้งใจไม่ผ่าน UnlockNextRecord เพราะจะทำให้การ์ดบันทึกเด้งซ้ำทั้ง 4 ใบตอนโหลดเซฟ
        /// Restore body — public so EditMode tests can drive it without a live subscription.</summary>
        public void RestoreFromSave(SaveData save)
        {
            if (save == null) return;
            EnsureCatalog();

            Progress = save.dataRecoveryProgress;
            RecordsRecovered = Mathf.Clamp(save.dataRecoveryRecords, 0, Order.Length);
            IsDecoding = save.dataRecoveryDecoding; // default false → an old save resumes idle, not mid-decode

            for (int i = 0; i < RecordsRecovered; i++)
            {
                var rec = GetRecord(Order[i]);
                if (rec != null && !string.IsNullOrEmpty(rec.unlocksLead))
                    KnowledgeDB.Instance.UnlockLead(rec.unlocksLead);
            }
        }

        private void HandleDayEnded(int day)
        {
            if (day <= 1) return;   // Day 1 = tutorial
            if (!IsDecoding) return; // the player has to ask for this now — see IsDecoding
            TickDay();
        }

        // ─────────────────────────────────────────
        //  Catalog (tests register directly; play mode auto-loads from Resources)
        // ─────────────────────────────────────────

        // [TH] ลงทะเบียน RecordCardSO เข้า catalog — เทสต์เรียกตรง ส่วน play mode โหลดจาก Resources/Records เอง
        public void RegisterCatalog(IEnumerable<RecordCardSO> records)
        {
            if (records != null)
                foreach (var r in records)
                    if (r != null && !string.IsNullOrEmpty(r.recordId)) _catalog[r.recordId] = r;
            _catalogLoaded = true;
        }

        private void EnsureCatalog()
        {
            if (_catalogLoaded) return;
            _catalogLoaded = true;
            RegisterCatalog(Resources.LoadAll<RecordCardSO>("Records")); // Assets/Resources/Records
        }

        // ─────────────────────────────────────────
        //  Daily tick (public for tests)
        // ─────────────────────────────────────────

        // [TH] ประมวลผล 1 วัน: คิดอัตราถอดรหัสจากนักวิจัยว่างในแล็บ → สะสม Progress → ครบเป้าก็ปลดบันทึกใบถัดไป
        public void TickDay()
        {
            EnsureCatalog();
            if (RecordsRecovered >= Order.Length) return;

            var lab = ResearchLab.Instance;
            if (lab == null || lab.IsRuined) return; // a ruined lab can't decrypt (GDD §24)

            int labWorkers = WorkerManager.Instance != null ? WorkerManager.Instance.GetWorkers(WorkerJobs.Lab).Count : 0;
            int busy = lab.ActiveJob != null && lab.ActiveJob.note != null
                ? Mathf.Min(labWorkers, lab.ActiveJob.note.researcherSlots)
                : 0;
            int idleResearchers = Mathf.Max(0, labWorkers - busy);

            float rate = _cfg.dataRecoveryPassive + idleResearchers * _cfg.dataRecoveryRate;
            if (lab.Level >= 2) rate *= _cfg.dataRecoveryLv2Mult;

            Progress += rate;
            while (Progress >= _cfg.dataRecoveryTarget && RecordsRecovered < Order.Length)
            {
                Progress -= _cfg.dataRecoveryTarget;
                UnlockNextRecord();
            }
        }

        private void UnlockNextRecord()
        {
            var rec = GetRecord(Order[RecordsRecovered]);
            RecordsRecovered++;
            IsDecoding = false; // one press, one record — the next has to be chosen again
            if (rec == null) return;

            if (!string.IsNullOrEmpty(rec.unlocksLead))
                KnowledgeDB.Instance.UnlockLead(rec.unlocksLead); // ★ pre-unlock the lead (power-up)

            EventManager.Instance?.RaiseRecordRecovered(rec); // pops the card + archives it
        }

        // [TH] หา RecordCardSO จาก id (null = ไม่พบใน catalog)
        public RecordCardSO GetRecord(string recordId)
        {
            EnsureCatalog();
            return _catalog.TryGetValue(recordId, out var r) ? r : null;
        }

        /// <summary>[TH] บันทึกที่กู้ได้แล้วทั้งหมด เรียงตามลำดับกู้ — ให้แผง Records ใช้แสดง
        /// Records recovered so far, in order — for the Records panel.</summary>
        public IEnumerable<RecordCardSO> Recovered
        {
            get
            {
                EnsureCatalog();
                for (int i = 0; i < RecordsRecovered && i < Order.Length; i++)
                {
                    var r = GetRecord(Order[i]);
                    if (r != null) yield return r;
                }
            }
        }
    }
}
