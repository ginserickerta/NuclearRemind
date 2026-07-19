namespace NuclearReMind
{
    /// <summary>
    /// Live snapshot the bark conditions read (BARKS.md). Built from the running systems.
    ///
    /// Every field here is populated from a live system EXCEPT mutationLabBuilt: the mutation lab exists
    /// only as an unlock string in Resources/ResearchNotes/irradiation.asset (no BuildingType, no asset,
    /// no manager), so D05 stays quiet until it ships. Do not fake it — a bark that fires with nothing
    /// behind it lies to the player. (co60Built is real: CARDS.md การ์ด 3 option C, not a building.)
    /// </summary>
    public struct BarkWorldState
    {
        // reactor
        public float fuelDemand, fuel, heat, cooling, core, tritium;
        public int toroidalLv, poloidalLv;
        public bool boosting, blackout, researchStalled, tritiumEver;

        // people / economy
        public int sickWorkers, dyingWorkers, workerDeaths, hungryWorkers, farmWorkers;
        public int medBayCapacity, zoneBWorkers, suitsMade;
        public float avgRadiation, food, hope;
        public bool hasMedBay, zoneBOpen;

        // farm / spoilage — co60Built + mutationLabBuilt have no backing building yet (see class doc)
        public bool spoilHigh, co60Built, mutationLabBuilt;

        // storm / decree
        public bool stormActive, decreeActive, decreeChildren, decreeNone;

        /// <summary>Decree asset id the citizens react to (C04) — Assets/ScriptableObjects/Decrees.</summary>
        public const string ChildLaborDecreeId = "Decree2_ChildLabor";

        public static BarkWorldState Snapshot()
        {
            var s = new BarkWorldState();
            var cfg = GameConfigSO.Instance;

            var rm = ResourceManager.Instance;
            if (rm != null) s.food = rm.Current.food;

            var wm = WorkerManager.Instance;
            if (wm != null)
            {
                s.sickWorkers = wm.SickCount;
                s.dyingWorkers = wm.DyingCount;
                s.hungryWorkers = wm.HungryCount;
                s.farmWorkers = wm.GetWorkers(WorkerJobs.Farm).Count;
                s.zoneBWorkers = wm.GetWorkers(WorkerJobs.ZoneB).Count;
                s.hope = wm.Hope != null ? wm.Hope.Current : 0f;
                if (cfg != null) s.workerDeaths = cfg.startPopulation - wm.AliveCount;

                float radSum = 0f; int n = 0;
                foreach (var w in wm.Workers) if (w.alive) { radSum += w.radiation; n++; }
                s.avgRadiation = n > 0 ? radSum / n : 0f;
            }

            var zb = ZoneBController.Instance;
            if (zb != null) { s.zoneBOpen = zb.IsOpen; s.tritiumEver = zb.ProducedDays > 0; }

            var suits = RadSuitManager.Instance;
            if (suits != null) s.suitsMade = suits.SuitsMade;

            var reactor = ReactorController.Instance; // Sprint 6 — Kova's reactor lines go live
            if (reactor != null)
            {
                s.heat = reactor.Heat;
                s.core = reactor.Core;
                s.fuel = reactor.Fuel;
                s.fuelDemand = reactor.FuelDemand;
                s.cooling = reactor.LastCooling;
                s.boosting = reactor.IsBoosting;
                s.toroidalLv = reactor.ToroidalLv;
                s.poloidalLv = reactor.PoloidalLv;
                s.tritium = zb != null ? zb.TritiumStock : reactor.Tritium;
            }
            var storm = StormSystem.Instance;
            if (storm != null) s.stormActive = storm.IsStormActive;

            // Blackout: a failed energy draw today, not power<0 (bug #17). Cleared at day start, so this
            // reads true only for the day it happened — barks fire at day end, before the reset.
            if (wm != null) s.blackout = wm.BlackoutToday;

            // A finished Hospital IS the Med Bay (GDD §6). No Hospital = 0 beds, not cfg.medBayCapacity —
            // otherwise M04 ("ที่พยาบาลเต็ม") could fire for a med bay that was never built.
            if (wm != null)
            {
                s.medBayCapacity = wm.MedBayBeds;
                s.hasMedBay = s.medBayCapacity > 0;
            }

            // Research stalled = a job is running with nobody staffing it (bug #7 deadlock).
            var lab = ResearchLab.Instance;
            if (lab != null && wm != null)
                s.researchStalled = lab.LabBusy && wm.GetWorkers(WorkerJobs.Lab).Count == 0;

            // Spoilage: BARKS.md D01/D02 want "spoilRate > 2x baseline" · D04 wants the Co-60 option taken.
            var ce = CrisisEffectManager.Instance;
            if (ce != null && cfg != null)
            {
                s.spoilHigh = ce.FoodSpoilRatePerDay > cfg.spoilHighMult * cfg.spoilBase;
                s.co60Built = ce.Co60Active;
            }

            // Decrees are additive-only (DecreeManager never repeals), so "active" == "ever enacted".
            var dm = DecreeManager.Instance;
            if (dm != null && dm.decrees != null)
            {
                foreach (var d in dm.decrees)
                {
                    if (d == null || !dm.IsActive(d)) continue;
                    s.decreeActive = true;
                    if (d.id == ChildLaborDecreeId) s.decreeChildren = true; // C04 "เด็กพวกนั้นไม่ควรต้องอยู่ตรงนั้น"
                }
                s.decreeNone = !s.decreeActive;
            }
            return s;
        }
    }

    /// <summary>
    /// The condition for each bark (BARKS.md — Kova/Mira/Dorn/Citizen tables), keyed by barkId. Bound
    /// to STATE not day (CLAUDE.md rule #1). hasNote / mastery conditions query KnowledgeDB /
    /// MasteryRegistry directly (read-only — allowed by the v6.3 exception). Inner-Voice (V##) barks are
    /// milestone/event one-shots and are NOT handled here (Sprint 6 story hooks).
    /// </summary>
    public static class BarkConditions
    {
        public static bool IsMet(string barkId, in BarkWorldState s)
        {
            var db = KnowledgeDB.Instance;
            var m = MasteryRegistry.Instance;
            switch (barkId)
            {
                // ── KOVA ────────────────────────────────────────────
                case "K01": return s.fuelDemand > 0f && s.fuel == 0f;
                case "K02": return s.heat > 35f;
                case "K03": return s.heat > 50f && !db.HasNote("confinement");
                case "K04": return db.HasNote("confinement") && s.toroidalLv == 0;
                case "K05": return s.toroidalLv > 0 && s.poloidalLv == 0;
                case "K06": return s.heat > 90f;
                case "K07": return s.boosting && s.cooling < 20f;
                case "K08": return s.core >= 78f;
                case "K09": return s.core >= 80f && s.tritium < 5f;
                case "K10": return s.zoneBOpen && s.tritiumEver;
                case "K11": return s.researchStalled;
                case "K12": return s.blackout;
                case "K13": return s.core >= 95f;
                case "K14": return s.hope < 40f && s.heat < 70f;

                // ── MIRA ────────────────────────────────────────────
                case "M01": return s.sickWorkers >= 2;
                case "M02": return s.sickWorkers >= 2 && !db.HasNote("nuclear_medicine");
                case "M03": return db.HasNote("nuclear_medicine") && !s.hasMedBay;
                case "M04": return s.hasMedBay && s.sickWorkers > s.medBayCapacity; // "เต็ม" needs one to exist
                case "M05": return s.dyingWorkers > 0;
                case "M06": return s.workerDeaths >= 1;
                case "M07": return s.zoneBOpen && s.suitsMade < s.zoneBWorkers;
                case "M08": return s.avgRadiation > 40f;
                case "M09": return m.Has(QuizIds.NuclearMedicine);
                case "M10": return s.hungryWorkers >= 3;
                case "M11": return s.hope < 40f;
                case "M12": return s.hope > 80f && s.core > 70f;
                case "M13": return s.decreeActive;
                case "M14": return s.stormActive;

                // ── DORN ────────────────────────────────────────────
                case "D01": return s.spoilHigh;
                case "D02": return s.spoilHigh && !db.HasNote("irradiation");
                case "D03": return db.HasNote("irradiation");
                case "D04": return s.co60Built;
                case "D05": return s.mutationLabBuilt && m.FarmYieldMult() > 1.001f;
                case "D06": return s.food == 0f;
                case "D07": return s.farmWorkers < 2;
                case "D08": return m.Has(QuizIds.FoodIrradiation);

                // ── CITIZEN ─────────────────────────────────────────
                case "C01": return s.hope < 60f;
                case "C02": return s.hope < 40f;
                case "C03": return s.hope < 25f;
                case "C04": return s.decreeChildren;
                case "C05": return s.decreeNone && s.stormActive;
                case "C06": return s.hope > 85f;

                default: return false;
            }
        }
    }
}
