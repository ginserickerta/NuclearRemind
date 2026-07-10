using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// นำไอคอนที่สไลซ์จาก atlas (1.png) เข้าเกม — idempotent · surgical (ไม่ rebuild HUD ทั้งก้อน)
    ///   1. Build Menu icons  → BuildingSelectionUI.menuIcons (map ตามชื่อ asset อาคาร)
    ///   2. Hammer icon       → ปุ่มทุบอาคาร (BuildingSelectionUI.hammerIcon)
    ///   3. Cursors           → CursorManager (arrow ปกติ / hand hover / hammer demolish)
    ///   4. Speed icons       → ปุ่ม Pause/Normal/Fast ใน SpeedPanel (set sprite ตรง ๆ)
    /// </summary>
    public static class AtlasUISetup
    {
        private const string ScenePath   = "Assets/Scenes/Gamescene.unity";
        private const string BuildMenuDir = "Assets/Sprites/Icons/BuildMenu/";
        private const string IconsDir     = "Assets/Sprites/Icons/";
        private const string CursorsDir   = "Assets/Sprites/Cursors/";

        // ชื่อ asset อาคาร → ไฟล์ไอคอน Build Menu (อาคารที่ไม่มีในนี้ = ใช้ sprite เดิม)
        private static readonly Dictionary<string, string> IconMap = new Dictionary<string, string>
        {
            { "PowerPlant", "PowerPlant" }, { "WaterPlant", "WaterPlant" }, { "Farm", "Farm" },
            { "Mine", "Mine" }, { "Hospital", "Hospital" }, { "Laboratory", "ResearchLab" },
            { "CoreTower", "CoreTower" }, { "RadiationShelter", "Shelter" },
        };

        [MenuItem("NuclearReMind/Setup/Atlas UI (building icons + cursors + speed)")]
        public static void Run()
        {
            // 1) import settings
            foreach (var p in Directory.GetFiles(BuildMenuDir, "*.png")) ConfigureSprite(p);
            ConfigureSprite(IconsDir + "SpeedPause.png");
            ConfigureSprite(IconsDir + "SpeedNormal.png");
            ConfigureSprite(IconsDir + "SpeedFast.png");
            ConfigureSprite(IconsDir + "Hammer.png");
            foreach (var p in Directory.GetFiles(CursorsDir, "*.png")) ConfigureCursor(p);
            AssetDatabase.SaveAssets();

            // 2) scene wiring
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            WireSpeedIcons();
            WireBuildingIcons();
            WireCursorManager();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[AtlasUISetup] เสร็จ — building icons + hammer + cursors + speed icons");
        }

        // ── ปุ่ม speed: set sprite กรอบ + ล้างตัวอักษร II/1x/2x ──
        private static void WireSpeedIcons()
        {
            SetButtonSprite("PauseButton",  IconsDir + "SpeedPause.png");
            SetButtonSprite("NormalButton", IconsDir + "SpeedNormal.png");
            SetButtonSprite("FastButton",   IconsDir + "SpeedFast.png");
        }

        private static void SetButtonSprite(string goName, string spritePath)
        {
            var go = GameObject.Find(goName);
            if (go == null) { Debug.LogWarning($"[AtlasUISetup] ไม่พบปุ่ม {goName}"); return; }
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            var img = go.GetComponent<Image>();
            if (img != null && sp != null) { img.sprite = sp; img.color = Color.white; }
            var label = go.GetComponentInChildren<Text>(true);
            if (label != null) label.text = "";
            EditorUtility.SetDirty(go);
        }

        // ── BuildingSelectionUI: menuIcons ตาม asset อาคาร + hammer ──
        private static void WireBuildingIcons()
        {
            var selUI = Object.FindFirstObjectByType<BuildingSelectionUI>(FindObjectsInactive.Include);
            if (selUI == null) { Debug.LogWarning("[AtlasUISetup] ไม่พบ BuildingSelectionUI"); return; }

            var buildings = selUI.buildings;
            if (buildings == null || buildings.Length == 0)
            {
                var pc = Object.FindFirstObjectByType<PlacementController>(FindObjectsInactive.Include);
                if (pc != null && pc.buildingHotbar != null)
                {
                    buildings = pc.buildingHotbar;
                    selUI.buildings = buildings;
                }
            }
            if (buildings == null) { Debug.LogWarning("[AtlasUISetup] buildings ว่าง"); return; }

            var icons = new Sprite[buildings.Length];
            int mapped = 0;
            for (int i = 0; i < buildings.Length; i++)
            {
                if (buildings[i] == null) continue;
                string assetName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(buildings[i]));
                if (!string.IsNullOrEmpty(assetName) && IconMap.TryGetValue(assetName, out var iconName))
                {
                    icons[i] = AssetDatabase.LoadAssetAtPath<Sprite>(BuildMenuDir + iconName + ".png");
                    if (icons[i] != null) mapped++;
                }
            }
            selUI.menuIcons = icons;
            selUI.hammerIcon = AssetDatabase.LoadAssetAtPath<Sprite>(IconsDir + "Hammer.png");
            EditorUtility.SetDirty(selUI);
            Debug.Log($"[AtlasUISetup] wire building icons {mapped}/{buildings.Length}");
        }

        // ── CursorManager: สร้าง/หา แล้ว set textures ──
        private static void WireCursorManager()
        {
            var go = GameObject.Find("CursorManager") ?? new GameObject("CursorManager");
            var cm = go.GetComponent<CursorManager>() ?? go.AddComponent<CursorManager>();
            cm.arrowCursor  = AssetDatabase.LoadAssetAtPath<Texture2D>(CursorsDir + "CursorArrow.png");
            cm.handCursor   = AssetDatabase.LoadAssetAtPath<Texture2D>(CursorsDir + "CursorHand.png");
            cm.hammerCursor = AssetDatabase.LoadAssetAtPath<Texture2D>(CursorsDir + "CursorHammer.png");
            EditorUtility.SetDirty(cm);
        }

        private static void ConfigureSprite(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogWarning($"[AtlasUISetup] ไม่พบไฟล์: {path}"); return; }
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }

        private static void ConfigureCursor(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogWarning($"[AtlasUISetup] ไม่พบ cursor: {path}"); return; }
            ti.textureType = TextureImporterType.Cursor; // readable + RGBA พร้อมใช้เป็นเคอร์เซอร์
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
    }
}
