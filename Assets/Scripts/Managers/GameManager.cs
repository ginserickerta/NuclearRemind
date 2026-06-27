// Assets/Scripts/Managers/GameManager.cs
using UnityEngine;

namespace NuclearReMind
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState { Playing, Paused, GameOver, Victory }
        public GameState CurrentState { get; private set; } = GameState.Playing;

        // ===== Day Cycle (§2.3) =====
        public const int MaxDay = 30;

        [Header("Day Cycle")]
        [Tooltip("ความยาววันที่จับเวลา (วินาที) — Day 2–30 ตาม §10 = 90")]
        public float dayLength = 90f;
        [Tooltip("Day 1 = tutorial ไม่จับเวลา (§2.3)")]
        public bool skipDay1Timer = true;

        public int CurrentDay { get; private set; } = 1;
        public float DayTimeRemaining { get; private set; }
        public bool DayTimerActive { get; private set; }

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
        }

        void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnTowerComplete -= HandleTowerComplete;
            EventManager.Instance.OnTutorialComplete -= HandleTutorialComplete;
            EventManager.Instance.OnSpeedChangeRequested -= HandleSpeedChangeRequested;
        }

        void Start()
        {
            Debug.Log("[GameManager] Initialized");
            // normalize timeScale (อาจค้างจาก play session ก่อน) + แจ้ง UI ให้ highlight ความเร็วเริ่มต้น
            Time.timeScale = GameSpeed;
            EventManager.Instance.RaiseSpeedChanged(Time.timeScale);
            BeginDay(1);
        }

        void HandleTowerComplete()
        {
            SetState(GameState.Victory);
            EventManager.Instance.RaiseGameOver(GameEndType.Win);
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

            Debug.Log($"[GameManager] Day {CurrentDay}/{MaxDay} started (timed={timed})");
            EventManager.Instance.RaiseDayStarted(CurrentDay, timed);
        }

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
            EventManager.Instance.RaiseDayEnded(finished);

            // ครบ 30 วัน — ปล่อยให้ระบบ ending (E2) มารับช่วงประเมินผล ตอนนี้แค่หยุดนับ
            if (finished >= MaxDay) return;

            BeginDay(finished + 1);
        }

        public void SetState(GameState newState)
        {
            CurrentState = newState;
            Debug.Log($"[GameManager] State → {newState}");

            Time.timeScale = newState switch
            {
                GameState.Paused  => 0f,
                GameState.Playing => GameSpeed, // ใช้ความเร็วที่เลือกไว้ (1× หรือ 2×)
                _                 => 1f,        // GameOver / Victory
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

            // นับถอยหลังวัน — ใช้ Time.deltaTime (สเกลตาม timeScale ของ pause/ความเร็ว A4)
            // pause → timeScale 0 → deltaTime 0 → freeze เอง ; เช็ค state ซ้ำกัน race
            if (DayTimerActive && CurrentState == GameState.Playing)
            {
                DayTimeRemaining -= Time.deltaTime;
                if (DayTimeRemaining <= 0f)
                {
                    DayTimeRemaining = 0f;
                    EndDay();
                }
            }
        }
    }
}
