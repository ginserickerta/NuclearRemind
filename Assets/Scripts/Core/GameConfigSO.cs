using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Single source of truth for every gameplay number (GDD v6.3 rule #2 — no hardcoding).
    /// Every default below mirrors docs/CONFIG.md; the asset is the runtime source.
    /// Fields marked [LOCK] must never be changed without re-running nrm_sim.py (CONFIG.md 🔒).
    /// Sprint 1 scope: WORKER + HOPE + RADIATION + ECONOMY + Day-1 start values.
    /// Later sprints append REACTOR/STORM/CARDS sections to this same asset.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "NRM/Game Config")]
    public class GameConfigSO : ScriptableObject
    {
        // ─────────────────────────────────────────
        //  [LOCK] WORKER (CONFIG.md 🔒 WORKER)
        // ─────────────────────────────────────────
        [Header("Worker — Shift (LOCK)")]
        [Tooltip("fatigue >= 70 → rests on their own (§17.5)")]
        public float restThreshold = 70f;
        [Tooltip("fatigue <= 25 → returns to last job (§17.5)")]
        public float restReturn = 25f;
        [Tooltip("[LOCK] bug #7: lab worker with labBusy && fatigue < 92 is never pulled to rest")]
        public float labDeadlockGuard = 92f;

        [Header("Worker — Fatigue (LOCK)")]
        public float fatigueWork = 12f;            // per day while working
        public float fatigueRest = -30f;           // per day while resting (no Barracks)
        public float fatigueRestBarracks = -45f;   // per day while resting with Barracks
        public float fatigueBoostCool = 6f;        // +6 when boosting && job == "cool"

        [Header("Worker — Hunger (LOCK)")]
        [Tooltip("[LOCK] bug #12: 30 not 20 — at 20 the S7 trigger never fires")]
        public float hungerPerDay = 30f;
        public float hungryThreshold = 55f;        // hunger > 55 → Hungry (eff ×0.6)
        public float foodPerWorkerPerDay = 1f;     // 1 food/person/day, no exceptions

        [Header("Worker — Efficiency (LOCK)")]
        public float effFatigueHard = 85f;         // fatigue > 85 → 0%
        public float effRadHard = 50f;             // radiation > 50 → 0%
        public float effFatigueSoft = 60f;         // fatigue > 60 → ×0.7
        public float effFatigueSoftMult = 0.7f;
        public float effHungerSoftMult = 0.6f;

        [Header("Worker — Status label (NOT efficiency)")]
        [Tooltip("fatigue > นี้ → สถานะ \"หมดแรง\" · แยกจาก effFatigueHard โดยตั้งใจ\n\n" +
                 "เดิมสองอย่างนี้ใช้เลข 85 ตัวเดียวกัน แต่ ShiftSystem ดึงคนไปพักที่ 70 คนงานจึงมียอด" +
                 "ความล้าสูงสุดแค่ 72 และไม่มีวันแตะ 85 → สถานะ \"หมดแรง\" ไม่เคยถูกติดให้ใคร และการ์ด" +
                 "overwork (exhaustedWorkers >= 3, CARDS.md) ก็ไม่มีทางเกิด\n\n" +
                 "ต้องอยู่ระหว่าง effFatigueSoft (60) กับยอดจริง 72 · ในรอบปกติความล้าเป็นพหุคูณของ 12" +
                 "จึงไม่เคยตกในช่วง 61-71 เลย เลขไหนในช่วงนี้ให้ผลเท่ากัน — เลือก 68 เผื่อเส้นทางที่ไม่ปกติ" +
                 "(คุมหล่อเย็นตอน boost ได้ +18/วัน อาจตกที่ 66/69) ให้ติดป้ายเฉพาะตอนใกล้ถูกดึงออกจริง ๆ\n\n" +
                 "★ ไม่กระทบผลผลิต: GetEfficiency ยังใช้ effFatigueHard = 85 เหมือนเดิม และคนงานไม่เคย" +
                 "ผลิตที่ความล้าเกิน 60 อยู่แล้ว (ผลิตเกิดก่อนความล้าเพิ่ม + ระบบพักดึงออกก่อนเสมอ)")]
        public float exhaustedThreshold = 68f;

        [Tooltip("fatigue > นี้ → สถานะ \"ล้า\" · แยกจาก effFatigueSoft (60) ด้วยเหตุผลเดียวกัน\n\n" +
                 "ความล้าเดินทีละ 12 (12→24→36→48→60→72) ถ้าใช้ 60 ตั้งชื่อ ช่วง \"ล้า\" จะเหลือแค่ 61-68 " +
                 "ซึ่งไม่มีค่าไหนตกลงไปเลย → ผู้เล่นเห็น \"ปกติ\" ติดกัน 5 วันแล้วจู่ ๆ เป็น \"หมดแรง\" " +
                 "ไม่มีสัญญาณเตือนล่วงหน้า ซึ่งขัดกับหน้าที่ของ Worker Dashboard\n\n" +
                 "45 ทำให้ 48 กับ 60 ติดป้าย \"ล้า\" → เตือนล่วงหน้า 2 วันก่อนถูกดึงไปพัก และ 42 " +
                 "(วันแรกหลังพัก) ยังต่ำกว่าเส้น จึงกลับเป็น \"ปกติ\" อย่างมีความหมาย ไม่ใช่ป้ายเดียว" +
                 "ครอบ 0-60 ทั้งหมด\n\n" +
                 "★ ไม่กระทบผลผลิต: GetEfficiency ยังใช้ effFatigueSoft = 60 (×0.7) เหมือนเดิม")]
        public float tiredThreshold = 45f;

        [Header("Worker — Re-staff floors (LOCK — bug #9)")]
        public int restaffFloorFarm = 2;
        public int restaffFloorWater = 2;
        public int restaffFloorMine = 1;

        // ─────────────────────────────────────────
        //  [LOCK] RADIATION (CONFIG.md 🔒 RADIATION)
        // ─────────────────────────────────────────
        [Header("Radiation (LOCK)")]
        public float radZoneB = 15f;               // sick in ~3.5 days · suit ~8 · +Mastery ~10
        public float radMine = 4f;
        public float radCore = 6f;                 // job "cool" works at the core tower
        public float radHeatLeak = 5f;             // heat > 85 → +5 to everyone
        public float radHeatLeakThreshold = 85f;
        public float radStorm = 3f;                // storm active → +3 to everyone
        public float radSuitMult = 0.4f;
        public float radAlaraMult = 0.8f;          // Mastery("nuclear_medicine")
        public float sickThreshold = 50f;          // radiation > 50 → Sick (eff 0)
        public float dyingThreshold = 80f;         // radiation > 80 → Dying
        public float deathRad = 80f;               // radiation > 80 → 20%/day death roll
        public float deathChance = 0.2f;
        public int suitCostLabMat = 30;            // Rad Suit craft cost · target 5 suits
        public int suitTargetCount = 5;

        [Header("Med Bay (Sprint 3 wires the building — rates live here)")]
        public int medBayCapacity = 4;             // patients healed per day
        public float medBayHeal = 25f;             // radiation −25/day (−35 with Mastery)
        public float medBayHealMastery = 35f;

        // ─────────────────────────────────────────
        //  [LOCK] HOPE (CONFIG.md 🔒 HOPE)
        // ─────────────────────────────────────────
        [Header("Hope — Core (LOCK)")]
        public float hopeStart = 70f;
        public float hopeMax = 100f;
        public float hopeMin = 0f;                 // 0 = Game Over
        public float coreProgressBonus = 1.6f;     // per CORE% gained

        [Header("Hope — Threshold events (LOCK · hysteresis reset at threshold + 8)")]
        public float hopeBarkLow = 60f;
        public float hopeStrike = 40f;             // 10% stop working 2 days
        public float hopeExodus = 25f;             // population −15% permanent
        public float hysteresisMargin = 8f;
        public float strikeRatio = 0.10f;
        public int strikeDays = 2;
        public float exodusRatio = 0.15f;

        [Header("Hope — Source values (LOCK · per CONFIG.md table)")]
        public float hopeWorkerExhausted = -1f;    // per person per day
        public float hopeWorkerHungry = -2f;
        public float hopeWorkerSick = -3f;
        public float hopeWorkerDying = -5f;
        public float hopeWorkerDeath = -8f;        // once per death
        public float hopeFoodSurplus = 2f;         // food > pop × foodSurplusPopMult (was +3 — see CONFIG.md 2026-07-21)
        public float hopeFoodEmpty = -6f;          // food == 0
        public float hopeWaterShortage = -4f;      // water < pop
        public float hopePowerBlackout = -5f;      // via flag, never power < 0 (bug #17)
        public float hopeHeatCritical = -3f;       // HEAT > 90
        public float hopeStormActive = -1f;
        public float hopeCoreStalled = -4f;        // no CORE gain 3 days straight
        public int hopeCoreStalledDays = 3;        // how many consecutive dead days count as stalled
        public float hopeCoreProgress = 1.6f;      // per CORE% gained
        public float hopeResearchComplete = 6f;    // per note
        public float hopeMemorialVisited = 2f;     // first click only
        public float hopeAlaraCompliant = 2f;      // Zone B fully suited
        public int quizRevealDelayDays = 1;        // days between USING knowledge and the quiz surfacing (QUIZZES.md pacing)
        public float hopeScram = -3f;
        public float foodSurplusPopMult = 6f;      // surplus condition: food > pop × 6 (was ×3 — CONFIG.md 2026-07-21)

        // ─────────────────────────────────────────
        //  [LOCK] ECONOMY / day (CONFIG.md 🔒 ECONOMY)
        // ─────────────────────────────────────────
        [Header("Economy per day (LOCK — always × Σ GetEfficiency, never headcount)")]
        public float foodPerFarmWorker = 9f;       // [LOCK] was 11, cut 18% in balance pass
        public float waterPerWaterWorker = 30f;
        public float waterPerPopPerDay = 1f;
        public float ironPerMineWorker = 14f;
        public float powerPerPowerWorker = 45f;
        public float labMatPerDay = 5f;
        public float drawBase = 20f;               // + extractor 15 / medbay 30 / zoneB 25 / co60 20
        public float drawExtractor = 15f;
        public float drawMedBay = 30f;
        public float drawZoneB = 25f;
        public float drawCo60 = 20f;
        public float powerCap = 400f;              // [LOCK] floor 0 (bug #17)
        public float waterCap = 200f;              // [LOCK] floor 0 (bug #18)
        // spoil = spoilBase + (avgRad/100) × spoilRadCoeff · ×0.3 with Co-60 (CONFIG.md ECONOMY)
        // ★ Spoilage only runs AFTER the "เสบียงเน่า" crisis card fires (CrisisEffectManager.SpoilageActive).
        //   CONFIG.md models it as always-on, but that is an unverified balance change — nrm_sim.py is not
        //   in the repo, so rule #3 cannot be satisfied. Card-gated keeps the numbers honest either way.
        public float spoilBase = 0.05f;
        public float spoilRadCoeff = 0.15f;
        public float spoilHighMult = 2f;           // D01/D02 fire above spoilHighMult × spoilBase

        // ─────────────────────────────────────────
        //  [LOCK] RESEARCH (CONFIG.md 🔒 RESEARCH)
        // ─────────────────────────────────────────
        [Header("Research (LOCK)")]
        public int researcherSlotsLv1 = 3;         // Lv2→5, Lv3→8
        public int researcherSlotsLv2 = 5;
        public int researcherSlotsLv3 = 8;
        public int queueCapacityLv1 = 1;           // Lv2→2, Lv3→3
        public int queueCapacityLv2 = 2;
        public int queueCapacityLv3 = 3;
        public int repairIron = 80;                // ★ lab starts ruined — repair: Iron 80 · 2 days · 2 workers
        public int repairDays = 2;
        public int repairWorkers = 2;
        [Tooltip("[LOCK] staffRatio < 0.5 → progress does not advance")]
        public float staffRatioMin = 0.5f;
        // [LOCK] bug #2: research cost is paid ONCE at start — never per day

        // ─────────────────────────────────────────
        //  [LOCK] SOFT TRIGGER (CONFIG.md 🔒 — bound to core%, never day)
        // ─────────────────────────────────────────
        [Header("Soft Trigger (LOCK — heat > base − (core/100)×slope)")]
        public float softHeatBase = 35f;           // magnetic_theory: 35 → 25
        public float softHeatSlope = 10f;
        public float softRadBase = 25f;            // radiation_biology: 25 → 19
        public float softRadSlope = 6f;
        public float softFoodBase = 45f;           // food_preservation (with avgRad)
        public float softFoodSlope = 8f;
        public float softRad2Base = 8f;
        public float softRad2Slope = 4f;
        public float softLithiumCore = 45f;        // core >= 45 → lithium_breeding
        public int softHungryCount = 3;            // ★ S7 → food_logistics
        public int softExhaustedCount = 3;         // ★ S8 → shift_management

        // ─────────────────────────────────────────
        //  [LOCK] MASTERY (CONFIG.md 🔒 — quiz bonuses, GDD §21)
        //  Applied magnitudes ONLY here (rule #2). MasteryBonusSO assets carry display copies.
        // ─────────────────────────────────────────
        [Header("Mastery — quiz bonuses (LOCK · CONFIG.md)")]
        public float fuelEffBonus = 0.08f;             // q_deuterium / q_dt_fuel: fuelEfficiency += 0.08 each
        public float coolingMasteryBonus = 5f;         // q_plasma: cooling + 5
        public float poloidalMasteryMult = 1.15f;      // q_magnetic_pair: poloidalDamp × 1.15
        public float farmYieldMasteryMult = 1.15f;     // q_mutation: yield + 15%
        public float spoilMasteryMult = 0.5f;          // q_food_irradiation: spoil − 50%
        public float boostHeatMasteryMult = 0.9f;      // q_fusion: boost heat cost − 10%
        [Tooltip("[LOCK] ★ q_tritium_breeding OFF — Zone B 3.0/day = short 6/day = lose 100%")]
        public float zoneBTritiumBase = 3.0f;
        [Tooltip("[LOCK] ★ q_tritium_breeding ON — Zone B 8.0/day = enough = win")]
        public float zoneBTritiumMastery = 8.0f;
        // radAlaraMult (0.8) and medBayHeal/medBayHealMastery (25/35) live in the RADIATION/Med Bay
        // sections above — reused by MasteryRegistry, not duplicated here.

        // ─────────────────────────────────────────
        //  [LOCK] REACTOR (CONFIG.md 🔒 REACTOR — GDD §26)
        // ─────────────────────────────────────────
        [Header("Reactor — CORE / HEAT (LOCK · CONFIG.md)")]
        public float coreWin = 100f;               // ★ check every day, never wait for D30
        public float coreGainBase = 1.05f;         // idle/normal gain
        [Tooltip("[LOCK] ★ fragile — below 2.6 loses; 2.3 = 100% loss every playstyle")]
        public float boostCoreGain = 3.0f;
        public float modeHeatPerDay = 9f;          // base heat/day
        public float boostHeatPerDay = 14f;        // extra heat/day while boosting
        public float heatMeltdown = 100f;          // ≥ 100 → Game Over
        public float heatWarn = 90f;               // UI warn + SCRAM unlock
        // ★ 2026-07-22 — Knowledge scales CORE (Q) gain: gain × (1 + knowledge/100 × this).
        //   Additive-only by design (multiplier can never drop below 1.0), so the fragile 🔒
        //   boost_core_gain = 3.0 floor is untouched and the sim-proven win path cannot regress.
        //   K20 start → +4% · K100 → +20% — supersedes the v4.1 Expert-only KnowBonus that was
        //   never wired into the v6.3 reactor.
        public float knowledgeQMaxBonus = 0.20f;
        // ★ 2026-07-23 (owner) — completing a research note grants Knowledge. Sized above the +2
        //   Codex read: 8 notes × 5 = 40 total, so research + codex + lab output reaches the
        //   Expert tier (80) only with broad engagement, never automatically.
        public float knowledgePerResearchNote = 5f;
        // ★ 2026-07-22 — ปุ่ม "หล่อเย็นเพิ่ม" (v6.3): จ่ายน้ำครั้งเดียว → ลด HEAT ทันที เห็นผลตรงหน้า
        //   (เดิมปุ่มวิ่งเข้า allocation path ที่ v6.3 ปิดไว้ = กดแล้วไม่มีอะไรเกิดเลย)
        public float manualCoolWaterCost = 20f;
        public float manualCoolHeatReduce = 8f;
        // ★ 2026-07-22 — Idle/Overdrive are REAL modes again (the v6.3 cutover collapsed the panel's
        //   4 mode buttons to base/boost, so Idle and Overdrive silently snapped back to Normal/Boost).
        //   Idle: gain 0, mode heat 0, burns no fuel/tritium — a deliberate cooldown day (stall Hope
        //   penalty still applies — no free choices). Overdrive: +40% over Boost, but heat 9+32=41/day
        //   (vs Boost 23) only survivable with researched cooling coils, and tritium 13/day outruns
        //   even mastery Zone B (8/day) so it burns stock. Additive-only: Normal/Boost paths and the
        //   🔒 boost_core_gain = 3.0 floor are byte-identical to the sim-proven build.
        public float odCoreGain = 4.2f;            // Overdrive CORE gain/day (Boost 3.0 × 1.4)
        public float odHeatPerDay = 32f;           // extra heat/day while overdriving (Boost = 14)
        public float odTritiumCost = 13f;          // reactor draw while overdriving (Boost = 9)

        [Header("Reactor — cooling (LOCK · bug #1: capped, was uncapped water/10)")]
        public float coolingBase = 6f;
        public float coolingWaterDiv = 12f;        // + min(water/12, 7)
        public float coolingWaterCap = 7f;
        public float coolPerWorker = 3f;           // + coolWorkers × 3
        public float coolPerToroidalLv = 9f;       // + min(toroidalLv, 3) × 9
        public int toroidalCoolMaxLv = 3;
        public float poloidalDampPerLv = 6f;       // poloidalDamp = poloidalLv × 6 (×1.15 mastery)

        [Header("Reactor — fuel / Method B gate (LOCK)")]
        public float fuelNeed = 6f;                // NORMAL fuelEfficiency = min(1, fuel/6) — sim baseline, unchanged
        // ★ 2026-07-23 (owner): deuterium demand scales with mode — push harder, drink more.
        //   Idle 0 · Normal 6 (= fuelNeed) · Boost 9 · Overdrive 12. One maxed extractor (8/day)
        //   no longer fully feeds Boost (fe 8/9 ≈ 0.89) — building more extraction is the intended
        //   answer, mirroring how Boost's tritium draw (9) outruns Zone B base production.
        public float boostFuelNeed = 9f;           // Boost deuterium demand/day
        public float odFuelNeed = 12f;             // Overdrive deuterium demand/day
        // ★ 2026-07-23 (owner): switching modes is a real decision now — one change per day, and the
        //   switch itself charges the TARGET mode's deuterium up front (Idle 2 · Normal 6 · Boost 9 ·
        //   Overdrive 12). Idle has no daily burn, so its whole price is this spin-down cost.
        public float idleFuelNeed = 2f;            // Idle switch cost (no daily burn in Idle)
        public float methodBCoreGate = 80f;        // core ≥ 80 && tritium < 5 → gain = 0
        public float methodBTritiumMin = 5f;
        public float tritiumSoftFloor = 6f;        // tritium < 6 → gain ×= tritium/6

        [Header("SCRAM — emergency brake (LOCK · CONFIG.md)")]
        public float scramHeatThreshold = 90f;     // usable when HEAT ≥ 90
        public float scramHeatReduce = 40f;
        public float scramCorePenalty = 10f;
        public float scramWaterCost = 30f;
        public int scramCooldownDays = 3;
        public float scramHopePenalty = 3f;

        // ─────────────────────────────────────────
        //  [LOCK] STORM (CONFIG.md 🔒 STORM — GDD §23)
        // ─────────────────────────────────────────
        [Header("Storm (LOCK · CONFIG.md)")]
        public float stormBaseRise = 0.9f;
        public float stormIgnitionBonus = 2.0f;    // core ≥ 80
        public float stormZoneBBonus = 0.8f;       // zoneB.isOpen
        public float stormCoreCoeff = 1.1f;        // + (core/100) × 1.1
        public float boostDaysCoeff = 0.30f;       // ★ escalating aggression per boost-day
        public float stormPressureMax = 100f;      // ≥ 100 → storm
        public float stormHeatPerDay = 40f;        // [LOCK] was 12 = cooling buried it, storm was pointless
        public float stormRadPerDay = 3f;
        public float sensorHeatRoom = 4f;          // Sensor Array gives cooling headroom

        // ─────────────────────────────────────────
        //  [LOCK] ZONE B (CONFIG.md 🔒 METHOD B — GDD §22)
        //  Tritium base/mastery live in the MASTERY section (zoneBTritiumBase/Mastery).
        //  rad_zoneb / rad_suit_mult / suit_cost live in the RADIATION section above.
        // ─────────────────────────────────────────
        [Header("Zone B (LOCK · CONFIG.md)")]
        public int zoneBMinStaff = 2;              // production needs ≥ 2 staff
        public float zoneBRotateRad = 32f;         // radiation > 32 → pull worker OUT before they sicken
        public float zoneBSendRadMax = 30f;        // only send workers with radiation < 30 (ascending)
        public float boostTritiumCost = 9f;        // reactor draw while boosting (Sprint 6 consumes)
        public float idleTritiumCost = 6f;         // reactor draw while idling
        public float zoneBBuildIron = 150f;        // GDD §6 buildings table: Zone B = Iron 150 + P 200
        public float zoneBBuildPower = 200f;       //   (unlocked by the tritium note · Phase 4 gate §7)

        // ─────────────────────────────────────────
        //  [LOCK] DATA RECOVERY (CONFIG.md 🔒 — GDD §24 / STORY.md)
        // ─────────────────────────────────────────
        [Header("Data Recovery (LOCK · CONFIG.md)")]
        public float dataRecoveryPassive = 14f;    // per day even with no idle researcher
        public float dataRecoveryRate = 10f;       // per idle researcher / day
        public float dataRecoveryLv2Mult = 1.3f;   // lab level ≥ 2
        public float dataRecoveryTarget = 100f;    // reach 100 → unlock next Record → reset 0

        // ─────────────────────────────────────────
        //  [LOCK] CARDS (CONFIG.md 🔒 CARDS — trigger thresholds, GDD §25)
        //  Cooldowns live on each CrisisCardSO asset; these are the state thresholds.
        // ─────────────────────────────────────────
        [Header("Crisis Cards — trigger thresholds (LOCK · CONFIG.md)")]
        public float cardHeatThreshold = 62f;   // card 1 heat: heat > 62
        public int cardSickCount = 3;           // card 2 sick: sickWorkers >= 3
        public float cardSpoilFood = 30f;       // card 3 spoil: food > 30 && ...
        public float cardSpoilRad = 6f;         //             ... avgRadiation > 6
        public int cardHungryCount = 3;         // card 4 hunger: hungryWorkers >= 3
        public int cardExhaustedCount = 3;      // card 5 overwork: exhaustedWorkers >= 3
        public int cardZoneBMinStaff = 2;       // card 6 zoneb: zoneBWorkers < 2
        public int cardTriageMinSick = 5;       // card 7 triage: sickWorkers >= 5 (and > beds)
        public int cardDecreeMinCool = 3;       // card 8 decree: coolingWorkers < 3

        // ─────────────────────────────────────────
        //  ★ Day 1 start (CONFIG.md ค่าเริ่มเกม)
        // ─────────────────────────────────────────
        [Header("Day 1 Start")]
        public int startPopulation = 14;           // [LOCK] bug #8
        public float startIron = 200f;             // §33: consider random 160-240 later
        public float startPower = 0f;
        public float startFood = 40f;
        public float startWater = 90f;
        public float startLabMat = 100f;
        public float startCore = 30f;
        public float startHeat = 0f;    // ★ 2026-07-21: was 20 — reactor starts cold, heat builds from running it
        public float startHope = 70f;
        public float startKnowledge = 20f;         // Auren already knows the basics — head start on the Novice tier (<30)

        [Header("Population growth (★ not sim-validated yet — CONFIG.md §33)")]
        public float growthFoodRatio = 3f;         // food > pop × 3 (was 5 — see CONFIG.md 2026-07-20 note)
        public float growthChance = 0.35f;         // 35%/day (was 0.25)
        public float growthFoodCost = 20f;
        public int growthAmount = 1;

        // Population ceiling by Shelter/Habitat level (CONFIG.md "★ Shelter (เพดานประชากร)").
        // L1 is free at game start ("เริ่มมี"), so the city always has at least shelterCapL1 —
        // which equals startPopulation, i.e. the city starts full and only grows after an upgrade.
        public int shelterCapL1 = 14;
        public int shelterCapL2 = 20;
        public int shelterCapL3 = 28;

        /// <summary>Population ceiling for a Shelter level. Levels above 3 clamp to L3.</summary>
        public int ShelterCapForLevel(int level)
        {
            if (level >= 3) return shelterCapL3;
            if (level == 2) return shelterCapL2;
            return shelterCapL1;
        }

        // ─────────────────────────────────────────
        //  Access helper
        // ─────────────────────────────────────────

        private static GameConfigSO _instance;

        /// <summary>
        /// Runtime access point. Loads Assets/Resources/GameConfig.asset once;
        /// falls back to CONFIG.md defaults (CreateInstance) so EditMode tests and
        /// headless sims never null-ref. Read-only query — direct access allowed (CLAUDE.md exception).
        /// </summary>
        public static GameConfigSO Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<GameConfigSO>("GameConfig");
                    if (_instance == null)
                        _instance = CreateInstance<GameConfigSO>(); // defaults mirror CONFIG.md
                }
                return _instance;
            }
        }

        /// <summary>Test hook — inject a custom config, or null to reset to the asset.</summary>
        public static void OverrideForTest(GameConfigSO config) => _instance = config;
    }
}
