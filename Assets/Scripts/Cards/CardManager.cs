using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>Outcome of resolving a card option.</summary>
    public struct CardResolveResult
    {
        public bool valid;       // false = locked option / no pending card / bad index
        public string afterText; // closing line to show ("" = silent, CARDS.md #7A)
    }

    /// <summary>
    /// Crisis Card manager (GDD §25 / CARDS.md). Each day-end it snapshots the live world, and if a
    /// card's STATE trigger fires (CardTriggers — never the calendar, rule #1) and its cooldown has
    /// elapsed, it presents ONE card and pauses the day clock. The player picks an option; locked
    /// options can't be chosen (rule #6). Resolving applies the option's effect through the live
    /// systems (Hope / Resources / Workers) and hands the deferred numbers to Sprint 5/6 via
    /// OnCrisisCardResolved.
    ///
    /// Plain MonoBehaviour singleton like ResearchLab; Initialize(cfg) is the EditMode-test entry.
    /// </summary>
    // -30: after WorkerManager (-50) and ResearchLab (-40) so the snapshot sees today's final
    // worker statuses (sick/hungry/exhausted) and research state.
    [DefaultExecutionOrder(-30)]
    public class CardManager : MonoBehaviour
    {
        public static CardManager Instance { get; private set; }

        private GameConfigSO _cfg;
        private readonly Dictionary<string, CrisisCardSO> _catalog = new Dictionary<string, CrisisCardSO>();
        private readonly Dictionary<string, int> _lastFiredDay = new Dictionary<string, int>();
        private readonly HashSet<string> _usedOnce = new HashSet<string>();
        private bool _catalogLoaded;
        private int _currentDay;

        private class RecurringHope { public string cardId; public float perDay; public int daysLeft; }
        private readonly List<RecurringHope> _recurring = new List<RecurringHope>();

        /// <summary>The card awaiting a decision (null = none). While set, no new card is presented.</summary>
        public CrisisCardSO Pending { get; private set; }
        public bool HasPending => Pending != null;

        // ★ v6.3 cutover (slice 4 Cards): auto-spawn into the live game (was F9-playtest-only). Once live it
        //   evaluates crisis cards on OnDayEnded and raises OnCrisisCardShown → CrisisCardPanelUI. Re-spawn on
        //   every sceneLoaded (same pattern as WorkerManager/ResearchLab).
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
                if (EventManager.Instance == null)
                {
                    // Normal in MainMenu; a real problem in Gamescene — say which, don't fail silently.
                    Trace("ยังไม่มี EventManager ตอน AutoSpawn — ข้ามไปก่อน (ปกติถ้าอยู่หน้าเมนู) " +
                          "รอ sceneLoaded รอบหน้าลองใหม่");
                    return;
                }
                if (FindFirstObjectByType<CardManager>() != null) return;
                new GameObject("CardManager (auto)").AddComponent<CardManager>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardManager] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

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
            _lastFiredDay.Clear();
            _usedOnce.Clear();
            _recurring.Clear();
            Pending = null;
        }

        private void OnEnable()
        {
            if (EventManager.Instance == null)
            {
                // Not a warning we can shrug off: with no subscription this manager never evaluates a
                // single card for the whole run, silently. Louder than a Trace on purpose.
                Debug.LogError("[Cards] CardManager ตื่นมาแต่ยังไม่มี EventManager — ไม่ได้ subscribe OnDayEnded " +
                               "แปลว่าจะไม่มีการ์ดขึ้นเลยทั้งเกม");
                return;
            }
            EventManager.Instance.OnDayStarted += HandleDayStarted;
            EventManager.Instance.OnDayEnded += HandleDayEnded;
            Trace($"CardManager พร้อมแล้ว (GameObject '{name}') — subscribe OnDayEnded เรียบร้อย");
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayStarted -= HandleDayStarted;
            EventManager.Instance.OnDayEnded -= HandleDayEnded;
        }

        private void HandleDayStarted(int day, bool timed) => _currentDay = day;

        private void HandleDayEnded(int day)
        {
            if (day <= 1)
            {
                Trace($"วันที่ {day} จบ — ข้ามการประเมินการ์ด (วันแรกเป็นบทเรียน)");
                return; // Day 1 = tutorial: no crisis yet (matches WorkerManager)
            }
            Trace($"วันที่ {day} จบ — เริ่มประเมินการ์ด");
            ApplyRecurringHope();
            EvaluateDay(day, CardWorldState.Snapshot());
        }

        // ─────────────────────────────────────────
        //  Catalog (tests register directly; play mode auto-loads from Resources)
        // ─────────────────────────────────────────

        public void RegisterCatalog(IEnumerable<CrisisCardSO> cards)
        {
            if (cards != null)
                foreach (var c in cards)
                    if (c != null && !string.IsNullOrEmpty(c.cardId)) _catalog[c.cardId] = c;
            _catalogLoaded = true;
        }

        private void EnsureCatalog()
        {
            if (_catalogLoaded) return;
            _catalogLoaded = true;
            RegisterCatalog(Resources.LoadAll<CrisisCardSO>("CrisisCards")); // Assets/Resources/CrisisCards
        }

        public CrisisCardSO GetCard(string cardId)
        {
            EnsureCatalog();
            return _catalog.TryGetValue(cardId, out var c) ? c : null;
        }

        // ─────────────────────────────────────────
        //  Evaluate → present ONE eligible card
        // ─────────────────────────────────────────

        /// <summary>
        /// Present the first eligible card for this day (triggered + off cooldown + not spent).
        /// Returns the presented card (also stored in Pending) or null. Pauses the day clock.
        /// </summary>
        public CrisisCardSO EvaluateDay(int day, in CardWorldState state)
        {
            EnsureCatalog();
            _currentDay = day;
            if (HasPending)
            {
                Trace($"วันที่ {day}: ข้าม — ยังมีการ์ด '{Pending.cardId}' ค้างรอคำตอบอยู่");
                return Pending; // one at a time — wait for the player to resolve
            }

            foreach (var cardId in CardIds.All)          // fixed priority: card 1 → 8
            {
                if (!_catalog.TryGetValue(cardId, out var card) || card == null) continue;
                if (!IsEligible(card, day, state)) continue;

                if (!Present(card)) continue; // presentation failed — try the next card, don't hang
                return card;
            }
            TraceNothingFired(day, state);
            return null;
        }

        /// <summary>
        /// Hand a card to the UI, then check it actually arrived.
        ///
        /// The order matters. Pending + the clock pause used to be set before RaiseCrisisCardShown, so a
        /// card that no panel picked up left Pending stuck forever — and Pending is cleared ONLY by
        /// ResolveOption. One silent miss and every remaining card of the run was blocked with no error,
        /// which is indistinguishable from "no trigger fired". Same shape as the day-9 record-card hang.
        ///
        /// Returns false when the card could not be shown; the caller keeps looking and the run continues.
        /// </summary>
        private bool Present(CrisisCardSO card)
        {
            Pending = card;
            TimeManager.Instance?.Pause(PauseReason.CrisisPopup);
            EventManager.Instance?.RaiseCrisisCardShown(card);

            // EditMode tests drive this headless — there is no panel to confirm against, by design.
            if (!Application.isPlaying || CrisisCardPanelUI.IsShowing) return true;

            Debug.LogError($"[Cards] การ์ด '{card.cardId}' ถูกสั่งแสดงแล้วแต่แผงไม่ขึ้น — " +
                           "CrisisCardPanelUI ไม่ได้อยู่ในซีนหรือ spawn ไม่สำเร็จ ปล่อยการ์ดทิ้งเพื่อไม่ให้ค้างทั้งเกม");
            Pending = null;
            TimeManager.Instance?.Resume(PauseReason.CrisisPopup);
            return false;
        }

        /// <summary>Dev/test: present a specific card now, bypassing triggers &amp; cooldown (F9 panel).</summary>
        public CrisisCardSO ForcePresent(string cardId)
        {
            EnsureCatalog();
            if (HasPending) return Pending;
            if (!_catalog.TryGetValue(cardId, out var card) || card == null)
            {
                Debug.LogError($"[Cards] ไม่พบการ์ด '{cardId}' ใน Resources/CrisisCards — asset หายหรือ cardId ไม่ตรง");
                return null;
            }
            return Present(card) ? card : null;
        }

        /// <summary>Dev/test: drop a card stuck in Pending and let the clock run again.</summary>
        public void ClearPending()
        {
            if (!HasPending) return;
            Debug.LogWarning($"[Cards] ล้างการ์ดค้าง '{Pending.cardId}' ทิ้ง");
            Pending = null;
            TimeManager.Instance?.Resume(PauseReason.CrisisPopup);
        }

        // ─────────────────────────────────────────
        //  Diagnostics — why did no card fire today?
        // ─────────────────────────────────────────

        /// <summary>Day-end trigger trace to the Console. Off in builds; toggled from the Editor menu.</summary>
        public static bool VerboseTrace = true;

        private static void Trace(string message)
        {
            if (VerboseTrace) Debug.Log($"[Cards] {message}");
        }

        /// <summary>
        /// Per-card "why not" table for one day. Public so the Editor tools can print the same text on
        /// demand instead of keeping a second copy that would drift — the same reason Describe() lives
        /// next to IsTriggered.
        /// </summary>
        public string BuildTriggerReport(int day, in CardWorldState state)
        {
            EnsureCatalog();
            var sb = new System.Text.StringBuilder();
            sb.Append($"วันที่ {day}: ไม่มีการ์ดขึ้น (โหลดได้ {_catalog.Count}/8 ใบ)");
            foreach (var cardId in CardIds.All)
            {
                sb.Append('\n').Append("  ").Append(cardId).Append(" — ");
                if (!_catalog.TryGetValue(cardId, out var card) || card == null)
                { sb.Append("★ ไม่มี asset ใน Resources/CrisisCards"); continue; }

                if (card.onceOnly && _usedOnce.Contains(cardId)) { sb.Append("ใช้ไปแล้ว (onceOnly)"); continue; }
                if (_lastFiredDay.TryGetValue(cardId, out var last) && day - last < card.cooldownDays)
                { sb.Append($"ติด cooldown (เหลืออีก {card.cooldownDays - (day - last)} วัน)"); continue; }

                sb.Append(CardTriggers.IsTriggered(cardId, state) ? "เข้าเงื่อนไข!" : "ยังไม่ถึงเกณฑ์")
                  .Append("  ▸ ").Append(CardTriggers.Describe(cardId, state));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Day-end reports kept in memory so a play session can be exported in one go. The Console alone
        /// was not enough to answer "which thresholds are actually out of reach": the interesting lines
        /// scroll away behind everything else the game logs, and there is no way to hand them over.
        /// Bounded so a long session cannot grow without limit.
        /// </summary>
        public static readonly List<string> TraceHistory = new List<string>();
        private const int TraceHistoryMax = 200;

        private void TraceNothingFired(int day, in CardWorldState state)
        {
            if (!VerboseTrace) return;

            string report = BuildTriggerReport(day, state);
            Debug.Log("[Cards] " + report);

            TraceHistory.Add(report);
            if (TraceHistory.Count > TraceHistoryMax) TraceHistory.RemoveAt(0);
        }

        private bool IsEligible(CrisisCardSO card, int day, in CardWorldState state)
        {
            if (card.onceOnly && _usedOnce.Contains(card.cardId)) return false;
            if (_lastFiredDay.TryGetValue(card.cardId, out var last) && day - last < card.cooldownDays)
                return false;
            return CardTriggers.IsTriggered(card.cardId, state);
        }

        /// <summary>Is this option pickable right now (false = locked behind an unresearched note)?</summary>
        public bool CanChoose(int optionIndex)
        {
            if (!HasPending || Pending.options == null) return false;
            if (optionIndex < 0 || optionIndex >= Pending.options.Length) return false;
            return !Pending.options[optionIndex].IsLocked(KnowledgeDB.Instance);
        }

        // ─────────────────────────────────────────
        //  Resolve → apply the chosen option
        // ─────────────────────────────────────────

        public CardResolveResult ResolveOption(int optionIndex)
        {
            var res = new CardResolveResult();
            if (!HasPending) return res;
            var card = Pending;
            if (card.options == null || optionIndex < 0 || optionIndex >= card.options.Length) return res;

            var opt = card.options[optionIndex];
            if (opt.IsLocked(KnowledgeDB.Instance)) return res; // ★ can't pick a locked option (rule #6)

            ApplyEffect(card.cardId, opt);

            _lastFiredDay[card.cardId] = _currentDay;
            if (card.onceOnly) _usedOnce.Add(card.cardId);

            EventManager.Instance?.RaiseCrisisCardResolved(card.cardId, optionIndex);

            res.valid = true;
            res.afterText = opt.afterText ?? "";
            Pending = null;
            TimeManager.Instance?.Resume(PauseReason.CrisisPopup);
            return res;
        }

        // ─────────────────────────────────────────
        //  Effects (live systems only; deferred numbers ride OnCrisisCardResolved)
        // ─────────────────────────────────────────

        private void ApplyEffect(string cardId, CardOption opt)
        {
            var e = opt.effect;
            if (e == null) return;

            // Hope — one-off + recurring drip
            if (Mathf.Abs(e.hopeDelta) > 0.001f)
                WorkerManager.Instance?.Hope?.Report($"card.{cardId}", opt.label, e.hopeDelta, HopeCategory.Card);
            if (Mathf.Abs(e.hopePerDay) > 0.001f && e.hopePerDayDays > 0)
                _recurring.Add(new RecurringHope { cardId = cardId, perDay = e.hopePerDay, daysLeft = e.hopePerDayDays });

            // Resources
            var em = EventManager.Instance;
            if (em != null)
            {
                if (Mathf.Abs(e.power) > 0.001f)  em.RaiseResourceDelta(ResourceType.Energy, e.power);
                if (Mathf.Abs(e.water) > 0.001f)  em.RaiseResourceDelta(ResourceType.Water, e.water);
                if (Mathf.Abs(e.food) > 0.001f)   em.RaiseResourceDelta(ResourceType.Food, e.food);
                if (Mathf.Abs(e.iron) > 0.001f)   em.RaiseResourceDelta(ResourceType.Iron, e.iron);
                if (Mathf.Abs(e.labMat) > 0.001f) em.RaiseResourceDelta(ResourceType.LabMat, e.labMat);

                if (e.foodMult > 0.001f)
                {
                    var rm = ResourceManager.Instance;
                    if (rm != null)
                    {
                        float delta = rm.Current.food * (e.foodMult - 1f); // e.g. ×0.7 → −30% of stock
                        if (Mathf.Abs(delta) > 0.001f) em.RaiseResourceDelta(ResourceType.Food, delta);
                    }
                }
            }

            // Workers
            var wm = WorkerManager.Instance;
            if (wm != null)
            {
                if (Mathf.Abs(e.fatigueAll) > 0.001f)
                    foreach (var w in wm.Workers) if (w.alive) w.fatigue = Mathf.Clamp(w.fatigue + e.fatigueAll, 0f, 100f);
                if (Mathf.Abs(e.hungerAll) > 0.001f)
                    foreach (var w in wm.Workers) if (w.alive) w.hunger = Mathf.Clamp(w.hunger + e.hungerAll, 0f, 100f);

                if (e.radiationTargets > 0 && Mathf.Abs(e.radiationAmount) > 0.001f)
                {
                    // least-exposed first — modelling "send the people who can still take it"
                    var targets = wm.Workers.Where(w => w.alive).OrderBy(w => w.radiation).Take(e.radiationTargets);
                    foreach (var w in targets) w.radiation = Mathf.Clamp(w.radiation + e.radiationAmount, 0f, 100f);
                }

                if (e.farmersReturned > 0) ReturnFarmers(wm, e.farmersReturned);
            }

            // Food spoilage (CARDS.md การ์ด 3) — the crisis itself switches rotting on; the Co-60 option
            // then mitigates it permanently (×0.3). Dorn's D01/D02 read the rate, D04 reads Co60Active.
            var ce = CrisisEffectManager.Instance;
            if (ce != null && cardId == CardIds.Spoil)
            {
                ce.ActivateSpoilage();
                if (e.spoilMult > 0f) ce.ApplySpoilMultiplier(e.spoilMult, isCo60: true);
            }

            // e.heatDelta / coreStallDays / stopZoneBDays → consumed by Sprint 6 via the event
        }

        private static void ReturnFarmers(WorkerManager wm, int count)
        {
            int moved = 0;
            // idle first, then anyone not already farming (never touch strikers)
            foreach (var w in wm.GetWorkers(WorkerJobs.Idle))
            {
                if (moved >= count) break;
                if (wm.AssignJob(w, WorkerJobs.Farm)) moved++;
            }
            if (moved >= count) return;
            foreach (var w in wm.Workers.ToList())
            {
                if (moved >= count) break;
                if (!w.alive || w.job == WorkerJobs.Farm || w.strikeDaysLeft > 0) continue;
                if (wm.AssignJob(w, WorkerJobs.Farm)) moved++;
            }
        }

        private void ApplyRecurringHope()
        {
            if (_recurring.Count == 0) return;
            var ledger = WorkerManager.Instance?.Hope;
            for (int i = _recurring.Count - 1; i >= 0; i--)
            {
                var r = _recurring[i];
                ledger?.Report($"card.{r.cardId}.drip", "ผลต่อเนื่องจากการตัดสินใจ", r.perDay, HopeCategory.Card);
                if (--r.daysLeft <= 0) _recurring.RemoveAt(i);
            }
        }
    }
}
