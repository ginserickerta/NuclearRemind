using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.Editor
{
    /// <summary>
    /// เติมค่าฐานตามตารางอาคาร V4 §6 ลง BuildingData assets (ค่า L1 — L2/L3 มาจากตัวคูณระดับ ×3/×7.5)
    /// + สร้าง Mine.asset ถ้ายังไม่มี (แหล่งผลิต Iron เพียงแหล่งเดียวของเกม)
    /// + ตั้ง Habitat ให้เป็นตึก Shelter (§5): shelterCapacity 10 → เพดานประชากร 20/40/80 ตามระดับ
    /// + ต่อ Mine เข้า PlacementController.buildingHotbar และ BuildingRegistry.allBuildingData ในซีน
    ///
    /// รันได้ 2 ทาง:
    ///   • เมนู Unity: NuclearReMind/Apply Building Balance (V4 §6)
    ///   • batch: -executeMethod NuclearReMind.Editor.BuildingBalanceSetup.ApplyFromBatch
    /// </summary>
    public static class BuildingBalanceSetup
    {
        private const string Dir = "Assets/ScriptableObjects/Buildings/";
        private const string MinePath = Dir + "Mine.asset";

        [MenuItem("NuclearReMind/Apply Building Balance (V4 §6)")]
        public static void Apply()
        {
            int n = ApplyCore();
            EditorUtility.DisplayDialog("V4 §6 Building Balance",
                $"อัปเดต {n} อาคารตามตาราง V4 §6 (ค่า L1):\n\n" +
                "  • Power Plant : +60⚡ / 1 คน / ไม่มี upkeep / สร้าง ⛏40\n" +
                "  • Water Plant : +50💧 / 1 คน / upkeep 10⚡ / สร้าง ⛏30\n" +
                "  • Food Plant  : +40🌿 / 1 คน / upkeep 8⚡+15💧 / สร้าง ⛏30\n" +
                "  • Mine        : +30⛏ / 2 คน / upkeep 12⚡ / สร้าง ⛏20 (ใหม่ — ช่อง 8)\n" +
                "  • Shelter (Habitat) : เพดานประชากร +10/+30/+70 / สร้าง ⛏60+⚡100\n\n" +
                "ค่าอัประดับ: Iron ×ระดับ + Energy ×ระดับ (ตึกผลิต ⛏40+⚡150, Mine ⚡120, Shelter ⛏120+⚡200)\n\n" +
                "อย่าลืม Save Scene + Save Project (Ctrl+S)", "OK");
        }

        // entry สำหรับ batchmode (ไม่เรียก dialog ซึ่ง headless แสดงไม่ได้)
        public static void ApplyFromBatch()
        {
            int n = ApplyCore();
            Debug.Log($"[BuildingBalanceSetup] ApplyFromBatch สำเร็จ — อัปเดต {n} อาคาร");
        }

        private static int ApplyCore()
        {
            int n = 0;

            // ── ตึกผลิต 4 โรง (V4 §6 — ค่า L1) ─────────────────────────
            // Power Plant L1: +60⚡ / 1 คน / ไม่มี upkeep / สร้างหลังถัดไป Iron 40
            n += SetBuilding("PowerPlant", b =>
            {
                b.energyProduction = 60f;
                b.workerRequired = 1;
                b.energyConsumption = 0f;
                b.waterConsumption = 0f;
                b.ironCost = 40;
                b.upgradeIronCost = 40;    // §6: L1→L2 Iron 40, L2→L3 Iron 80 (= ×ระดับ)
                b.upgradeEnergyCost = 150; // §6: L1→L2 Energy 150
            });

            // Water Plant L1: +50💧 / 1 คน / upkeep 10⚡ / สร้าง Iron 30
            n += SetBuilding("WaterPlant", b =>
            {
                b.waterProduction = 50f;
                b.workerRequired = 1;
                b.energyConsumption = 10f;
                b.waterConsumption = 0f;
                b.ironCost = 30;
                b.upgradeIronCost = 40;
                b.upgradeEnergyCost = 150;
            });

            // Food Plant (Farm) L1: +40🌿 / 1 คน / upkeep 8⚡ + 15💧 / สร้าง Iron 30
            n += SetBuilding("Farm", b =>
            {
                b.foodProduction = 40f;
                b.workerRequired = 1;
                b.energyConsumption = 8f;
                b.waterConsumption = 15f;
                b.ironCost = 30;
                b.upgradeIronCost = 40;
                b.upgradeEnergyCost = 150;
            });

            // Mine L1: +30⛏ / 2 คน / upkeep 12⚡ / สร้าง Iron 20 — สร้าง asset ถ้ายังไม่มี
            var mine = EnsureMineAsset();
            n += SetBuilding("Mine", b =>
            {
                b.ironProduction = 30f;
                b.workerRequired = 2;
                b.energyConsumption = 12f;
                b.waterConsumption = 0f;
                b.ironCost = 20;
                b.energyCost = 0;
                b.upgradeIronCost = 40;    // §6: L1→L2 Iron 40, L2→L3 Iron 80
                b.upgradeEnergyCost = 120; // §6: L1→L2 Energy 120
            });

            // ── Shelter (§5) — ใช้ asset Habitat เดิม (ชื่อไทยคงเดิม กันเซฟเก่าพัง) ──
            // เพดานประชากร: ฐานเริ่มเกม 10 + ตึกนี้ L1/L2/L3 → +10/+30/+70 = 20/40/80 (§5 L2–L4)
            // ต้นทุนตามตาราง §5: สร้าง (=L2) Iron60+E100 · อัป (=L3/L4) Iron 120/240 + Energy 200/400
            n += SetBuilding("Habitat", b =>
            {
                b.shelterCapacity = 10;
                b.ironCost = 60;
                b.energyCost = 100;
                b.workerRequired = 0;
                b.upgradeIronCost = 120;
                b.upgradeEnergyCost = 200;
                b.description = "ขยายเพดานประชากรของเมือง +10 คน (อัประดับด้วยปุ่ม U → +30/+70) " +
                                "ประชากรเพิ่ม +1 คน/วันเมื่ออาหารพอและ Hope ≥ 50";
            });

            WireSceneReferences(mine);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return n;
        }

        /// <summary>สร้าง Mine.asset (BuildingData) + placeholder sprite ถ้ายังไม่มีบนดิสก์</summary>
        private static BuildingData EnsureMineAsset()
        {
            var data = AssetDatabase.LoadAssetAtPath<BuildingData>(MinePath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<BuildingData>();
                data.buildingName = "เหมืองแร่";
                data.description = "ขุดแร่เหล็กสำหรับสร้าง/อัปเกรดอาคารและคอยล์ CORE TOWER — " +
                                   "แหล่งผลิต Iron เพียงแหล่งเดียวของเมือง (ใช้คนงาน 2 คน)";
                data.nuclearKnowledge = "แร่จากเหมืองไม่ได้มีแค่โลหะก่อสร้าง — ลิเธียมจากหินใต้ดินคือวัตถุดิบผลิตทริเทียม " +
                                        "เชื้อเพลิงฟิวชัน: ลิเธียม-6 จับนิวตรอนแล้วแตกตัวเป็นทริเทียม + ฮีเลียม (Li-6 + n → T + He-4) " +
                                        "เตาฟิวชันจริงอย่าง ITER จึงบุผนังด้วย 'breeding blanket' ที่มีลิเธียมเพื่อผลิตเชื้อเพลิงของตัวเอง";
                data.size = Vector2Int.one;
                data.buildingType = BuildingType.Mine;
                AssetDatabase.CreateAsset(data, MinePath);
                Debug.Log($"[BuildingBalanceSetup] สร้าง {MinePath}");
            }

            if (data.sprite == null)
            {
                data.sprite = EditorTools.PlaceholderSpriteGenerator.EnsureBuildingSprite("Mine");
                EditorUtility.SetDirty(data);
            }
            return data;
        }

        /// <summary>
        /// ต่อ Mine เข้า hotbar (PlacementController) + ตาราง restore (BuildingRegistry) ในซีนที่เปิดอยู่
        /// พร้อมถอด PowerConduit ออกจากแถบเลือกตึก (power grid ไม่ gate การผลิตแล้ว — conduit เป็น no-op)
        /// </summary>
        private static void WireSceneReferences(BuildingData mine)
        {
            if (mine == null) return;

            var conduit = AssetDatabase.LoadAssetAtPath<BuildingData>(Dir + "PowerConduit.asset");

            var placement = Object.FindFirstObjectByType<PlacementController>();
            if (placement != null)
            {
                bool changed = AppendIfMissing(ref placement.buildingHotbar, mine);
                changed |= RemoveIfPresent(ref placement.buildingHotbar, conduit);
                if (changed)
                {
                    EditorUtility.SetDirty(placement);
                    Debug.Log($"[BuildingBalanceSetup] hotbar = {placement.buildingHotbar.Length} ช่อง (Mine เพิ่ม / Conduit ถอด)");
                }

                // BuildingSelectionUI.buildings เป็น array แยก (serialize คนละก้อน) — sync ให้ตรง hotbar เสมอ
                var selUI = Object.FindFirstObjectByType<BuildingSelectionUI>();
                if (selUI != null)
                {
                    selUI.buildings = (BuildingData[])placement.buildingHotbar.Clone();
                    EditorUtility.SetDirty(selUI);
                }
            }

            var registry = Object.FindFirstObjectByType<BuildingRegistry>();
            if (registry != null && AppendIfMissing(ref registry.allBuildingData, mine))
            {
                EditorUtility.SetDirty(registry);
                Debug.Log("[BuildingBalanceSetup] เพิ่ม Mine เข้า BuildingRegistry.allBuildingData");
            }

            if (placement == null || registry == null)
                Debug.LogWarning("[BuildingBalanceSetup] ไม่พบ PlacementController/BuildingRegistry ในซีน — " +
                                 "เปิด Gamescene.unity แล้วรันเมนูนี้อีกครั้งเพื่อ wire Mine เข้า hotbar");
            else
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static bool AppendIfMissing(ref BuildingData[] array, BuildingData item)
        {
            var list = array != null ? new List<BuildingData>(array) : new List<BuildingData>();
            if (list.Contains(item)) return false;
            list.Add(item);
            array = list.ToArray();
            return true;
        }

        private static bool RemoveIfPresent(ref BuildingData[] array, BuildingData item)
        {
            if (item == null || array == null) return false;
            var list = new List<BuildingData>(array);
            if (!list.Remove(item)) return false;
            array = list.ToArray();
            return true;
        }

        private static int SetBuilding(string assetName, System.Action<BuildingData> configure)
        {
            var data = AssetDatabase.LoadAssetAtPath<BuildingData>(Dir + assetName + ".asset");
            if (data == null)
            {
                Debug.LogWarning($"[BuildingBalanceSetup] ไม่พบ {assetName}.asset — ข้าม");
                return 0;
            }

            configure(data);
            EditorUtility.SetDirty(data);

            Debug.Log($"[BuildingBalanceSetup] {assetName}: " +
                      $"prod(E{data.energyProduction}/W{data.waterProduction}/F{data.foodProduction}/Fe{data.ironProduction}) " +
                      $"worker={data.workerRequired} upkeep(E{data.energyConsumption}/W{data.waterConsumption}) " +
                      $"build(⛏{data.ironCost}+⚡{data.energyCost}) upgrade(⛏{data.upgradeIronCost}+⚡{data.upgradeEnergyCost} ×ระดับ)" +
                      (data.shelterCapacity > 0 ? $" shelterCap+{data.shelterCapacity}" : ""));
            return 1;
        }
    }
}
