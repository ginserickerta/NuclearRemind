namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: ภาพถ่ายสถานะโลก ณ ตอนนั้น (heat, คนป่วย, อาหาร, พายุ ฯลฯ) ที่เงื่อนไขการ์ดใช้อ่าน
    /// [TH] ดึงจากระบบที่รันอยู่จริง หรือเทสต์/แผง F9 ตั้งค่าเองได้
    /// Live world snapshot the card triggers read (GDD §25 / CONFIG.md 🔒 CARDS). Built from the
    /// running systems, or set by tests / the F9 panel.
    ///
    /// ⚠ The old note here said heat/storm/Zone B "aren't built yet" and that those cards not firing was
    /// correct rather than a bug. That has been stale since the reactor and Zone B clusters went live
    /// (9b231ce / b3e0b24) — all three are running now, so a card of theirs that never appears IS worth
    /// investigating. Use CardManager.VerboseTrace to see the actual per-card reason.
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

        /// <summary>
        /// [TH] อ่านค่าจากระบบสด ๆ (WorkerManager/ResourceManager/Reactor/Storm/ZoneB) แบบกัน null ทุกตัว
        /// Read the live systems (WorkerManager + ResourceManager + config). Null-safe.
        /// </summary>
        public static CardWorldState Snapshot()
        {
            var s = new CardWorldState();

            var rm = ResourceManager.Instance;
            if (rm != null) s.food = rm.Current.food;

            var wm = WorkerManager.Instance;
            if (wm != null)
            {
                // ★ Beds, not the heal rate. This used to read GameConfigSO.medBayCapacity, which is
                // "patients healed per day" (4) and exists whether or not a Hospital was ever built —
                // so card 7 (triage) demanded 5+ sick against 4 imaginary beds. WorkerManager.MedBayBeds
                // is the real count and is 0 until a Hospital is finished, which is what the spec's
                // "sickWorkers > medBayCapacity" is actually asking about.
                s.medBayCapacity = wm.MedBayBeds;

                // ★ Dying counts as sick. WorkerStatus is exclusive and severity-ordered, so a worker who
                // deteriorates from Sick to Dying silently LEFT the sick tally — the worse the outbreak
                // got, the fewer "sick workers" cards 2 and 7 could see, and a city with everyone dying
                // reported zero. Neither card's threshold moved; the input just stopped lying.
                s.sickWorkers = wm.SickCount + wm.DyingCount;
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
    /// [TH] หน้าที่: เงื่อนไข trigger ของการ์ดทั้ง 8 ใบรวมไว้ที่เดียว — ทุกกฎผูกกับ "state" จริง
    /// [TH] (เตาร้อน/คนป่วย/คนหิว ฯลฯ) ห้ามผูกกับวันที่ (กติกาข้อ 1) · เกณฑ์ตัวเลขอ่านจาก GameConfigSO
    /// The trigger CONDITION for each card, in one place (GDD §25 / CONFIG.md 🔒 CARDS). Every rule
    /// is bound to STATE, never the calendar (CLAUDE.md rule #1). Thresholds that also live in
    /// CONFIG.md are read from GameConfigSO, not hardcoded here.
    /// </summary>
    public static class CardTriggers
    {
        // [TH] เช็คว่าการ์ดใบนี้เข้าเงื่อนไขเด้งหรือยัง เทียบ state ปัจจุบันกับเกณฑ์ใน config
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
        /// [TH] สำหรับ debug: พิมพ์กฎเดียวกับ IsTriggered เป็นข้อความ "ค่าจริง / เกณฑ์" ให้ไล่ดูได้ว่าทำไมการ์ดไม่เด้ง
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
