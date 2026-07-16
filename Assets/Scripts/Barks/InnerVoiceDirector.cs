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
    /// Wired to the events/state that exist in v6.3. A few milestone lines (V02 memorial · V04 research
    /// menu · V06 extractor · V12 med bay) need building/UI hooks not present yet — their assets exist
    /// so a later pass just calls Fire("V06").
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
            Fire("V01"); // first tick of the run — "มืดสนิท เริ่มจากไฟก่อน"

            var reactor = ReactorController.Instance;
            if (reactor != null)
            {
                if (reactor.Heat > 90f) Fire("V07");
                if (reactor.ToroidalLv > 0 && reactor.PoloidalLv > 0) Fire("V08");
                float tritium = ZoneBController.Instance != null ? ZoneBController.Instance.TritiumStock : reactor.Tritium;
                if (reactor.Core >= 80f && tritium < 5f) Fire("V13");
                if (reactor.Core >= 100f) Fire("V20");
            }

            var wm = WorkerManager.Instance;
            if (wm != null)
            {
                if (wm.SickCount >= 1) Fire("V10");
                if (GameConfigSO.Instance != null && GameConfigSO.Instance.startPopulation - wm.AliveCount >= 1) Fire("V11");
                if (wm.Hope != null && wm.Hope.Current < 30f) Fire("V18");
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
