using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: ตัดสินจบเกมทุกสิ้นวัน — ชนะเมื่อ CORE ≥ 100 (ไม่รอวัน 30 — บั๊ก #15) ·
    /// แพ้เมื่อ HEAT ≥ 100 (เตาหลอมละลาย) หรือ Hope ≤ 0 · ครบวัน 30 แล้วยังไม่ถึง 100 = จบแบบรอง
    /// ส่งผลลัพธ์ (GameEndType) ผ่าน EventManager ให้ GameManager แสดงฉากจบ — ยิงครั้งเดียวเท่านั้น
    /// Ending evaluator (STORY.md §4 / GDD §26). Checked every day-end (win/loss are state, not the
    /// calendar — the only allowed day literal is the day-30 deadline, rule #1):
    ///   • HEAT ≥ 100        → Meltdown
    ///   • CORE ≥ 100        → True Ending   (★ never wait for D30 — bug #15)
    ///   • Hope ≤ 0          → HopeZero
    ///   • Day 30, CORE 50–99 → Normal Ending
    ///   • Day 30, CORE &lt; 50 → TimeoutLowQ
    /// Raises GameEndType through EventManager (GameManager already listens). Fires once.
    ///
    /// Plain MonoBehaviour singleton; Initialize() + Evaluate(...) are the EditMode-test entry.
    /// </summary>
    // -15: last day-end handler, so it judges the fully-settled state (after reactor/storm/hope).
    [DefaultExecutionOrder(-15)]
    public class EndingSystem : MonoBehaviour
    {
        public static EndingSystem Instance { get; private set; }

        public const int DeadlineDay = 30; // ★ the one allowed `day == X` (rule #1 exception)

        public bool Ended { get; private set; }
        public GameEndType Result { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Initialize();
        }

        public void Initialize() => Ended = false;

        private void OnEnable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayEnded += HandleDayEnded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayEnded -= HandleDayEnded;
        }

        // สิ้นวัน: รวบรวมค่า core/heat/hope ปัจจุบันแล้วส่งให้ Evaluate ตัดสิน
        private void HandleDayEnded(int day)
        {
            var reactor = ReactorController.Instance;
            float core = reactor != null ? reactor.Core : 0f;
            float heat = reactor != null ? reactor.Heat : 0f;
            float hope = WorkerManager.Instance != null && WorkerManager.Instance.Hope != null
                ? WorkerManager.Instance.Hope.Current : 100f;
            Evaluate(day, core, heat, hope);
        }

        // หัวใจการตัดสิน: เช็คชนะก่อน (CORE 100 กันพายุได้แม้ HEAT ทะลุวันเดียวกัน) แล้วค่อยเช็คเงื่อนไขแพ้
        /// <summary>Pure evaluation — returns the end type, or null if the game continues. Public for tests.</summary>
        public GameEndType? Evaluate(int day, float core, float heat, float hope)
        {
            if (Ended) return Result;

            // Win is checked first: reaching CORE 100 succeeds even if HEAT also crosses 100 the same
            // day (the completed tower shields the storm — STORY.md True Ending).
            GameEndType? end = null;
            if (core >= (GameConfigSO.Instance?.coreWin ?? 100f)) end = GameEndType.TrueEnding;
            else if (heat >= (GameConfigSO.Instance?.heatMeltdown ?? 100f)) end = GameEndType.Meltdown;
            else if (hope <= 0f) end = GameEndType.HopeZero;
            else if (day >= DeadlineDay) end = core >= 50f ? GameEndType.NormalEnding : GameEndType.TimeoutLowQ;

            if (end.HasValue)
            {
                Ended = true;
                Result = end.Value;
                EventManager.Instance?.RaiseGameOver(end.Value);
            }
            return end;
        }
    }
}
