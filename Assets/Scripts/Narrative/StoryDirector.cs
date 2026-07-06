using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ตัวคุมลำดับเนื้อเรื่อง (Story Guide §1/§3) — ฟัง event ของเมือง → เช็ค trigger → เล่น StoryBeat
    /// ลำดับบังคับใน beat: record → infoCard → crisis(Choice/Outcome) → afterText → quiz (ห้ามสลับ)
    /// - แต่ละ beat ยิงครั้งเดียวต่อรอบเล่น (จำลง SaveData.firedStoryBeats)
    /// - beat ที่ trigger ระหว่างมี beat เล่นอยู่ → เข้าคิวรอ
    /// - วิกฤตส่งผ่าน OnDilemmaTriggerRequested → DilemmaManager (pause/effects เดินทางเดิมทั้งหมด)
    /// - ยังไม่มี Card UI (CardUIAvailable = false) → การ์ด degrade เป็น toast แล้วเดินต่อทันที เกมไม่ค้าง
    /// </summary>
    public class StoryDirector : MonoBehaviour
    {
        public static StoryDirector Instance { get; private set; }

        [Header("Story Beats (wire โดย Story Setup — เรียงตามไทม์ไลน์)")]
        public StoryBeatSO[] beats = new StoryBeatSO[0];

        /// <summary>CardUIController (เฟส 3) ตั้ง true ตอน OnEnable — ก่อนหน้านั้นการ์ดเป็น toast + auto-advance</summary>
        public static bool CardUIAvailable = false;

        private enum Step { Idle, Record, Info, Crisis, Outcome, Quiz }
        private Step _step = Step.Idle;
        private StoryBeatSO _activeBeat;
        private int _crisisChoice = -1;

        private readonly HashSet<string> _fired = new HashSet<string>();
        private readonly List<string> _firedOrder = new List<string>();      // ตามลำดับที่ยิง — ลงเซฟ
        private readonly List<RecordCardSO> _archived = new List<RecordCardSO>();
        private readonly Queue<StoryBeatSO> _pendingBeats = new Queue<StoryBeatSO>();
        private readonly Queue<string> _pendingLogLines = new Queue<string>(); // logLines กระจายวันละบรรทัด (ลางพายุ)

        // latch — ยิงครั้งแรกครั้งเดียว (แบบเดียวกับ Q1 ใน QuizManager)
        private bool _deuteriumFired, _reactorStartFired;

        // snapshot สำหรับ OnStatThreshold/OnStormActive (อ่านผ่าน event — ไม่ direct reference manager)
        private ResourceData _resources;
        private TowerData _tower;

        /// <summary>beatId ที่เล่นแล้ว ตามลำดับ (read-only ให้ SaveManager — precedent: CodexManager.UnlockedIds)</summary>
        public IReadOnlyList<string> FiredBeatIds => _firedOrder;

        /// <summary>recordId ที่กู้คืนแล้ว (read-only ให้ SaveManager/RecordsPanel)</summary>
        public List<string> ArchivedRecordIds
        {
            get
            {
                var ids = new List<string>(_archived.Count);
                foreach (var r in _archived)
                    if (r != null) ids.Add(r.recordId);
                return ids;
            }
        }

        /// <summary>การ์ดบันทึกที่กู้คืนแล้ว ตามลำดับ (ให้ RecordsPanel ย้อนอ่าน)</summary>
        public IReadOnlyList<RecordCardSO> ArchivedRecords => _archived;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnDayStarted += HandleDayStarted;
            EventManager.Instance.OnDayEnded += HandleDayEnded;
            EventManager.Instance.OnConstructionComplete += HandleConstructionComplete;
            EventManager.Instance.OnResourceChanged += HandleResourceChanged;
            EventManager.Instance.OnTowerProgressChanged += HandleTowerProgressChanged;
            EventManager.Instance.OnDilemmaResolved += HandleDilemmaResolved;
            EventManager.Instance.OnStoryCardDismissed += HandleCardDismissed;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayStarted -= HandleDayStarted;
            EventManager.Instance.OnDayEnded -= HandleDayEnded;
            EventManager.Instance.OnConstructionComplete -= HandleConstructionComplete;
            EventManager.Instance.OnResourceChanged -= HandleResourceChanged;
            EventManager.Instance.OnTowerProgressChanged -= HandleTowerProgressChanged;
            EventManager.Instance.OnDilemmaResolved -= HandleDilemmaResolved;
            EventManager.Instance.OnStoryCardDismissed -= HandleCardDismissed;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
        }

        // ═════════════════ Trigger detection ═════════════════

        private void HandleDayStarted(int day, bool timed)
        {
            // logLines ของ beat แบบกระจาย (ลางพายุ §4) — ปล่อยวันละบรรทัด "ไม่รวบ"
            if (_pendingLogLines.Count > 0)
                Notice(_pendingLogLines.Dequeue());

            foreach (var beat in beats)
            {
                if (!Eligible(beat)) continue;

                switch (beat.triggerType)
                {
                    case StoryTriggerType.OnDay:
                        if (int.TryParse(beat.triggerParam, out int d) && d == day)
                            FireBeat(beat);
                        break;
                    case StoryTriggerType.OnStormApproach:
                        if (day == CoreTowerManager.StormStartDay)
                            FireBeat(beat);
                        break;
                }
            }
        }

        private void HandleDayEnded(int day)
        {
            foreach (var beat in beats)
            {
                if (!Eligible(beat)) continue;

                switch (beat.triggerType)
                {
                    case StoryTriggerType.OnStatThreshold:
                        if (StatCondition.Matches(beat.triggerParam, day, _resources, _tower))
                            FireBeat(beat);
                        break;
                    case StoryTriggerType.OnStormActive:
                        // ระหว่างพายุ (Day 25–30): param ว่าง = ยิงเลย · ไม่ว่างประเมินแบบ StatCondition
                        // (คีย์เวิร์ดเฉพาะเช่น "coolingWorkerShortage" จะนิยามตอนใส่เนื้อหา — ไม่รู้จัก = ไม่ยิง)
                        if (day >= CoreTowerManager.StormStartDay && day <= GameManager.MaxDay
                            && (string.IsNullOrEmpty(beat.triggerParam)
                                || StatCondition.Matches(beat.triggerParam, day, _resources, _tower)))
                            FireBeat(beat);
                        break;
                }
            }
        }

        private void HandleConstructionComplete(Vector2Int cell, BuildingData data)
        {
            if (data == null) return;
            foreach (var beat in beats)
                if (Eligible(beat) && beat.triggerType == StoryTriggerType.OnBuildingBuilt
                    && beat.triggerParam == data.buildingName) // buildingName ไทยเป๊ะ — ดูคอมเมนต์ StoryTriggerType
                    FireBeat(beat);
        }

        private void HandleResourceChanged(ResourceData data)
        {
            _resources = data;

            if (_deuteriumFired || data.deuterium <= 0f) return;
            _deuteriumFired = true;
            foreach (var beat in beats)
                if (Eligible(beat) && beat.triggerType == StoryTriggerType.OnDeuteriumExtracted)
                    FireBeat(beat);
        }

        private void HandleTowerProgressChanged(TowerData data)
        {
            _tower = data;

            if (_reactorStartFired || !data.isUnlocked) return;
            _reactorStartFired = true;
            foreach (var beat in beats)
                if (Eligible(beat) && beat.triggerType == StoryTriggerType.OnReactorStart)
                    FireBeat(beat);
        }

        private bool Eligible(StoryBeatSO beat)
            => beat != null && !string.IsNullOrEmpty(beat.beatId) && !_fired.Contains(beat.beatId);

        // ═════════════════ Beat playback (state machine) ═════════════════

        private void FireBeat(StoryBeatSO beat)
        {
            _fired.Add(beat.beatId);
            _firedOrder.Add(beat.beatId);
            _pendingBeats.Enqueue(beat);
            TryPlayNext();
        }

        private void TryPlayNext()
        {
            if (_step != Step.Idle || _pendingBeats.Count == 0) return;

            _activeBeat = _pendingBeats.Dequeue();
            _crisisChoice = -1;

            if (!string.IsNullOrEmpty(_activeBeat.npcLinePre))
                Notice(_activeBeat.npcLinePre);

            // logLines: บรรทัดแรกทันที ที่เหลือปล่อยวันละบรรทัดตอนเริ่มวันถัดไป
            if (_activeBeat.logLines != null && _activeBeat.logLines.Length > 0)
            {
                Notice(_activeBeat.logLines[0]);
                for (int i = 1; i < _activeBeat.logLines.Length; i++)
                    _pendingLogLines.Enqueue(_activeBeat.logLines[i]);
            }

            Advance(Step.Record);
        }

        // เดินไปขั้นถัดไปตั้งแต่ from — ข้ามขั้นที่ beat ไม่มีชิ้นส่วน
        private void Advance(Step from)
        {
            _step = from;
            switch (from)
            {
                case Step.Record:
                    if (_activeBeat.record != null)
                    {
                        ArchiveRecord(_activeBeat.record);
                        ShowCard(() => EventManager.Instance.RaiseStoryRecordShown(_activeBeat.record),
                                 $"📼 กู้คืนบันทึก: {_activeBeat.record.archiveTitle}");
                        return;
                    }
                    Advance(Step.Info);
                    return;

                case Step.Info:
                    if (_activeBeat.infoCard != null)
                    {
                        ShowCard(() => EventManager.Instance.RaiseStoryInfoShown(_activeBeat.infoCard),
                                 $"📘 {_activeBeat.infoCard.title}");
                        return;
                    }
                    Advance(Step.Crisis);
                    return;

                case Step.Crisis:
                    if (_activeBeat.crisis != null)
                    {
                        // เข้า pipeline ปกติของ DilemmaManager (pause/popup/effects) — ถ้ามีวิกฤตอื่นค้าง
                        // DilemmaManager จะจัดคิวให้แล้วเล่นต่อทันทีที่ตัวเก่า resolve
                        EventManager.Instance.RaiseDilemmaTriggerRequested(_activeBeat.crisis);
                        return; // รอ OnDilemmaResolved
                    }
                    Advance(Step.Outcome);
                    return;

                case Step.Outcome:
                    string afterText = _activeBeat.crisis != null ? _activeBeat.crisis.GetAfterText(_crisisChoice) : null;
                    if (!string.IsNullOrEmpty(afterText))
                    {
                        ShowCard(() => EventManager.Instance.RaiseStoryOutcomeShown(afterText), afterText);
                        return;
                    }
                    Advance(Step.Quiz);
                    return;

                case Step.Quiz:
                    if (!string.IsNullOrEmpty(_activeBeat.innerVoiceAfter))
                        Notice($"▸ ความคิด: {_activeBeat.innerVoiceAfter}");

                    if (QuizManager.Instance != null)
                    {
                        // ควิซรายทางเลือกของวิกฤต (Story Guide quizRef) — DilemmaManager ข้ามให้แล้วเพราะ story-driven
                        if (_activeBeat.crisis != null && _crisisChoice >= 0)
                            QuizManager.Instance.TriggerByIds(_activeBeat.crisis.GetQuizIdsForChoice(_crisisChoice));

                        // ควิซของ beat เอง (★ ความรู้มาก่อนควิซ — infoCard/record เล่นก่อนหน้าแล้วตามลำดับบังคับ)
                        QuizManager.Instance.EnqueueQuizzes(_activeBeat);
                    }

                    _activeBeat = null;
                    _step = Step.Idle;
                    TryPlayNext(); // มี beat รอคิว → เล่นต่อ
                    return;
            }
        }

        // แสดงการ์ดผ่าน event ให้ CardUI (เฟส 3) — ยังไม่มี UI → toast + เดินต่อทันที (เกมไม่ค้าง)
        private void ShowCard(System.Action raise, string fallbackNotice)
        {
            raise();
            if (!CardUIAvailable)
            {
                Notice(fallbackNotice);
                AdvancePastCurrentCard();
            }
            // มี UI → รอ OnStoryCardDismissed
        }

        private void HandleCardDismissed() => AdvancePastCurrentCard();

        private void AdvancePastCurrentCard()
        {
            switch (_step)
            {
                case Step.Record: Advance(Step.Info); break;
                case Step.Info: Advance(Step.Crisis); break;
                case Step.Outcome: Advance(Step.Quiz); break;
            }
        }

        private void HandleDilemmaResolved(DilemmaData dilemma, int choiceIndex)
        {
            if (_step != Step.Crisis || _activeBeat == null || dilemma != _activeBeat.crisis) return;
            _crisisChoice = choiceIndex;
            Advance(Step.Outcome);
        }

        private void ArchiveRecord(RecordCardSO record)
        {
            if (record == null || _archived.Contains(record)) return;
            _archived.Add(record);
            EventManager.Instance.RaiseRecordArchived(record);
        }

        private static void Notice(string message)
        {
            Debug.Log($"[Story] {message}");
            EventManager.Instance?.RaiseNotice(message);
        }

        // ═════════════════ Save / Load ═════════════════

        private void HandleSaveLoaded(SaveData save)
        {
            // ล้างสถานะรันไทม์ — beat ที่ค้างกลางทางไม่เล่นต่อหลังโหลด (ยิงใหม่ไม่ได้เพราะถูกจำว่า fired)
            _step = Step.Idle;
            _activeBeat = null;
            _crisisChoice = -1;
            _pendingBeats.Clear();
            _pendingLogLines.Clear();

            _fired.Clear();
            _firedOrder.Clear();
            if (save.firedStoryBeats != null)
                foreach (var id in save.firedStoryBeats)
                    if (!string.IsNullOrEmpty(id) && _fired.Add(id))
                        _firedOrder.Add(id);

            _archived.Clear();
            if (save.archivedRecords != null)
                foreach (var id in save.archivedRecords)
                {
                    var record = FindRecordById(id);
                    if (record != null && !_archived.Contains(record))
                        _archived.Add(record);
                }

            // latch ตามสถานะเซฟ — ไม่ยิงซ้ำหลังโหลด
            _deuteriumFired = save.resources.deuterium > 0f;
            _reactorStartFired = save.tower.isUnlocked;
            _resources = save.resources;
            _tower = save.tower;
        }

        private RecordCardSO FindRecordById(string recordId)
        {
            if (string.IsNullOrEmpty(recordId) || beats == null) return null;
            foreach (var beat in beats)
                if (beat != null && beat.record != null && beat.record.recordId == recordId)
                    return beat.record;
            return null;
        }
    }
}
