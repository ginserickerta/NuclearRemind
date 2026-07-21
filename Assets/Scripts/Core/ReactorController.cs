using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// The reactor loop (GDD §26 / CONFIG.md 🔒 REACTOR) — the engine every other v6.3 system feeds:
    /// cooling (capped — bug #1), HEAT, CORE gain, and the ★ Method B tritium gate that makes Zone B
    /// the win condition. Consumes tritium from ZoneBController; reads cooling/fuel/poloidal/boost-heat
    /// bonuses from MasteryRegistry; takes storm heat from StormSystem and headroom from SensorArray.
    ///
    /// CORE ≥ 100 wins, HEAT ≥ 100 melts down (EndingSystem raises the actual game-over). Without the
    /// tritium quiz, CORE stalls at 80 (gain = 0) — the whole game's thesis in one branch.
    ///
    /// Plain MonoBehaviour singleton; Initialize(cfg) + DailyTick(...) are the EditMode-test entry
    /// (environmental couplings are passed in, so a test needs no ResourceManager/scene).
    /// </summary>
    // -33: after StormSystem (-37) and ZoneBController (-35) so stormActive/tritium are current, and
    // before CardManager (-30)/BarkManager (-20) so their snapshots see today's heat/core.
    [DefaultExecutionOrder(-33)]
    public class ReactorController : MonoBehaviour
    {
        public static ReactorController Instance { get; private set; }

        // ★ v6.3 cutover (slice 5 Reactor): auto-spawn the whole reactor cluster into the live game
        //   (was F9-playtest-only). Spawn order = OnDayEnded subscription order (multicast delegates fire
        //   in subscribe order): Storm → ZoneB → Reactor → Sensor → Ending, so each day the reactor reads
        //   today's storm/tritium and EndingSystem judges the fully-settled state. Once Instance exists,
        //   CoreTowerManager flips to facade mode (legacy sim off, HUD mirrors v6.3 CORE/HEAT).
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
                if (EventManager.Instance == null) return; // no core yet (MainMenu) — a later sceneLoaded retries
                Ensure<StormSystem>("StormSystem (auto)");
                Ensure<ZoneBController>("ZoneBController (auto)");
                Ensure<ReactorController>("ReactorController (auto)");
                Ensure<SensorArray>("SensorArray (auto)");
                // ★ slice 6: remaining Sprint-5 systems — narrators/decryption run on the settled day
                //   state but BEFORE EndingSystem judges it (Ending stays last in the OnDayEnded chain).
                Ensure<DataRecovery>("DataRecovery (auto)");
                Ensure<BarkManager>("BarkManager (auto)");
                Ensure<InnerVoiceDirector>("InnerVoiceDirector (auto)");
                Ensure<RadSuitManager>("RadSuitManager (auto)");
                Ensure<RecordFlowBridge>("RecordFlowBridge (auto)");
                Ensure<EndingSystem>("EndingSystem (auto)");
                // Push the initial CORE/HEAT to the HUD mirror (CoreTowerManager facade) right away.
                if (Instance != null)
                    EventManager.Instance.RaiseReactorStateChanged(Instance.Core, Instance.Heat);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ReactorController] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private static void Ensure<T>(string name) where T : Component
        {
            if (FindFirstObjectByType<T>() != null) return;
            new GameObject(name).AddComponent<T>();
        }

        private GameConfigSO _cfg;

        public float Core { get; set; }               // settable: save/load restore + tests
        public float Heat { get; set; }
        // Headless fallback for CurrentFuel only. In a real session the reactor reads (and spends) the
        // deuterium ledger on ResourceManager, which CONFIG.md starts at 0 — this field is never consulted.
        public float Fuel { get; set; }
        public bool IsBoosting { get; private set; }
        public int ToroidalLv { get; private set; }
        public int PoloidalLv { get; private set; }
        public float Tritium { get; set; }            // local fallback when no ZoneBController (tests)
        public float LastCooling { get; private set; }
        public float LastGain { get; private set; }
        public float LastTritiumConsumed { get; private set; } // tritium the core burned this tick (q_dt_fuel gate)
        private int _scramCooldown;

        public float FuelDemand => _cfg != null ? _cfg.fuelNeed : 6f;
        public bool IsWin => Core >= (_cfg != null ? _cfg.coreWin : 100f);
        public bool IsMeltdown => Heat >= (_cfg != null ? _cfg.heatMeltdown : 100f);
        public int ScramCooldown => _scramCooldown;

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
            Core = cfg.startCore;   // 30
            Heat = cfg.startHeat;   // 0 — reactor starts cold (CONFIG.md heat_start)
            Fuel = cfg.fuelNeed;    // headless default = one day of demand; the live game reads the ledger
            Tritium = 0f;
            IsBoosting = false;
            ToroidalLv = 0;
            PoloidalLv = 0;
            _scramCooldown = 0;
            LastCooling = 0f;
            LastGain = 0f;
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
            float water = ResourceManager.Instance != null ? ResourceManager.Instance.Current.water : 0f;
            int coolWorkers = WorkerManager.Instance != null ? WorkerManager.Instance.GetWorkers(WorkerJobs.Cool).Count : 0;
            bool storm = StormSystem.Instance != null && StormSystem.Instance.IsStormActive;
            bool sensor = SensorArray.Instance != null && SensorArray.Instance.IsActive;
            DailyTick(water, coolWorkers, storm, sensor);
        }

        // ─────────────────────────────────────────
        //  Mode / coils / SCRAM
        // ─────────────────────────────────────────

        public void SetBoosting(bool boosting) => IsBoosting = boosting;

        /// <summary>Install a Toroidal coil (needs the confinement note; caps at toroidalCoolMaxLv).</summary>
        public bool InstallToroidal()
        {
            if (!KnowledgeDB.Instance.HasNote("confinement") || ToroidalLv >= _cfg.toroidalCoolMaxLv) return false;
            ToroidalLv++;
            return true;
        }

        public bool InstallPoloidal()
        {
            if (!KnowledgeDB.Instance.HasNote("confinement") || PoloidalLv >= _cfg.toroidalCoolMaxLv) return false;
            PoloidalLv++;
            return true;
        }

        public bool CanScram => Heat >= _cfg.scramHeatThreshold && _scramCooldown <= 0;

        /// <summary>Emergency brake (GDD SCRAM): HEAT −40, CORE −10, water −30, hope −3, force idle.</summary>
        public bool Scram()
        {
            if (!CanScram) return false;
            Heat = Mathf.Max(0f, Heat - _cfg.scramHeatReduce);
            Core = Mathf.Max(0f, Core - _cfg.scramCorePenalty);
            IsBoosting = false; // scram_force_idle
            _scramCooldown = _cfg.scramCooldownDays;
            EventManager.Instance?.RaiseResourceDelta(ResourceType.Water, -_cfg.scramWaterCost);
            WorkerManager.Instance?.Hope?.Report("scram", "กด SCRAM ฉุกเฉิน", -_cfg.scramHopePenalty, HopeCategory.Reactor);
            return true;
        }

        // ─────────────────────────────────────────
        //  Daily tick (§26 formula — public for tests)
        // ─────────────────────────────────────────

        public void DailyTick(float water, int coolWorkers, bool stormActive, bool sensorActive)
        {
            if (_scramCooldown > 0) _scramCooldown--;
            var m = MasteryRegistry.Instance;
            LastTritiumConsumed = 0f;

            // ── HEAT ──
            float cooling = _cfg.coolingBase
                + Mathf.Min(water / _cfg.coolingWaterDiv, _cfg.coolingWaterCap)   // ★ capped (bug #1)
                + coolWorkers * _cfg.coolPerWorker
                + Mathf.Min(ToroidalLv, _cfg.toroidalCoolMaxLv) * _cfg.coolPerToroidalLv
                + m.CoolingBonus();                                              // +5 (confinement)
            if (sensorActive) cooling += _cfg.sensorHeatRoom;                    // Sensor headroom
            float poloidalDamp = PoloidalLv * _cfg.poloidalDampPerLv * m.PoloidalDampMult();

            float boostHeat = IsBoosting ? _cfg.boostHeatPerDay * m.BoostHeatMult() : 0f;
            float modeHeat = _cfg.modeHeatPerDay + boostHeat;
            float stormHeat = stormActive ? _cfg.stormHeatPerDay : 0f;

            Heat = Mathf.Max(0f, Heat + modeHeat + stormHeat - cooling - poloidalDamp);
            LastCooling = cooling;

            // ── CORE ──
            float gain;
            float tritium = CurrentTritium;
            float fuel = CurrentFuel;
            if (fuel <= 0f) gain = 0f;                                           // K01: no fuel, no gain
            else if (Core >= _cfg.methodBCoreGate && tritium < _cfg.methodBTritiumMin)
                gain = 0f;                                                       // ★ Method B gate — stalls at 80
            else
            {
                float baseGain = IsBoosting ? _cfg.boostCoreGain : _cfg.coreGainBase;
                float fe = Mathf.Min(1f, fuel / _cfg.fuelNeed) + m.FuelEfficiencyBonus();
                gain = baseGain * fe;

                // Burn what the day's run actually drew: the full demand when it was met, otherwise
                // whatever was in the tank. fe is the fraction of demand satisfied, so a partly-fuelled
                // day both advances less and costs less.
                ConsumeFuel(Mathf.Min(fuel, _cfg.fuelNeed));
                if (Core >= _cfg.methodBCoreGate)
                {
                    float cost = IsBoosting ? _cfg.boostTritiumCost : _cfg.idleTritiumCost;
                    ConsumeTritium(cost);
                    LastTritiumConsumed = Mathf.Min(cost, tritium); // what the core actually burned (0 if dry)
                    float remaining = CurrentTritium;
                    if (remaining < _cfg.tritiumSoftFloor)
                        gain *= Mathf.Min(1f, remaining / _cfg.tritiumSoftFloor);
                }
            }
            Core = Mathf.Min(_cfg.coreWin, Core + gain);
            LastGain = gain;

            ReportHope(stormActive, gain);
            EventManager.Instance?.RaiseReactorStateChanged(Core, Heat);
        }

        private int _stalledDays;

        /// <summary>
        /// The four reactor-side Hope sources from CONFIG.md's table. All four were documented with
        /// non-zero values in GameConfigSO and had no call site anywhere in the project, which is a large
        /// part of why Hope looked like it only ever went up: the recurring positives were wired and the
        /// recurring negatives were not.
        ///
        /// Rule #8 — never write Hope directly; everything goes through the ledger WorkerManager owns.
        /// WorkerManager commits at execution order -50 and this runs at -33, so these entries land in the
        /// following day's total. That one-day lag already applies to research.complete and is left as-is
        /// rather than reshuffling execution order on a deadline.
        /// </summary>
        private void ReportHope(bool stormActive, float gain)
        {
            var hope = WorkerManager.Instance?.Hope;
            if (hope == null) return;

            if (Heat > _cfg.heatWarn)
                hope.Report("heat.critical", "เตาร้อนวิกฤต", _cfg.hopeHeatCritical, HopeCategory.Reactor);

            if (stormActive)
                hope.Report("storm.active", "พายุรังสี", _cfg.hopeStormActive, HopeCategory.Storm);

            if (gain > 0f)
            {
                hope.Report("core.progress", $"CORE +{gain:0.0}%", gain * _cfg.hopeCoreProgress, HopeCategory.Reactor);
                _stalledDays = 0;
            }
            else if (!IsWin)
            {
                // Only nag while the tower still needs work — a finished core is not "stalled".
                _stalledDays++;
                if (_stalledDays >= _cfg.hopeCoreStalledDays)
                {
                    hope.Report("core.stalled", "หอคอยไม่คืบหน้า", _cfg.hopeCoreStalled, HopeCategory.Reactor);
                    _stalledDays = 0; // re-arm, so a long stall keeps costing rather than firing once
                }
            }
        }

        // ── fuel: the deuterium ledger, mirroring the tritium pair below ──
        //
        // Fuel used to be set once in Initialize and never written again by anything in the project, so
        // fe = min(1, Fuel/fuelNeed) was permanently 1 and CORE climbed every single day whether or not
        // the player had produced, allocated or spent a unit of deuterium. Extraction and the reactor
        // were simply two systems that had never been introduced to each other.
        private float CurrentFuel => ResourceManager.Instance != null
            ? ResourceManager.Instance.Current.deuterium
            : Fuel;

        /// <summary>
        /// Burn deuterium through the ledger. Unlike ConsumeTritium this does NOT decrement the local
        /// field when no ResourceManager exists: with no ledger there is nothing refilling it either, so
        /// draining it would leave the reactor permanently dry after one tick. Headless callers therefore
        /// treat Fuel as "fuel available per day" and set it themselves — which is exactly how the EditMode
        /// reactor tests already use it, several of which tick more than once and assert on gain.
        /// </summary>
        private void ConsumeFuel(float amount)
        {
            if (amount <= 0f) return;
            if (ResourceManager.Instance == null || EventManager.Instance == null) return;
            EventManager.Instance.RaiseResourceDelta(ResourceType.Deuterium, -amount);
        }

        // tritium is Zone B's stock when it exists, else a local field (test injection)
        private float CurrentTritium => ZoneBController.Instance != null ? ZoneBController.Instance.TritiumStock : Tritium;

        private void ConsumeTritium(float amount)
        {
            if (ZoneBController.Instance != null) ZoneBController.Instance.ConsumeTritium(amount);
            else Tritium = Mathf.Max(0f, Tritium - amount);
        }
    }
}
