using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// หน้าต่างสรุปคีย์ลัด — เปิด/ปิดด้วยปุ่ม "คีย์ลัด" ใน HUD หรือแป้น F1
    /// เป็น panel อ่านอย่างเดียว ไม่หยุดเวลา (เปิดดูระหว่างเล่นได้ — ใช้ unscaled input จึงกดได้แม้ pause)
    /// </summary>
    public class HotkeyHelpController : MonoBehaviour
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
            helpPanel.SetActive(show);
            // เปิด → ดันไปบนสุดของ Canvas กันแผงอื่น (Codex ฯลฯ ที่สร้างทีหลัง) วาดทับ
            if (show) helpPanel.transform.SetAsLastSibling();
        }
    }
}
