using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// ใส่ art จริงของ CORE TOWER (pixel art จาก Aseprite) แทน placeholder ที่โค้ดวาด
    ///   1. ตั้ง import settings ของ PNG ให้ตรงกับอาคารอื่น — Single / Point / ไม่บีบอัด / pivot ล่างกลาง
    ///   2. คำนวณ PPU จากความกว้างภาพ ให้ตึกกว้างเท่าเดิม (2.5 unit ≈ placeholder 160px @ PPU 64)
    ///      → เปลี่ยน art ขนาดไหนมาก็สเกลถูกเสมอ ไม่ต้องมานั่งเดา PPU
    ///   3. assign เข้า CoreTower.asset (BuildingVisualSpawner อ่าน data.sprite → ตึกในซีนเปลี่ยนตาม)
    ///
    /// pivot ล่างกลาง = จุดวางเดียวกับ placeholder เดิม → ตำแหน่ง PrePlacedCoreTower ไม่ขยับ
    /// รัน: เมนู NuclearReMind/Apply Core Tower Sprite (Pixel Art)
    /// </summary>
    public static class CoreTowerSpriteSetup
    {
        private const string SpritePath = "Assets/Sprites/core/building_1/building_1 sprite.png";
        private const string DataPath = "Assets/ScriptableObjects/Buildings/CoreTower.asset";

        // ความกว้างเป้าหมายของตึกในหน่วย world — placeholder เดิม 160px / PPU 64 = 2.5
        // (footprint 3×3 ทำให้ diamond กว้าง 3.0 unit — 2.5 จึงพอดีไม่ล้นช่อง)
        private const float TargetWidthUnits = 2.5f;

        [MenuItem("NuclearReMind/Apply Core Tower Sprite (Pixel Art)")]
        public static void Apply()
        {
            var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[CoreTowerSpriteSetup] ไม่พบไฟล์ {SpritePath}");
                return;
            }

            int width = SpriteWidth(SpritePath);
            if (width <= 0)
            {
                Debug.LogError($"[CoreTowerSpriteSetup] อ่านความกว้างของ {SpritePath} ไม่ได้");
                return;
            }

            float ppu = width / TargetWidthUnits;

            // เดิมไฟล์นี้ import เป็น Multiple + pivot กลางภาพ + PPU 100 → ตึกจะลอยและขนาดเพี้ยน
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;              // pixel art ห้าม bilinear
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spritePivot = new Vector2(0.5f, 0f);
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (sprite == null)
            {
                Debug.LogError($"[CoreTowerSpriteSetup] reimport แล้วแต่โหลด Sprite จาก {SpritePath} ไม่ได้");
                return;
            }

            var data = AssetDatabase.LoadAssetAtPath<BuildingData>(DataPath);
            if (data == null)
            {
                Debug.LogError($"[CoreTowerSpriteSetup] ไม่พบ {DataPath}");
                return;
            }

            data.sprite = sprite;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            Debug.Log($"[CoreTowerSpriteSetup] ✅ CORE TOWER ใช้ art จริงแล้ว — {sprite.rect.width}×{sprite.rect.height}px " +
                      $"@ PPU {ppu:0.##} = {sprite.rect.width / ppu:0.##}×{sprite.rect.height / ppu:0.##} unit " +
                      "· pivot ล่างกลาง · กด Play เพื่อดูตึกในซีน");
        }

        private static int SpriteWidth(string path)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return tex != null ? tex.width : 0;
        }
    }
}
