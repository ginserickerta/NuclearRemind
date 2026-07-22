using UnityEngine;

namespace NuclearReMind
{
    /// <summary>ชนิดทรัพยากรที่ผู้เล่นจัดสรรให้เตาต่อเทิร์น (UI −/+ · §2/§5)</summary>
    public enum ReactorAllocation { Deuterium, Tritium, CoolingEngineer, CoolingWater }

    /// <summary>
    /// CORE TOWER (V4 §8) — วัดด้วย CORE% (0→100) + ระบบความร้อน HEAT · ก้าวหน้าต่อวัน (OnDayEnded)
    ///
    /// สูตร (GDD v4.1 §8 — 3 เฟสเชื้อเพลิงตามแบนด์ CORE%):
    ///   30–50% Cold Assembly = เหล็ก+วิศวกร (ไม่ต้องมี Deuterium · fuelEff เต็ม) · 50–80% = Deuterium · 80–100% = Tritium (gate)
    ///   Tritium gate: ดันช่วง ≥80 แล้วไม่มี Tritium → ΔCORE=0 · Ignition เผา Deuterium + Tritium พร้อมกัน
    ///   fuelEff = min(1, deuterium/FuelNeed[mode]) + KnowBonus (Knowledge≥80 +0.10) · cap 1.10
    ///   cooling = 15 + waterUsed/10 + coolingEngineers×4 + min(toroidalLv,3)×10
    ///   HEAT: warn 80 (micro-damage CORE%−2 ถ้าไม่มี Poloidal) · meltdown 100 · พายุ +12 (Day 25–30) · Boost/OD upkeep +60E/วัน
    /// เชื่อมครบทุกเฟสแล้ว: coolingEngineers จาก PopulationManager (เฟส 3) · Coils (เฟส 6) · Deuterium/Tritium จาก Water/Lab L3 (เฟส 6)
    /// </summary>
    public class CoreTowerManager : MonoBehaviour
    {
        public static CoreTowerManager Instance { get; private set; }

        // ===== Phase / unlock =====
        public const int UnlockDay = 11;       // CORE ปลดล็อก Day 11
        public const float StartPercent = 30f; // เริ่มที่ 30% เมื่อปลดล็อก
        public const float Phase2At = 50f;     // 50% → เข้า Plasma Ramp
        public const float Phase3At = 80f;     // 80% → เข้า Ignition
        public const float WinPercent = 100f;

        // ===== Overclock modes (§3.3) index 0..3 =====
        private static readonly float[] ModeMultiplier = { 0f, 1f, 2f, 3f };  // Idle/Normal/Boost/Overdrive
        private static readonly float[] ModeHeat       = { 0f, 5f, 20f, 40f };
        public const int ModeIdle = 0, ModeNormal = 1, ModeBoost = 2, ModeOverdrive = 3;

        // fuelNeed ต่อโหมด (Idle/Normal/Boost/Overdrive) — V4 §18
        private static readonly float[] FuelNeed = { 0f, 10f, 20f, 30f };

        // ===== Formula constants (§8 / §18) =====
        [Header("Balance (CORE Spec §2/§12)")]
        public float baseCoreGain = 3f;        // dCORE = 3 × mult × fuelEff
        public float tritiumFuelNeed = 10f;    // §7/§12: Tritium/วัน ที่เผาเมื่อดันช่วง CORE ≥ 80
        public float baseCooling = 15f;
        public float coolingWaterCap = 100f;   // น้ำสูงสุดที่ใช้หล่อเย็นต่อเทิร์น
        public float stormHeat = 12f;          // พายุ Day 25–30 (GDD v4.1 §8/§18 = +12/วัน)
        public float reactorBoostUpkeepEnergy = 60f; // GDD v4.1 §18: Boost/Overdrive กิน energy upkeep +60/วัน (energy sink)
        public int   coolingTowerLevel = 0;    // Toroidal Coils (มีผล ×10, cap 3)
        public bool  hasPoloidalCoils = false; // Poloidal Coils (เปิด engineers×4 + กัน micro-damage)

        [Header("Coils cost (CORE Spec §5)")]
        public int toroidalIronCost = 50;    // Toroidal Coils: เหล็ก 50 + พลังงาน 80 /ระดับ
        public int toroidalEnergyCost = 80;
        public int poloidalIronCost = 60;    // Poloidal Coils: เหล็ก 60 + พลังงาน 100 (ครั้งเดียว)
        public int poloidalEnergyCost = 100;
        public const int MaxToroidalLevel = 3;

        public const float HeatWarnZone = 80f;    // เข้าโซนเตือน (micro-damage ถ้าไม่มี Poloidal)
        public const float HeatMeltdown = 100f;   // ≥ นี้ = MELTDOWN
        public const int   StormStartDay = 25;    // พายุเริ่ม Day 25
        public const float MicroDamageCore = 2f;  // CORE% ที่เสียต่อวันในโซนเตือน (ไม่มี Poloidal)

        [Header("SCRAM (§3.3)")]
        public float scramHeatThreshold = 90f; // กดได้เมื่อ HEAT >= ค่านี้
        public float scramHeatReduction = 40f;
        public float scramCorePenalty = 10f;
        public float scramWaterCost = 50f;
        public int scramCooldownTurns = 2;

        [Header("Allocation (§2/§5 — ผู้เล่นจัดสรรต่อเทิร์น)")]
        public float maxCoolingWater = 300f; // เพดานน้ำที่ทุ่มหล่อเย็นได้ (ปุ่ม "หล่อเย็นเพิ่ม")

        // การจัดสรรต่อเทิร์น (transient — reset ทุกเช้าเป็นค่า default ที่พอดี · ไม่เซฟ)
        private float _planDeut, _planTrit, _planCoolWater;
        private int _planCoolEng;

        public float PlannedDeuterium => _planDeut;
        public float PlannedTritium => _planTrit;
        public int PlannedCoolingEngineers => _planCoolEng;
        public float PlannedCoolingWater => _planCoolWater;
        public float DeuteriumNeed => FuelNeed[Mathf.Clamp(Current.overclockMode, 0, 3)];
        public float TritiumNeed => tritiumFuelNeed;
        private int TotalEngineers => PopulationManager.Instance != null ? PopulationManager.Instance.Current.engineers : 0;
        public int MaxCoolingEngineers => TotalEngineers;

