using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Day 1 Tutorial (GDD §3) — checklist โต้ตอบ 3 ภารกิจ ติ๊กเองเมื่อผู้เล่นทำจริง:
    ///   1) เดินโรงงานพื้นฐาน 3 โรง (ไฟ/น้ำ/อาหาร)  2) ขยาย Shelter  3) จัดคนงานเข้าประจำอาคาร
    /// (ภารกิจฝึกคลาสเดิมย้ายพ้น Day 1 — ห้องปฏิบัติการปลดล็อกเฟส 2 วัน 6+ ตาม GDD §6)
    /// พาเนลมุมจอ ไม่บล็อกการเล่น · ทำครบ → ปุ่ม "เริ่ม Day 2" ทำงาน → RaiseTutorialComplete (จบ Day 1)
    /// อ่านสถานะผ่าน event เท่านั้น (OnBuildingPlaced / OnWorkerAssignmentChanged) — ไม่เรียก manager อื่นเปลี่ยนสถานะ
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Header("UI (wire โดย HUDCanvasSetup)")]
        public GameObject tutorialPanel;
        public Text task1Text;   // โรงงาน 3 โรง
        public Text task2Text;   // Shelter
        public Text task3Text;   // จัดคนงานประจำอาคาร
        public Button startButton;
        public Text startLabel;

        [Header("Colors")]
        public Color doneColor = new Color(0.2f, 0.55f, 0.33f);   // เขียว = เสร็จ
        public Color pendingColor = new Color(0.2f, 0.27f, 0.33f); // เข้ม = ยังไม่เสร็จ

        // ── task state ──
        private readonly HashSet<string> _factoryCats = new HashSet<string>(); // power/water/food
        private bool _shelterDone;
        private bool _assignDone;

        private bool FactoriesDone => _factoryCats.Count >= 3;
        public bool AllComplete => FactoriesDone && _shelterDone && _assignDone;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnBuildingPlaced += HandleBuildingPlaced;
            EventManager.Instance.OnWorkerAssignmentChanged += HandleWorkerAssignmentChanged;
            EventManager.Instance.OnDayStarted += HandleDayStarted; // พ้น Day 1 → ซ่อนพาเนล (กันค้างข้ามวัน)
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingPlaced -= HandleBuildingPlaced;
            EventManager.Instance.OnWorkerAssignmentChanged -= HandleWorkerAssignmentChanged;
            EventManager.Instance.OnDayStarted -= HandleDayStarted;
        }

        private void Start()
        {
            if (startButton != null)
                startButton.onClick.AddListener(OnStartClicked);

            // แสดงเฉพาะ Day 1 (tutorial) — วันอื่น (เช่นโหลดเซฟ) ไม่ต้องโชว์
            bool day1 = GameManager.Instance == null || GameManager.Instance.CurrentDay <= 1;
            if (tutorialPanel != null) tutorialPanel.SetActive(day1);
            Refresh();
        }

        // ── task detection (ผ่าน event) ──
        private void HandleBuildingPlaced(Cell cell, BuildingData data)
        {
            if (data == null) return;
            if (data.energyProduction > 0f) _factoryCats.Add("power");
            if (data.waterProduction  > 0f) _factoryCats.Add("water");
            if (data.foodProduction   > 0f) _factoryCats.Add("food");
            if (data.shelterCapacity  > 0)  _shelterDone = true;
            Refresh();
        }

        // จัดคนเข้าอาคารครั้งแรก (count > 0) = ภารกิจสำเร็จ — event นี้ยิงจาก WorkerAssignmentManager
        // (count 0 = ปลดคน/ทุบตึก ไม่นับ · ตอนโหลดเซฟ Day 1 ก็ยิง event นี้ ติ๊กให้ตามสถานะจริง)
        private void HandleWorkerAssignmentChanged(Vector2Int cell, int count)
        {
            if (count <= 0) return;
            _assignDone = true;
            Refresh();
        }

        // พ้น Day 1 (จบ tutorial ด้วยปุ่ม, debug end-day, หรือโหลดเซฟวัน 2+) → ซ่อนพาเนลถาวร
        // Start() เช็ก CurrentDay แค่ครั้งเดียว ถ้าวันเปลี่ยนทีหลังต้องมี hook นี้ ไม่งั้นพาเนลค้าง (บั๊กที่เจอ)
        private void HandleDayStarted(int day, bool timed)
        {
            if (day > 1 && tutorialPanel != null) tutorialPanel.SetActive(false);
        }

        // ── ui ──
        private void Refresh()
        {
            SetTask(task1Text, FactoriesDone, $"เดินโรงงานพื้นฐาน 3 โรง (ไฟ/น้ำ/อาหาร)  {_factoryCats.Count}/3");
            SetTask(task2Text, _shelterDone, "ขยาย Shelter เพิ่มเพดานประชากร");
            SetTask(task3Text, _assignDone, "จัดคนงานเข้าประจำอาคาร");

            if (startButton != null) startButton.interactable = AllComplete;
            if (startLabel != null)
                startLabel.text = AllComplete ? "เริ่ม Day 2 ▶" : $"ทำภารกิจให้ครบ ({DoneCount()}/3)";
        }

        private int DoneCount() => (FactoriesDone ? 1 : 0) + (_shelterDone ? 1 : 0) + (_assignDone ? 1 : 0);

        private void SetTask(Text label, bool done, string text)
        {
            if (label == null) return;
            label.text = (done ? "✅ " : "⬜ ") + text;
            label.color = done ? doneColor : pendingColor;
        }

        private void OnStartClicked()
        {
            if (!AllComplete) return; // ปุ่มถูกล็อกอยู่แล้ว แต่กันไว้อีกชั้น
            if (tutorialPanel != null) tutorialPanel.SetActive(false);
            EventManager.Instance.RaiseTutorialComplete(); // → GameManager จบ Day 1 → เริ่ม Day 2
        }
    }
}
