using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// ไอคอน "บันทึก" เล็ก ๆ มุมขวาล่าง HUD + notification badge (ระบบใหม่ — แทนการ์ดเด้งกลางจอ)
    ///   • มีบันทึกใหม่เข้ามา (OnRecordArchived) → เพิ่มตัวเลข badge + เด้ง (pulse) ไอคอน · ไม่หยุดเกม
    ///   • กดไอคอน → เปิดแผง Records (RecordsPanelController) แล้วเคลียร์ badge (อ่านแล้ว)
    /// badge นับเฉพาะบันทึกที่ "เข้ามาใหม่รอบนี้" — โหลดเซฟ rebuild list ตรง ๆ ไม่ยิง OnRecordArchived จึงไม่สแปม badge
    /// wire โดย RecordNotificationSetup (ไอคอน/วง badge/ตัวเลข)
    /// </summary>
    public class RecordNotificationHUD : MonoBehaviour
    {
        [Header("Wire โดย RecordNotificationSetup")]
        public Button iconButton;   // ไอคอนบันทึกมุมขวาล่าง
        public GameObject badge;    // วงกลมแดง (ซ่อนเมื่อไม่มีบันทึกใหม่)
        public Text badgeText;      // จำนวนบันทึกใหม่ที่ยังไม่ได้อ่าน

        [Header("Pulse (เด้งตอนมีบันทึกใหม่)")]
        public float pulseScale = 1.25f;
        public float pulseSeconds = 0.25f;

        private int _unread;
        private Coroutine _pulse;

        private void OnEnable()
        {
            EventManager.Instance.OnRecordArchived += HandleArchived;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnRecordArchived -= HandleArchived;
        }

        private void Start()
        {
            if (iconButton != null) iconButton.onClick.AddListener(OpenPanel);
            RefreshBadge();
        }

        private void HandleArchived(RecordCardSO record)
        {
            _unread++;
            RefreshBadge();
            Pulse();
        }

        private void OpenPanel()
        {
            var rp = RecordsPanelController.Instance;
            if (rp == null) return;
            rp.Toggle();
            if (rp.IsOpen) { _unread = 0; RefreshBadge(); } // เปิดดูแล้ว → เคลียร์ badge
        }

        private void RefreshBadge()
        {
            if (badge != null) badge.SetActive(_unread > 0);
            if (badgeText != null) badgeText.text = _unread > 99 ? "99+" : _unread.ToString();
        }

        private void Pulse()
        {
            if (iconButton == null) return;
            if (_pulse != null) StopCoroutine(_pulse);
            _pulse = StartCoroutine(PulseRoutine(iconButton.transform));
        }

        // เด้งขึ้น-ลง (unscaled — ทำงานแม้เกม pause) เรียกความสนใจว่ามีบันทึกใหม่
        private IEnumerator PulseRoutine(Transform t)
        {
            float half = Mathf.Max(0.01f, pulseSeconds * 0.5f);
            float e = 0f;
            while (e < half) { e += Time.unscaledDeltaTime; t.localScale = Vector3.one * Mathf.Lerp(1f, pulseScale, e / half); yield return null; }
            e = 0f;
            while (e < half) { e += Time.unscaledDeltaTime; t.localScale = Vector3.one * Mathf.Lerp(pulseScale, 1f, e / half); yield return null; }
            t.localScale = Vector3.one;
            _pulse = null;
        }
    }
}
