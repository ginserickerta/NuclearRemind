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
                if (EventManager.Instance == null) return; // no core yet (MainMenu) — a later sceneLoaded retries
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
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayStarted += HandleDayStarted;
            EventManager.Instance.OnDayEnded += HandleDayEnded;
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
            if (day <= 1) return; // Day 1 = tutorial: no crisis yet (matches WorkerManager)
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
            if (HasPending) return Pending; // one at a time — wait for the player to resolve

            foreach (var cardId in CardIds.All)          // fixed priority: card 1 → 8
            {
                if (!_catalog.TryGetValue(cardId, out var card) || card == null) continue;
                if (!IsEligible(card, day, state)) continue;

                Pending = card;
                TimeManager.Instance?.Pause(PauseReason.CrisisPopup);
                EventManager.Instance?.RaiseCrisisCardShown(card);
                return card;
            }
            return null;
        }

        /// <summary>Dev/test: present a specific card now, bypassing triggers &amp; cooldown (F9 panel).</summary>
        public CrisisCardSO ForcePresent(string cardId)
        {
            EnsureCatalog();
            if (HasPending) return Pending;
            if (!_catalog.TryGetValue(cardId, out var card) || card == null) return null;
            Pending = card;
            TimeManager.Instance?.Pause(PauseReason.CrisisPopup);
            EventManager.Instance?.RaiseCrisisCardShown(card);
            return card;
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
