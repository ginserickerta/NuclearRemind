using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// CORE TOWER (V4 §8) — วัดด้วย CORE% (0→100) + ระบบความร้อน HEAT · ก้าวหน้าต่อวัน (OnDayEnded)
    ///
    /// สูตร §8 (เฟส 2):
    ///   fuel = Deuterium (Phase 2) / Tritium (Phase 3) · Phase 1 ประกอบ (ไม่กินเชื้อเพลิงฟิวชัน)
    ///   fuelEff = min(1, stock/FuelNeed[mode]) + KnowBonus (Expert +0.10) · cap 1.10
    ///   cooling = 15 + waterUsed/10 + coolingEngineers×4 + min(toroidalLv,3)×10
    ///   HEAT: warn 80 (micro-damage CORE%−2 ถ้าไม่มี Poloidal) · meltdown 100 · พายุ +12 (Day 25–30)
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
        [Header("Balance (V4 §18 — จูนจริงเฟส 8)")]
        public float baseCoreGain = 3f;        // dCORE = 3 × mult × fuelEff
        public float baseCooling = 15f;
        public float coolingWaterCap = 100f;   // น้ำสูงสุดที่ใช้หล่อเย็นต่อเทิร์น
        public float stormHeat = 12f;          // พายุ Day 25–30 (V4 §18)
        public int   coolingTowerLevel = 0;    // Toroidal Coils (มีผล ×10, cap 3)
        public bool  hasPoloidalCoils = false; // Poloidal Coils (เปิด engineers×4 + กัน micro-damage)

        [Header("Coils cost (V4 §6 — เฟส 6)")]
        public int toroidalIronCost = 50;  // ต้นทุน Toroidal Coils ต่อระดับ
        public int poloidalIronCost = 60;  // ต้นทุน Poloidal Coils (ครั้งเดียว)
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

        /// <summary>Q Factor = CORE% / 100 (§8: CORE% 100 = Q 1.0 = Breakeven) — ใช้ตัดสิน ending (เฟส 7)</summary>
        public float Q => Current.corePercent / 100f;

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
            EventManager.Instance.OnSaveLoaded             += HandleSaveLoaded;
            EventManager.Instance.OnOverclockModeRequested += HandleOverclockModeRequested;
            EventManager.Instance.OnScramRequested         += HandleScramRequested;
            EventManager.Instance.OnUpgradeToroidalRequested += UpgradeToroidal;
            EventManager.Instance.OnInstallPoloidalRequested += InstallPoloidal;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayStarted             -= HandleDayStarted;
            EventManager.Instance.OnDayEnded               -= HandleDayEnded;
            EventManager.Instance.OnSaveLoaded             -= HandleSaveLoaded;
            EventManager.Instance.OnOverclockModeRequested -= HandleOverclockModeRequested;
            EventManager.Instance.OnScramRequested         -= HandleScramRequested;
            EventManager.Instance.OnUpgradeToroidalRequested -= UpgradeToroidal;
            EventManager.Instance.OnInstallPoloidalRequested -= InstallPoloidal;
        }

        private void Start()
        {
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

        // ───────────────────────── Unlock (Day 11) ─────────────────────────

        private void HandleDayStarted(int day, bool timed)
        {
            if (Current.isUnlocked || day < UnlockDay) return;

            var t = Current;
            t.isUnlocked = true;
            t.corePercent = StartPercent;
            t.currentPhase = 1;
            t.heatCap = HeatMeltdown; // heatCap = 100 คงที่ (V4: threshold ตายตัว ไม่เสื่อม)
            t.coreHeat = 0f;
            t.overclockMode = ModeNormal;
            Current = t;

            Debug.Log($"[CoreTower] ปลดล็อก Day {day} — CORE {StartPercent}% (Phase 1 Cold Assembly)");
            EventManager.Instance.RaiseOverclockModeChanged(t.overclockMode);
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

        // ───────────────────────── Turn (per day) ─────────────────────────

        private void HandleDayEnded(int day)
        {
            if (_ended || !Current.isUnlocked) return;
            if (!HasActiveCoreTowerPart()) return; // ต้องสร้าง CORE TOWER ก่อนถึงจะเดินเครื่อง

            AdvanceTurn(day);
        }

        private void AdvanceTurn(int day)
        {
            var t = Current;

            if (t.scramCooldown > 0) t.scramCooldown--; // ลด cooldown SCRAM ทุกเทิร์น

            // โหมด: วิกฤต 2·B บังคับ Idle N วัน > Phase 1 ล็อก Normal > โหมดที่ผู้เล่นเลือก (Phase 2–3)
            int mode;
            if (_forcedIdleDays > 0) { mode = ModeIdle; _forcedIdleDays--; }
            else if (t.currentPhase <= 1) mode = ModeNormal;
            else mode = Mathf.Clamp(t.overclockMode, 0, 3);

            // ----- เชื้อเพลิงตามเฟส (§8): P1 ประกอบ (ไม่กิน) · P2 Deuterium · P3 Tritium -----
            float knowBonus = ResourceManager.Instance != null ? ResourceManager.Instance.KnowBonus : 0f;
            float fuelEff;
            if (t.currentPhase <= 1)
            {
                fuelEff = 1f + knowBonus; // ประกอบด้วยเหล็ก+วิศวกร ไม่ติดเชื้อเพลิงฟิวชัน
            }
            else
            {
                ResourceType fuelType = (t.currentPhase >= 3) ? ResourceType.Tritium : ResourceType.Deuterium;
                float stock = 0f;
                if (ResourceManager.Instance != null)
                    stock = (fuelType == ResourceType.Tritium)
                        ? ResourceManager.Instance.Current.tritium
                        : ResourceManager.Instance.Current.deuterium;

                float need = FuelNeed[mode];
                fuelEff = (need > 0f ? Mathf.Min(1f, stock / need) : 1f) + knowBonus;

                float fuelUsed = Mathf.Min(stock, need);
                if (fuelUsed > 0f)
                    EventManager.Instance.RaiseResourceDelta(fuelType, -fuelUsed);
            }
            fuelEff = Mathf.Min(fuelEff, 1.10f); // knowBonus ดันเกิน 1 ได้เล็กน้อย (§8)

            // ----- หล่อเย็น (§8): 15 + water/10 + engineers×4 + min(toroidalLv,3)×10 -----
            float water = ResourceManager.Instance != null ? ResourceManager.Instance.Current.water : 0f;
            float waterUsed = Mathf.Min(water, coolingWaterCap);
            if (waterUsed > 0f)
                EventManager.Instance.RaiseResourceDelta(ResourceType.Water, -waterUsed);

            // coolingEngineers นับเฉพาะเมื่อมี Poloidal Coils (§8) — ดึงจาก PopulationManager จริง (เฟส 3 ปิด stub แล้ว)
            int coolEng = (hasPoloidalCoils && PopulationManager.Instance != null)
                ? PopulationManager.Instance.AssignedCoolingEngineers : 0;
            int decreeCooling = DecreeManager.Instance != null ? DecreeManager.Instance.CoolingLaborBonus : 0;
            float cooling = baseCooling
                          + waterUsed / 10f
                          + coolEng * 4f
                          + Mathf.Min(coolingTowerLevel, 3) * 10f
                          + decreeCooling; // แรงงานหล่อเย็นจากประกาศฉุกเฉิน (§11)

            // ----- CORE% -----
            float dCore = baseCoreGain * ModeMultiplier[mode] * fuelEff;
            t.corePercent = Mathf.Min(WinPercent, t.corePercent + dCore);

            // ----- HEAT (พายุ +12 เฉพาะ Day 25–30) -----
            float storm = (day >= StormStartDay) ? stormHeat : 0f;
            float dHeat = ModeHeat[mode] + storm - cooling;
            t.coreHeat = Mathf.Max(0f, t.coreHeat + dHeat);

            // ----- โซนเตือน 80–99: micro-damage CORE%−2 ถ้าไม่มี Poloidal (§8) -----
            if (t.coreHeat >= HeatWarnZone && t.coreHeat < HeatMeltdown && !hasPoloidalCoils)
                t.corePercent = Mathf.Max(0f, t.corePercent - MicroDamageCore);

            Current = t;

            // ----- meltdown -----
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
            if (amount <= 0f || !Current.isUnlocked) return;
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
            if (amount <= 0f || !Current.isUnlocked) return;
            var t = Current;
            t.corePercent = Mathf.Max(0f, t.corePercent - amount);
            Current = t;
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

        /// <summary>ล็อกโหมดเตา (เรียกตอนเข้าเฟส Live) — เปลี่ยนโหมดไม่ได้จนกว่าจะ Planning วันถัดไป (V4 §3)</summary>
        public void LockMode() => _modeLocked = true;
        /// <summary>ปลดล็อกโหมด (เรียกตอนเริ่มวัน/Planning)</summary>
        public void UnlockMode() => _modeLocked = false;

        /// <summary>ตั้งโหมดเร่งเครื่อง — ได้เฉพาะ Phase 2–3 ช่วง Planning (Phase 1 / Live ล็อก)</summary>
        public void SetOverclockMode(int mode)
        {
            if (_modeLocked) return; // ล็อกช่วง Live — รอ Planning วันถัดไป (V4 §3)
            if (!Current.isUnlocked || Current.currentPhase <= 1) return; // Phase 1 ล็อกเร่ง

            var t = Current;
            t.overclockMode = Mathf.Clamp(mode, 0, 3);
            Current = t;

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

        // ───────────────────────── Coils (V4 §6 — ส่วนต่อขยาย CORE TOWER) ─────────────────────────

        /// <summary>ติดตั้ง/อัป Toroidal Coils (+1 ระดับหล่อเย็น ×10, cap 3) — จ่ายแร่เหล็ก</summary>
        public void UpgradeToroidal()
        {
            if (coolingTowerLevel >= MaxToroidalLevel) return;
            if (!SpendIron(toroidalIronCost)) return;
            coolingTowerLevel++;
            Debug.Log($"[CoreTower] Toroidal Coils → level {coolingTowerLevel}");
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

        /// <summary>ติดตั้ง Poloidal Coils (เปิดเทอม engineers×4 + กัน micro-damage) — ครั้งเดียว</summary>
        public void InstallPoloidal()
        {
            if (hasPoloidalCoils) return;
            if (!SpendIron(poloidalIronCost)) return;
            hasPoloidalCoils = true;
            Debug.Log("[CoreTower] Poloidal Coils installed");
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }

        private bool SpendIron(int cost)
        {
            var rm = ResourceManager.Instance;
            if (rm == null) return true;          // ไม่มีคลัง (test harness) → อนุญาต
            if (rm.Current.iron < cost) return false;
            EventManager.Instance.RaiseResourceDelta(ResourceType.Iron, -cost);
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
            Current = save.tower;
            _ended = Current.corePercent >= WinPercent || Current.coreHeat >= HeatMeltdown;
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }
    }
}
