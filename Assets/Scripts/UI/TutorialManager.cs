using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// แสดง tutorial popup หน้าเดียวเมื่อเกมเริ่ม — กดปิดได้ทันที
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Header("UI References")]
        public GameObject tutorialPanel;
        public Button dismissButton;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (tutorialPanel != null)
                tutorialPanel.SetActive(true);

            if (dismissButton != null)
                dismissButton.onClick.AddListener(Dismiss);
        }

        public void Dismiss()
        {
            if (tutorialPanel != null)
                tutorialPanel.SetActive(false);

            EventManager.Instance.RaiseTutorialComplete();
        }
    }
}
