using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Setup ระบบ Inventory (GDD §13 — ไอเทมคราฟต์) รันผ่าน NuclearReMind / Setup Inventory (Items GDD §13)
    /// idempotent (find-or-create · asset = source of truth แบบ Apply Building Balance):
    ///   1. สร้าง/อัปเดต ItemSO 6 ชนิดตามตาราง §13 ที่ Assets/ScriptableObjects/Items/
    ///      (Deuterium/Tritium คงเป็นทรัพยากรใน ResourceManager · โล่พลาสมา = เงื่อนไขชนะที่ CoreTowerManager ไม่ใช่ไอเทม)
    ///   2. InventoryManager GO + wire catalog allItems
    ///   3. InventoryCanvas แยกจาก HUDCanvas (แบบ StoryCanvas) — แผงไอเทมกลางจอ + ปุ่ม toggle ซ้ายล่าง
    ///      (เหนือปุ่ม Records ที่ y=298) + row template ให้ controller instantiate ตอน runtime
    /// ปุ่มทุกปุ่ม wire onClick ตอน runtime ใน controller.Start — รัน setup ซ้ำได้ไม่มี listener ซ้ำ
    ///
    /// หมายเหตุ §13: GDD บอกโคบอลต์-60 ผลิตที่ "โรงงาน" แต่เกมไม่มีอาคารโรงงาน — ใช้ห้องวิจัยตามหัวข้อ §13
    /// ("ผลิตที่ห้องวิจัย/โรงพยาบาล") · น้ำหล่อเย็น "จากน้ำสะอาด" → โรงกรองน้ำ (WaterPlant)
    /// </summary>
    public static class InventorySetup
    {
        private const string ItemFolder = "Assets/ScriptableObjects/Items";

        [MenuItem("NuclearReMind/Setup Inventory (Items GDD §13)")]
        public static void Setup()
        {
            var items = CreateItemAssets();
            SetupManager(items);
            RemoveOldInventoryUI(); // คลังแบบลิสต์เดิม (คราฟต์/ใช้) ถูกยกเลิก — ใช้ InventoryGridUI (ปุ่ม I) แทน

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[InventorySetup] อัปเดต ItemSO + InventoryManager · ลบแผงคลังเก่า (คราฟต์) ออก — ใช้ InventoryGridUI แทน · กด Save Scene (Ctrl+S)");
        }

        // ─────────────────────────────────────────────
        //  1. ItemSO assets (ค่าเกมทั้งหมดอยู่ที่นี่ → asset — rule 4)
        // ─────────────────────────────────────────────
        private static ItemSO[] CreateItemAssets()
        {
            if (!Directory.Exists(ItemFolder))
            {
                Directory.CreateDirectory(ItemFolder);
                AssetDatabase.Refresh();
            }

            return new[]
            {
                // Rad-Gear — อุปกรณ์ · Iron + วิจัย · กันคนป่วย/ตายเมื่อเข้าพื้นที่รังสี (ถือไว้มีผล — passive)
                Item("RadGear", so =>
                {
                    so.id = "rad_gear";
                    so.displayName = "Rad-Gear";
                    so.description = "ชุดป้องกันรังสี — กันคนป่วย/ตายเมื่อส่งเข้าพื้นที่รังสี (โซน B) ตามหลัก ALARA: ลดการรับรังสีให้ต่ำที่สุดเท่าที่ทำได้";
                    so.category = ItemCategory.Equipment;
                    so.craftCost = Costs((ResourceType.Iron, 150f));
                    so.craftedAt = BuildingType.Laboratory;
                    so.craftTicks = 6;
                    so.requiresResearch = true;
                    so.maxStack = 5;
                    so.isPassive = true;
                }),

                // เครื่องสแกน PET/SPECT — การแพทย์ · โรงพยาบาล + พลังงาน · วินิจฉัย (วิกฤต 2·A — เช็ก HasItem)
                Item("PetScanner", so =>
                {
                    so.id = "pet_scanner";
                    so.displayName = "เครื่องสแกน PET/SPECT";
                    so.description = "เครื่องถ่ายภาพเวชศาสตร์นิวเคลียร์ — ใช้ไอโซโทปรังสีตามหาเซลล์ผิดปกติในร่างกาย ใช้วินิจฉัยโรคกลายพันธุ์ (วิกฤต 2·A)";
                    so.category = ItemCategory.Medical;
                    so.craftCost = Costs((ResourceType.Energy, 200f));
                    so.craftedAt = BuildingType.Hospital;
                    so.craftTicks = 8;
                    so.requiresResearch = false;
                    so.maxStack = 1;
                    so.isPassive = true;
                }),

                // ไอโซโทปการแพทย์ — วัสดุแล็บ + ฟลักซ์นิวตรอน (18 tick ≈ 1 วันเกม = "เตา Idle 1 วัน") · รักษาหายสนิท (2·B)
                Item("MedicalIsotope", so =>
                {
                    so.id = "medical_isotope";
                    so.displayName = "ไอโซโทปการแพทย์";
                    so.description = "ยาเฉพาะจุดจากการอาบนิวตรอน (เช่น Tc-99m, Lu-177, I-131) — ฟลักซ์นิวตรอนจากเตาฟิวชันผลิตยารักษาผู้ป่วยหายสนิท (วิกฤต 2·B) ใช้เวลา ~1 วันเกม";
                    so.category = ItemCategory.Medical;
                    so.craftCost = Costs((ResourceType.Iron, 50f), (ResourceType.Energy, 50f));
                    so.craftedAt = BuildingType.Laboratory;
                    so.craftTicks = 18; // 90s/วัน ÷ 5s/tick = 18 tick ≈ 1 วันเกม (GDD: เตา Idle 1 วัน)
                    so.requiresResearch = true;
                    so.maxStack = 3;
                    so.curesAllSick = true;
                }),

                // เมล็ดพันธุ์ฉายรังสี — เกษตร · ห้องวิจัย + Iron 250 (GDD ระบุ) · เพิ่มผลผลิตอาหารยาว (3·A)
                Item("IrradiatedSeeds", so =>
                {
                    so.id = "irradiated_seeds";
                    so.displayName = "เมล็ดพันธุ์ฉายรังสี";
                    so.description = "เมล็ดพันธุ์ปรับปรุงด้วยการฉายรังสี (mutation breeding — เทคนิคเกษตรนิวเคลียร์จริง) เพิ่มผลผลิตอาหารถาวร (วิกฤต 3·A)";
                    so.category = ItemCategory.Agri;
                    so.craftCost = Costs((ResourceType.Iron, 250f)); // GDD §13 ระบุตรง
                    so.craftedAt = BuildingType.Laboratory;
                    so.craftTicks = 8;
                    so.requiresResearch = true;
                    so.maxStack = 2;
                    so.foodYieldBonus = 0.5f; // ผลผลิตอาหาร +50% ต่อการใช้ (จูนเฟส 8)
                }),

                // เครื่องฉายโคบอลต์-60 — ถนอมอาหาร · Energy 300 (GDD ระบุ) · อาหารเน่า → 0% (3·B)
                Item("Cobalt60Irradiator", so =>
                {
                    so.id = "cobalt60_irradiator";
                    so.displayName = "เครื่องฉายโคบอลต์-60";
                    so.description = "เครื่องฉายรังสีแกมมาจาก Co-60 ฆ่าเชื้อ/ถนอมอาหาร (food irradiation จริง) — หยุดอาหารเน่าเสียถาวร (วิกฤต 3·B)";
                    so.category = ItemCategory.Agri;
                    so.craftCost = Costs((ResourceType.Energy, 300f)); // GDD §13 ระบุตรง
                    so.craftedAt = BuildingType.Laboratory; // §13: ผลิตที่ห้องวิจัย/รพ. (เกมไม่มีอาคาร "โรงงาน")
                    so.craftTicks = 10;
                    so.requiresResearch = false;
                    so.maxStack = 1;
                    so.stopsFoodSpoilage = true;
                }),

                // น้ำหล่อเย็นฉุกเฉิน — ฉุกเฉิน · น้ำสะอาด (โรงกรองน้ำ) · ได้ทันที · ลด HEAT เตาเร่งด่วน (1·C)
                Item("EmergencyCoolant", so =>
                {
                    so.id = "emergency_coolant";
                    so.displayName = "น้ำหล่อเย็นฉุกเฉิน";
                    so.description = "น้ำสะอาดสำรองสำหรับหล่อเย็นเตาเร่งด่วน — ใช้แล้วลด HEAT ทันที (วิกฤต 1·C) การหล่อเย็นคือหัวใจความปลอดภัยของเตาปฏิกรณ์";
                    so.category = ItemCategory.Emergency;
                    so.craftCost = Costs((ResourceType.Water, 100f));
                    so.craftedAt = BuildingType.WaterPlant;
                    so.craftTicks = 0; // ของฉุกเฉิน — ได้ทันที
                    so.requiresResearch = false;
                    so.maxStack = 3;
                    so.coreHeatReduction = 30f; // อ่อนกว่า SCRAM (−40) แต่ไม่มี penalty/cooldown
                }),
            };
        }

        /// <summary>find-or-create ItemSO ที่ path แล้วตั้งค่า (setup = source of truth — รันซ้ำค่ากลับเป๊ะ)</summary>
        private static ItemSO Item(string fileName, System.Action<ItemSO> configure)
        {
            string path = $"{ItemFolder}/{fileName}.asset";
            var so = AssetDatabase.LoadAssetAtPath<ItemSO>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<ItemSO>();
                AssetDatabase.CreateAsset(so, path);
            }
            configure(so);
            EditorUtility.SetDirty(so);
            return so;
        }

        private static ResourceCost[] Costs(params (ResourceType type, float amount)[] costs)
        {
            var result = new ResourceCost[costs.Length];
            for (int i = 0; i < costs.Length; i++)
                result[i] = new ResourceCost { type = costs[i].type, amount = costs[i].amount };
            return result;
        }

        // ─────────────────────────────────────────────
        //  2. InventoryManager + wire catalog
        // ─────────────────────────────────────────────
        private static void SetupManager(ItemSO[] items)
        {
            var go = GameObject.Find("InventoryManager") ?? new GameObject("InventoryManager");
            var mgr = go.GetComponent<InventoryManager>() ?? go.AddComponent<InventoryManager>();
            mgr.allItems = items;
            EditorUtility.SetDirty(go);
        }

        // ─────────────────────────────────────────────
        //  3. ลบคลังไอเทมแบบลิสต์เดิม (คราฟต์/ใช้) — ยกเลิกระบบคราฟต์ตามคำขอผู้ใช้
        //     เหลือแค่ InventoryGridUI (กริด ดู/ทิ้ง · ปุ่ม I · auto-spawn ใต้ HUDCanvas)
        //     ItemSO + InventoryManager ยังอยู่ (เป็นข้อมูล + ตัวจัดการ discard ที่กริดใหม่เรียกใช้)
        // ─────────────────────────────────────────────
        private static void RemoveOldInventoryUI()
        {
            var canvas = GameObject.Find("InventoryCanvas");
            if (canvas != null)
            {
                Undo.DestroyObjectImmediate(canvas);
                Debug.Log("[InventorySetup] ลบ InventoryCanvas (คลังเก่า) ออก");
            }

            var ctrl = GameObject.Find("InventoryPanelController");
            if (ctrl != null)
            {
                Undo.DestroyObjectImmediate(ctrl);
                Debug.Log("[InventorySetup] ลบ InventoryPanelController (คลังเก่า) ออก");
            }
        }
    }
}
