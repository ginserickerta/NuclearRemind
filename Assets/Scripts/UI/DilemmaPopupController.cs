using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Popup สำหรับ Moral Dilemma (V4 §10) — แสดง scenario + ปุ่ม 3 ตัวเลือก A/B/C
    /// เมื่อกดปุ่ม → raise OnDilemmaResolved(dilemma, choiceIndex) ให้ DilemmaManager นำผลไปปรับ state
    /// ปุ่ม C ซ่อนอัตโนมัติถ้า dilemma ไม่มี choiceCText (รองรับ dilemma 2 ทางแบบเดิม)
    /// </summary>
    public class DilemmaPopupController : MonoBehaviour
    {
        [Header("Panel")]
        public GameObject popupPanel;

        [Header("Texts")]
        public Text scenarioText;
        public Text choiceAText;
        public Text choiceBText;
        public Text choiceCText;

        [Header("Buttons")]
        public Button choiceAButton;
        public Button choiceBButton;
        public Button choiceCButton;

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
            if (choiceCText != null) choiceCText.text = dilemma.choiceCText;

            // ปุ่ม C โผล่เฉพาะเมื่อ dilemma มีทางเลือก C (รองรับทั้ง 2 และ 3 ทาง)
            bool hasC = !string.IsNullOrEmpty(dilemma.choiceCText);
            if (choiceCButton != null) choiceCButton.gameObject.SetActive(hasC);
        }

        public void ChooseA() => Resolve(0);
        public void ChooseB() => Resolve(1);
        public void ChooseC() => Resolve(2);

        private void Resolve(int choiceIndex)
        {
            if (_activeDilemma == null) return;

            if (popupPanel != null) popupPanel.SetActive(false);
            EventManager.Instance.RaiseDilemmaResolved(_activeDilemma, choiceIndex);
            _activeDilemma = null;
        }
    }
}
