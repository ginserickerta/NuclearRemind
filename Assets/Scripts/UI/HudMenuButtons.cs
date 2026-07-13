using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ตัวกลางของ "แถบปุ่มด้านข้าง HUD" (ไอเทม/คีย์ลัด/Codex/บันทึก) — ให้ปุ่มสไปรต์ผูก onClick
    /// มาที่เมท็อดพวกนี้ได้ตอน edit-time (คอมโพเนนต์อยู่ในซีน → AddPersistentListener ติดถาวร)
    ///
    /// เหตุที่ต้องมีตัวกลาง: InventoryGridUI สร้างตอน runtime (AutoSpawn) จึงผูกปุ่มจาก editor ตรง ๆ ไม่ได้
    /// · บันทึกต้องยิงผ่าน EventManager (ห้ามเรียก manager ตรงข้ามระบบ) · Codex/คีย์ลัด toggle ผ่าน controller
    /// </summary>
    public class HudMenuButtons : MonoBehaviour
    {
        private HotkeyHelpController _help;

        private void Start()
        {
            _help = FindFirstObjectByType<HotkeyHelpController>();
        }

        /// <summary>เปิด/ปิดคลังไอเทม (ปุ่ม ไอเทม · คีย์ I) — หา InventoryGridUI ตอนคลิก (auto-spawn แล้ว)</summary>
        public void ToggleInventory()
        {
            var inv = FindFirstObjectByType<InventoryGridUI>(); // ตอนคลิก ไม่ใช่ใน Update — ปลอดภัยตามกติกา
            if (inv != null) inv.Toggle();
        }

        /// <summary>เปิด/ปิดหน้าต่างคีย์ลัด (ปุ่ม คีย์ลัด · คีย์ F1)</summary>
        public void ToggleHotkeyHelp()
        {
            if (_help == null) _help = FindFirstObjectByType<HotkeyHelpController>();
            if (_help != null) _help.Toggle();
        }

        /// <summary>เปิด/ปิดคลังความรู้ (ปุ่ม Codex · คีย์ C)</summary>
        public void ToggleCodex()
        {
            if (CodexUIController.Instance != null) CodexUIController.Instance.Toggle();
        }

        /// <summary>บันทึกเกม (ปุ่ม บันทึก · คีย์ F5) — ยิงผ่าน EventManager</summary>
        public void RequestSave()
        {
            if (EventManager.Instance != null) EventManager.Instance.RaiseSaveRequested();
        }
    }
}
