using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Popup สำหรับ Moral Dilemma — แสดง scenario + ปุ่ม 2 ตัวเลือกตาม EventManager.OnDilemmaTriggered
    /// เมื่อผู้เล่นกดปุ่ม จะ raise OnDilemmaResolved(dilemma, choiceA) ให้ DilemmaManager นำผลไปปรับ state
    /// </summary>
    public class DilemmaPopupController : MonoBehaviour
    {
        [Header("Panel")]
        public GameObject popupPanel;

        [Header("Texts")]
        public Text scenarioText;
        public Text choiceAText;
        public Text choiceBText;

        [Header("Buttons")]
        public Button choiceAButton;
        public Button choiceBButton;

        private DilemmaData _activeDilemma;

        private void OnEnable()
        {
            EventManager.Instance.OnDilemmaTriggered += HandleDilemmaTriggered;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDilemmaTriggered -= HandleDilemmaTriggered;
        }

        private void Start()
        {
            if (popupPanel != null) popupPanel.SetActive(false);
        }

        private void HandleDilemmaTriggered(DilemmaData dilemma)
        {
            _activeDilemma = dilemma;

            if (popupPanel != null) popupPanel.SetActive(true);
            if (scenarioText != null) scenarioText.text = dilemma.scenarioText;
            if (choiceAText != null) choiceAText.text = dilemma.choiceAText;
            if (choiceBText != null) choiceBText.text = dilemma.choiceBText;
        }

        public void ChooseA() => Resolve(true);
        public void ChooseB() => Resolve(false);

        private void Resolve(bool choiceA)
        {
            if (_activeDilemma == null) return;

            if (popupPanel != null) popupPanel.SetActive(false);
            EventManager.Instance.RaiseDilemmaResolved(_activeDilemma, choiceA);
            _activeDilemma = null;
        }
    }
}
