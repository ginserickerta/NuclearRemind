using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Inner Voice — Auren's ▸ lines (BARKS.md §Inner Voice, V01–V20). A separate channel from the NPC
    /// barks: these are one-shot milestones (fire once each), not the ≤2/day state chorus BarkManager
    /// runs. Assets live in Resources/InnerVoice (speaker = Auren) and fire via OnBarkFired, so the HUD
    /// shows them as the nameless ▸ prompt.
    ///
    /// 19/20 wired — V01 was cut 2026-07-22 (owner removed the game-start dialogue box; the intro
    /// cards cover that beat). Callers outside this class: V02 (MemorialPanelController, first click),
    /// V04 (ResearchQueuePanel, first open), V06 (CodexQuizManager.MarkExtractorRan, first extraction day).
    ///
    /// V06 "Extractor เดินครั้งแรก" was long thought blocked on a building that does not exist. It is
    /// not: v6.3 defines the extractor as a max-level Water Plant producing deuterium, which
    /// QuizAppliedWatcher already detects for q_deuterium — so the trigger was there all along.
    /// </summary>
    // -18: after BarkManager (-20) so the ▸ line lands alongside the day's NPC bark.
    [DefaultExecutionOrder(-18)]
    public class InnerVoiceDirector : MonoBehaviour
    {
        public static InnerVoiceDirector Instance { get; private set; }

        private readonly Dictionary<string, BarkSO> _catalog = new Dictionary<string, BarkSO>();
        private readonly HashSet<string> _fired = new HashSet<string>();
        private bool _catalogLoaded;
        private int _recordCount;
        private int _coreStallDays;   // V13 needs 3 consecutive stalled days, not just one

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            var em = EventManager.Instance;
            if (em == null) return;
            em.OnDayEnded += HandleDayEnded;
            em.OnRecordRecovered += HandleRecordRecovered;
            em.OnZoneBOpened += HandleZoneBOpened;
            em.OnStormTriggered += HandleStorm;
            em.OnResearchNoteCompleted += HandleResearchDone;
            em.OnCrisisCardResolved += HandleCardResolved;
        }

        private void OnDisable()
        {
            var em = EventManager.Instance;
            if (em == null) return;
            em.OnDayEnded -= HandleDayEnded;
            em.OnRecordRecovered -= HandleRecordRecovered;
            em.OnZoneBOpened -= HandleZoneBOpened;
            em.OnStormTriggered -= HandleStorm;
            em.OnResearchNoteCompleted -= HandleResearchDone;
            em.OnCrisisCardResolved -= HandleCardResolved;
        }

        // ── event hooks ─────────────────────────────────────────────
        private void HandleRecordRecovered(RecordCardSO r)
        {
            _recordCount++;
            switch (_recordCount)
            {
                case 1: Fire("V03"); break;
                case 2: Fire("V09"); break;
                case 3: Fire("V15"); break;
                case 4: Fire("V16"); break;
            }
        }

        private void HandleZoneBOpened() => Fire("V14");
        private void HandleStorm() => Fire("V17");
        private void HandleResearchDone(string noteId) => Fire("V05"); // first completion only (fired once)

        private void HandleCardResolved(string cardId, int optionIndex)
        {
            if (cardId == CardIds.Decree && optionIndex >= 1) Fire("V19"); // Decree B/C = conscription
        }

        // ── state milestones (checked each day, fire once) ──────────
        private void HandleDayEnded(int day)
        {
            // ★ 2026-07-22: V01 backstop removed — the owner cut the game-start dialogue box
            // (the 7-card intro covers that beat). V01 stays in the catalog but never fires.

            var reactor = ReactorController.Instance;
            if (reactor != null)
            {
                if (reactor.Heat > 90f) Fire("V07");
                if (reactor.ToroidalLv > 0 && reactor.PoloidalLv > 0) Fire("V08");
                float tritium = ZoneBController.Instance != null ? ZoneBController.Instance.TritiumStock : reactor.Tritium;

                // V13 "ค้างอยู่ที่แปดสิบสามวันแล้ว" — BARKS.md requires 3 CONSECUTIVE days of the stall.
                // The line names the streak out loud, so firing on day 1 of it would be a lie.
                if (reactor.Core >= 80f && tritium < 5f) _coreStallDays++;
                else _coreStallDays = 0;
                if (_coreStallDays >= 3) Fire("V13");

                if (reactor.Core >= 100f) Fire("V20");
            }

            var wm = WorkerManager.Instance;
            if (wm != null)
            {
                if (wm.SickCount >= 1) Fire("V10");
                if (GameConfigSO.Instance != null && GameConfigSO.Instance.startPopulation - wm.AliveCount >= 1) Fire("V11");
                if (wm.Hope != null && wm.Hope.Current < 30f) Fire("V18");

                // V12 "รังสีทำให้เขาป่วย แล้วรังสีก็รักษาเขา" — BARKS.md "สร้าง Med Bay". A finished
                // Hospital is the Med Bay (GDD §6); polling here beats a build-event hook we don't have.
                if (wm.MedBayBeds > 0) Fire("V12");
            }
        }

        // ── firing ──────────────────────────────────────────────────
        /// <summary>Fire an inner-voice line once (public so future building hooks can call it).</summary>
        public void Fire(string id)
        {
            EnsureCatalog();
            if (_fired.Contains(id)) return;
            if (!_catalog.TryGetValue(id, out var bark) || bark == null) return;
            _fired.Add(id);
            EventManager.Instance?.RaiseBarkFired(bark);
        }

        public void RegisterCatalog(IEnumerable<BarkSO> barks)
        {
            if (barks != null)
                foreach (var b in barks)
                    if (b != null && !string.IsNullOrEmpty(b.barkId)) _catalog[b.barkId] = b;
            _catalogLoaded = true;
        }

        private void EnsureCatalog()
        {
            if (_catalogLoaded) return;
            _catalogLoaded = true;
            RegisterCatalog(Resources.LoadAll<BarkSO>("InnerVoice")); // Assets/Resources/InnerVoice
        }
    }
}
