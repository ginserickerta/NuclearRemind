namespace NuclearReMind
{
    /// <summary>
    /// Live world snapshot the card triggers read (GDD §25 / CONFIG.md 🔒 CARDS). Built from the
    /// running systems, or set by tests / the F9 panel. Fields whose systems aren't built yet
    /// (heat, storm, Zone B) default to values that simply keep those cards from firing — correct,
    /// not a bug: card 1 can't trigger until the reactor exists, etc.
    /// </summary>
    public struct CardWorldState
    {
        public float heat;            // reactor HEAT (Sprint 6) — 0 until then
        public int sickWorkers;
        public float food;
        public float avgRadiation;
        public int hungryWorkers;
        public int exhaustedWorkers;
        public bool zoneBOpen;        // Sprint 5 — false until then
        public int zoneBWorkers;
        public int medBayCapacity;
        public bool stormActive;      // Sprint 6 — false until then
        public int coolingWorkers;

        /// <summary>Read the live systems (WorkerManager + ResourceManager + config). Null-safe.</summary>
        public static CardWorldState Snapshot()
        {
            var s = new CardWorldState();
            var cfg = GameConfigSO.Instance;
            s.medBayCapacity = cfg != null ? cfg.medBayCapacity : 4;

            var rm = ResourceManager.Instance;
            if (rm != null) s.food = rm.Current.food;

            var wm = WorkerManager.Instance;
            if (wm != null)
            {
                s.sickWorkers = wm.SickCount;
                s.hungryWorkers = wm.HungryCount;
                s.exhaustedWorkers = wm.ExhaustedCount;
                s.coolingWorkers = wm.GetWorkers(WorkerJobs.Cool).Count;
                s.zoneBWorkers = wm.GetWorkers(WorkerJobs.ZoneB).Count;

                float radSum = 0f; int n = 0;
                foreach (var w in wm.Workers)
                    if (w.alive) { radSum += w.radiation; n++; }
                s.avgRadiation = n > 0 ? radSum / n : 0f;
            }

            var zb = ZoneBController.Instance; // Sprint 5 — live for card #6
            if (zb != null) s.zoneBOpen = zb.IsOpen;

            var reactor = ReactorController.Instance; // Sprint 6 — live for card #1
            if (reactor != null) s.heat = reactor.Heat;
            var storm = StormSystem.Instance;         // Sprint 6 — live for card #8
            if (storm != null) s.stormActive = storm.IsStormActive;
            return s;
        }
    }

    /// <summary>
    /// The trigger CONDITION for each card, in one place (GDD §25 / CONFIG.md 🔒 CARDS). Every rule
    /// is bound to STATE, never the calendar (CLAUDE.md rule #1). Thresholds that also live in
    /// CONFIG.md are read from GameConfigSO, not hardcoded here.
    /// </summary>
    public static class CardTriggers
    {
        public static bool IsTriggered(string cardId, in CardWorldState s)
        {
            var cfg = GameConfigSO.Instance;
            switch (cardId)
            {
                case CardIds.Heat:     return s.heat > cfg.cardHeatThreshold;                       // heat > 62
                case CardIds.Sick:     return s.sickWorkers >= cfg.cardSickCount;                   // sick ≥ 3
                case CardIds.Spoil:    return s.food > cfg.cardSpoilFood && s.avgRadiation > cfg.cardSpoilRad; // food>30 && rad>6
                case CardIds.Hunger:   return s.hungryWorkers >= cfg.cardHungryCount;               // hungry ≥ 3
                case CardIds.Overwork: return s.exhaustedWorkers >= cfg.cardExhaustedCount;         // exhausted ≥ 3
                case CardIds.ZoneB:    return s.zoneBOpen && s.zoneBWorkers < cfg.cardZoneBMinStaff; // open && staff < 2
                case CardIds.Triage:   return s.sickWorkers > s.medBayCapacity && s.sickWorkers >= cfg.cardTriageMinSick; // sick>beds && ≥5
                case CardIds.Decree:   return s.stormActive && s.coolingWorkers < cfg.cardDecreeMinCool;   // storm && cool < 3
                default:               return false;
            }
        }

        /// <summary>
        /// Diagnostic: the same rule as IsTriggered, written out as "actual vs threshold".
        ///
        /// Kept next to IsTriggered on purpose — a log that reads thresholds from somewhere else would
        /// drift and then lie, which is worse than no log at all. Used by CardManager's day-end trace.
        /// </summary>
        public static string Describe(string cardId, in CardWorldState s)
        {
            var cfg = GameConfigSO.Instance;
            switch (cardId)
            {
                case CardIds.Heat:     return $"heat {s.heat:0.0} / >{cfg.cardHeatThreshold}";
                case CardIds.Sick:     return $"sick {s.sickWorkers} / >={cfg.cardSickCount}";
                case CardIds.Spoil:    return $"food {s.food:0.0} / >{cfg.cardSpoilFood} && rad {s.avgRadiation:0.0} / >{cfg.cardSpoilRad}";
                case CardIds.Hunger:   return $"hungry {s.hungryWorkers} / >={cfg.cardHungryCount}";
                case CardIds.Overwork: return $"exhausted {s.exhaustedWorkers} / >={cfg.cardExhaustedCount}";
                case CardIds.ZoneB:    return $"zoneBOpen {s.zoneBOpen} && staff {s.zoneBWorkers} / <{cfg.cardZoneBMinStaff}";
                case CardIds.Triage:   return $"sick {s.sickWorkers} / >beds {s.medBayCapacity} && >={cfg.cardTriageMinSick}";
                case CardIds.Decree:   return $"storm {s.stormActive} && cooling {s.coolingWorkers} / <{cfg.cardDecreeMinCool}";
                default:               return "ไม่รู้จักการ์ดนี้";
            }
        }
    }
}
