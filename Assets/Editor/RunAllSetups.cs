using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// รัน setup menu ทั้งหมดตามลำดับใน UNITY_SETUP_AND_PLAYTEST.md §2 แล้วเซฟซีน
    /// ลำดับสำคัญ — manager/HUD ต้องมาก่อน content ที่ wire เข้า manager (Quiz ต้องหลัง Crisis + HUD)
    ///
    /// รัน 2 ทาง:
    ///   • เมนู: NuclearReMind/Run All Setups (in order)
    ///   • batch: -executeMethod NuclearReMind.EditorTools.RunAllSetups.Execute
    /// </summary>
    public static class RunAllSetups
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";

        private static readonly string[] MenuOrder =
        {
            // ★ ต้องมาก่อนทุกขั้นที่คำนวณตำแหน่งจากขนาดกริด (pre-placed CORE TOWER §16, อนุสรณ์, แหล่งแร่ A/B)
            "NuclearReMind/Setup Grid 43x28",

            "NuclearReMind/Setup HUD Canvas",
            "NuclearReMind/Setup/Resource Icons + Rebuild HUD", // ไอคอนจริง + rebuild HUD (ต้องหลัง HUD Canvas ก่อนขั้นอื่น wire ทับ)
            "NuclearReMind/Setup Tooltip and Dilemma UI",
            "NuclearReMind/Apply Building Balance (V4 §6)",
            "NuclearReMind/Apply Core Tower Sprite (Pixel Art)", // art จริงแทน placeholder (หลัง Building Balance)
            "NuclearReMind/Apply Core Tower Animation",           // อนิเมชัน idle 4 เฟรม (ทับ sprite นิ่งด้วยเฟรม 0 + animationFrames)
            "NuclearReMind/Setup Phase 3 Population",
            "NuclearReMind/Setup/Character Sprites (คนงาน·วิศวกร·หมอ)", // art จริงแทน placeholder คนงาน (หลัง Phase 3 — spawner เพิ่งถูกสร้าง)
            "NuclearReMind/Setup Phase 6 Buildings",
            "NuclearReMind/Setup Codex System",
            "NuclearReMind/Setup Radiation System", // RadiationManager — exposure สะสม (Zone A §4)
            "NuclearReMind/Setup Crisis Effects",   // CrisisEffectManager — ผลกระทบวิกฤตเต็มระบบ (§4)
            "NuclearReMind/Setup Crisis Dilemmas",
            "NuclearReMind/Setup Quiz System",
            "NuclearReMind/Setup Decrees",
            // Power Grid ถอดจาก chain แล้ว — GDD V4 ไม่มีกลไกรัศมีไฟ (ต้นทุนพลังงาน = upkeep §6)
            // และ setup เดิมจะยัด PowerConduit (no-op) กลับเข้า hotbar — เมนูยังอยู่ ถ้าอยากรันเอง
            "NuclearReMind/Setup Day 11 Systems",
            "NuclearReMind/Setup Demolition System",
            "NuclearReMind/Setup Pause Menu", // PauseCanvas แยกจาก HUDCanvas — กด ESC ตอนเล่น
            "NuclearReMind/Setup Story UI",   // StoryCanvas แยกเช่นกัน — การ์ดเนื้อเรื่อง + Records + อนุสรณ์
            "NuclearReMind/Setup Story Content", // beats/records/info cards/อนุสรณ์ (ต้องหลัง Story UI + Crisis + Quiz)
            "NuclearReMind/Setup Hospital (GDD §6)",       // โรงพยาบาล — asset + hotbar (ต้องหลัง Building Balance + Day 11)
            "NuclearReMind/Apply Building Art (Team Pixel Art)", // art จริงแทน placeholder (ต้องหลัง Hospital — asset เพิ่งถูกสร้าง)
            "NuclearReMind/Setup Ore Deposits (Zone A-B)", // แหล่งแร่ A/B + ถอด Mine จาก hotbar (ต้องหลัง Building Balance + Day 11)
            "NuclearReMind/Apply Resource Caps (V4 §4)",   // เพดานคลัง 9999 + ปลดสะพาน Tritium ของ Lab (หลัง Phase 6)
            "NuclearReMind/Setup/Atlas UI (building icons + cursors + speed)", // map icon ตาม index buildingHotbar — ต้องหลังทุกขั้นที่แก้ hotbar (Hospital/Ore Deposits/Resource Caps)
            "NuclearReMind/Update Building Hover Panel",    // ย้ายบาร์ก่อสร้างเข้าแผง hover (GDD §6 — หลัง HUD Canvas)
            "NuclearReMind/Setup Inventory (Items GDD §13)", // ItemSO 6 ชนิด + InventoryManager + แผงไอเทม (หลัง Hospital — pet_scanner ใช้ BuildingType.Hospital)

            "NuclearReMind/Setup Zone Barrier (Fence + Gate)", // รั้ว/ประตูเส้นแบ่งโซน (ต้องหลัง Ore Deposits — sync zoneAColumns)
            "NuclearReMind/Setup Ground Tiles (Team Grass)",   // Tile หญ้าจาก Map.PNG (fallback ถ้าไม่มีไทล์คัด)
            "NuclearReMind/Setup Iso Nature Tileset (Import Only)", // นำเข้าไทล์ IsoNature (fallback ชุดเต็ม)
            "NuclearReMind/Fix Isometric Sort (Tile Walls)",   // ตั้ง sort ให้บล็อกขอบเรียงถูก (ต้องก่อน Fill Grids)
            "NuclearReMind/Flatten Ground Tiles (Top Face)",   // ★ สร้างไทล์หน้าบนแบน (ข้างในเรียบ · ขอบบล็อกเต็ม)
            "NuclearReMind/Fill Grids (Ground + Fog)",         // ระบาย Ground/Fog แบ่งโซน สุ่มจากไทล์คัด
            "NuclearReMind/Fix 2D Lighting (Light All Sorting Layers)", // กัน sprite รันไทม์เรนเดอร์ดำ (Sprite-Lit-Default)
            "NuclearReMind/Lighting Mood (Warm Key + Cool Fill)", // มู้ดแสงอุ่น+เย็น (ต้องหลัง Fix 2D Lighting)
            "NuclearReMind/Fix Colors (สี sprite สดขึ้น)",        // post-processing color grade (ต้องหลัง Lighting Mood)
            "NuclearReMind/Apply UI Scale (Font Size)", // ขยาย UI/ตัวอักษรทั้งเกม (ต้องหลังทุกขั้นที่สร้าง Canvas)
            "NuclearReMind/Apply Kanit Font (Scene + Prefabs)",
        };

        [MenuItem("NuclearReMind/Run All Setups (in order)")]
        public static void Execute()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // เมนูบางตัวอาจยังไม่มาถึงเครื่องนี้ (สคริปต์อยู่อีกเครื่อง รอ push/pull) —
            // ข้ามแบบไม่ throw ทั้งเชนพัง แล้วสรุปให้ท้ายสุด รันซ้ำได้เองทันทีที่ pull สคริปต์มาครบ
            var skipped = new List<string>();

            for (int i = 0; i < MenuOrder.Length; i++)
            {
                string item = MenuOrder[i];
                Debug.Log($"[RunAllSetups] ▶ ({i + 1}/{MenuOrder.Length}) {item}");
                if (!EditorApplication.ExecuteMenuItem(item))
                {
                    Debug.LogWarning($"[RunAllSetups] ⚠ ข้าม — ไม่พบเมนู (สคริปต์ยังไม่มีในเครื่องนี้?): {item}");
                    skipped.Add(item);
                }
            }

            // บางขั้นเซฟเองแล้ว แต่ขั้นที่แค่ MarkSceneDirty ต้องเซฟปิดท้าย (= ขั้น 13 Ctrl+S)
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            if (skipped.Count == 0)
                Debug.Log($"[RunAllSetups] ✅ รันครบ {MenuOrder.Length} เมนู + เซฟซีนแล้ว");
            else
                Debug.LogWarning($"[RunAllSetups] ⚠ รันเสร็จแต่ข้ามไป {skipped.Count} เมนู (ไม่พบสคริปต์): " +
                                  string.Join(", ", skipped) +
                                  " — pull โค้ดจากเครื่อง/branch ที่มีสคริปต์เหล่านี้แล้วรันเมนูนี้ซ้ำ");
        }
    }
}
