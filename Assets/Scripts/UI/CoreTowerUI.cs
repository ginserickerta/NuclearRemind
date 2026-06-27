using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// B2 — Overclock UI: CORE% bar + HEAT bar + ปุ่ม 4 โหมด (Idle/Normal/Boost/Overdrive) + SCRAM
    /// ส่งคำสั่งผ่าน event (OnOverclockModeRequested / OnScramRequested) ไม่เรียก CoreTowerManager ตรง
    /// อัปเดตจาก OnTowerProgressChanged / OnOverclockModeChanged
    /// </summary>
    public class CoreTowerUI : MonoBehaviour
    {
        [Header("Bars / status")]
        public Slider coreBar;   // CORE% 0–100
        public Slider heatBar;   // HEAT 0–heatCap
        public Image heatFill;   // เปลี่ยนสีเตือนเมื่อใกล้ meltdown
        public Text statusText;

        [Header("Overclock buttons (Idle/Normal/Boost/Overdrive)")]
        public Button idleButton;
        public Button normalButton;
        public Button boostButton;
        public Button overdriveButton;
        public Button scramButton;

        [Header("Colors")]
        public Color selectedColor = new Color(0.3f, 0.7f, 1f);
        public Color idleColor = Color.white;
        public Color heatNormalColor = new Color(1f, 0.6f, 0.2f);
        public Color heatDangerColor = new Color(0.9f, 0.2f, 0.2f);

        private const float ScramHeatThreshold = 90f; // §3.3 (ตรงกับ CoreTowerManager.scramHeatThreshold)

        private static readonly string[] PhaseNames = { "Locked", "Cold Assembly", "Plasma Ramp", "Ignition" };

        private void OnEnable()
        {
            EventManager.Instance.OnTowerProgressChanged += HandleTowerProgress;
            EventManager.Instance.OnOverclockModeChanged += HandleModeChanged;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnTowerProgressChanged -= HandleTowerProgress;
            EventManager.Instance.OnOverclockModeChanged -= HandleModeChanged;
        }

        private void Start()
        {
            WireMode(idleButton, CoreTowerManager.ModeIdle);
            WireMode(normalButton, CoreTowerManager.ModeNormal);
            WireMode(boostButton, CoreTowerManager.ModeBoost);
            WireMode(overdriveButton, CoreTowerManager.ModeOverdrive);
            if (scramButton != null)
                scramButton.onClick.AddListener(() => EventManager.Instance.RaiseScramRequested());
        }

        private void WireMode(Button b, int mode)
        {
            if (b != null)
                b.onClick.AddListener(() => EventManager.Instance.RaiseOverclockModeRequested(mode));
        }

        private void HandleModeChanged(int mode) => Highlight(mode);

        private void HandleTowerProgress(TowerData d)
        {
            if (coreBar != null) { coreBar.maxValue = 100f; coreBar.value = d.corePercent; }
            if (heatBar != null) { heatBar.maxValue = Mathf.Max(1f, d.heatCap); heatBar.value = d.coreHeat; }
            if (heatFill != null)
                heatFill.color = (d.coreHeat >= d.heatCap * 0.8f) ? heatDangerColor : heatNormalColor;

            if (statusText != null)
            {
                if (!d.isUnlocked)
                    statusText.text = "CORE TOWER — ล็อก (Day 11)";
                else
                {
                    int p = Mathf.Clamp(d.currentPhase, 0, PhaseNames.Length - 1);
                    statusText.text = $"CORE {d.corePercent:0}% · {PhaseNames[p]} · HEAT {d.coreHeat:0}/{d.heatCap:0}";
                }
            }

            // Phase 1 ล็อกโหมด (เลือกได้เฉพาะ Phase 2–3) ; ก่อนปลดล็อกปิดหมด
            bool overclockable = d.isUnlocked && d.currentPhase >= 2;
            SetInteractable(idleButton, overclockable);
            SetInteractable(normalButton, overclockable);
            SetInteractable(boostButton, overclockable);
            SetInteractable(overdriveButton, overclockable);

            // SCRAM: HEAT >= 90 และ cooldown หมด
            SetInteractable(scramButton, d.isUnlocked && d.coreHeat >= ScramHeatThreshold && d.scramCooldown <= 0);

            Highlight(d.overclockMode);
        }

        private void Highlight(int mode)
        {
            Tint(idleButton, mode == CoreTowerManager.ModeIdle);
            Tint(normalButton, mode == CoreTowerManager.ModeNormal);
            Tint(boostButton, mode == CoreTowerManager.ModeBoost);
            Tint(overdriveButton, mode == CoreTowerManager.ModeOverdrive);
        }

        private void Tint(Button b, bool selected)
        {
            if (b == null || b.image == null) return;
            b.image.color = selected ? selectedColor : idleColor;
        }

        private static void SetInteractable(Button b, bool value)
        {
            if (b != null) b.interactable = value;
        }
    }
}
