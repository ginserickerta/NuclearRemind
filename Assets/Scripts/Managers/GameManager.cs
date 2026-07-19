// Assets/Scripts/Managers/GameManager.cs
using UnityEngine;

namespace NuclearReMind
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState { Playing, Paused, GameOver, Victory }
        public GameState CurrentState { get; private set; } = GameState.Playing;

        // ===== Day Cycle (§2.3 / §3) =====
        public const int MaxDay = 30;

        /// <summary>เฟสภายในวัน (V4 §3): Planning วางแผน/วางอาคาร → Live เดินเครื่อง/เหตุการณ์</summary>
        public enum DayPhase { Planning, Live }

        [Header("Day Cycle")]
        [Tooltip("ความยาววันที่จับเวลา (วินาที) — Day 2–30 = 90 (Planning 30 + Live 60)")]
        public float dayLength = 90f;
        [Tooltip("ช่วง Live (วินาที) — Planning = dayLength − liveSeconds (V4 §3)")]
        public float liveSeconds = 60f;
        [Tooltip("Day 1 = tutorial ไม่จับเวลา (§2.3)")]
        public bool skipDay1Timer = true;

        public int CurrentDay { get; private set; } = 1;
        public float DayTimeRemaining { get; private set; }
        public bool DayTimerActive { get; private set; }
        public DayPhase CurrentDayPhase { get; private set; } = DayPhase.Planning;

        /// <summary>เฟสของเกมตามช่วงวัน (GDD §6 "ปลดล็อก") — 1–5→1 · 6–10→2 · 11–20→3 · 21+→4</summary>
        public int CurrentPhase => GamePhase.FromDay(CurrentDay);

        /// <summary>ความยาวเต็มของเฟส Planning (วินาที) = dayLength − liveSeconds</summary>
        public float PlanningSeconds => Mathf.Max(0f, dayLength - liveSeconds);

        /// <summary>เวลาที่เหลือใน "เฟสปัจจุบัน" — สำหรับ UI นับถอยหลังแยกเฟส (Planning/Live)</summary>
        public float PhaseTimeRemaining => CurrentDayPhase == DayPhase.Planning
            ? Mathf.Max(0f, DayTimeRemaining - liveSeconds)
            : DayTimeRemaining;

        /// <summary>ความยาวเต็มของเฟสปัจจุบัน (Planning = 30, Live = 60) — ใช้หา % ของแถบเวลา</summary>
        public float PhaseDuration => CurrentDayPhase == DayPhase.Planning ? PlanningSeconds : liveSeconds;

        // ===== Speed Control (A4) =====
        // ความเร็วเล่นปัจจุบันเมื่อไม่ pause (1× ปกติ / 2× เร่ง) — pause = timeScale 0 ชั่วคราว
        public float GameSpeed { get; private set; } = 1f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnEnable()
        {
            EventManager.Instance.OnTowerComplete += HandleTowerComplete;
            EventManager.Instance.OnTutorialComplete += HandleTutorialComplete;
            EventManager.Instance.OnSpeedChangeRequested += HandleSpeedChangeRequested;
            EventManager.Instance.OnGameOver += HandleGameOver;
        }

        void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnTowerComplete -= HandleTowerComplete;
            EventManager.Instance.OnTutorialComplete -= HandleTutorialComplete;
            EventManager.Instance.OnSpeedChangeRequested -= HandleSpeedChangeRequested;
            EventManager.Instance.OnGameOver -= HandleGameOver;
        }

        // จบเกม → บันทึกคลังความรู้ถาวร (§9) · True/Normal = ชนะ · ที่เหลือ = แพ้
        // ★ v6.3 cutover (slice 5): EndingSystem raises win types straight from the daily check
        //   (core ≥ 100 ends the run immediately — bug #15), so Victory must be set HERE too;
        //   legacy paths (HandleTowerComplete/EvaluateFinalEnding) already set it before raising → no-op.
        void HandleGameOver(GameEndType endType)
        {
            CaptureMetaProgress();

            if (CurrentState == GameState.GameOver || CurrentState == GameState.Victory) return;
            SetState(endType == GameEndType.TrueEnding || endType == GameEndType.NormalEnding
                ? GameState.Victory
                : GameState.GameOver);
        }

        void Start()
        {
            Debug.Log("[GameManager] Initialized");
            // normalize timeScale (อาจค้างจาก play session ก่อน) + แจ้ง UI ให้ highlight ความเร็วเริ่มต้น
            Time.timeScale = GameSpeed;
            EventManager.Instance.RaiseSpeedChanged(Time.timeScale);
            ApplyMetaProgress(); // §9: คืน Knowledge/Codex จากรอบก่อน
            BeginDay(1);
        }

        // CORE% ถึง 100% ระหว่างทาง → ชนะสมบูรณ์ (True Ending, Q ≥ 1.0)
        void HandleTowerComplete()
        {
            SetState(GameState.Victory);
            EventManager.Instance.RaiseGameOver(GameEndType.TrueEnding);
        }

        // จบ tutorial (Day 1) → เริ่มวันที่จับเวลา
        void HandleTutorialComplete()
        {
            if (CurrentDay == 1) RequestEndDay();
        }

        // ───────────────────────────── Day Cycle ─────────────────────────────

        private void BeginDay(int day)
        {
            CurrentDay = Mathf.Clamp(day, 1, MaxDay);
            bool timed = !(skipDay1Timer && CurrentDay == 1);
            DayTimeRemaining = timed ? dayLength : 0f;
            DayTimerActive = timed;
            CurrentDayPhase = DayPhase.Planning; // เริ่มวันด้วยเฟสวางแผน (V4 §3)

            // Planning: ปลดล็อกให้เปลี่ยนโหมดเตาได้ (ล็อกใหม่ตอนเข้า Live)
            ReactorController()?.UnlockMode();

            Debug.Log($"[GameManager] Day {CurrentDay}/{MaxDay} started (timed={timed})");
            EventManager.Instance.RaiseDayStarted(CurrentDay, timed);
            EventManager.Instance.RaiseDayPhaseChanged(DayPhase.Planning);
        }

        // เข้าเฟส Live (เดินเครื่อง 60s สุดท้ายของวัน) — ล็อกโหมดเตาที่เลือกไว้ (V4 §3)
        private void EnterLivePhase()
        {
            CurrentDayPhase = DayPhase.Live;
            ReactorController()?.LockMode();
            EventManager.Instance.RaiseDayPhaseChanged(DayPhase.Live);
        }

        private static CoreTowerManager ReactorController() => CoreTowerManager.Instance;

        // นาฬิกาวันเดินต่อเมื่อไม่มี pause-reason (placement / quiz / crisis popup) — V4 §15
        private static bool TimeRunning() => TimeManager.Instance == null || TimeManager.Instance.IsRunning;

        /// <summary>จบวันก่อนเวลา — ใช้โดย Tutorial (Day 1) หรือปุ่ม debug</summary>
        public void RequestEndDay()
        {
            if (CurrentState == GameState.GameOver || CurrentState == GameState.Victory) return;
            EndDay();
        }

        private void EndDay()
        {
            DayTimerActive = false;
            int finished = CurrentDay;

            Debug.Log($"[GameManager] Day {finished} ended");
            // ลำดับจบวัน (V4 §3): batch ผลิต+บริโภค (OnDayProduction) → เตา/ขวัญ/วิกฤต (OnDayEnded)
            EventManager.Instance.RaiseDayProduction(finished);
            EventManager.Instance.RaiseDayEnded(finished);

            // ★ v6.3 cutover (slice 5): EndingSystem may end the run INSIDE OnDayEnded (win/lose are
            //   checked daily, not on D30 — bug #15). Ended → don't start another day.
            if (CurrentState == GameState.GameOver || CurrentState == GameState.Victory) return;

            // ครบ 30 วัน → ประเมินฉากจบด้วยค่า Q (V4 §14 — legacy fallback เมื่อไม่มี EndingSystem)
            if (finished >= MaxDay) { EvaluateFinalEnding(); return; }

            BeginDay(finished + 1);
        }

        // ประเมินฉากจบตอน Day 30 (V4 §14): Q≥1.0 True · Q 0.5–0.99 Normal · Q<0.5 แพ้ (TimeoutLowQ)
        private void EvaluateFinalEnding()
        {
            if (CurrentState == GameState.GameOver || CurrentState == GameState.Victory) return;

            float q = CoreTowerManager.Instance != null ? CoreTowerManager.Instance.Q : 0f;
            if (q >= 1.0f)
            {
                SetState(GameState.Victory);
                EventManager.Instance.RaiseGameOver(GameEndType.TrueEnding);
            }
            else if (q >= 0.5f)
            {
                SetState(GameState.Victory);
                EventManager.Instance.RaiseGameOver(GameEndType.NormalEnding);
            }
            else
            {
                EventManager.Instance.RaiseGameOver(GameEndType.TimeoutLowQ);
            }
        }

        // ───────────────────────── MetaProgress (§9) + Restart (§14) ─────────────────────────

        private void ApplyMetaProgress()
        {
            MetaProgress.Load();
            if (MetaProgress.KnowledgeBank > 0)
                EventManager.Instance.RaiseResourceDelta(ResourceType.Knowledge, MetaProgress.KnowledgeBank);
            CodexManager.Instance?.RestoreUnlocked(MetaProgress.UnlockedCodex);
        }

        private void CaptureMetaProgress()
        {
            int knowledge = ResourceManager.Instance != null
                ? Mathf.RoundToInt(ResourceManager.Instance.Current.knowledge) : 0;
            // ★ v6.3: nothing to merge in. This used to pass CodexManager.UnlockedIds, but that manager
            // is retired and its set only ever held ids from the dead legacy unlock path — so the capture
            // contributed nothing for entries earned this run. The live path
            // (CodexQuizManager.UnlockCodexFor) writes straight into MetaProgress.UnlockedCodex, which is
            // the same set Capture would merge into, so passing null is correct rather than lossy. Capture
            // still banks Knowledge and Saves.
            MetaProgress.Capture(knowledge, null);
        }

        /// <summary>เริ่มเกมใหม่ (V4 §14) — reload scene · คลังความรู้ถาวรคงอยู่ (Knowledge/Codex)</summary>
        public void Restart()
        {
            CaptureMetaProgress();
            // CodexQuizManager is a plain static singleton, so reloading the scene does not touch it.
            // Without this, the new run starts with last run's applied-state already latched (quizzes
            // pre-unlocked) and with every skipped prompt still marked as offered, so it never invites
            // the player again. Mastery/Codex are NOT lost — those live in MetaProgress by design.
            CodexQuizManager.ResetForRun();
            Time.timeScale = 1f;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            SceneFader.FadeToScene(scene.buildIndex); // เฟดจอดำ → reload → เฟดสว่าง
        }

        public void SetState(GameState newState)
        {
            CurrentState = newState;
            Debug.Log($"[GameManager] State → {newState}");

            Time.timeScale = newState switch
            {
                GameState.Paused   => 0f,
                GameState.Playing  => GameSpeed, // ใช้ความเร็วที่เลือกไว้ (1× หรือ 2×)
                GameState.GameOver => 0f,        // จบเกม → freeze simulation ทั้งหมด (tick/day timer หยุดเอง)
                GameState.Victory  => 0f,        // ชนะ → freeze เช่นกัน (UI เป็น unscaled ยังทำงาน)
                _                  => 1f,
            };

            EventManager.Instance.RaiseGameStateChanged(newState);
            EventManager.Instance.RaiseSpeedChanged(Time.timeScale);
        }

        // ───────────────────────────── Speed Control (A4) ─────────────────────────────

        /// <summary>ตั้งความเร็วเกม: 0 = pause, 1 = ปกติ, 2 = เร่ง — ผูกกับ Time.timeScale</summary>
        public void SetSpeed(float speed)
        {
            if (CurrentState == GameState.GameOver || CurrentState == GameState.Victory)
                return; // เกมจบแล้ว ไม่ปรับความเร็ว

            if (speed <= 0f)
            {
                SetState(GameState.Paused); // ความเร็วเดิม (GameSpeed) คงไว้ กลับมาเล่นแล้วใช้ต่อ
                return;
            }

            GameSpeed = speed;
            SetState(GameState.Playing); // apply GameSpeed เข้า timeScale
        }

        private void HandleSpeedChangeRequested(float speed) => SetSpeed(speed);

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SetState(CurrentState == GameState.Paused
                    ? GameState.Playing
                    : GameState.Paused);
            }

            AdvanceTime(Time.deltaTime);
        }

        /// <summary>
        /// นับถอยหลังวัน + สลับเฟส Planning→Live + จบวัน (แยกจาก Update ให้เทสต์เรียกได้)
        /// เดินเฉพาะเมื่อ: จับเวลา & Playing & ไม่มี pause-reason (placement/quiz/crisis — V4 §15)
        /// </summary>
        public void AdvanceTime(float deltaTime)
        {
            if (!DayTimerActive || CurrentState != GameState.Playing || !TimeRunning())
                return;

            DayTimeRemaining -= deltaTime;

            // Planning → Live เมื่อเหลือเวลา ≤ ช่วง Live (เช่น เหลือ 60 จาก 90)
            if (CurrentDayPhase == DayPhase.Planning && DayTimeRemaining <= liveSeconds)
                EnterLivePhase();

            if (DayTimeRemaining <= 0f)
            {
                DayTimeRemaining = 0f;
                EndDay();
            }
        }
    }
}
