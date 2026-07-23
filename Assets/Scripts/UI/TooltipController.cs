using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Tooltip 3 ชั้น: ชั้น 1 ชื่อ+cost / ชั้น 2 description (gameplay) / ชั้น 3 nuclearKnowledge (ความรู้จริง)
    /// แสดง/ซ่อนตาม EventManager.OnBuildingSelected (raise จาก PlacementController ตอนเลือกอาคารวาง)
    ///
    /// ★ 2026-07-22 — three cosmetic upgrades, all code-side so the scene stays untouched:
    ///   • background: metal frame sprite (Resources/CardUI/tooltip_frame, nine-sliced) replaces the
    ///     scene's flat Image; missing sprite → the scene look stays as-is
    ///   • open/close: shared UIPopIn animation instead of a raw SetActive snap
    ///   • always on top: an override-sorting Canvas at order 900 — above every HUD panel, below the
    ///     day-transition fade (1000), which must be able to cover everything
    /// </summary>
    public class TooltipController : MonoBehaviour
    {
        public static TooltipController Instance { get; private set; }

        // Above all HUD panels; DayTransitionUI owns 1000 and must stay on top of even this.
        private const int TopSortingOrder = 900;

        [Header("Panel")]
        public GameObject tooltipPanel;

        [Header("Layer 1 - Name + Cost")]
        public Text nameCostText;

        [Header("Layer 2 - Gameplay Description")]
        public Text descriptionText;

        [Header("Layer 3 - Nuclear Knowledge")]
        public Text nuclearKnowledgeText;

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
            EventManager.Instance.OnBuildingSelected += HandleBuildingSelected;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingSelected -= HandleBuildingSelected;
        }

        private void Start()
        {
            ApplySkin();
            if (tooltipPanel != null) tooltipPanel.SetActive(false);
        }

        // เปลี่ยนพื้นหลังเป็นกรอบโลหะ tooltip_frame (9-slice) — ไม่มีรูปใน Resources ก็คงหน้าตาเดิมจากซีน
        // Metal frame background (nine-sliced). The sprite carries its own interior fill, so the
        // Image tint goes white; no sprite in Resources → leave the scene-authored look alone.
        private void ApplySkin()
        {
            if (tooltipPanel == null) return;
            var img = tooltipPanel.GetComponent<Image>();
            if (img == null) return;
            var frame = Resources.Load<Sprite>("CardUI/tooltip_frame");
            if (frame == null) return;
            img.sprite = frame;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 3f; // 95px art border → ~32px on screen
            img.color = Color.white;
        }

        // ใส่ Canvas override-sorting ให้ tooltip ลอยเหนือทุกแผง HUD — ต้องเรียก "หลังแผง active" ทุกครั้งที่โชว์
        // Override-sorting canvas floats the tooltip above every sibling HUD panel regardless of
        // hierarchy order. No GraphicRaycaster on purpose: the tooltip is display-only and must
        // never swallow clicks meant for what's underneath it.
        //
        // ★ Must run AFTER the panel is active, on every show: Unity silently drops overrideSorting
        //   set on a disabled Canvas, so a one-time setup in Start() (where the panel is hidden)
        //   never sticks — which is exactly the bug this replaced.
        private void EnsureTopmost()
        {
            if (tooltipPanel == null || !tooltipPanel.activeInHierarchy) return;
            var canvas = tooltipPanel.GetComponent<Canvas>();
            if (canvas == null) canvas = tooltipPanel.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = TopSortingOrder;
        }

        // เลือกอาคารตอนจะวาง → เปิด tooltip + เติมข้อความ 3 ชั้น · data null = ยกเลิกเลือก → หุบปิด
        private void HandleBuildingSelected(BuildingData data)
        {
            if (data == null)
            {
                // PlayClose animates the shrink-out and deactivates itself; a panel that is already
                // hidden is deactivated immediately (no-op animation).
                if (tooltipPanel != null) UIPopIn.PlayClose(tooltipPanel);
                return;
            }

            // PlayOpen (not SetActive): selecting building B while A's tooltip is mid-close must
            // cancel the in-flight close, or the close coroutine would hide B's fresh tooltip.
            if (tooltipPanel != null) UIPopIn.PlayOpen(tooltipPanel, self: true);
            EnsureTopmost(); // after activation — see note on the method

            if (nameCostText != null)
                nameCostText.text = $"{data.buildingName}\nIron {data.ironCost} / Energy {data.energyCost} / Worker {data.workerRequired}";

            if (descriptionText != null)
                descriptionText.text = data.description;

            if (nuclearKnowledgeText != null)
                nuclearKnowledgeText.text = data.nuclearKnowledge;
        }
    }
}
