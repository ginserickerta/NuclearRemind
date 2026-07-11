using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// ทำให้ CORE TOWER มีอนิเมชัน idle (V4 §16) — เดิมเป็นภาพนิ่งเพราะเกมไม่มีระบบเล่นอนิเมชันเลย
    ///   1. import spritesheet 4 เฟรม (building_1 animation.png) ให้ตรงกับภาพนิ่งเดิม:
    ///      Multiple / Point / ไม่บีบอัด / pivot ล่างกลางทุกเฟรม / PPU ตามความกว้างเฟรม (2.5 unit)
    ///      (ต้นฉบับ slice ไว้ pivot ซ้ายล่าง PPU 100 → ถ้าใช้ตรง ๆ ตึกจะลอยและขนาดเพี้ยน)
    ///   2. โหลด sub-sprite ทั้ง 4 เรียงตามเลขท้ายชื่อ → assign เข้า CoreTower.asset (animationFrames + fps)
    ///      แล้วตั้ง sprite = เฟรม 0 (กันตึกหายถ้า SpriteFrameAnimator ยังไม่ทันรัน)
    ///
    /// BuildingVisualSpawner เห็น animationFrames ≥ 2 → ใส่ SpriteFrameAnimator วนเล่นให้เอง
    /// รัน: เมนู NuclearReMind/Apply Core Tower Animation
    /// </summary>
    public static class CoreTowerAnimationSetup
    {
        private const string SheetPath = "Assets/Sprites/core/building_1/building_1 animation.png";
        private const string DataPath = "Assets/ScriptableObjects/Buildings/CoreTower.asset";
        private const float TargetWidthUnits = 2.5f; // เท่าภาพนิ่งเดิม (CoreTowerSpriteSetup)
        private const float Fps = 6f;

        [MenuItem("NuclearReMind/Apply Core Tower Animation")]
        public static void Apply()
        {
            var importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[CoreTowerAnimationSetup] ไม่พบไฟล์ {SheetPath}");
                return;
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            int frameWidth = FirstFrameWidth(importer, tex);
            float ppu = frameWidth > 0 ? frameWidth / TargetWidthUnits : 100f;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple; // คง 4 slice จาก Aseprite
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;                // pixel art ห้าม bilinear
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;

            // pivot ล่างกลางทุกเฟรม — ให้ยืนตรงช่องเหมือนภาพนิ่ง (ต้นฉบับเป็นซ้ายล่าง)
            // spritesheet เป็น API เดิม (obsolete ใน Unity 6 แต่ยังทำงาน) — วิธีตั้ง pivot ต่อเฟรมที่สั้นสุด
#pragma warning disable 618
            var sheet = importer.spritesheet;
            if (sheet != null)
            {
                for (int i = 0; i < sheet.Length; i++)
                {
                    sheet[i].alignment = (int)SpriteAlignment.BottomCenter;
                    sheet[i].pivot = new Vector2(0.5f, 0f);
                }
                importer.spritesheet = sheet;
            }
#pragma warning restore 618

            importer.SaveAndReimport();

            var frames = LoadFramesInOrder(SheetPath);
            if (frames.Count < 2)
            {
                Debug.LogError($"[CoreTowerAnimationSetup] คาดว่าจะมี ≥2 เฟรม แต่พบ {frames.Count} " +
                               "— เช็คว่า Aseprite slice spritesheet ไว้หรือยัง");
                return;
            }

            var data = AssetDatabase.LoadAssetAtPath<BuildingData>(DataPath);
            if (data == null)
            {
                Debug.LogError($"[CoreTowerAnimationSetup] ไม่พบ {DataPath}");
                return;
            }

            data.animationFrames = frames.ToArray();
            data.animationFps = Fps;
            data.sprite = frames[0]; // fallback เฟรมแรก (กันตึกหายก่อน animator รัน)
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            Debug.Log($"[CoreTowerAnimationSetup] ✅ CORE TOWER เล่นอนิเมชัน {frames.Count} เฟรม @ {Fps} fps " +
                      $"· {frames[0].rect.width}×{frames[0].rect.height}px @ PPU {ppu:0.##} · กด Play เพื่อดู");
        }

        // ความกว้างเฟรมแรก — จาก slice เดิม ถ้าไม่มีก็เดาจากความกว้าง texture หาร 4
        private static int FirstFrameWidth(TextureImporter importer, Texture2D tex)
        {
#pragma warning disable 618
            var sheet = importer.spritesheet;
#pragma warning restore 618
            if (sheet != null && sheet.Length > 0 && sheet[0].rect.width > 0)
                return (int)sheet[0].rect.width;
            return tex != null ? tex.width / 4 : 0;
        }

        // โหลด sub-sprite ทั้งหมดจาก spritesheet เรียงตามเลขท้ายชื่อ (…_0, _1, _2, _3)
        private static List<Sprite> LoadFramesInOrder(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(s => TrailingIndex(s.name))
                .ToList();
        }

        private static int TrailingIndex(string name)
        {
            int us = name.LastIndexOf('_');
            if (us >= 0 && us < name.Length - 1 && int.TryParse(name.Substring(us + 1), out int n))
                return n;
            return 0;
        }
    }
}
