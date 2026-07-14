using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// นำเข้าสไปรต์ของประดับจาก Assets/Sprites/Decor/*.png (ตั้ง importer แบบ pixel-art · pivot ฐานล่างกลาง) +
    /// สร้าง/wire GameObject "DecorSpawner" ในซีน — idempotent · รันเมนูซ้ำได้
    ///
    /// ⚠ asset ของประดับยังไม่มาถึงเครื่องนี้ (อยู่ Downloads อีกเครื่อง) → เมนูนี้จะสร้างโฟลเดอร์ +
    /// spawner "เปล่า" ไว้ก่อน (ไม่โรยอะไร ไม่พัง) · ก็อป PNG ลง Assets/Sprites/Decor/ แล้วรันเมนูซ้ำ = โรยจริง
    /// </summary>
    public static class DecorSetup
    {
        private const string ScenePath   = "Assets/Scenes/Gamescene.unity";
        private const string DecorFolder = "Assets/Sprites/Decor";
        private const float  TargetWidthUnits = 2f;   // กว้างเป้าหมายต่อชิ้น (tile=1) → PPU = artW / 2 (เทียบ scale กับอาคาร)
        private const float  DefaultPPU = 256f;

        [MenuItem("NuclearReMind/Setup Decor")]
        public static void SetupAll()
        {
            EnsureFolder();

            var sprites = ImportDecorSprites();

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // idempotent — หา/สร้าง DecorSpawner (รันซ้ำ = ใช้ตัวเดิม)
            var spawnerGO = GameObject.Find("DecorSpawner");
            if (spawnerGO == null)
                spawnerGO = new GameObject("DecorSpawner");
            var spawner = spawnerGO.GetComponent<DecorSpawner>();
            if (spawner == null) spawner = spawnerGO.AddComponent<DecorSpawner>();

            spawner.decorSprites = sprites.ToArray();

            // ให้ apron ตรงกับ OreDepositManager ถ้ามีในซีน (ไม่งั้นคง default 35)
            var ore = Object.FindFirstObjectByType<OreDepositManager>();
            if (ore != null) spawner.apronMargin = ore.apronMargin;

            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (sprites.Count == 0)
                Debug.LogWarning($"[DecorSetup] ⚠ ไม่พบ PNG ใน {DecorFolder} — สร้าง DecorSpawner เปล่าไว้แล้ว " +
                                 "· ก็อปสไปรต์ของประดับลงโฟลเดอร์นี้แล้วรันเมนูซ้ำเพื่อโรยจริง");
            else
                Debug.Log($"[DecorSetup] ✅ นำเข้า {sprites.Count} สไปรต์ + wire DecorSpawner (apron {spawner.apronMargin}, density {spawner.densityPercent}%)");
        }

        /// <summary>ลบ DecorSpawner (auto โรยของประดับ Zone C) ออกจากซีน — ไว้วางเองด้วย DecorPiece แทน</summary>
        [MenuItem("NuclearReMind/Remove Decor Spawner (Zone C auto)")]
        public static void RemoveSpawner()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var go = GameObject.Find("DecorSpawner");
            if (go == null)
            {
                EditorUtility.DisplayDialog("Remove Decor Spawner",
                    "ไม่พบ DecorSpawner ในซีน — ไม่มี auto decor ให้ลบอยู่แล้ว (วางเองได้เลย)", "OK");
                return;
            }

            Undo.DestroyObjectImmediate(go);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[DecorSetup] ลบ DecorSpawner (auto decor Zone C) แล้ว — วางของประดับเองได้เลย (แนบ DecorPiece)");
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(DecorFolder)) return;
            if (!AssetDatabase.IsValidFolder("Assets/Sprites"))
                AssetDatabase.CreateFolder("Assets", "Sprites");
            AssetDatabase.CreateFolder("Assets/Sprites", "Decor");
            AssetDatabase.Refresh();
        }

        private static List<Sprite> ImportDecorSprites()
        {
            var result = new List<Sprite>();
            if (!AssetDatabase.IsValidFolder(DecorFolder)) return result;

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { DecorFolder });
            var paths = new List<string>();
            foreach (var guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                    paths.Add(p);
            }
            paths.Sort(System.StringComparer.OrdinalIgnoreCase); // ลำดับคงที่ → index สไปรต์คงที่ทุกครั้ง

            foreach (var path in paths)
            {
                ConfigureDecorImporter(path);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) result.Add(sprite);
                else Debug.LogWarning($"[DecorSetup] โหลด Sprite จาก {path} ไม่ได้ (ข้าม)");
            }
            return result;
        }

        // ตั้ง importer แบบ pixel-art (แนวเดียวกับ BuildingArtSetup.ConfigureImporter · pivot ฐานล่างกลาง)
        private static void ConfigureDecorImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            // ขนาดต้นฉบับ → PPU ให้กว้าง ~TargetWidthUnits tile (อ่านความกว้าง texture ที่ import แล้ว)
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            float ppu = (tex != null && tex.width > 0) ? tex.width / TargetWidthUnits : DefaultPPU;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;                    // pixel art ห้าม bilinear
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter; // ฐานล่างกลางนั่งบนพื้น apron
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }
    }
}
