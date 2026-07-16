using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// The radiation storm (GDD §23 / CONFIG.md 🔒 STORM). Pressure climbs every day — faster the more
    /// the player has been boosting (escalating aggression), the higher CORE is, and while Zone B is
    /// open. At 100 the storm breaks: +40 HEAT/day into the reactor and +3 radiation/day on everyone.
    /// That's the timer the whole game races — you must finish CORE before it lands (Record #4).
    ///
    /// Sensor Array (unlocked via Record #3 → storm_detection) reads Pressure + LastRise to predict the
    /// arrival day; without it the storm is a blind deadline.
    ///
    /// Plain MonoBehaviour singleton; Initialize(cfg) + TickDay(...) are the EditMode-test entry.
    /// </summary>
    // -37: before ZoneBController (-35) and ReactorController (-33) so IsStormActive is set before the
    // reactor reads it for storm heat this same day.
    [DefaultExecutionOrder(-37)]
    public class StormSystem : MonoBehaviour
    {
        public static StormSystem Instance { get; private set; }

        private GameConfigSO _cfg;

        public float Pressure { get; private set; }
        public float LastRise { get; private set; }
        public bool IsStormActive { get; private set; }
        public int BoostDaysTotal { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Initialize(GameConfigSO.Instance);
        }

        /// <summary>Bootstrap — also the EditMode-test entry point.</summary>
        public void Initialize(GameConfigSO cfg)
        {
            _cfg = cfg;
            Pressure = 0f;
            LastRise = 0f;
            IsStormActive = false;
            BoostDaysTotal = 0;
        }

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

        private void HandleDayEnded(int day)
        {
            if (day <= 1) return; // Day 1 = tutorial
            float core = ReactorController.Instance != null ? ReactorController.Instance.Core : 0f;
            bool boosting = ReactorController.Instance != null && ReactorController.Instance.IsBoosting;
            bool zoneBOpen = ZoneBController.Instance != null && ZoneBController.Instance.IsOpen;
            TickDay(core, boosting, zoneBOpen);
        }

        /// <summary>§23 pressure rise — public for tests (state passed in, no live systems needed).</summary>
        public void TickDay(float core, bool boosting, bool zoneBOpen)
        {
            if (boosting) BoostDaysTotal++;

            float rise = _cfg.stormBaseRise
                       + BoostDaysTotal * _cfg.boostDaysCoeff          // ★ escalating aggression
                       + (core / 100f) * _cfg.stormCoreCoeff;
            if (core >= _cfg.methodBCoreGate) rise += _cfg.stormIgnitionBonus; // core ≥ 80
            if (zoneBOpen) rise += _cfg.stormZoneBBonus;

            LastRise = rise;
            Pressure = Mathf.Min(Pressure + rise, _cfg.stormPressureMax);
            if (Pressure >= _cfg.stormPressureMax) TriggerStorm();
        }

        private void TriggerStorm()
        {
            if (IsStormActive) return;
            IsStormActive = true;
            EventManager.Instance?.RaiseStormTriggered();
        }
    }
}
