using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// ★ ทดลองล้วน (reversible) — สลับพื้นจาก Tilemap "Ground" (ทาสีต่อ tile) ไปเป็น
    /// สไปรต์เดียวแผ่นใหญ่ IsoGround_forUnity.png (เรนเดอร์จาก Blender · ไล่โซนเบคมาในภาพ)
    ///
    /// Apply  = ปิด (SetActive false — ไม่ลบ! = สำรองพื้นเดิมไว้) Ground tilemap แล้ววางสไปรต์ทดลอง
    ///          "GroundSpriteExperiment" ทับ ที่ sorting layer เดียวกับพื้นเดิม (อยู่ใต้อาคาร)
    /// Revert = ลบสไปรต์ทดลอง + เปิด Ground tilemap กลับมา (คืนสภาพเดิม 100%)
    ///
    /// ปรับสเกล/ตำแหน่งต่อได้ใน Inspector ของ GroundSpriteExperiment (มันคือของทดลอง)
    /// กริด 43×43 (tile 1×0.5) → พื้นเพชรกว้าง ~43 สูง ~21.5 หน่วย · ศูนย์กลาง (0, 10.5)
    /// PNG 3000×2040 → PPU 69.767 ให้กว้าง 43 พอดี · scaleY 0.735 บีบอัตราส่วนเพชรให้เป็น 2:1
    /// </summary>
    public static class GroundSpriteExperiment
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const string PngPath   = "Assets/Sprites/Experimental/IsoGround_forUnity.png";
        private const string GroundName = "Ground";
        private const string ExpName    = "GroundSpriteExperiment";

        // กริด 43×43, tileWidth 1 → กว้าง 43 หน่วย · PNG 3000 px → PPU 3000/43
        private const float TargetWidthUnits = 43f;
        private const int   PngWidthPx = 3000, PngHeightPx = 2040;
        // เพชรกริดสูงจริง ~21.5 หน่วย (row 0..42, tileHeight 0.5) — บีบ scaleY ให้ตรง
        private const float TargetHeightUnits = 21.5f;
        private static readonly Vector3 GridCenter = new Vector3(0f, 10.5f, 0f);

        [MenuItem("NuclearReMind/ทดลอง: ใช้ IsoGround เป็นพื้น (Apply)")]
        public static void Apply()
        {
            OpenSceneIfNeeded();

            Sprite sprite = ImportSprite();
            if (sprite == null)
            {
                EditorUtility.DisplayDialog("Ground Experiment", $"ไม่พบ/นำเข้าไม่ได้: {PngPath}", "OK");
                return;
            }

            var ground = GameObject.Find(GroundName);
            if (ground == null)
            {
                EditorUtility.DisplayDialog("Ground Experiment",
                    "ไม่พบ Tilemap 'Ground' ในซีน — เปิด Gamescene ก่อน", "OK");
                return;
            }

            // อ่าน sorting ของพื้นเดิม เพื่อวางสไปรต์ทดลองที่ความลึกเดียวกัน (ใต้อาคาร)
            int sortLayerId = 0, sortOrder = 0;
            var tmr = ground.GetComponent<TilemapRenderer>();
            if (tmr != null) { sortLayerId = tmr.sortingLayerID; sortOrder = tmr.sortingOrder; }

            // ★ สำรองพื้นเดิม = ปิดไว้เฉย ๆ (ไม่ลบ) → Revert เปิดกลับได้
            Undo.RegisterFullObjectHierarchyUndo(ground, "Ground Experiment");
            ground.SetActive(false);

            // ลบของทดลองเก่า (ถ้ารันซ้ำ) แล้วสร้างใหม่ใต้ parent เดียวกับพื้นเดิม
            var old = GameObject.Find(ExpName);
            if (old != null) Object.DestroyImmediate(old);

            var go = new GameObject(ExpName);
            Undo.RegisterCreatedObjectUndo(go, "Ground Experiment");
            go.transform.SetParent(ground.transform.parent, false);
            go.transform.position = GridCenter;
            // width ตรงจาก PPU · บีบ Y ให้เพชรเป็นอัตราส่วน 2:1 ของกริด
            float importedHeightUnits = PngHeightPx / (PngWidthPx / TargetWidthUnits);
            go.transform.localScale = new Vector3(1f, TargetHeightUnits / importedHeightUnits, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerID = sortLayerId;
            sr.sortingOrder = sortOrder; // พื้นเดิมปิดแล้ว → ชนกันไม่ได้ · ยังอยู่ใต้อาคาร

            Save();
            Debug.Log($"[GroundSpriteExperiment] ✅ Apply — ปิด Ground tilemap (สำรองไว้) + วาง {ExpName} " +
                      $"ที่ {GridCenter} scaleY {go.transform.localScale.y:0.000} · Revert เพื่อคืนพื้นเดิม");
            EditorUtility.DisplayDialog("Ground Experiment",
                "สลับเป็นสไปรต์ทดลองแล้ว ✅\nพื้นเดิมถูก 'ปิดไว้' (ไม่ลบ) เป็นสำรอง\n\n" +
                "ปรับสเกล/ตำแหน่งได้ที่ GroundSpriteExperiment ใน Hierarchy\n" +
                "คืนพื้นเดิม: เมนู NuclearReMind → ทดลอง: คืนพื้นเดิม (Revert)", "OK");
        }

        [MenuItem("NuclearReMind/ทดลอง: คืนพื้นเดิม (Revert)")]
        public static void Revert()
        {
            OpenSceneIfNeeded();

            var exp = GameObject.Find(ExpName);
            if (exp != null) Object.DestroyImmediate(exp);

            var ground = GameObject.Find(GroundName);
            if (ground == null)
            {
                // ถูกปิดอยู่ → Find เจอเฉพาะ active · หา inactive ทั้งซีน
                foreach (var t in Object.FindObjectsByType<Transform>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (t.name == GroundName) { ground = t.gameObject; break; }
            }
            if (ground != null) ground.SetActive(true);

            Save();
            Debug.Log("[GroundSpriteExperiment] ↩ Revert — ลบสไปรต์ทดลอง + เปิด Ground tilemap คืน");
            EditorUtility.DisplayDialog("Ground Experiment",
                ground != null ? "คืนพื้นเดิมเรียบร้อย ↩" : "ลบสไปรต์ทดลองแล้ว (ไม่พบ Ground เดิม?)", "OK");
        }

        // นำเข้า PNG เป็น Sprite — PPU ให้กว้าง 43 หน่วย · pivot กลาง · bilinear (เรนเดอร์ Blender ไม่ใช่ pixel art)
        private static Sprite ImportSprite()
        {
            var importer = AssetImporter.GetAtPath(PngPath) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(PngPath, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(PngPath) as TextureImporter;
            }
            if (importer == null) return null;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PngWidthPx / TargetWidthUnits; // 3000/43 ≈ 69.767
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096; // 3000 px ไม่โดนย่อ

            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Center;
            s.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(s);

            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(PngPath);
        }

        private static void OpenSceneIfNeeded()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static void Save()
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