        public TowerData Current { get; private set; } = new TowerData
        {
            corePercent = 0f,
            coreHeat = 0f,
            currentPhase = 0,      // 0 = ยังล็อก
            overclockMode = ModeNormal,
            heatCap = 100f,
            isUnlocked = false,
        };

        private bool _ended; // meltdown หรือ ignition แล้ว — หยุดเดินเครื่อง
        private int _forcedIdleDays; // วิกฤต 2·B (GDD ล่าสุด): บังคับเตา Idle N วัน (ผลิตไอโซโทปการแพทย์)
        private bool _modeLocked;    // ล็อกโหมดตอนเข้าเฟส Live (V4 §3) — เปลี่ยนโหมดได้เฉพาะ Planning
        private float _simmedFrac;   // สัดส่วนของวันที่เดินเตาแบบเรียลไทม์ไปแล้ว (0..1) — จบวัน reconcile เศษที่เหลือ

        // ★ v6.3 cutover (slice 5 Reactor): when ReactorController is live this manager becomes a thin
        //   facade (CUTOVER_PLAN §2 shim) — the legacy simulation stops entirely and TowerData mirrors
        //   v6.3 CORE/HEAT, so every legacy consumer (CoreTowerUI, CoreTowerPanelUI, UIManagerHUD,
        //   SaveManager, SoftTriggerWatcher, story hooks) keeps working on v6.3 numbers. Delete this
        //   whole class once the HUD is migrated to a native v6.3 reactor panel (cleanup phase).
        private static bool V63Live => ReactorController.Instance != null;

        /// <summary>Q Factor = CORE% / 100 (§8: CORE% 100 = Q 1.0 = Breakeven) — ใช้ตัดสิน ending (เฟส 7)</summary>
        public float Q => V63Live ? ReactorController.Instance.Core / 100f : Current.corePercent / 100f;

        /// <summary>
        /// v6.3 mirror: rebuild TowerData from ReactorController and re-raise OnTowerProgressChanged so
        /// legacy HUD/save stay in sync. isUnlocked is always true (v6.3 has no Day-11 unlock — rule #1);
        /// overclockMode mirrors the reactor's real 4-mode state (Idle/Normal/Boost/Overdrive).
        /// </summary>
        private void MirrorV63()
        {
            var r = ReactorController.Instance;
            if (r == null) return;
            var t = Current;
            t.corePercent = r.Core;
            t.coreHeat = r.Heat;
            t.currentPhase = PhaseFor(r.Core);   // legacy HUD naming (Cold/Plasma/Ignition @50/80)
            t.overclockMode = r.Mode; // same 0..3 constants — full 4-mode reactor (2026-07-22)
            t.heatCap = HeatMeltdown;
            t.isUnlocked = true;
            t.scramCooldown = r.ScramCooldown;
            Current = t;
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

        private void HandleV63ReactorState(float core, float heat) => MirrorV63();

        /// <summary>จำนวนวันที่เตาถูกบังคับ Idle ที่เหลือ (วิกฤต 2·B) — read-only</summary>
        public int ForcedIdleDaysRemaining => _forcedIdleDays;

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
            EventManager.Instance.OnDayStarted             += HandleDayStarted;
            EventManager.Instance.OnDayEnded               += HandleDayEnded;
            EventManager.Instance.OnGameTick               += HandleGameTick;
            EventManager.Instance.OnSaveLoaded             += HandleSaveLoaded;
            EventManager.Instance.OnOverclockModeRequested += HandleOverclockModeRequested;
            EventManager.Instance.OnScramRequested         += HandleScramRequested;
            EventManager.Instance.OnReactorAllocationAdjust += HandleAllocationAdjust;
            EventManager.Instance.OnUpgradeToroidalRequested += UpgradeToroidal;
            EventManager.Instance.OnInstallPoloidalRequested += InstallPoloidal;
            EventManager.Instance.OnItemUsed               += HandleItemUsed;
            EventManager.Instance.OnResearchCompleted      += HandleResearchCompleted;
            EventManager.Instance.OnReactorStateChanged    += HandleV63ReactorState; // ★ v6.3 facade mirror
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayStarted             -= HandleDayStarted;
            EventManager.Instance.OnDayEnded               -= HandleDayEnded;
            EventManager.Instance.OnGameTick               -= HandleGameTick;
            EventManager.Instance.OnSaveLoaded             -= HandleSaveLoaded;
            EventManager.Instance.OnOverclockModeRequested -= HandleOverclockModeRequested;
            EventManager.Instance.OnScramRequested         -= HandleScramRequested;
            EventManager.Instance.OnReactorAllocationAdjust -= HandleAllocationAdjust;
            EventManager.Instance.OnUpgradeToroidalRequested -= UpgradeToroidal;
            EventManager.Instance.OnInstallPoloidalRequested -= InstallPoloidal;
            EventManager.Instance.OnItemUsed               -= HandleItemUsed;
            EventManager.Instance.OnResearchCompleted      -= HandleResearchCompleted;
            EventManager.Instance.OnReactorStateChanged    -= HandleV63ReactorState;
        }

        /// <summary>
        /// ไอเทม §13 ที่กระทบเตา — น้ำหล่อเย็นฉุกเฉิน (วิกฤต 1·C): ลด HEAT ทันทีตามค่าใน asset
        /// มาทาง event (InventoryManager ไม่เรียก CoreTowerManager ตรง) — ค่าลดอยู่ใน ItemSO ไม่ hardcode
        /// </summary>
        private void HandleItemUsed(ItemSO item)
        {
            if (item != null && item.coreHeatReduction > 0f)
                ReduceHeat(item.coreHeatReduction);
        }

