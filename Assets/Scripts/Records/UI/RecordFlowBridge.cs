using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ★ v6.3 cutover (slice 6 Records): bridges DataRecovery's OnRecordRecovered into the existing
    /// record UI flow that StoryDirector used to drive (StoryDirector is archived/silenced — D5):
    ///
    ///   DataRecovery → OnRecordRecovered → (bridge) → OnStoryRecordShown → RecordCardUI popup
    ///   RecordCardUI "เก็บเข้าแผง" → OnRecordArchiveRequested → (bridge) → OnRecordArchived
    ///     → RecordNotificationHUD badge + RecordsPanelController refresh
    ///
    /// The lead unlock itself happens in DataRecovery.UnlockNextRecord (KnowledgeDB.UnlockLead) —
    /// this component is display plumbing only. RecordsPanelController lists DataRecovery.Recovered
    /// directly, so a record stays readable even if the player only clicks "รับทราบ".
    /// </summary>
    public class RecordFlowBridge : MonoBehaviour
    {
        public static RecordFlowBridge Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            var em = EventManager.Instance;
            if (em == null) return;
            em.OnRecordRecovered += HandleRecordRecovered;
            em.OnRecordArchiveRequested += HandleArchiveRequested;
        }

        private void OnDisable()
        {
            var em = EventManager.Instance;
            if (em == null) return;
            em.OnRecordRecovered -= HandleRecordRecovered;
            em.OnRecordArchiveRequested -= HandleArchiveRequested;
        }

        // Pop the recovered log as the wooden record card (RecordCardUI pauses the clock itself —
        // pause-reason stack per §15, never timeScale).
        private void HandleRecordRecovered(RecordCardSO record)
        {
            if (record == null) return;
            EventManager.Instance.RaiseStoryRecordShown(record);
        }

        // StoryDirector used to own the archive step; with it gone the bridge completes the loop so
        // the badge HUD and Records panel keep reacting.
        private void HandleArchiveRequested(RecordCardSO record)
        {
            if (record == null) return;
            EventManager.Instance.RaiseRecordArchived(record);
        }
    }
}
