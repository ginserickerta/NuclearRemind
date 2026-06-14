using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Tooltip 3 ชั้น: ชั้น 1 ชื่อ+cost / ชั้น 2 description (gameplay) / ชั้น 3 nuclearKnowledge (ความรู้จริง)
    /// แสดง/ซ่อนตาม EventManager.OnBuildingSelected (raise จาก PlacementController ตอนเลือกอาคารวาง)
    /// </summary>
    public class TooltipController : MonoBehaviour
    {
        public static TooltipController Instance { get; private set; }

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
            if (tooltipPanel != null) tooltipPanel.SetActive(false);
        }

        private void HandleBuildingSelected(BuildingData data)
        {
            if (data == null)
            {
                if (tooltipPanel != null) tooltipPanel.SetActive(false);
                return;
            }

            if (tooltipPanel != null) tooltipPanel.SetActive(true);

            if (nameCostText != null)
                nameCostText.text = $"{data.buildingName}\nMaterial {data.materialCost} / Energy {data.energyCost} / Worker {data.workerRequired}";

            if (descriptionText != null)
                descriptionText.text = data.description;

            if (nuclearKnowledgeText != null)
                nuclearKnowledgeText.text = data.nuclearKnowledge;
        }
    }
}