        private void Start()
        {
            if (V63Live) { MirrorV63(); return; } // ★ v6.3 facade: HUD starts on v6.3 numbers
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

        // ───────────────────────── Unlock (Day 11 + วิจัย) ─────────────────────────

        private void HandleDayStarted(int day, bool timed)
        {
            if (V63Live) { _manualCoolWaterToday = 0f; MirrorV63(); return; } // ★ v6.3: reset today's manual-cool spend, mirror only

            _simmedFrac = 0f; // เริ่มวันใหม่ → รีเซ็ตความคืบเรียลไทม์ (เดินใหม่ทั้งวัน)

            if (Current.isUnlocked)
            {
                RecomputeAllocationDefaults(); // รีเซ็ตการจัดสรรทุกเช้า (Planning) — ผู้เล่นปรับได้ระหว่างวัน
                EventManager.Instance.RaiseTowerProgressChanged(Current);
                return;
            }
            TryUnlock(day);
        }

        // ปลดเตาเมื่อครบ 2 เงื่อนไข: ถึงวัน (UnlockDay) + วิจัย "ปลดล็อก CORE TOWER" แล้ว (ResearchLab_Spec §3
        // / GDD §6: ต้นทุนเตา "Iron 200 + Energy 200 + วิจัย") — read-only query · ไม่มี ResearchManager
        // (เช่นใน EditMode tests) = ไม่บังคับวิจัย → พฤติกรรม Day 11 เดิมคงอยู่
        private void TryUnlock(int day)
        {
            if (Current.isUnlocked || day < UnlockDay) return;
            if (ResearchManager.Instance != null && !ResearchManager.Instance.CoreUnlockDone)
            {
                EventManager.Instance.RaiseNotice("CORE TOWER รอผลวิจัย 'ปลดล็อก CORE TOWER' จากห้องวิจัยก่อนเดินเครื่อง");
                return;
            }

            var t = Current;
            t.isUnlocked = true;
            t.corePercent = StartPercent;
            t.currentPhase = 1;
            t.heatCap = HeatMeltdown; // heatCap = 100 คงที่ (V4: threshold ตายตัว ไม่เสื่อม)
            t.coreHeat = 0f;
            t.overclockMode = ModeNormal;
            Current = t;
            RecomputeAllocationDefaults();

            Debug.Log($"[CoreTower] ปลดล็อก Day {day} — CORE {StartPercent}%");
            EventManager.Instance.RaiseOverclockModeChanged(t.overclockMode);
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

        // วิจัยเสร็จหลังถึง Day 11 แล้ว → ปลดเตาทันที (ไม่ต้องรอเช้าวันถัดไป)
        private void HandleResearchCompleted(string projectId)
        {
            if (projectId != ResearchManager.ProjectCoreTower) return;
            int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 0;
            TryUnlock(day);
        }

        // ★ v6.3: no-op — ควิซไทรเทียมไม่เด้งตอนป้อนเชื้อเพลิงอีกแล้ว
        //
        // ของเดิมเด้ง QT1/QT2 ซึ่งเป็นควิซชุด v4.1 ที่ quizId ว่าง → ตอบแล้วไม่ได้อะไร (ดู OreDepositManager)
        // ตัวจริงคือ q_tritium_breeding / q_dt_fuel ใน Resources/Quizzes ซึ่ง QuizAppliedWatcher จะปลดให้เอง
        // หลังเตาได้กินไทรเทียมจริง แล้วเด้งเชิญผ่าน OnQuizShown · เก็บเมธอดไว้เพื่อไม่ต้องแก้ call site
        private bool _tritiumQuizFired;
        private void TriggerTritiumQuizzesOnce()
        {
            if (_tritiumQuizFired) return;
            _tritiumQuizFired = true;
        }

        // ───────────────────────── Turn (per day) ─────────────────────────

        // จบวัน (V4 §3): reconcile — เดินเตาเฉพาะ "เศษวัน" ที่ยังไม่ได้เดินแบบเรียลไทม์
        // (เทส/Day 1 ที่ไม่มี tick → _simmedFrac=0 → เดินเต็มวัน frac=1 = พฤติกรรมเดิมเป๊ะ)
        private void HandleDayEnded(int day)
        {
            if (V63Live) return; // ★ v6.3: ReactorController ticks itself on OnDayEnded — legacy sim off
            if (_ended || !Current.isUnlocked) return;
            if (!HasActiveCoreTowerPart()) return; // ต้องสร้าง CORE TOWER ก่อนถึงจะเดินเครื่อง

            float remaining = Mathf.Clamp01(1f - _simmedFrac);
            StepReactor(day, remaining, dayBoundary: true);
            _simmedFrac = 0f;
        }

        // เดินเตาแบบเรียลไทม์ระหว่างเฟส Live (V4 §3): เดินทีละเสี้ยว (frac = tick/liveSeconds)
        // → หลอด CORE% ไต่ต่อเนื่องไม่ต้องรอข้ามวัน · เศษที่เหลือ reconcile ตอน HandleDayEnded
        private void HandleGameTick()
        {
            if (V63Live) return; // ★ v6.3: production is batch end-of-day (§2) — no realtime reactor
            if (_ended || !Current.isUnlocked) return;

            var gm = GameManager.Instance;
            if (gm == null || !gm.DayTimerActive) return;                 // Day 1 tutorial ไม่เดินเรียลไทม์
            if (gm.CurrentState != GameManager.GameState.Playing) return; // pause/จบเกม → หยุด
            if (gm.CurrentDayPhase != GameManager.DayPhase.Live) return;  // เดินเฉพาะเฟส Live (โหมดถูกล็อกแล้ว)
            if (!HasActiveCoreTowerPart()) return;

            float live = gm.liveSeconds;
            float tick = ResourceManager.Instance != null ? ResourceManager.Instance.tickInterval : 5f;
            if (live <= 0f || tick <= 0f) return;

            float frac = Mathf.Min(tick / live, 1f - _simmedFrac); // ไม่ให้เกิน 1 ก่อนจบวัน
            if (frac <= 0f) return;

            StepReactor(gm.CurrentDay, frac, dayBoundary: false);
            _simmedFrac += frac;
        }

        /// <summary>
        /// เดินเตา "เสี้ยวหนึ่งของวัน" (frac) — สูตรเดิมทั้งหมดคูณ frac (CORE%/HEAT/เชื้อเพลิง/หล่อเย็น/พายุ/micro-damage)
        /// frac=1 + dayBoundary=true = เทิร์นเต็มวันแบบเดิมเป๊ะ · งานรายวันครั้งเดียว (cooldown/forcedIdle) ทำที่ dayBoundary
        /// </summary>
        private void StepReactor(int day, float frac, bool dayBoundary)
        {
            var t = Current;

            // ----- งานรายวันครั้งเดียว (ทำที่ขอบวันเสมอ แม้ frac=0) -----
            bool forcedIdle = _forcedIdleDays > 0;
            if (dayBoundary)
            {
                if (t.scramCooldown > 0) t.scramCooldown--;   // ลด cooldown SCRAM ทุกเทิร์น
                if (forcedIdle) _forcedIdleDays--;            // นับวัน Idle ที่ถูกบังคับ (วิกฤต 2·B)
            }
            if (frac <= 0f)
            {
                Current = t;
                if (dayBoundary) EventManager.Instance.RaiseTowerProgressChanged(Current);
                return;
            }

            // โหมด: วิกฤต 2·B บังคับ Idle > โหมดที่ผู้เล่นเลือก (Overclock ปลดล็อกตั้งแต่ Day 11/30% §9)
            int mode = forcedIdle ? ModeIdle : Mathf.Clamp(t.overclockMode, 0, 3);

            // ----- เชื้อเพลิงตามแบนด์ CORE% (GDD v4.1 §8 — 3 เฟสเชื้อเพลิง) — ป้อนตามที่ผู้เล่นจัดสรร -----
            //   30–50% Cold Assembly = เหล็ก+วิศวกร (ไม่ต้องมี Deuterium · fuelEff เต็ม)
            //   50–80% Plasma Ramp   = Deuterium ดัน fuelEff (min(1, deut/need))
            //   80–100% Ignition     = ต้องมี Tritium (gate: ไม่มี → ΔCORE=0) + เผา Deuterium & Tritium พร้อมกัน
            var rm = ResourceManager.Instance;
            float knowBonus = rm != null ? rm.KnowBonus : 0f;
            float need = FuelNeed[mode];
            float deutFed = Mathf.Clamp(_planDeut, 0f, rm != null ? rm.Current.deuterium : 0f);
            float tritFed = Mathf.Clamp(_planTrit, 0f, rm != null ? rm.Current.tritium : 0f);

            bool coldAssembly = t.corePercent < Phase2At;   // 30–50%: ยังไม่ต้องใช้เชื้อเพลิงฟิวชัน
            bool ignition     = t.corePercent >= Phase3At;  // 80–100%: ต้องมี Tritium

            // Cold Assembly → fuelEff เต็ม (เดินด้วยเหล็ก+วิศวกรที่มีอยู่แล้ว) · เฟส 2+ → ตาม Deuterium ที่ป้อน
            float fuelEff = coldAssembly
                ? Mathf.Min(1f + knowBonus, 1.10f)
                : Mathf.Min((need > 0f ? Mathf.Min(1f, deutFed / need) : 1f) + knowBonus, 1.10f);

            bool pushing = mode != ModeIdle;
            bool tritiumGateBlocked = pushing && ignition && tritFed <= 0f;

            float dCore;
            if (!pushing || tritiumGateBlocked)
            {
                dCore = 0f; // Idle หรือ ติดกำแพง Tritium → ไม่ดัน + ไม่เผาเชื้อเพลิง
            }
            else
            {
                dCore = baseCoreGain * ModeMultiplier[mode] * fuelEff;

                // Cold Assembly (30–50%) ไม่เผา Deuterium · เฟส 2+ เผาตามที่จัดสรร
                if (!coldAssembly && deutFed > 0f)
                    EventManager.Instance.RaiseResourceDelta(ResourceType.Deuterium, -deutFed * frac);
                if (ignition && tritFed > 0f) // §8: เผา Tritium ช่วง Ignition (CORE ≥ 80)
                {
                    EventManager.Instance.RaiseResourceDelta(ResourceType.Tritium, -tritFed * frac);
                    TriggerTritiumQuizzesOnce(); // ควิซ #Tritium (Codex_Spec v8) — ป้อน Tritium ครั้งแรก
                }
            }

            // Boost/Overdrive upkeep (GDD v4.1 §18): +60E/วัน — energy sink ทำให้ Phase 4 ตึงตาม climax
            if (pushing && mode >= ModeBoost)
                EventManager.Instance.RaiseResourceDelta(ResourceType.Energy, -reactorBoostUpkeepEnergy * frac);

            // ----- หล่อเย็น (§5): 15 + น้ำที่จัดสรร/10 + วิศวกรที่จัดสรร×4 + min(toroidalLv,3)×10 -----
            float water = rm != null ? rm.Current.water : 0f;
            float waterUsed = Mathf.Clamp(_planCoolWater, 0f, water);
            if (waterUsed > 0f)
                EventManager.Instance.RaiseResourceDelta(ResourceType.Water, -waterUsed * frac);

            // วิศวกรหล่อเย็น: นับเฉพาะเมื่อมี Poloidal Coils (§5) · ตามที่ผู้เล่นจัดสรร (clamp กับที่มีจริง)
            int coolEng = hasPoloidalCoils ? Mathf.Clamp(_planCoolEng, 0, TotalEngineers) : 0;
            int decreeCooling = DecreeManager.Instance != null ? DecreeManager.Instance.CoolingLaborBonus : 0;
            float cooling = baseCooling
                          + waterUsed / 10f
                          + coolEng * 4f
                          + Mathf.Min(coolingTowerLevel, 3) * 10f
                          + decreeCooling; // แรงงานหล่อเย็นจากประกาศฉุกเฉิน (§11)

            // ----- CORE% (× frac) -----
            t.corePercent = Mathf.Min(WinPercent, t.corePercent + dCore * frac);

            // ----- HEAT (พายุ +12 เฉพาะ Day 25–30, GDD v4.1 §8) × frac -----
            float storm = (day >= StormStartDay) ? stormHeat : 0f;
            float dHeat = ModeHeat[mode] + storm - cooling;
            t.coreHeat = Mathf.Max(0f, t.coreHeat + dHeat * frac);

            // ----- โซนเตือน 80–99: micro-damage CORE%−2/วัน ถ้าไม่มี Poloidal (§8) × frac -----
            if (t.coreHeat >= HeatWarnZone && t.coreHeat < HeatMeltdown && !hasPoloidalCoils)
                t.corePercent = Mathf.Max(0f, t.corePercent - MicroDamageCore * frac);

            Current = t;

            // ----- meltdown (เช็คทุกเสี้ยว → หลอมได้กลางวันแบบเรียลไทม์) -----
            if (t.coreHeat >= HeatMeltdown)
            {
                _ended = true;
                Debug.Log("[CoreTower] MELTDOWN — HEAT ≥ 100");
                EventManager.Instance.RaiseTowerProgressChanged(Current);
                EventManager.Instance.RaiseGameOver(GameEndType.Meltdown);
                return;
            }

            UpdatePhaseTransitions();

            if (Current.corePercent >= WinPercent)
            {
                _ended = true;
                EventManager.Instance.RaiseTowerProgressChanged(Current);
                EventManager.Instance.RaiseTowerComplete();
                return;
            }

            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

        /// <summary>เลื่อน phase ตาม CORE% และ raise OnTowerPhaseComplete สำหรับ phase ที่เพิ่งจบ</summary>
        private void UpdatePhaseTransitions()
        {
            int target = PhaseFor(Current.corePercent);
            while (target > Current.currentPhase)
            {
                int completed = Current.currentPhase; // phase ที่เพิ่งจบ
                var t = Current;
                t.currentPhase++;
                Current = t;
                EventManager.Instance.RaiseTowerPhaseComplete(completed);

                // Q8/Q9 ไม่ยิงตรงนี้แล้ว — ย้ายไป StoryBeat "storm_first_light" (Day 25)
                // ให้ InfoCard ฟิวชันเด้งก่อนควิซตามกฎเหล็ก Story Guide (ความรู้มาก่อนควิซ)
            }
        }

        private static int PhaseFor(float corePercent)
        {
            if (corePercent < Phase2At) return 1; // Cold
            if (corePercent < Phase3At) return 2; // Plasma
            return 3;                              // Ignition
        }

        // ───────────────────────── Overclock control ─────────────────────────

        private void HandleOverclockModeRequested(int mode) => SetOverclockMode(mode);

        private void HandleScramRequested() => Scram();

        /// <summary>
        /// SCRAM (§3.3): ฉุกเฉินเมื่อ HEAT >= 90 → HEAT −40, CORE% −10, เสีย Water, มี cooldown
        /// phase ไม่ถอยกลับ (currentPhase ไม่ลด แม้ CORE% จะตกต่ำกว่าเกณฑ์)
        /// </summary>
        public void Scram()
        {
            // ★ v6.3 facade: forward to ReactorController (§8 — HEAT −40, CORE −10, water −30, hope −3)
            if (V63Live)
            {
                if (ReactorController.Instance.Scram()) MirrorV63();
                return;
            }

            if (_ended || !Current.isUnlocked) return;
            if (Current.scramCooldown > 0) return;
            if (Current.coreHeat < scramHeatThreshold) return;

            var t = Current;
            t.coreHeat = Mathf.Max(0f, t.coreHeat - scramHeatReduction);
            t.corePercent = Mathf.Max(0f, t.corePercent - scramCorePenalty);
            t.scramCooldown = scramCooldownTurns;
            Current = t;

            if (ResourceManager.Instance != null)
                EventManager.Instance.RaiseResourceDelta(ResourceType.Water, -scramWaterCost);

            Debug.Log($"[CoreTower] SCRAM — HEAT −{scramHeatReduction}, CORE% −{scramCorePenalty}, cooldown {scramCooldownTurns}");
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

        /// <summary>
        /// ลด HEAT ตอนแก้วิกฤต (Story Guide §4 พลาสมา — afterText "HEAT กลับสู่ระดับปลอดภัย")
        /// เหมือน Scram แต่ไม่มีเงื่อนไข HEAT≥90/cooldown (วิกฤตเป็นคนสั่ง ไม่ใช่ปุ่มผู้เล่น) · clamp ≥ 0
        /// ปิดบั๊ก "แก้พลาสมาเสร็จแล้ว HEAT ยังสูงจนหลอมทันที" (เดิมไม่มีอะไรลด HEAT ตอน resolve)
        /// </summary>
        public void ReduceHeat(float amount)
        {
            if (amount <= 0f) return;
            if (V63Live) // ★ v6.3 facade: crisis/item heat relief lands on the real reactor
            {
                var r = ReactorController.Instance;
                r.Heat = Mathf.Max(0f, r.Heat - amount);
                MirrorV63();
                return;
            }
            if (!Current.isUnlocked) return;
            var t = Current;
            t.coreHeat = Mathf.Max(0f, t.coreHeat - amount);
            Current = t;
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

        /// <summary>
        /// ลด CORE% ตอนแก้วิกฤต (Story Guide §4 พลาสมา C — q:-0.2 = CORE% −20) · clamp ≥ 0
        /// ไม่ถอย currentPhase (invariant เดียวกับ Scram — เฟสเดินหน้าอย่างเดียว)
        /// </summary>
        public void ReduceCore(float amount)
        {
            if (amount <= 0f) return;
            if (V63Live) // ★ v6.3 facade
            {
                var r = ReactorController.Instance;
                r.Core = Mathf.Max(0f, r.Core - amount);
                MirrorV63();
                return;
            }
            if (!Current.isUnlocked) return;
            var t = Current;
            t.corePercent = Mathf.Max(0f, t.corePercent - amount);
            Current = t;
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

        /// <summary>ล็อกโหมดเตา (เรียกตอนเข้าเฟส Live) — เปลี่ยนโหมดไม่ได้จนกว่าจะ Planning วันถัดไป (V4 §3)</summary>
        public void LockMode() => _modeLocked = true;
        /// <summary>ปลดล็อกโหมด (เรียกตอนเริ่มวัน/Planning) — วันใหม่ = สิทธิ์เปลี่ยนโหมด 1 ครั้งกลับมา</summary>
        public void UnlockMode() { _modeLocked = false; _modeChangedToday = false; }

        // ★ 2026-07-23 (owner): one mode change per day. GameManager.BeginDay calls UnlockMode
        //   every morning, so that is also where the daily allowance resets.
        private bool _modeChangedToday;

        /// <summary>ตั้งโหมดเร่งเครื่อง — ได้ตั้งแต่ปลดล็อก (Day 11/30%) ช่วง Planning · Live ล็อก (§9)</summary>
        public void SetOverclockMode(int mode)
        {
            if (_modeLocked) return; // ล็อกช่วง Live — รอ Planning วันถัดไป (V4 §3)

            // ★ v6.3 facade (2026-07-22): all 4 modes are real on ReactorController now — the old
            //   collapse to base/boost made the panel's Idle/Overdrive buttons snap back silently.
            //   Selectable from Day 1 (no unlock gate — rule #1).
            if (V63Live)
            {
                int norm = Mathf.Clamp(mode, ModeIdle, ModeOverdrive);
                var r = ReactorController.Instance;
                if (norm == r.Mode) return; // same mode — no cost, no daily allowance spent

                // ★ 2026-07-23 (owner): switching is a priced decision — once per day, and the switch
                //   charges the TARGET mode's deuterium up front (Idle 2 · Normal 6 · Boost 9 · OD 12).
                //   Before this, mode flips were free and unlimited even with an empty tank.
                if (_modeChangedToday)
                {
                    EventManager.Instance.RaiseNotice("เปลี่ยนโหมดเตาได้วันละ 1 ครั้ง — เปลี่ยนได้อีกครั้งพรุ่งนี้");
                    MirrorV63(); // snap the panel back to the real mode
                    return;
                }
                float cost = r.ModeSwitchCost(norm);
                float have = ResourceManager.Instance != null ? ResourceManager.Instance.Current.deuterium : 0f;
                if (have < cost)
                {
                    EventManager.Instance.RaiseNotice(
                        $"ดิวเทอเรียมไม่พอเปลี่ยนโหมด — ต้องใช้ {cost:0} (มี {have:0})");
                    MirrorV63();
                    return;
                }
                if (cost > 0f) EventManager.Instance.RaiseResourceDelta(ResourceType.Deuterium, -cost);
                _modeChangedToday = true;

                r.SetMode(norm);
                EventManager.Instance.RaiseOverclockModeChanged(norm);
                MirrorV63();
                return;
            }

            if (!Current.isUnlocked) return; // ยังไม่ปลดล็อกเตา

            var t = Current;
            t.overclockMode = Mathf.Clamp(mode, 0, 3);
            Current = t;

            _planDeut = FuelNeed[t.overclockMode]; // ตั้งเชื้อเพลิงเริ่มต้นตาม fuelNeed ของโหมดใหม่

            EventManager.Instance.RaiseOverclockModeChanged(t.overclockMode);
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

        /// <summary>
        /// บังคับเตาเดินโหมด Idle N วัน (CORE% ไม่ขึ้น) — รองรับวิกฤต 2·B (GDD §10 ฉบับล่าสุด):
        /// ใช้ฟลักซ์นิวตรอนจากเตาผลิตไอโซโทปการแพทย์ แลกกับจังหวะดัน Q วันนั้น
        /// </summary>
        public void ForceIdle(int days)
        {
            if (days <= 0) return;
            _forcedIdleDays = Mathf.Max(_forcedIdleDays, days);
        }

        // ───────────────────────── Allocation (§2/§5 — ผู้เล่นจัดสรรต่อเทิร์น) ─────────────────────────

        /// <summary>รีเซ็ตการจัดสรรเป็นค่า default ที่พอดี (ทุกเช้า/หลังโหลด) — คงพฤติกรรม auto เดิม</summary>
        private void RecomputeAllocationDefaults()
        {
            _planDeut = FuelNeed[Mathf.Clamp(Current.overclockMode, 0, 3)];
            _planTrit = tritiumFuelNeed;
            _planCoolWater = coolingWaterCap;
            _planCoolEng = TotalEngineers;
        }

        private void HandleAllocationAdjust(ReactorAllocation kind, int delta)
        {
            // ★ v6.3 (2026-07-22): this used to be a bare `return` — every fuel/cooling button on the
            //   Core Tower panel silently did nothing. The two controls that mean something on the live
            //   path now land on the real reactor; the rest genuinely have no v6.3 equivalent.
            if (V63Live)
            {
                var r = ReactorController.Instance;
                switch (kind)
                {
                    case ReactorAllocation.Deuterium:      // ± daily fuel feed (0..fuelNeed)
                        r.AdjustFuelFeed(delta);
                        break;
                    case ReactorAllocation.CoolingWater:   // "หล่อเย็นเพิ่ม" — spend water, drop HEAT now
                        ManualCoolV63(r);
                        break;
                    // Tritium/CoolingEngineer: no per-turn allocation in v6.3 — tritium burns
                    // automatically past the Method B gate; cooling crew is the "cool" job.
                }
                MirrorV63();
                return;
            }
            if (!Current.isUnlocked) return; // เตายังไม่ปลดล็อก (ก่อน Day 11) → จัดสรรไม่ได้
            var rm = ResourceManager.Instance;
            switch (kind)
            {
                case ReactorAllocation.Deuterium:
                    _planDeut = Mathf.Clamp(_planDeut + delta, 0f, rm != null ? rm.Current.deuterium : 9999f);
                    break;
                case ReactorAllocation.Tritium:
                    _planTrit = Mathf.Clamp(_planTrit + delta, 0f, rm != null ? rm.Current.tritium : 9999f);
                    break;
                case ReactorAllocation.CoolingEngineer:
                    _planCoolEng = Mathf.Clamp(_planCoolEng + delta, 0, TotalEngineers);
                    break;
                case ReactorAllocation.CoolingWater:
                    float wcap = Mathf.Min(maxCoolingWater, rm != null ? rm.Current.water : maxCoolingWater);
                    _planCoolWater = Mathf.Clamp(_planCoolWater + delta, 0f, wcap);
                    break;
            }
            EventManager.Instance.RaiseTowerProgressChanged(Current); // ให้ UI รีเฟรช
        }

        // ★ v6.3 manual cooling — the tangible version of "หล่อเย็นเพิ่ม": pay water once, HEAT drops on
        //   the spot. Guards keep it honest: a cold core refuses (no wasted water on nothing), an empty
        //   tank refuses with the reason. Values live in CONFIG.md (rule #2).
        private float _manualCoolWaterToday;

        private void ManualCoolV63(ReactorController r)
        {
            var cfg = GameConfigSO.Instance;
            var rm = ResourceManager.Instance;
            if (r.Heat <= 0f)
            {
                EventManager.Instance.RaiseNotice("เตายังเย็นอยู่ — ยังไม่ต้องหล่อเย็นเพิ่ม");
                return;
            }
            if (rm == null || rm.Current.water < cfg.manualCoolWaterCost)
            {
                EventManager.Instance.RaiseNotice($"น้ำไม่พอ — หล่อเย็นเพิ่มใช้น้ำ {cfg.manualCoolWaterCost:0}");
                return;
            }
            EventManager.Instance.RaiseResourceDelta(ResourceType.Water, -cfg.manualCoolWaterCost);
            _manualCoolWaterToday += cfg.manualCoolWaterCost;
            ReduceHeat(cfg.manualCoolHeatReduce); // v63 branch lands on the real reactor + mirrors
            EventManager.Instance.RaiseNotice($"หล่อเย็นเพิ่ม — ความร้อน −{cfg.manualCoolHeatReduce:0} (น้ำ −{cfg.manualCoolWaterCost:0})");
        }

        /// <summary>กำลังหล่อเย็นตามการจัดสรรปัจจุบัน (preview ให้ UI) — สูตรเดียวกับ AdvanceTurn</summary>
        public float PreviewCooling()
        {
            // v6.3: the reactor's own formula (base + water pool + cool crew + coils + mastery) — the
            // legacy plan numbers below belong to the retired allocation system and read as nonsense.
            if (V63Live) return ReactorController.Instance.PreviewCooling();

            int coolEng = hasPoloidalCoils ? Mathf.Clamp(_planCoolEng, 0, TotalEngineers) : 0;
            int decree = DecreeManager.Instance != null ? DecreeManager.Instance.CoolingLaborBonus : 0;
            return baseCooling + PreviewWaterUsed() / 10f + coolEng * 4f + Mathf.Min(coolingTowerLevel, 3) * 10f + decree;
        }

        /// <summary>น้ำที่จะถูกใช้หล่อเย็นจริงเทิร์นนี้ (clamp กับคลัง) — preview ให้ UI</summary>
        public float PreviewWaterUsed()
        {
            // v6.3: passive cooling reads the pool without consuming — the honest number here is the
            // water actually SPENT today on manual cooling presses.
            if (V63Live) return _manualCoolWaterToday;

            float stock = ResourceManager.Instance != null ? ResourceManager.Instance.Current.water : 0f;
            return Mathf.Clamp(_planCoolWater, 0f, stock);
        }

        /// <summary>Daily fuel feed passthrough for the panel (v6.3 fuel throttle).</summary>
        public float FuelFeedPerDay => V63Live ? ReactorController.Instance.FuelFeedPerDay : PlannedDeuterium;

        // ───────────────────────── Coils (V4 §6 — ส่วนต่อขยาย CORE TOWER) ─────────────────────────

        /// <summary>ติดตั้ง/อัป Toroidal Coils (+1 ระดับหล่อเย็น ×10, cap 3) — จ่ายเหล็ก 50 + พลังงาน 80 (§5)</summary>
        public void UpgradeToroidal()
        {
            // ★ v6.3 facade (§6): coil needs the confinement note · cost = iron only (CONFIG.md table)
            if (V63Live)
            {
                var r = ReactorController.Instance;
                if (!KnowledgeDB.Instance.HasNote("confinement"))
                {
                    EventManager.Instance.RaiseNotice("ต้องวิจัย Note 'confinement' ก่อนติดตั้ง Toroidal Coil");
                    return;
                }
                if (r.ToroidalLv >= GameConfigSO.Instance.toroidalCoolMaxLv) return;
                if (!SpendIronEnergy(toroidalIronCost, 0)) return;
                r.InstallToroidal();
                EventManager.Instance.RaiseCoilsChanged(r.ToroidalLv, r.PoloidalLv > 0);
                MirrorV63();
                return;
            }

            if (coolingTowerLevel >= MaxToroidalLevel) return;
            if (!SpendIronEnergy(toroidalIronCost, toroidalEnergyCost)) return;
            coolingTowerLevel++;
            Debug.Log($"[CoreTower] Toroidal Coils → level {coolingTowerLevel}");
            EventManager.Instance.RaiseTowerProgressChanged(Current);
            EventManager.Instance.RaiseCoilsChanged(coolingTowerLevel, hasPoloidalCoils);
        }

        /// <summary>ติดตั้ง Poloidal Coils (เปิดเทอม engineers×4 + กัน micro-damage) — เหล็ก 60 + พลังงาน 100 ครั้งเดียว (§5)</summary>
        public void InstallPoloidal()
        {
            // ★ v6.3 facade (§6): Poloidal Coil per level (poloidalDamp +6/Lv) — needs confinement note
            if (V63Live)
            {
                var r = ReactorController.Instance;
                if (!KnowledgeDB.Instance.HasNote("confinement"))
                {
                    EventManager.Instance.RaiseNotice("ต้องวิจัย Note 'confinement' ก่อนติดตั้ง Poloidal Coil");
                    return;
                }
                if (r.PoloidalLv >= GameConfigSO.Instance.toroidalCoolMaxLv) return;
                if (!SpendIronEnergy(poloidalIronCost, 0)) return;
                r.InstallPoloidal();
                EventManager.Instance.RaiseCoilsChanged(r.ToroidalLv, r.PoloidalLv > 0);
                MirrorV63();
                return;
            }

            if (hasPoloidalCoils) return;
            if (!SpendIronEnergy(poloidalIronCost, poloidalEnergyCost)) return;
            hasPoloidalCoils = true;
            Debug.Log("[CoreTower] Poloidal Coils installed");
            EventManager.Instance.RaiseTowerProgressChanged(Current);
            EventManager.Instance.RaiseCoilsChanged(coolingTowerLevel, hasPoloidalCoils);
        }

        // จ่ายเหล็ก + พลังงานพร้อมกัน — เช็กพอทั้งคู่ก่อนค่อยหัก (ไม่หักครึ่งเดียว)
        private bool SpendIronEnergy(int iron, int energy)
        {
            var rm = ResourceManager.Instance;
            if (rm == null) return true;          // ไม่มีคลัง (test harness) → อนุญาต
            if (rm.Current.iron < iron || rm.Current.energy < energy) return false;
            if (iron > 0)   EventManager.Instance.RaiseResourceDelta(ResourceType.Iron, -iron);
            if (energy > 0) EventManager.Instance.RaiseResourceDelta(ResourceType.Energy, -energy);
            return true;
        }

        // ───────────────────────── Helpers ─────────────────────────

        private bool HasActiveCoreTowerPart()
        {
            foreach (var kvp in BuildingRegistry.Instance.PlacedBuildings)
            {
                if (ConstructionController.Instance != null &&
                    ConstructionController.Instance.IsUnderConstruction(kvp.Key))
                    continue;

                var data = kvp.Value;
                if (data.isCoreTowerPart &&
                    (data.towerPhaseRequired == 0 || data.towerPhaseRequired == Current.currentPhase))
                    return true;
            }
            return false;
        }

        private void HandleSaveLoaded(SaveData save)
        {
            // ★ v6.3 facade: push saved CORE/HEAT into the live reactor (TowerData is just the mirror).
            //   Legacy pre-unlock saves have corePercent 0 → fall back to startCore (save-compat rule).
            if (V63Live)
            {
                var r = ReactorController.Instance;
                r.Core = save.tower.corePercent > 0f ? save.tower.corePercent : GameConfigSO.Instance.startCore;
                r.Heat = Mathf.Max(0f, save.tower.coreHeat);
                MirrorV63();
                return;
            }

            Current = save.tower;
            _ended = Current.corePercent >= WinPercent || Current.coreHeat >= HeatMeltdown;
            _simmedFrac = 0f;
            RecomputeAllocationDefaults(); // การจัดสรรไม่เซฟ — ตั้ง default ตามสถานะที่โหลด
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ───────────────────────── Debug / Cheat (เฉพาะทดสอบ — คอมไพล์ทิ้งใน release build) ─────────────────────────
        // ใช้โดย DebugCheatPanel เท่านั้น · ตั้งสถานะตรง ๆ ไม่ยุ่งกับสูตรสมดุล

        /// <summary>[DEBUG] ปลดล็อกเตาทันที (ข้ามเงื่อนไข Day 11) — ตั้ง CORE = 30%, HEAT = 0</summary>
        public void DebugUnlockNow()
        {
            if (V63Live) { MirrorV63(); return; } // ★ v6.3: always unlocked — just refresh the mirror
            var t = Current;
            if (!t.isUnlocked)
            {
                t.isUnlocked = true;
                t.corePercent = StartPercent;
                t.currentPhase = 1;
                t.overclockMode = ModeNormal;
            }
            t.heatCap = HeatMeltdown;
            Current = t;
            _ended = false;
            RecomputeAllocationDefaults();
            EventManager.Instance.RaiseOverclockModeChanged(Current.overclockMode);
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

        /// <summary>[DEBUG] ตั้งค่า CORE% ตรง ๆ (ปลดล็อกอัตโนมัติถ้ายัง) — ไม่ trigger ชนะ/แพ้เอง</summary>
        public void DebugSetCore(float percent)
        {
            if (V63Live) // ★ v6.3: write through to the live reactor
            {
                var r = ReactorController.Instance;
                r.Core = Mathf.Clamp(percent, 0f, GameConfigSO.Instance.coreWin);
                // ★ 2026-07-23: raise the REACTOR event, not just the tower mirror — phase-bound UI
                //   (build hotbar's hidden hospital slot, Zone B row) listens to OnReactorStateChanged.
                //   Without this, a debug core jump past 60/80 didn't reveal anything until the next
                //   day started. MirrorV63 re-syncs from this event, so no separate call needed.
                EventManager.Instance.RaiseReactorStateChanged(r.Core, r.Heat);
                return;
            }
            if (!Current.isUnlocked) DebugUnlockNow();
            var t = Current;
            t.corePercent = Mathf.Clamp(percent, 0f, WinPercent);
            t.currentPhase = Mathf.Max(t.currentPhase, PhaseFor(t.corePercent));
            Current = t;
            _ended = false;
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

        /// <summary>[DEBUG] ตั้งค่า HEAT ตรง ๆ — ไม่ trigger meltdown เอง (ตั้ง ≥100 แล้วข้ามวันเพื่อทดสอบ meltdown)</summary>
        public void DebugSetHeat(float heat)
        {
            if (V63Live) // ★ v6.3: write through to the live reactor
            {
                var r = ReactorController.Instance;
                r.Heat = Mathf.Max(0f, heat);
                // Same reason as DebugSetCore: heat listeners (crisis card immediate trigger,
                // panel gauges) ride OnReactorStateChanged — the mirror alone reaches neither.
                EventManager.Instance.RaiseReactorStateChanged(r.Core, r.Heat);
                return;
            }
            if (!Current.isUnlocked) DebugUnlockNow();
            var t = Current;
            t.coreHeat = Mathf.Max(0f, heat);
            Current = t;
            _ended = false;
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }
#endif
    }
}
