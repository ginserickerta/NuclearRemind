using UnityEditor;
using UnityEngine;
using NuclearReMind;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Bake แผง Building UI (ที่ BuildingUpgradeUI สร้างสดด้วยโค้ด) ออกมาเป็น prefab ที่ "แก้ layout ด้วยตา" ใน Editor ได้
    ///   • สร้าง Assets/Resources/BuildingUI/BuildingStatusPanel.prefab — เก็บ visual + BuildingPanelRefs (ref ทุกชิ้น wire อัตโนมัติ)
    ///   • prefab นี้ "ไม่มี" BuildingUpgradeUI (ลบออกตอน bake) → กัน singleton ชนกับ component ในซีน
    ///   • runtime: BuildingUpgradeUI ในซีนจะ Instantiate prefab นี้ + bind ref แทนการสร้างสด (ไม่มี prefab = สร้างสดเหมือนเดิม)
    ///   • เปิด prefab → ลาก/ปรับ layout·สี ได้เลย · กด Play = เห็นผลจริง (logic ยังผูกครบ)
    ///
    /// ⚠ รันซ้ำ = เขียนทับ prefab เดิม → งานที่แก้มือหาย · ลบ prefab = กลับไปใช้โค้ดสร้างสด
    /// ไม่อยู่ใน Run All Setups (กันเขียนทับงานที่ปรับเอง)
    /// </summary>
    public static class BuildingPanelBaker
    {
        private const string Dir  = "Assets/Resources/BuildingUI";
        private const string Path = Dir + "/BuildingStatusPanel.prefab";

        [MenuItem("NuclearReMind/UI/Bake Building Panel Prefab")]
        public static void Bake()
        {
            EnsureFolder();

            var go = new GameObject("BuildingStatusPanel", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var comp = go.AddComponent<BuildingUpgradeUI>();
            comp.BuildForBake();

            var refs = go.AddComponent<BuildingPanelRefs>();
            comp.CopyRefsTo(refs);

            Object.DestroyImmediate(comp);   // เหลือแค่ visual + BuildingPanelRefs (กัน singleton ชน)

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, Path);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();

            if (prefab != null)
            {
                EditorGUIUtility.PingObject(prefab);
                Selection.activeObject = prefab;
                Debug.Log($"[BuildingPanelBaker] ✅ bake แล้ว: {Path} — ดับเบิลคลิกเปิดแก้ layout ด้วยตาได้เลย · กด Play = ใช้ prefab นี้");
            }
            else Debug.LogError("[BuildingPanelBaker] SaveAsPrefabAsset ล้มเหลว");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets/Resources", "BuildingUI");
        }
    }
}
