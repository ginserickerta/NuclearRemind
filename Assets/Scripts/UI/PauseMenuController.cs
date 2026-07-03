using UnityEngine;
using UnityEngine.SceneManagement;

namespace NuclearReMind
{
    /// <summary>
    /// เมนูหยุดชั่วคราวในเกม (กด ESC): เล่นต่อ / เริ่มใหม่ / กลับเมนูหลัก / ออกจากเกม
    /// อยู่บน Canvas แยก (PauseCanvas, sortingOrder สูง) จึงไม่โดน Setup HUD Canvas ลบทิ้ง
    ///
    /// การหยุด: เรียกผ่าน GameManager.SetSpeed(0) (= GameState.Paused, timeScale 0 แต่คง GameSpeed)
    /// เล่นต่อ = SetSpeed(GameSpeed เดิม) — sync กับปุ่มความเร็ว/แป้น Space ผ่าน OnGameStateChanged
    /// (ถ้าผู้เล่นกด Space เล่นต่อระหว่างเมนูเปิด → ปิดเมนูให้อัตโนมัติ)
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Wiring (ผูกโดย MenuSystemSetup)")]
        public GameObject pausePanel;

        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private bool _isOpen;

        private void OnEnable()
        {
            if (EventManager.Instance != null)
                EventManager.Instance.OnGameStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (EventManager.Instance != null)
                EventManager.Instance.OnGameStateChanged -= HandleStateChanged;
        }

        private void Start()
        {
            if (pausePanel != null) pausePanel.SetActive(false);
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            // เกมจบแล้ว (แพ้/ชนะ) → ไม่เปิดเมนูหยุด
            var gm = GameManager.Instance;
            if (gm != null && (gm.CurrentState == GameManager.GameState.GameOver
                            || gm.CurrentState == GameManager.GameState.Victory))
                return;

            if (_isOpen) Resume();
            else Open();
        }

        private void Open()
        {
            _isOpen = true;
            if (pausePanel != null) pausePanel.SetActive(true);
            GameManager.Instance?.SetSpeed(0f); // → Paused (timeScale 0, คง GameSpeed)
        }

        /// <summary>เล่นต่อ — คืนความเร็วที่เลือกไว้ก่อนหยุด</summary>
        public void Resume()
        {
            _isOpen = false;
            if (pausePanel != null) pausePanel.SetActive(false);

            var gm = GameManager.Instance;
            if (gm != null) gm.SetSpeed(gm.GameSpeed > 0f ? gm.GameSpeed : 1f);
            else Time.timeScale = 1f;
        }

        /// <summary>เริ่มเกมใหม่ (คลังความรู้ถาวรคงอยู่ — GameManager.Restart)</summary>
        public void Restart()
        {
            _isOpen = false;
            if (pausePanel != null) pausePanel.SetActive(false);
            Time.timeScale = 1f;
            GameManager.Instance?.Restart();
        }

        /// <summary>กลับเมนูหลัก — โหลดซีน MainMenu (MainMenuController จะล้าง singleton ค้าง)</summary>
        public void GoToMainMenu()
        {
            _isOpen = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        /// <summary>ออกจากเกม (ใน editor = หยุด Play)</summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // sync กับการเล่นต่อจากทางอื่น (แป้น Space/ปุ่มความเร็ว): ถ้าเกมกลับมา Playing ระหว่างเมนูเปิด → ปิดเมนู
        private void HandleStateChanged(GameManager.GameState state)
        {
            if (!_isOpen) return;
            if (state == GameManager.GameState.Playing
             || state == GameManager.GameState.GameOver
             || state == GameManager.GameState.Victory)
            {
                _isOpen = false;
                if (pausePanel != null) pausePanel.SetActive(false);
            }
        }
    }
}
