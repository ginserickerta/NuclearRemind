using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ประกาศฉุกเฉิน (V4 §11) — ผู้เล่นเลือกใช้เพื่อได้แรงงานหล่อเย็นเพิ่ม แลกกับ Hope
    /// CoolingLaborBonus ป้อนสูตรหล่อเย็น CORE (§8) · Hope ดิ่งทันที + ต่อวัน · ผูกควิซ Q10
    /// </summary>
    public class DecreeManager : MonoBehaviour
    {
        public static DecreeManager Instance { get; private set; }

        [Header("Decrees (V4 §11 — ตั้งโดย Setup Decrees)")]
        public DecreeSO[] decrees;

        private readonly List<DecreeSO> _active = new List<DecreeSO>();

        /// <summary>แรงงานหล่อเย็นรวมจาก decree ที่ประกาศใช้ (เข้าสูตรหล่อเย็นเตา §8)</summary>
        public int CoolingLaborBonus { get; private set; }

        public bool IsActive(DecreeSO d) => _active.Contains(d);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnEnactDecreeRequested += EnactDecree;
            EventManager.Instance.OnDayEnded += HandleDayEnded;
            EventManager.Instance.OnDilemmaResolved += HandleDilemmaResolved;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnEnactDecreeRequested -= EnactDecree;
            EventManager.Instance.OnDayEnded -= HandleDayEnded;
            EventManager.Instance.OnDilemmaResolved -= HandleDilemmaResolved;
        }

        /// <summary>ประกาศใช้ decree index (จากปุ่ม UI) — ได้หล่อเย็น + Hope ดิ่งทันที + เด้ง Q10</summary>
        public void EnactDecree(int index)
        {
            if (decrees == null || index < 0 || index >= decrees.Length) return;

            var d = decrees[index];
            if (d == null || _active.Contains(d)) return; // ประกาศแล้ว

            _active.Add(d);
            CoolingLaborBonus += d.coolingLaborGain;

            if (d.hopeImmediate != 0f)
                EventManager.Instance.RaiseMoraleDelta(d.hopeImmediate);

            // ★ v6.3: ไม่เด้งควิซผูกประกาศแล้ว — linkedQuizIds ชี้ไปที่ควิซชุด v4.1 ที่ quizId ว่าง
            // ตอบแล้วไม่ได้รางวัลอะไร (ดู OreDepositManager.ShowAlaraQuiz) · ควิซ v6.3 อยู่ใน Codex

            Debug.Log($"[Decree] ประกาศ {d.id}: +{d.coolingLaborGain} หล่อเย็น, Hope {d.hopeImmediate}");
        }

        // แรงงานหล่อเย็นจากทางเลือกวิกฤต (Story Guide decree_emergency: effects coolingWorkers)
        // เช่น ประกาศฉุกเฉิน B/C — Hope/ควิซเดินทาง DilemmaManager ตามปกติ ที่นี่รับแค่แต้มหล่อเย็น
        private void HandleDilemmaResolved(DilemmaData dilemma, int choiceIndex)
        {
            int gain = dilemma != null ? dilemma.GetCoolingWorkers(choiceIndex) : 0;
            if (gain <= 0) return;

            CoolingLaborBonus += gain;
            Debug.Log($"[Decree] {dilemma.dilemmaId} ทางเลือก {(char)('A' + choiceIndex)}: แรงงานหล่อเย็น +{gain}");
        }

        // Hope ดิ่งต่อวันระหว่าง decree ยังใช้อยู่ (V4 §11)
        private void HandleDayEnded(int day)
        {
            foreach (var d in _active)
                if (d != null && d.hopePerDay != 0f)
                    EventManager.Instance.RaiseMoraleDelta(d.hopePerDay);
        }
    }
}
