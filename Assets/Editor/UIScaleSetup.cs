using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: สคริปต์ Editor ปรับขนาด UI/ตัวอักษรทั้งเกมจากค่าเดียว (UiScale) ผ่าน CanvasScaler
    /// ขนาด UI ทั้งเกม — คุมจากค่าเดียว (UiScale)
    ///
    /// ทำไมไม่ไล่เพิ่ม fontSize ทีละตัว:
    ///   HUD วาง Text/ปุ่มด้วย anchoredPosition + sizeDelta คงที่ (ไม่มี layout group ส่วนใหญ่)
    ///   ถ้าเพิ่มแต่ font ข้อความไทยจะล้นกล่อง/ทับแถวข้าง ๆ (ดูคอมเมนต์ ShrinkButtonLabel ใน HUDCanvasSetup)
    ///   CanvasScaler แบบ ScaleWithScreenSize ขยาย "ทุกอย่าง" พร้อมกัน — สัดส่วนเดิม ไม่มีอะไรล้น
    ///
    /// วิธีคิด: referenceResolution เล็กลง = scaleFactor โตขึ้น
    ///   1920/1.25 = 1536 → บนจอ 1920 ทุกอย่างใหญ่ขึ้น 1.25 เท่า (font 13px วาดจริง ~16px)
    ///
    /// เพดานความปลอดภัย: element กว้างสุดในเกมคือ 1200 ref-px (GameOverText / ชื่อเกมในเมนู)
    ///   UiScale 1.6 → ref width 1200 = เต็มจอพอดี ⇒ อย่าเกิน ~1.5 ถ้าไม่ย่อ element พวกนั้นด้วย
    ///
    /// idempotent: เขียนค่าสัมบูรณ์ทับ — รันซ้ำกี่ครั้งก็ได้ผลเท่าเดิม (ไม่คูณสะสม)
    /// </summary>
    public static class UIScaleSetup
    {
        /// <summary>ตัวคูณขนาด UI เทียบจอ 1920×1080 — แก้ค่านี้ค่าเดียวแล้วรันเมนูใหม่</summary>
        public const float UiScale = 1.25f;

        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";

        /// <summary>ค่าที่ CanvasScaler ทุกตัวต้องใช้ — setup script ที่สร้าง Canvas อ่านจากที่นี่</summary>
        public static Vector2 ReferenceResolution =>
            new Vector2(DesignWidth / UiScale, DesignHeight / UiScale);

        // เมนูนี้: เขียนค่า referenceResolution ใหม่ให้ CanvasScaler ทุกตัว (ซีนเกม + เมนูหลัก)
        [MenuItem("NuclearReMind/Apply UI Scale (Font Size)")]
        public static void Apply()
        {
            int changed = ApplyToLoadedScenes();
            changed += ApplyToMainMenu();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[UIScaleSetup] UI ×{UiScale} (reference {ReferenceResolution.x}×{ReferenceResolution.y}) " +
                      $"— ปรับ CanvasScaler {changed} ตัว");
        }

        // ซีนที่เปิดอยู่ (Gamescene ตอนรันจาก Run All Setups)
        private static int ApplyToLoadedScenes()
        {
            int changed = 0;
            foreach (var scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsSortMode.None))
                if (ApplyTo(scaler, "Apply UI Scale")) changed++;
            return changed;
        }

        // MainMenu เปิดแบบ additive → ไม่สลับ active scene ของผู้ใช้/ของ chain
        private static int ApplyToMainMenu()
        {
            var loaded = SceneManager.GetSceneByPath(MainMenuPath);
            if (loaded.isLoaded) return 0; // ApplyToLoadedScenes จัดการไปแล้ว

            var scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Additive);
            if (!scene.IsValid())
            {
                Debug.LogWarning($"[UIScaleSetup] เปิด {MainMenuPath} ไม่ได้ — ข้ามเมนูหลัก");
                return 0;
            }

            int changed = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var scaler in root.GetComponentsInChildren<CanvasScaler>(true))
                    if (ApplyTo(scaler, null)) changed++;

            if (changed > 0) EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
            return changed;
        }

        // undoLabel = null ตอนแก้ซีน additive (Undo ข้ามซีนทำให้ reference ค้าง)
        private static bool ApplyTo(CanvasScaler scaler, string undoLabel)
        {
            var target = ReferenceResolution;
            if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
                && scaler.referenceResolution == target)
                return false;

            if (undoLabel != null) Undo.RecordObject(scaler, undoLabel);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = target;
            EditorUtility.SetDirty(scaler);
            return true;
        }
    }
}
