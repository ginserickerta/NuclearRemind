using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Inputs for one worker daily tick — injected so EditMode tests / headless sims can run
    /// 30 days without a scene. WorkerManager fills this from live systems in play mode;
    /// reactor/storm/lab fields keep defaults until their sprints wire them in.
    /// </summary>
    public class WorkerTickContext
    {
        public float foodStock;                // in: available food · out: reduced by what was eaten
        public float foodConsumed;             // out: food eaten this tick (peopleFed × foodPerWorkerPerDay)
        public bool boosting;                  // reactor Boost mode (Sprint 6)
        public bool stormActive;               // storm system (Sprint 6)
        public float heat;                     // reactor HEAT (Sprint 6) — > 85 leaks +5 rad
        public bool hasBarracks;               // Barracks built (Sprint 4) — rest −45 instead of −30
        public bool labBusy;                   // research job active (Sprint 2) — bug #7 guard
        public int medBayCapacity;             // 0 = no Med Bay yet (Sprint 3)
        public float medBayHeal;               // 25 (35 with Mastery)
        public bool masteryNuclearMedicine;    // rad ×0.8 (Sprint 3)
        public System.Random rng;              // deterministic death rolls — tests seed this
    }

    /// <summary>
    /// Per-worker population system (GDD §17 / §17.5 / §18) — replaces PopulationManager's
    /// aggregate counters with individual fatigue/hunger/radiation state.
    ///
    /// Owns the HopeLedger (GDD §18: hope is never written directly — every system calls
    /// WorkerManager.Instance.Hope.Report(...)) and the HopeThresholdWatcher.
    ///
    /// CRITICAL (GDD §17): building output must multiply Σ GetEfficiency(workers),
    /// never headcount — SumEfficiency(job) is the query production code must use.
    ///
    /// While this manager is active (Instance != null), PopulationManager's hope logic and
    /// ResourceManager's per-person consumption defer to it (guards added in both).
    /// </summary>
    // Execution order (after EventManager -100): WorkerManager -50 subscribes OnDayEnded BEFORE
    // ResearchLab -40, so worker status (hunger/exhaustion/radiation) is refreshed for the day
    // before ResearchLab's preempt/soft-trigger reads it. Multicast delegates fire in subscribe order.
    [DefaultExecutionOrder(-50)]
    public class WorkerManager : MonoBehaviour
    {
        public static WorkerManager Instance { get; private set; }

        // ★ v6.3 cutover (slice 2 Workers): auto-spawn into the live game (was F9-playtest-only). Presence
        //   flips the existing guards in PopulationManager (hope/pop-growth) + ResourceManager (per-person
        //   consumption) so the v6.3 per-worker systems take over. AfterSceneLoad fires once at the app's
        //   first scene; re-spawn on every sceneLoaded (same pattern as ResearchLab/LabPanelUI).
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
                if (FindFirstObjectByType<WorkerManager>() != null) return;
                new GameObject("WorkerManager (auto)").AddComponent<WorkerManager>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[WorkerManager] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// Worker names — short US/UK given names, one word each so they fit the name tag under a
        /// sprite at world scale. Handed out in order (i % length), never shuffled: the name is part of
        /// the saved Worker, and a run that renamed its people on reload would make every bark, death
        /// notice and dashboard row from before the save refer to somebody who no longer exists.
        /// </summary>
        private static readonly string[] NamePool =
        {
            "Alice", "Bennett", "Clara", "Dorian", "Eleanor", "Felix", "Grace", "Harvey",
            "Imogen", "Jasper", "Kate", "Lewis", "Maeve", "Nathan", "Olive", "Peter",
            "Quinn", "Rosalie", "Silas", "Thea", "Vera", "Walter", "Wren", "Xavier",
            "Yvette", "Zachary", "Cormac", "Delia",
        };

        private readonly List<Worker> _workers = new List<Worker>();
        private GameConfigSO _cfg;
        private bool _blackoutToday;           // bug #17: blackout is a FLAG, power itself never goes below 0

        /// <summary>The one hope ledger (GDD §18). All systems report through here.</summary>
        public HopeLedger Hope { get; private set; }
        public HopeThresholdWatcher Thresholds { get; private set; }

        /// <summary>True if an energy draw failed today (bug #17 flag). Cleared at day start — read it at day end.</summary>
        public bool BlackoutToday => _blackoutToday;

        /// <summary>Med Bay beds available right now — 0 when no finished Hospital exists.</summary>
        public int MedBayBeds => HospitalBeds();

        /// <summary>Mean radiation over living workers (CONFIG.md avgRad). 0 when nobody is alive.</summary>
        public float AvgRadiation
        {
            get
            {
                float sum = 0f; int n = 0;
                foreach (var w in _workers) if (w.alive) { sum += w.radiation; n++; }
                return n > 0 ? sum / n : 0f;
            }
        }

        /// <summary>Fired after every daily tick — UI panels refresh from this.</summary>
        public event System.Action OnWorkersChanged;

        public IReadOnlyList<Worker> Workers => _workers;
        public int AliveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _workers.Count; i++)
                    if (_workers[i].alive) n++;
                return n;
            }
        }

        /// <summary>
        /// Population ceiling from the best Shelter/Habitat currently standing (CONFIG.md ★ Shelter:
        /// L1 14 · L2 20 · L3 28). L1 is free at game start, so this never drops below shelterCapL1.
        /// Levels are read straight off BuildingRegistry — read-only query, no mutation.
        /// </summary>
        public int ShelterCap
        {
            get
            {
                int cap = _cfg.ShelterCapForLevel(1);
                var registry = BuildingRegistry.Instance;
                if (registry == null) return cap;

                foreach (var kvp in registry.PlacedBuildings)
                {
                    if (kvp.Value == null || kvp.Value.buildingType != BuildingType.Habitat) continue;
                    cap = Mathf.Max(cap, _cfg.ShelterCapForLevel(registry.GetLevel(kvp.Key)));
                }
                return cap;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Initialize(GameConfigSO.Instance);
        }

        /// <summary>Bootstrap from config — also the EditMode-test entry point (call with a test config).</summary>
        public void Initialize(GameConfigSO cfg)
        {
            _cfg = cfg;
            Hope = new HopeLedger(cfg);
            Thresholds = new HopeThresholdWatcher(cfg);
            Thresholds.OnStrike += HandleStrike;
            Thresholds.OnExodus += HandleExodus;
            Thresholds.OnGameOver += HandleHopeGameOver;

            _workers.Clear();
            for (int i = 0; i < cfg.startPopulation; i++)
                _workers.Add(new Worker
                {
                    id = i + 1,
                    displayName = NamePool[i % NamePool.Length],
                });
        }

        private void OnEnable()
        {
            if (EventManager.Instance == null) return; // EditMode tests may drive ticks directly
            EventManager.Instance.OnDayStarted += HandleDayStarted;
            EventManager.Instance.OnDayEnded += HandleDayEnded;
            EventManager.Instance.OnResourceDepleted += HandleResourceDepleted;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayStarted -= HandleDayStarted;
            EventManager.Instance.OnDayEnded -= HandleDayEnded;
            EventManager.Instance.OnResourceDepleted -= HandleResourceDepleted;
        }

        private void Start()
        {
            if (EventManager.Instance != null && Hope != null)
                EventManager.Instance.RaiseMoraleChanged(Hope.Current); // sync HUD to hope_start (70)
        }

        // ─────────────────────────────────────────
        //  Job assignment (planning-phase API)
        // ─────────────────────────────────────────

        /// <summary>Assign a worker to a job. Striking workers refuse; resting is cleared by choice.</summary>
        public bool AssignJob(Worker w, string job)
        {
            if (w == null || !w.alive || w.strikeDaysLeft > 0) return false;
            w.job = job;
            w.lastJob = WorkerJobs.Idle;
            w.resting = false;
            OnWorkersChanged?.Invoke();
            return true;
        }

        /// <summary>Fill jobs by count from idle workers (scene bootstrap / tests) — e.g. สมดุล 3/2/2/4/3.</summary>
        public void AssignJobCounts(IDictionary<string, int> counts)
        {
            foreach (var kvp in counts)
            {
                int need = kvp.Value;
                for (int i = 0; i < _workers.Count && need > 0; i++)
                {
                    var w = _workers[i];
                    if (!w.alive || w.strikeDaysLeft > 0 || w.job != WorkerJobs.Idle || w.resting) continue;
                    w.job = kvp.Key;
                    need--;
                }
            }
            OnWorkersChanged?.Invoke();
        }

        public List<Worker> GetWorkers(string job)
        {
            var list = new List<Worker>();
            for (int i = 0; i < _workers.Count; i++)
                if (_workers[i].alive && _workers[i].job == job) list.Add(_workers[i]);
            return list;
        }

        public int CountByStatus(WorkerStatus status)
        {
            int n = 0;
            for (int i = 0; i < _workers.Count; i++)
                if (_workers[i].alive && _workers[i].status == status) n++;
            return n;
        }

        public int HungryCount => CountByStatus(WorkerStatus.Hungry);
        public int ExhaustedCount => CountByStatus(WorkerStatus.Exhausted);
        public int SickCount => CountByStatus(WorkerStatus.Sick);
        public int DyingCount => CountByStatus(WorkerStatus.Dying);

        // ─────────────────────────────────────────
        //  Efficiency (GDD §17 — CRITICAL)
        // ─────────────────────────────────────────

        /// <summary>Per-worker efficiency (GDD §17 formula, thresholds from CONFIG.md).</summary>
        public float GetEfficiency(Worker w)
        {
            if (w == null || !w.alive) return 0f;
            if (w.fatigue > _cfg.effFatigueHard || w.radiation > _cfg.effRadHard) return 0f;
            float e = 1f;
            if (w.fatigue > _cfg.effFatigueSoft) e *= _cfg.effFatigueSoftMult;
            if (w.hunger > _cfg.hungryThreshold) e *= _cfg.effHungerSoftMult;
            return e;
        }

        /// <summary>
        /// Σ GetEfficiency over working staff of a job — the multiplier production code MUST use
        /// (GDD rule #7: never multiply headcount).
        /// </summary>
        public float SumEfficiency(string job)
        {
            float sum = 0f;
            for (int i = 0; i < _workers.Count; i++)
            {
                var w = _workers[i];
                if (w.alive && w.IsWorking && w.job == job)
                    sum += GetEfficiency(w);
            }
            return sum;
        }

        // ─────────────────────────────────────────
        //  Day-cycle wiring (play mode)
        // ─────────────────────────────────────────

        private void HandleDayStarted(int day, bool timed) => _blackoutToday = false;

        private void HandleResourceDepleted(ResourceType type)
        {
            if (type == ResourceType.Energy) _blackoutToday = true; // flag, not power<0 (bug #17)
        }

        private void HandleDayEnded(int day)
        {
            if (day <= 1) return; // Day 1 = tutorial: teach systems, no attrition yet (matches ResourceManager)

            var rm = ResourceManager.Instance;
            // Sprint 3: Mastery("nuclear_medicine") drives radiation ×0.8 (ALARA) and heal 25→35.
            // MasteryRegistry is a plain singleton — read-only query allowed (CLAUDE.md v6.3 exception).
            var mastery = MasteryRegistry.Instance;
            var ctx = new WorkerTickContext
            {
                foodStock = rm != null ? rm.Current.food : 0f,
                rng = _dayRng ?? (_dayRng = new System.Random()),
                medBayCapacity = HospitalBeds(),   // 0 unless a finished Hospital exists (heal was dormant before)
                medBayHeal = mastery.MedBayHeal(),
                masteryNuclearMedicine = mastery.Has(QuizIds.NuclearMedicine),
                // Sprint 6: heat leak (> 85 → +5 rad) and storm rad (+3) — last-known state (this handler
                // runs before Reactor/Storm tick, so it reads yesterday's; both persist, so the lag is fine).
                heat = ReactorController.Instance != null ? ReactorController.Instance.Heat : 0f,
                stormActive = StormSystem.Instance != null && StormSystem.Instance.IsStormActive,
            };

            RunDailyTick(ctx);

            if (rm != null && EventManager.Instance != null)
            {
                // apply what the tick decided: food eaten (hungriest first), water pop×1, labMat +5/day
                if (ctx.foodConsumed > 0)
                    EventManager.Instance.RaiseResourceDelta(ResourceType.Food, -ctx.foodConsumed);
                EventManager.Instance.RaiseResourceDelta(ResourceType.Water, -AliveCount * _cfg.waterPerPopPerDay);
                EventManager.Instance.RaiseResourceDelta(ResourceType.LabMat, _cfg.labMatPerDay);

                // rm.Current is already post-deduction here (RaiseResourceDelta applies synchronously),
                // so read the stocks straight — do NOT subtract foodConsumed again (that double-counted).
                ReportResourceHope(rm.Current.food, rm.Current.water);
            }

            TryGrowPopulation();               // before TickShifts so a newcomer gets a shift the same day
            ShiftSystem.TickShifts(_workers, ctx.labBusy, _cfg);
            TickStrikes();
            CommitDay();
        }

        private System.Random _dayRng;

        /// <summary>Resource-level hope sources (CONFIG.md Hope Sources) — both stocks are post-consumption.</summary>
        private void ReportResourceHope(float foodAfter, float waterAfter)
        {
            int pop = AliveCount;
            if (foodAfter > pop * _cfg.foodSurplusPopMult)
                Hope.Report("food.surplus", "อาหารเต็มคลัง", _cfg.hopeFoodSurplus, HopeCategory.Food);
            else if (foodAfter <= 0f)
                Hope.Report("food.empty", "อาหารหมด", _cfg.hopeFoodEmpty, HopeCategory.Food);

            if (waterAfter < pop)
                Hope.Report("water.shortage", "น้ำไม่พอ", _cfg.hopeWaterShortage, HopeCategory.Water);

            if (_blackoutToday)
                Hope.Report("power.blackout", "ไฟดับ", _cfg.hopePowerBlackout, HopeCategory.Power);
        }

        /// <summary>
        /// Newcomers arrive (GDD §5 "กลไกเติมประชากร" · CONFIG.md ★ POPULATION GROWTH):
        ///     if (food > pop * 5 && pop < shelterCap)  if (Random() &lt; 0.25) { pop += 1; food -= 20; }
        /// Deliberately NOT gated on Hope — v5.2 dropped that (Hope now starts at 70, the old ≥50 gate
        /// would have been free). Food is read post-consumption, so a city that only just fed itself
        /// does not recruit.
        ///
        /// This is the ONLY place workers are added after Initialize(). The v4.1 implementation lived in
        /// PopulationManager.HandleDayEnded, which early-returns whenever WorkerManager exists — i.e. always,
        /// in the live game. So population could only ever shrink until this was written.
        /// </summary>
        private void TryGrowPopulation()
        {
            var rm = ResourceManager.Instance;
            if (rm == null || EventManager.Instance == null) return;

            int pop = AliveCount;
            int cap = ShelterCap;
            if (pop >= cap) return;                                        // shelter is full — upgrade to grow
            if (rm.Current.food <= pop * _cfg.growthFoodRatio) return;     // needs a real surplus, not just "fed"

            var rng = _dayRng ?? (_dayRng = new System.Random());
            if (rng.NextDouble() >= _cfg.growthChance) return;             // 25%/day

            int added = 0;
            for (int i = 0; i < _cfg.growthAmount && AliveCount < cap; i++)
            {
                int id = NextWorkerId();
                _workers.Add(new Worker
                {
                    id = id,
                    displayName = NamePool[(id - 1) % NamePool.Length],
                });
                added++;
            }
            if (added == 0) return;

            EventManager.Instance.RaiseResourceDelta(ResourceType.Food, -_cfg.growthFoodCost * added);
        }

        /// <summary>Next free worker id — max existing + 1, so ids stay unique even after deaths.</summary>
        private int NextWorkerId()
        {
            int max = 0;
            for (int i = 0; i < _workers.Count; i++)
                if (_workers[i].id > max) max = _workers[i].id;
            return max + 1;
        }

        /// <summary>Commit the ledger, run threshold events, sync HUD. Public so tests can drive full days.</summary>
        public void CommitDay()
        {
            Hope.OnDayEnd();
            Thresholds.Evaluate(Hope.Current);
            EventManager.Instance?.RaiseMoraleChanged(Hope.Current);
            OnWorkersChanged?.Invoke();
        }

        /// <summary>
        /// Live running hope = committed Current + everything reported (not yet committed) today.
        /// The HUD bar shows this; it settles to Current at the end-of-day commit.
        /// </summary>
        public float LiveHope
        {
            get
            {
                if (Hope == null) return 0f;
                float sum = 0f;
                var today = Hope.GetTodayBreakdown();
                for (int i = 0; i < today.Count; i++) sum += today[i].value;
                return Mathf.Clamp(Hope.Current + sum, _cfg.hopeMin, _cfg.hopeMax);
            }
        }

        /// <summary>
        /// Report a hope entry AND reflect it on the HUD immediately (raises OnMoraleChanged with the live
        /// running value). Use for player-visible instant feedback — e.g. the Memorial's +2 first-click
        /// bonus (STORY.md §②). The entry still commits normally at end of day; this only makes the change
        /// show now instead of only in the daily total. Hope is still written solely through the ledger.
        /// </summary>
        public void ReportHopeLive(string sourceKey, string text, float value, HopeCategory category)
        {
            if (Hope == null) return;
            Hope.Report(sourceKey, text, value, category);
            EventManager.Instance?.RaiseMoraleChanged(LiveHope);
        }

        // ─────────────────────────────────────────
        //  Daily Tick (GDD §17 — order is law, DO NOT swap)
        // ─────────────────────────────────────────

        /// <summary>
        /// The fixed 7-step daily tick:
        /// 1 ApplyFatigue → 2 ApplyHunger → 3 ApplyRadiation → 4 MedBayHeal
        /// → 5 RecalcStatus → 6 ProcessDeaths → 7 ReportToHopeLedger
        /// Public + context-injected so EditMode tests simulate D1-30 headless.
        /// </summary>
        public void RunDailyTick(WorkerTickContext ctx)
        {
            _deathsThisTick = 0;
            ApplyFatigue(ctx);       // 1
            ApplyHunger(ctx);        // 2
            ApplyRadiation(ctx);     // 3
            MedBayHeal(ctx);         // 4
            RecalcStatus();          // 5
            ProcessDeaths(ctx);      // 6
            ReportToHopeLedger();    // 7
        }

        private int _deathsThisTick;

        private void ApplyFatigue(WorkerTickContext ctx)
        {
            for (int i = 0; i < _workers.Count; i++)
            {
                var w = _workers[i];
                if (!w.alive) continue;
                float g = w.IsWorking
                    ? _cfg.fatigueWork
                    : (ctx.hasBarracks ? _cfg.fatigueRestBarracks : _cfg.fatigueRest);
                if (ctx.boosting && w.job == WorkerJobs.Cool) g += _cfg.fatigueBoostCool;
                w.fatigue = Mathf.Clamp(w.fatigue + g, 0f, 100f);
            }
        }

        /// <summary>Hungriest eat first (GDD §17 — OrderByDescending, NEVER random). hunger +30/day unfed (bug #12).</summary>
        private void ApplyHunger(WorkerTickContext ctx)
        {
            var alive = _workers.Where(w => w.alive).ToList();

            // Ration size comes from config (CONFIG.md: food -= aliveWorkers * foodPerWorkerPerDay).
            // Guard <= 0 so a misconfigured asset feeds everyone for free instead of dividing by zero.
            float perHead = _cfg.foodPerWorkerPerDay;
            int peopleFed = perHead <= 0f
                ? alive.Count
                : Mathf.Min(alive.Count, Mathf.FloorToInt(ctx.foodStock / perHead));

            float eaten = peopleFed * Mathf.Max(perHead, 0f);
            ctx.foodStock -= eaten;
            ctx.foodConsumed = eaten;

            int fed = 0;
            foreach (var w in alive.OrderByDescending(w => w.hunger))
            {
                if (fed < peopleFed) { w.hunger = 0f; fed++; }
                else w.hunger = Mathf.Min(w.hunger + _cfg.hungerPerDay, 100f); // ★ 30 not 20
            }
        }

        private void ApplyRadiation(WorkerTickContext ctx)
        {
            for (int i = 0; i < _workers.Count; i++)
            {
                var w = _workers[i];
                if (!w.alive) continue;

                float r;
                switch (w.Zone)
                {
                    case WorkerZone.Mine: r = _cfg.radMine; break;
                    case WorkerZone.ZoneB: r = _cfg.radZoneB; break;
                    case WorkerZone.Core: r = _cfg.radCore; break;
                    default: r = 0f; break;
                }
                if (ctx.heat > _cfg.radHeatLeakThreshold) r += _cfg.radHeatLeak;
                if (ctx.stormActive) r += _cfg.radStorm;
                if (w.hasRadSuit) r *= _cfg.radSuitMult;
                if (ctx.masteryNuclearMedicine) r *= _cfg.radAlaraMult;

                // radiation never decays on its own — Med Bay is the only way down (GDD §17)
                w.radiation = Mathf.Clamp(w.radiation + r, 0f, 100f);
            }
        }

        private int _lastMedBayHealed;
        /// <summary>Patients healed on the last tick — the q_nuclear_medicine "applied" signal (QuizAppliedWatcher).</summary>
        public int LastMedBayHealed => _lastMedBayHealed;

        /// <summary>Med Bay heals the most irradiated first, up to capacity (0 until a Hospital is built).</summary>
        private void MedBayHeal(WorkerTickContext ctx)
        {
            _lastMedBayHealed = 0;
            if (ctx.medBayCapacity <= 0 || ctx.medBayHeal <= 0f) return;
            var patients = _workers
                .Where(w => w.alive && w.radiation > 0f)
                .OrderByDescending(w => w.radiation)
                .Take(ctx.medBayCapacity);
            foreach (var w in patients)
            {
                w.radiation = Mathf.Max(0f, w.radiation - ctx.medBayHeal);
                _lastMedBayHealed++;
            }
        }

        // A built, finished Hospital provides Med Bay beds (GDD §6). Without one, medBayCapacity stays 0 and
        // MedBayHeal is a no-op — which is why healing (and q_nuclear_medicine) was dormant before this wiring.
        private int HospitalBeds()
        {
            var reg = BuildingRegistry.Instance;
            if (reg == null || _cfg == null) return 0;
            var cc = ConstructionController.Instance;
            foreach (var kv in reg.PlacedBuildings)
            {
                var d = kv.Value;
                if (d == null || d.buildingType != BuildingType.Hospital) continue;
                if (cc != null && cc.IsUnderConstruction(kv.Key)) continue;
                return _cfg.medBayCapacity;
            }
            return 0;
        }

        /// <summary>Single status per worker, severity-ordered: Dying > Sick > Hungry > Exhausted > Tired.</summary>
        private void RecalcStatus()
        {
            for (int i = 0; i < _workers.Count; i++)
            {
                var w = _workers[i];
                if (!w.alive) { w.status = WorkerStatus.Dead; continue; }

                if (w.radiation > _cfg.dyingThreshold) w.status = WorkerStatus.Dying;
                else if (w.radiation > _cfg.sickThreshold) w.status = WorkerStatus.Sick;
                else if (w.hunger > _cfg.hungryThreshold) w.status = WorkerStatus.Hungry;
                // ★ exhaustedThreshold (68) ไม่ใช่ effFatigueHard (85) — เลข 85 เคยทำสองหน้าที่พร้อมกัน
                //   (วัดกำลัง + ตั้งชื่อสถานะ) แต่ ShiftSystem ดึงคนไปพักที่ 70 ยอดความล้าจริงจึงแค่ 72
                //   ป้าย "หมดแรง" เลยไม่เคยถูกติดให้ใคร และการ์ด overwork ไม่มีทางเกิด (ดู GameConfigSO)
                else if (w.fatigue > _cfg.exhaustedThreshold) w.status = WorkerStatus.Exhausted;
                // tiredThreshold (45) ไม่ใช่ effFatigueSoft (60) — ความล้าเดินทีละ 12 ถ้าใช้ 60 ช่วง "ล้า"
                // จะเหลือ 61-68 ซึ่งไม่มีค่าไหนตกลงไปเลย ผู้เล่นจะไม่ได้สัญญาณเตือนก่อนคนถูกดึงไปพัก
                else if (w.fatigue > _cfg.tiredThreshold) w.status = WorkerStatus.Tired;
                else w.status = WorkerStatus.Healthy;
            }
        }

        // ───────── Debug / bypass (DebugCheatPanel — test rig; callers compile out of release) ─────────

        /// <summary>
        /// Debug: force radiation on up to `count` alive workers (lowest dose first) and recalc
        /// statuses immediately, so the radiation table (>sickThreshold Sick · >dyingThreshold Dying)
        /// shows without waiting for the day tick. Gameplay radiation still goes through the daily tick.
        /// </summary>
        public void DebugSetRadiation(int count, float radiation)
        {
            foreach (var w in _workers.Where(x => x.alive).OrderBy(x => x.radiation).Take(Mathf.Max(0, count)))
                w.radiation = Mathf.Clamp(radiation, 0f, 100f);
            RecalcStatus();
        }

        /// <summary>Debug: wipe radiation/hunger/fatigue on everyone alive — instant full heal.</summary>
        public void DebugHealAll()
        {
            foreach (var w in _workers)
            {
                if (!w.alive) continue;
                w.radiation = 0f;
                w.hunger = 0f;
                w.fatigue = 0f;
            }
            RecalcStatus();
        }

        /// <summary>radiation > 80 → 20%/day death roll (GDD §17). rng injected for determinism.</summary>
        private void ProcessDeaths(WorkerTickContext ctx)
        {
            var rng = ctx.rng ?? (_dayRng ?? (_dayRng = new System.Random()));
            for (int i = 0; i < _workers.Count; i++)
            {
                var w = _workers[i];
                if (!w.alive || w.radiation <= _cfg.deathRad) continue;
                if (rng.NextDouble() < _cfg.deathChance)
                {
                    w.alive = false;
                    w.status = WorkerStatus.Dead;
                    w.job = WorkerJobs.Idle;
                    w.resting = false;
                    _deathsThisTick++;
                }
            }
        }

        /// <summary>Aggregate per-status hope entries (CONFIG.md table) — one row per source, value × count.</summary>
        private void ReportToHopeLedger()
        {
            ReportStatus(WorkerStatus.Exhausted, "worker.exhausted", "คนหมดแรง", _cfg.hopeWorkerExhausted);
            ReportStatus(WorkerStatus.Hungry, "worker.hungry", "คนหิว", _cfg.hopeWorkerHungry);
            ReportStatus(WorkerStatus.Sick, "worker.sick", "คนป่วย", _cfg.hopeWorkerSick);
            ReportStatus(WorkerStatus.Dying, "worker.dying", "คนอาการหนัก", _cfg.hopeWorkerDying);

            if (_deathsThisTick > 0)
                Hope.Report("worker.death", $"เสียคนไป {_deathsThisTick} คน",
                    _cfg.hopeWorkerDeath * _deathsThisTick, HopeCategory.Worker);

            ReportAlara();
        }

        /// <summary>
        /// alara.compliant (CONFIG.md Hope Sources) — the crew sent into Zone B is fully suited. Documented
        /// with a value since v6.3 and never reported by anything, like the reactor-side sources. Pays only
        /// when someone is actually down there: an empty Zone B is not compliance, it is just an empty room.
        /// </summary>
        private void ReportAlara()
        {
            int inZone = 0, suited = 0;
            for (int i = 0; i < _workers.Count; i++)
            {
                var w = _workers[i];
                if (!w.alive || !w.IsWorking || w.job != WorkerJobs.ZoneB) continue;
                inZone++;
                if (w.hasRadSuit) suited++;
            }

            if (inZone > 0 && suited == inZone)
                Hope.Report("alara.compliant", $"Zone B ใส่ชุดครบ {inZone} คน",
                    _cfg.hopeAlaraCompliant, HopeCategory.Worker);
        }

        private void ReportStatus(WorkerStatus status, string key, string label, float perHead)
        {
            int n = CountByStatus(status);
            if (n > 0)
                Hope.Report(key, $"{label} {n} คน", perHead * n, HopeCategory.Worker);
        }

        // ─────────────────────────────────────────
        //  Threshold effects (GDD §18)
        // ─────────────────────────────────────────

        /// <summary>Strike: ratio of alive workers stop for cfg.strikeDays — they keep lastJob and return.</summary>
        private void HandleStrike(float ratio)
        {
            var candidates = _workers.Where(w => w.alive && w.strikeDaysLeft <= 0).ToList();
            int count = Mathf.Max(1, Mathf.RoundToInt(candidates.Count * ratio));
            foreach (var w in candidates.OrderByDescending(w => w.fatigue).Take(count))
            {
                if (w.job != WorkerJobs.Idle) { w.lastJob = w.job; w.job = WorkerJobs.Idle; }
                w.strikeDaysLeft = _cfg.strikeDays;
            }
            EventManager.Instance?.RaiseNotice("คนงานบางส่วนหยุดงานประท้วง — ขวัญเมืองต่ำเกินไป");
        }

        /// <summary>Count down strike days and send finished strikers back to their job — once per day.</summary>
        public void TickStrikes()
        {
            for (int i = 0; i < _workers.Count; i++)
            {
                var w = _workers[i];
                if (!w.alive || w.strikeDaysLeft <= 0) continue;
                w.strikeDaysLeft--;
                if (w.strikeDaysLeft <= 0 && w.lastJob != WorkerJobs.Idle && !w.resting)
                {
                    w.job = w.lastJob;
                    w.lastJob = WorkerJobs.Idle;
                }
            }
        }

        /// <summary>Exodus: ratio of population leaves permanently (removed, no death penalty — they left).</summary>
        private void HandleExodus(float ratio)
        {
            var alive = _workers.Where(w => w.alive).ToList();
            int count = Mathf.Max(1, Mathf.FloorToInt(alive.Count * ratio));
            // idle/resting leave first — the city keeps its working hands longest
            foreach (var w in alive.OrderBy(w => w.IsWorking ? 1 : 0).Take(count))
                _workers.Remove(w);
            EventManager.Instance?.RaiseNotice($"ประชากร {count} คนทิ้งเมืองไป — ความหวังใกล้หมด");
        }

        private void HandleHopeGameOver()
        {
            EventManager.Instance?.RaiseGameOver(GameEndType.HopeZero);
        }
    }
}
