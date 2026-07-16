using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// หน้าต่างสรุปคีย์ลัด — เปิด/ปิดด้วยปุ่ม "คีย์ลัด" ใน HUD หรือแป้น F1
    /// เป็น panel อ่านอย่างเดียว ไม่หยุดเวลา (เปิดดูระหว่างเล่นได้ — ใช้ unscaled input จึงกดได้แม้ pause)
    /// </summary>
    public class HotkeyHelpController : MonoBehaviour, GameUIStack.IPanel
    {
        [Header("Wiring (ผูกโดย Setup HUD Canvas)")]
        public GameObject helpPanel;

        private void Start()
        {
            if (helpPanel != null) helpPanel.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
                Toggle();
        }

        /// <summary>สลับเปิด/ปิดหน้าต่างคีย์ลัด (ผูกกับปุ่ม HUD + แป้น F1)</summary>
        public void Toggle()
        {
            if (helpPanel == null) return;
            bool show = !helpPanel.activeSelf;
            if (show)
            {
                UIPopIn.Ensure(helpPanel);
                helpPanel.SetActive(true);
                GameUIStack.Push(this); // ขึ้นบนสุด + ลงทะเบียน (บล็อก Pause / Esc=ปิด)
            }
            else
            {
                GameUIStack.Pop(this);
                UIPopIn.PlayClose(helpPanel); // หุบออก (Windows 11) แล้วปิดเอง
            }
        }

        // ── GameUIStack (แผงปิดได้: Esc=ปิดเหมือน ✕ · กติกากลางใน PauseMenuController) ──
        bool GameUIStack.IPanel.ClosableByEscape => true;
        void GameUIStack.IPanel.BringToFront() => GameUIStack.RaiseToTop(helpPanel);
        GameObject GameUIStack.IPanel.PanelRoot => helpPanel;
        void GameUIStack.IPanel.CloseFromStack()
        {
            GameUIStack.Pop(this);
            UIPopIn.PlayClose(helpPanel); // หุบออกแล้วปิดเอง
        }
    }
}
