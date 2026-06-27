using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// CORE TOWER v2.1 (GDD §07★) — วัดด้วย CORE% (0→100) + ระบบความร้อน HEAT
    /// ก้าวหน้า "ต่อเทิร์น" = ต่อวัน (ฟัง OnDayEnded) ผู้เล่นเลือกโหมดเร่งเครื่องใน Phase 2–3
    ///
    /// สูตร §3.4 (simplified — input ที่ยังไม่มีระบบ ใช้ proxy):
    ///   fuel = Energy (proxy ของ Deuterium/Tritium ที่ ResourceData ยังไม่มี)
    ///   cooling = 15 + waterUsed/10  (engineers×4 / towerLevel×6 / decrees ยังไม่มี = 0)
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

        // ===== Formula constants (§3.4) =====
        [Header("Balance (rough — ปรับภายหลัง)")]
        public float baseCoreGain = 3f;       // dCORE = 3 × mult × fuelEff
        public float fuelNeededPerTurn = 50f; // Energy ที่ต้องการต่อเทิร์น (proxy fuel)
        public float baseCooling = 15f;
        public float coolingWaterCap = 100f;  // น้ำสูงสุดที่ใช้หล่อเย็นต่อเทิร์น
        public float stormHeat = 8f;          // Phase 3 เท่านั้น
        public float startHeatCap = 100f;

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
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayStarted             -= HandleDayStarted;
            EventManager.Instance.OnDayEnded               -= HandleDayEnded;
            EventManager.Instance.OnSaveLoaded             -= HandleSaveLoaded;
            EventManager.Instance.OnOverclockModeRequested -= HandleOverclockModeRequested;
            EventManager.Instance.OnScramRequested         -= HandleScramRequested;
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
            t.heatCap = startHeatCap;
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

            AdvanceTurn();
        }

        private void AdvanceTurn()
        {
            var t = Current;

            if (t.scramCooldown > 0) t.scramCooldown--; // ลด cooldown SCRAM ทุกเทิร์น

            // Phase 1 ล็อกเร่ง — ใช้ Normal เสมอ ; Phase 2–3 ใช้โหมดที่ผู้เล่นเลือก
            int mode = (t.currentPhase <= 1) ? ModeNormal : Mathf.Clamp(t.overclockMode, 0, 3);

            // ----- fuel (proxy = Energy) -----
            float energy = ResourceManager.Instance != null ? ResourceManager.Instance.Current.energy : 0f;
            float fuelEff = fuelNeededPerTurn > 0f ? Mathf.Min(1f, energy / fuelNeededPerTurn) : 1f;
            float fuelUsed = Mathf.Min(energy, fuelNeededPerTurn);
            if (fuelUsed > 0f)
                EventManager.Instance.RaiseResourceDelta(ResourceType.Energy, -fuelUsed);

            // ----- cooling (15 + water/10 ; engineers/towerLevel/decrees = future) -----
            float water = ResourceManager.Instance != null ? ResourceManager.Instance.Current.water : 0f;
            float waterUsed = Mathf.Min(water, coolingWaterCap);
            if (waterUsed > 0f)
                EventManager.Instance.RaiseResourceDelta(ResourceType.Water, -waterUsed);
            float cooling = baseCooling + waterUsed / 10f;

            // ----- CORE% -----
            float dCore = baseCoreGain * ModeMultiplier[mode] * fuelEff;
            t.corePercent = Mathf.Min(WinPercent, t.corePercent + dCore);

            // ----- HEAT -----
            float storm = (t.currentPhase >= 3) ? stormHeat : 0f;
            float dHeat = ModeHeat[mode] + storm - cooling;
            t.coreHeat = Mathf.Max(0f, t.coreHeat + dHeat);

            // ----- RedZone micro-damage (>= 80% ของ heatCap แต่ยังไม่ meltdown) -----
            if (t.coreHeat >= t.heatCap * 0.8f && t.coreHeat < t.heatCap)
            {
                float chance = Random.Range(0.15f, 0.30f);
                if (Random.value < chance)
                    t.heatCap = Mathf.Max(1f, t.heatCap - 1f);
            }

            Current = t;

            // ----- thresholds -----
            if (t.coreHeat >= t.heatCap)
            {
                _ended = true;
                Debug.Log("[CoreTower] MELTDOWN — coreHeat ทะลุ heatCap");
                EventManager.Instance.RaiseTowerProgressChanged(Current);
                EventManager.Instance.RaiseGameOver(GameEndType.TowerDestroyed);
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

        /// <summary>ตั้งโหมดเร่งเครื่อง — ได้เฉพาะ Phase 2–3 (Phase 1 ล็อก)</summary>
        public void SetOverclockMode(int mode)
        {
            if (!Current.isUnlocked || Current.currentPhase <= 1) return; // Phase 1 ล็อกเร่ง

            var t = Current;
            t.overclockMode = Mathf.Clamp(mode, 0, 3);
            Current = t;

            EventManager.Instance.RaiseOverclockModeChanged(t.overclockMode);
            EventManager.Instance.RaiseTowerProgressChanged(Current);
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
            _ended = Current.corePercent >= WinPercent || Current.coreHeat >= Current.heatCap;
            EventManager.Instance.RaiseTowerProgressChanged(Current);
        }
    }
}
