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

        /// <summary>DialogueUIController (v8.5) ตั้ง true ตอน OnEnable — ก่อนหน้านั้นบทสนทนา degrade เป็น toast รายบรรทัด</summary>
        public static bool DialogueUIAvailable = false;

        // วิกฤตซ้อน (deferredCrisis §4): เลือกทาง C แล้วปัญหาใหม่ตามมาอีก N วัน
        private const int DeferredCrisisDelayDays = 2;

        // "coolingWorkerShortage" (decree_emergency §4) = จบวันระหว่างพายุที่ HEAT ≥ 70
        // (พายุ +12/วัน เกินกำลังหล่อเย็น — decree ให้ CoolingLaborBonus เข้าสูตรหล่อเย็นแก้ตรงจุด)
        private const string CoolingShortageKeyword = "coolingWorkerShortage";
        private const string CoolingShortageCondition = "heat_above_70";

        private enum Step { Idle, Record, Info, DialoguePre, Crisis, Outcome, DialoguePost, Quiz }
        private Step _step = Step.Idle;
        private StoryBeatSO _activeBeat;
        private int _crisisChoice = -1;
        private int _currentDay = 1;

        private readonly HashSet<string> _fired = new HashSet<string>();
        private readonly List<string> _firedOrder = new List<string>();      // ตามลำดับที่ยิง — ลงเซฟ
        private readonly List<RecordCardSO> _archived = new List<RecordCardSO>();
        private readonly List<int> _archivedDays = new List<int>(); // วันเกมที่กู้คืนแต่ละใบ (คู่ index กับ _archived · โชว์ในแผง Records)
        private readonly Queue<StoryBeatSO> _pendingBeats = new Queue<StoryBeatSO>();
        private readonly Queue<string> _pendingLogLines = new Queue<string>(); // logLines กระจายวันละบรรทัด (ลางพายุ)

        // วิกฤตซ้อนที่รอวันยิง — list คู่ index (JsonUtility ไม่รองรับ dict) · ลงเซฟ
        private readonly List<string> _deferredKeys = new List<string>();
        private readonly List<int> _deferredFireDays = new List<int>();

        // latch — ยิงครั้งแรกครั้งเดียว (แบบเดียวกับ Q1 ใน QuizManager)
        private bool _deuteriumFired, _tritiumFired, _reactorStartFired;

        // snapshot สำหรับ OnStatThreshold/OnStormActive (อ่านผ่าน event — ไม่ direct reference manager)
        private ResourceData _resources;
        private TowerData _tower;
        private float _radiationExposure; // ค่าเสี่ยงรังสีสะสม (RadiationManager) — เงื่อนไข exposure_above_* §4

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

        /// <summary>วันเกมที่กู้คืนบันทึกแต่ละใบ (คู่ index กับ ArchivedRecords · ให้ RecordsPanel/SaveManager)</summary>
        public IReadOnlyList<int> ArchivedRecordDays => _archivedDays;

        /// <summary>วิกฤตซ้อนที่รอวันยิง (read-only ให้ SaveManager — คู่ index กับ DeferredCrisisFireDays)</summary>
        public IReadOnlyList<string> DeferredCrisisKeys => _deferredKeys;
        public IReadOnlyList<int> DeferredCrisisFireDays => _deferredFireDays;

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
            EventManager.Instance.OnRadiationExposureChanged += HandleRadiationExposureChanged;
            EventManager.Instance.OnWorkerAssignmentChanged += HandleWorkerAssignmentChanged;
            EventManager.Instance.OnCoilsChanged += HandleCoilsChanged;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
            EventManager.Instance.OnRecordArchiveRequested += ArchiveRecord; // ปุ่ม "เก็บเข้าแผง Record" (RecordCardUI)
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
            EventManager.Instance.OnRadiationExposureChanged -= HandleRadiationExposureChanged;
            EventManager.Instance.OnWorkerAssignmentChanged -= HandleWorkerAssignmentChanged;
            EventManager.Instance.OnCoilsChanged -= HandleCoilsChanged;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            EventManager.Instance.OnRecordArchiveRequested -= ArchiveRecord;
        }

        // ═════════════════ Trigger detection ═════════════════

        private void HandleDayStarted(int day, bool timed)
        {
            _currentDay = day;

            // logLines ของ beat แบบกระจาย (ลางพายุ §4) — ปล่อยวันละบรรทัด "ไม่รวบ"
            if (_pendingLogLines.Count > 0)
                Notice(_pendingLogLines.Dequeue());

            // วิกฤตซ้อนที่ครบกำหนด → ยิง beat OnDeferredCrisis ที่คีย์ตรงกัน (คีย์ใช้ครั้งเดียว)
            for (int i = _deferredKeys.Count - 1; i >= 0; i--)
            {
                if (day < _deferredFireDays[i]) continue;
                string key = _deferredKeys[i];
                _deferredKeys.RemoveAt(i);
                _deferredFireDays.RemoveAt(i);

                foreach (var beat in beats)
                    if (Eligible(beat) && beat.triggerType == StoryTriggerType.OnDeferredCrisis
                        && beat.triggerParam == key)
                        FireBeat(beat);
            }

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
                        if (StatCondition.Matches(beat.triggerParam, day, _resources, _tower, _radiationExposure))
                            FireBeat(beat);
                        break;
                    case StoryTriggerType.OnStormActive:
                        // ระหว่างพายุ (Day 25–30): param ว่าง = ยิงเลย · "coolingWorkerShortage" = HEAT ≥ 70
                        // (หล่อเย็นตามพายุไม่ทัน) · คีย์อื่นประเมินแบบ StatCondition · ไม่รู้จัก = ไม่ยิง
                        if (day >= CoreTowerManager.StormStartDay && day <= GameManager.MaxDay
                            && StormParamMatches(beat.triggerParam, day))
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

        // ★ สเปกโรงวิจัย: บันทึก Elara #01 ปลดตอน "จัดคนเข้าห้องวิจัย" (อาคาร pre-placed →
        // OnBuildingBuilt ยิงตั้งแต่เริ่มเกม ใช้ไม่ได้) — ยิงเมื่ออาคารตาม triggerParam มีคนประจำ > 0
        // Eligible() latch ต่อ beat อยู่แล้ว (เล่นครั้งเดียว + คงสถานะข้ามเซฟผ่าน firedStoryBeats)
        private void HandleWorkerAssignmentChanged(Vector2Int cell, int newCount)
        {
            if (newCount <= 0) return;
            var registry = BuildingRegistry.Instance;
            if (registry == null || !registry.PlacedBuildings.TryGetValue(cell, out var data) || data == null)
                return;

            foreach (var beat in beats)
                if (Eligible(beat) && beat.triggerType == StoryTriggerType.OnBuildingStaffed
                    && beat.triggerParam == data.buildingName)
                    FireBeat(beat);
        }

        private void HandleResourceChanged(ResourceData data)
        {
            _resources = data;
            // latch เชื้อเพลิงครั้งแรก — เช็คทั้งคู่แยกกัน (ห้าม early-return ที่ deuterium ไม่งั้น tritium ไม่ถูกจับ)
            TryFireFuelLatch(ref _deuteriumFired, data.deuterium, StoryTriggerType.OnDeuteriumExtracted);
            TryFireFuelLatch(ref _tritiumFired, data.tritium, StoryTriggerType.OnTritiumExtracted);
        }

        // ยิง beat ของ trigger เชื้อเพลิงครั้งแรกที่ amount > 0 (latch กันยิงซ้ำ · OnResourceChanged ถี่)
        private void TryFireFuelLatch(ref bool latch, float amount, StoryTriggerType type)
        {
            if (latch || amount <= 0f) return;
            latch = true;
            foreach (var beat in beats)
                if (Eligible(beat) && beat.triggerType == type)
                    FireBeat(beat);
        }

        // ติดตั้งขดลวดครบทั้งสองชนิด (Toroidal ≥ 1 + Poloidal) — v8.5 ปลดบันทึกเสริม
        // ไม่ต้อง latch: Eligible() กันยิงซ้ำ (beat เข้า _fired แล้ว) · OnCoilsChanged ยิงไม่ถี่
        private void HandleCoilsChanged(int toroidalLevel, bool hasPoloidal)
        {
            if (toroidalLevel < 1 || !hasPoloidal) return;
            foreach (var beat in beats)
                if (Eligible(beat) && beat.triggerType == StoryTriggerType.OnCoilsComplete)
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

        // ค่าเสี่ยงรังสีสะสม (RadiationManager) — ประเมิน exposure_above_* ตอน OnDayEnded (§4 วิกฤตโรครังสี)
        private void HandleRadiationExposureChanged(float exposure) => _radiationExposure = exposure;

        private bool Eligible(StoryBeatSO beat)
            => beat != null && !string.IsNullOrEmpty(beat.beatId) && !_fired.Contains(beat.beatId);

        // เงื่อนไข OnStormActive: ว่าง = จริงเสมอ · คีย์เวิร์ดพิเศษแปลงเป็นเงื่อนไข StatCondition ก่อนประเมิน
        private bool StormParamMatches(string param, int day)
        {
            if (string.IsNullOrEmpty(param)) return true;
            string condition = param == CoolingShortageKeyword ? CoolingShortageCondition : param;
            return StatCondition.Matches(condition, day, _resources, _tower, _radiationExposure);
        }

        // ═════════════════ Beat playback (state machine) ═════════════════

        private void FireBeat(StoryBeatSO beat)
        {
            _fired.Add(beat.beatId);
            _firedOrder.Add(beat.beatId);
            if (beat.unlocksZoneB) EventManager.Instance.RaiseZoneBUnlocked(); // เปิดประตูโซน B ทันที (ไม่รอคิวเล่นการ์ด)
            _pendingBeats.Enqueue(beat);
            TryPlayNext();
        }

        private void TryPlayNext()
        {
            if (_step != Step.Idle || _pendingBeats.Count == 0) return;

            _activeBeat = _pendingBeats.Dequeue();
            _crisisChoice = -1;

            // npcLinePre ย้ายไปแสดงในกล่องบทสนทนา (รวมกับ dialoguePre ที่ Step.DialoguePre) — ไม่ยัด Alert แล้ว

            // logLines: บรรทัดแรกทันที · logLinesDaily = ที่เหลือปล่อยวันละบรรทัด (ลางพายุ "ไม่รวบ")
            //           ไม่ daily = โชว์ทุกบรรทัดทันที (เช่น ปฏิกิริยาโรงไฟฟ้าแรก / แจ้งพายุ)
            if (_activeBeat.logLines != null && _activeBeat.logLines.Length > 0)
            {
                Notice(_activeBeat.logLines[0]);
                for (int i = 1; i < _activeBeat.logLines.Length; i++)
                {
                    if (_activeBeat.logLinesDaily) _pendingLogLines.Enqueue(_activeBeat.logLines[i]);
                    else Notice(_activeBeat.logLines[i]);
                }
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
                        // ระบบใหม่: บันทึกไม่เด้งกลางจอ/ไม่หยุดเกม — เก็บอัตโนมัติทุกใบ + ยิง OnRecordArchived
                        //   → RecordNotificationHUD เด้ง badge ที่ไอคอนขวาล่าง (ผู้เล่นกดเปิดแผง Records อ่านทีหลัง)
                        //   แล้วเดินเรื่องต่อทันที ไม่รอกดปิดการ์ด (RecordCardUI ถูกปลดออกจาก flow นี้)
                        ArchiveRecord(_activeBeat.record);
                        Advance(Step.Info);
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
                    Advance(Step.DialoguePre);
                    return;

                case Step.DialoguePre:
                {
                    // รวม npcLinePre (บทพูด NPC นำ) เข้าหน้ากล่องบทสนทนา แทนการยัด Alert
                    var pre = BuildPreLines(_activeBeat);
                    if (pre.Length > 0)
                    {
                        ShowDialogue(pre);
                        return;
                    }
                    Advance(Step.Crisis);
                    return;
                }

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
                    Advance(Step.DialoguePost);
                    return;

                case Step.DialoguePost:
                {
                    // รวม innerVoiceAfter (เสียงในใจ Auren ปิดท้าย) เข้าท้ายกล่องบทสนทนา แทนการยัด Alert
                    var post = BuildPostLines(_activeBeat);
                    if (post.Length > 0)
                    {
                        ShowDialogue(post);
                        return;
                    }
                    Advance(Step.Quiz);
                    return;
                }

                case Step.Quiz:

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

        // แสดงบทสนทนาหลายตัวละคร (v8.5) ผ่าน Dialogue UI — ยังไม่มี UI → toast รายบรรทัด + เดินต่อ
        private void ShowDialogue(DialogueLine[] lines)
        {
            EventManager.Instance.RaiseStoryDialogueShown(lines);
            if (!DialogueUIAvailable)
            {
                foreach (var line in lines)
                    if (!string.IsNullOrEmpty(line.textTH))
                        Notice(SpeakerMeta.Prefix(line.speaker) + line.textTH);
                AdvancePastCurrentCard();
            }
            // มี UI → รอ OnStoryCardDismissed (Dialogue UI raise ตอนจบบททั้งชุด)
        }

        private void HandleCardDismissed() => AdvancePastCurrentCard();

        private void AdvancePastCurrentCard()
        {
            switch (_step)
            {
                case Step.Record: Advance(Step.Info); break;
                case Step.Info: Advance(Step.DialoguePre); break;
                case Step.DialoguePre: Advance(Step.Crisis); break;
                case Step.Outcome: Advance(Step.DialoguePost); break;
                case Step.DialoguePost: Advance(Step.Quiz); break;
            }
        }

        private void HandleDilemmaResolved(DilemmaData dilemma, int choiceIndex)
        {
            // วิกฤตซ้อน (deferredCrisis §4): จดคีย์ก่อน guard — ใช้ได้กับทุก dilemma ไม่เฉพาะของ beat
            string deferred = dilemma != null ? dilemma.GetDeferredCrisis(choiceIndex) : null;
            if (!string.IsNullOrEmpty(deferred) && !_deferredKeys.Contains(deferred))
            {
                _deferredKeys.Add(deferred);
                _deferredFireDays.Add(_currentDay + DeferredCrisisDelayDays);
            }

            if (_step != Step.Crisis || _activeBeat == null || dilemma != _activeBeat.crisis) return;
            _crisisChoice = choiceIndex;
            Advance(Step.Outcome);
        }

        private void ArchiveRecord(RecordCardSO record)
        {
            if (record == null || _archived.Contains(record)) return;
            _archived.Add(record);
            _archivedDays.Add(_currentDay); // วันที่บันทึกนี้เข้ามา (คู่ index กับ _archived)
            EventManager.Instance.RaiseRecordArchived(record);
        }

        private static void Notice(string message)
        {
            Debug.Log($"[Story] {message}");
            EventManager.Instance?.RaiseNotice(message);
        }

        // ═════════════════ เนื้อเรื่อง → กล่องบทสนทนา (ย้ายออกจาก Alert) ═════════════════

        // npcLinePre นำหน้า dialoguePre · เสียงในใจ innerVoiceAfter ปิดท้าย dialoguePost — โชว์ในกล่องบทสนทนา
        private static DialogueLine[] BuildPreLines(StoryBeatSO beat)
        {
            var list = new List<DialogueLine>();
            if (!string.IsNullOrEmpty(beat.npcLinePre)) list.Add(ParseLine(beat.npcLinePre));
            if (beat.dialoguePre != null) list.AddRange(beat.dialoguePre);
            return list.ToArray();
        }

        private static DialogueLine[] BuildPostLines(StoryBeatSO beat)
        {
            var list = new List<DialogueLine>();
            if (beat.dialoguePost != null) list.AddRange(beat.dialoguePost);
            if (!string.IsNullOrEmpty(beat.innerVoiceAfter))
                list.Add(new DialogueLine { speaker = Speaker.InnerVoice, textTH = beat.innerVoiceAfter });
            return list.ToArray();
        }

        // แปลง "Kova: ข้อความ" → DialogueLine (แยกผู้พูดจากคำนำหน้า) · ไม่มีคำนำหน้าที่รู้จัก → System
        private static DialogueLine ParseLine(string raw)
        {
            Speaker sp = Speaker.System;
            string text = raw;
            int idx = raw.IndexOf(':');
            if (idx > 0)
            {
                string name = raw.Substring(0, idx).Trim();
                foreach (Speaker s in System.Enum.GetValues(typeof(Speaker)))
                {
                    if (SpeakerMeta.DisplayName(s) == name || s.ToString() == name)
                    {
                        sp = s;
                        text = raw.Substring(idx + 1).Trim();
                        break;
                    }
                }
            }
            return new DialogueLine { speaker = sp, textTH = text };
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

            // สถานะประตูโซน B ไม่ได้เซฟตรง — อนุมานจาก _fired: ถ้ามี beat ปลดล็อกยิงไปแล้ว → เปิดประตูอีกครั้ง (ไม่เล่นการ์ดซ้ำ)
            if (beats != null)
                foreach (var b in beats)
                    if (b != null && b.unlocksZoneB && _fired.Contains(b.beatId))
                    {
                        EventManager.Instance.RaiseZoneBUnlocked();
                        break;
                    }

            _archived.Clear();
            _archivedDays.Clear();
            if (save.archivedRecords != null)
                for (int i = 0; i < save.archivedRecords.Count; i++)
                {
                    var record = FindRecordById(save.archivedRecords[i]);
                    if (record == null || _archived.Contains(record)) continue;
                    _archived.Add(record);
                    int day = (save.archivedRecordDays != null && i < save.archivedRecordDays.Count)
                        ? save.archivedRecordDays[i] : 0; // เซฟเก่าไม่มี field → 0 (ไม่โชว์วัน)
                    _archivedDays.Add(day);
                }

            // วิกฤตซ้อนที่ค้างรอวันยิง — list คู่ index (เซฟเก่าไม่มี field → default ว่าง ปลอดภัย)
            _deferredKeys.Clear();
            _deferredFireDays.Clear();
            if (save.deferredCrisisKeys != null && save.deferredCrisisDays != null)
            {
                int n = Mathf.Min(save.deferredCrisisKeys.Count, save.deferredCrisisDays.Count);
                for (int i = 0; i < n; i++)
                    if (!string.IsNullOrEmpty(save.deferredCrisisKeys[i]))
                    {
                        _deferredKeys.Add(save.deferredCrisisKeys[i]);
                        _deferredFireDays.Add(save.deferredCrisisDays[i]);
                    }
            }

            // latch ตามสถานะเซฟ — ไม่ยิงซ้ำหลังโหลด (coils ไม่มีใน save → พึ่ง _fired dedup พอ)
            _deuteriumFired = save.resources.deuterium > 0f;
            _tritiumFired = save.resources.tritium > 0f;
            _reactorStartFired = save.tower.isUnlocked;
            _resources = save.resources;
            _tower = save.tower;
            _radiationExposure = save.radiationExposure;
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
