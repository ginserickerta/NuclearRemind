using UnityEditor;
using UnityEngine;
using NuclearReMind;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Bake แผง CORE TOWER (ที่ CoreTowerPanelUI สร้างสดด้วยโค้ด) ออกมาเป็น prefab ที่ "แก้ layout ด้วยตา" ใน Editor ได้
    ///   • สร้าง Assets/Resources/CoreTowerUI/CoreTowerPanel.prefab — ref ทุกชิ้น (ปุ่ม/ข้อความ/หลอด) ถูก wire อัตโนมัติ
    ///   • runtime: CoreTowerPanelUI.AutoSpawn จะ Instantiate prefab นี้แทนการสร้างสด (ไม่มี prefab = fallback สร้างสดเหมือนเดิม)
    ///   • เปิด prefab → ลาก/ปรับตำแหน่ง·ขนาด·สี ได้เลย · กด Play = เห็นผลจริง (logic/ข้อมูลยังผูกครบ)
    ///
    /// ⚠ รันซ้ำ = เขียนทับ prefab เดิม → งานที่แก้มือใน prefab หาย · bake ใหม่เฉพาะตอนอยากรีเซ็ตกลับ layout จากโค้ด
    /// ไม่อยู่ใน Run All Setups (กันเขียนทับงานที่ปรับเอง) · ลบ prefab = กลับไปใช้โค้ดสร้างสด
    /// </summary>
    public static class CoreTowerPanelBaker
    {
        private const string Dir  = "Assets/Resources/CoreTowerUI";
        private const string Path = Dir + "/CoreTowerPanel.prefab";

        [MenuItem("NuclearReMind/UI/Bake Core Tower Panel Prefab")]
        public static void Bake()
        {
            EnsureFolder();

            // root = RectTransform เต็มจอ (แทน canvas-child backdrop เดิม) → build ใต้ตัวเอง
            var go = new GameObject("CoreTowerPanel", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var comp = go.AddComponent<CoreTowerPanelUI>();
            comp.BuildForBake();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, Path);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();

            if (prefab != null)
            {
                EditorGUIUtility.PingObject(prefab);
                Selection.activeObject = prefab;
                Debug.Log($"[CoreTowerPanelBaker] ✅ bake แล้ว: {Path} — ดับเบิลคลิกเปิดแก้ layout ด้วยตาได้เลย · กด Play = ใช้ prefab นี้");
            }
            else Debug.LogError("[CoreTowerPanelBaker] SaveAsPrefabAsset ล้มเหลว");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets/Resources", "CoreTowerUI");
        }
    }
}
