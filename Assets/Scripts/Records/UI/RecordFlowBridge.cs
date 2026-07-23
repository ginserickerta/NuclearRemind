using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: สะพาน event ฝั่งแสดงผลของระบบกู้บันทึก — รับ OnRecordRecovered จาก DataRecovery
    /// [TH] แล้วส่งต่อให้ UI เดิม (popup การ์ดบันทึก → เก็บเข้าแผง Records + badge แจ้งเตือน)
    /// [TH] เป็นแค่ท่อแสดงผล — การปลด LEAD จริงเกิดใน DataRecovery.UnlockNextRecord ไม่ใช่ที่นี่
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

        // [TH] เด้งบันทึกที่กู้ได้เป็นการ์ดไม้กลางจอ (RecordCardUI หยุดเวลาเองผ่าน pause-reason stack)
        // Pop the recovered log as the wooden record card (RecordCardUI pauses the clock itself —
        // pause-reason stack per §15, never timeScale).
        private void HandleRecordRecovered(RecordCardSO record)
        {
            if (record == null) return;
            EventManager.Instance.RaiseStoryRecordShown(record);
        }

        // [TH] ปิดลูปขั้น "เก็บเข้าแผง" แทน StoryDirector ที่ถูกปลดไป — ให้ badge HUD กับแผง Records ยังอัปเดต
        // StoryDirector used to own the archive step; with it gone the bridge completes the loop so
        // the badge HUD and Records panel keep reacting.
        private void HandleArchiveRequested(RecordCardSO record)
        {
            if (record == null) return;
            EventManager.Instance.RaiseRecordArchived(record);
        }
    }
}
