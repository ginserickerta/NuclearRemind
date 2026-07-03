using UnityEngine;
using UnityEngine.SceneManagement;

namespace NuclearReMind
{
    /// <summary>
    /// ควบคุมเมนูหลัก (ซีน MainMenu — build index 0): เริ่มเกมใหม่ / ออกจากเกม
    /// ปุ่มผูก onClick มาที่ NewGame()/QuitGame() โดย MenuSystemSetup
    ///
    /// สำคัญ: GameManager กับ EventManager เป็น DontDestroyOnLoad — เมื่อกลับจาก Gamescene
    /// มาเมนูหลัก มันจะรอดข้ามซีนมาด้วย ทำให้ "เริ่มเกมใหม่" ได้ singleton เก่า (state ค้าง)
    /// จึงล้างทิ้งตอนเข้าเมนู เพื่อให้โหลด Gamescene รอบใหม่สร้าง manager สดทุกครั้ง
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string gameSceneName = "Gamescene";

        private void Awake()
        {
            CleanupPersistentSingletons();
        }

        private void Start()
        {
            // เผื่อ timeScale ค้าง 0 จากตอน pause ในเกมก่อนกลับเมนู — เมนูต้องเดินปกติ
            Time.timeScale = 1f;
        }

        /// <summary>เริ่มเกมใหม่ — โหลด Gamescene (manager สดจะ ApplyMetaProgress คืน Knowledge/Codex สะสม)</summary>
        public void NewGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameSceneName);
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

        // ล้าง singleton ที่รอดข้ามซีน (GameManager/EventManager) — Destroy แบบ deferred พอ
        // เพราะยังไม่ได้โหลด Gamescene จนกว่าผู้เล่นจะกด NewGame (คนละเฟรม)
        private static void CleanupPersistentSingletons()
        {
            if (GameManager.Instance != null)
                Destroy(GameManager.Instance.gameObject);
            if (EventManager.Instance != null)
                Destroy(EventManager.Instance.gameObject);
        }
    }
}
