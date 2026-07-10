using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// นำเข้าอาร์ตไอคอนทรัพยากร (Assets/Sprites/Icons) เป็น Sprite แล้ว rebuild HUD
    /// ให้ ResourcePanel/PopulationPanel ใช้ไอคอนจริงแทน emoji/placeholder · idempotent
    ///
    /// map: Food·Water·Iron(วัสดุ)·Energy·Deuterium·Tritium → ResourcePanel · Knowledge → PopulationPanel
    /// (Electric/Fire/Plasma/Radiation/Smoke/Quest/Warning/Crate นำเข้าไว้ให้ใช้ต่อกับ UI เตา/เตือน)
    /// </summary>
    public static class HUDIconSetup
    {
        private const string Dir = "Assets/Sprites/Icons/";
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";

        private static readonly string[] Icons =
        {
            "Food", "Water", "Iron", "Energy", "Deuterium", "Tritium", "Knowledge",
            "Electric", "Fire", "Plasma", "Radiation", "Smoke", "Quest", "Warning", "Crate",
        };

        [MenuItem("NuclearReMind/Setup/Resource Icons + Rebuild HUD")]
        public static void Run()
        {
            // 1) ตั้งค่า import ไอคอนทั้งหมดเป็น Sprite (UI — ขนาดคุมด้วย RectTransform, PPU ไม่สำคัญ)
            int ok = 0;
            foreach (var name in Icons)
                if (ConfigureIcon(Dir + name + ".png")) ok++;
            AssetDatabase.SaveAssets();
            Debug.Log($"[HUDIconSetup] ตั้งค่า icon sprite {ok}/{Icons.Length} ไฟล์");

            // 2) rebuild HUD ในซีน (HUDCanvasSetup อ่านไอคอนจริงผ่าน LoadIcon)
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (GameObject.Find("UIManagerHUD") == null)
            {
                Debug.LogError("[HUDIconSetup] ไม่พบ UIManagerHUD ใน Gamescene — ข้าม rebuild HUD");
                return;
            }
            HUDCanvasSetup.SetupHUD();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[HUDIconSetup] เสร็จ — ResourcePanel/PopulationPanel ใช้ไอคอนจริงแล้ว");
        }

        private static bool ConfigureIcon(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[HUDIconSetup] ไม่พบไอคอน: {path}");
                return false;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return true;
        }
    }
}
