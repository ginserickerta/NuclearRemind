using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// เหตุที่ทำให้ "นาฬิกาวัน" หยุด (V4 §15/§16) — ซ้อนกันได้หลายเหตุ
    /// </summary>
    public enum PauseReason { Placement, QuizPopup, CrisisPopup, Manual, Demolition }

    /// <summary>
    /// จัดการ "นาฬิกาวัน" หยุด/เดิน ผ่าน pause-reason stack (V4 §15/§16)
    /// IsRunning == (ไม่มีเหตุหยุดใดเลย) — GameManager/ResourceManager เดินเวลาเฉพาะตอน IsRunning
    /// หยุดเฉพาะนาฬิกาวัน ไม่แตะ Time.timeScale → UI/ghost ยังเลื่อนได้ลื่น (V4 §15)
    /// </summary>
    public class TimeManager : MonoBehaviour
    {
        public static TimeManager Instance { get; private set; }

        private readonly HashSet<PauseReason> _activePauses = new HashSet<PauseReason>();

        /// <summary>นาฬิกาวันเดินต่อเมื่อไม่มีเหตุหยุดใดเลย</summary>
        public bool IsRunning => _activePauses.Count == 0;

        public bool IsPaused(PauseReason reason) => _activePauses.Contains(reason);
        public int ActivePauseCount => _activePauses.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>เพิ่มเหตุหยุด (เช่นเปิด Build Menu / quiz / crisis popup) — ซ้อนได้</summary>
        public void Pause(PauseReason reason) => _activePauses.Add(reason);

        /// <summary>ปลดเหตุหยุด — นาฬิกาเดินต่อเมื่อไม่เหลือเหตุใดเลย</summary>
        public void Resume(PauseReason reason) => _activePauses.Remove(reason);
    }
}
