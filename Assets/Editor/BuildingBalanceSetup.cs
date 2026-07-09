using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.Editor
{
    /// <summary>
    /// เติมค่าฐานตามตารางอาคาร V4 §6 ลง BuildingData assets (ค่า L1 — L2/L3 มาจากตัวคูณระดับ ×3/×7.5)
    /// + สร้าง Mine.asset ถ้ายังไม่มี (คงใน registry ให้เซฟเก่า restore ได้)
    /// + ตั้ง Habitat ให้เป็นตึก Shelter (§5): shelterCapacity 10 → เพดานประชากร 20/40/80 ตามระดับ
    /// + ถอด Mine จาก hotbar (ระบบแหล่งแร่แทน — OreDepositSetup) แต่คงใน BuildingRegistry.allBuildingData
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
                "  • Mine        : +30⛏ / 2 คน (ถอดจาก hotbar แล้ว — เหล็กมาจากแหล่งแร่ A/B)\n" +
                "  • Research Lab: ⛏80+⚡120 / 2 วิศวกร / upkeep 20⚡ / Knowledge 2/วัน\n" +
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

            // Food Plant (Farm) L1: +40🌿 / 1 คน (เกษตรกร §5) / upkeep 8⚡ + 15💧 / สร้าง Iron 30
            n += SetBuilding("Farm", b =>
            {
                b.foodProduction = 40f;
                b.workerRequired = 1;
                b.requiredClass = WorkerClass.Farmer; // ฟาร์มใช้เกษตรกร (เริ่มเกมมี 2 คน — bootstrap)
                b.energyConsumption = 8f;
                b.waterConsumption = 15f;
                b.ironCost = 30;
                b.upgradeIronCost = 40;
                b.upgradeEnergyCost = 150;
            });

            // ── Research Lab (§6 อาคารวิจัย): Iron 80 + E 120 / 2 วิศวกร / upkeep 20⚡ · ปลดเฟส 2 ──
            // ศูนย์ฝึกทุกคลาส (ธง unlocks* ตั้งใน Phase3PopulationSetup) + ผลิต Knowledge 2/วัน (×ระดับ)
            n += SetBuilding("Laboratory", b =>
            {
                b.ironCost = 80;
                b.energyCost = 120;
                b.workerRequired = 2;
                b.requiredClass = WorkerClass.Engineer;
                b.energyConsumption = 20f;
                b.waterConsumption = 0f;
                b.knowledgeProduction = 2f;
                b.unlockPhase = 2; // GDD §6 "ปลดล็อก Phase 2"
                b.upgradeIronCost = 40;
                b.upgradeEnergyCost = 150;
                b.description = "หัวใจงานวิจัย — ฝึกวิศวกร/แพทย์/เกษตรกร ผลิต Knowledge และปลดใบความรู้นิวเคลียร์ " +
                                "ต้องมีวิศวกรประจำ 2 คนจึงเดินเครื่อง";
            });

            // ── RadiationShelter: ปลดเฟส 2 (ก่อนยุครังสีหนัก) — ค่าอื่นคง asset เดิม ──
            n += SetBuilding("RadiationShelter", b => { b.unlockPhase = 2; });

            // ── ตึกผลิตพื้นฐาน + Shelter: เฟส 1 (วางได้วันแรก) — เซ็ตชัดกัน asset ค้างค่าอื่น ──
            SetBuilding("PowerPlant", b => { b.unlockPhase = 1; });
            SetBuilding("WaterPlant", b => { b.unlockPhase = 1; });
            SetBuilding("Farm", b => { b.unlockPhase = 1; });
            SetBuilding("Habitat", b => { b.unlockPhase = 1; });

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
        /// ถอด PowerConduit (no-op หลังเลิก gate การผลิต) และ CORE TOWER (มากับแมพ — PrePlacedBuilding
        /// วางกลางกริดให้ตอนเริ่มเกม ผู้เล่นไม่ต้องลากวาง) ออกจากแถบเลือกตึก
        /// </summary>
        private static void WireSceneReferences(BuildingData mine)
        {
            if (mine == null) return;

            var conduit = AssetDatabase.LoadAssetAtPath<BuildingData>(Dir + "PowerConduit.asset");
            var coreTower = AssetDatabase.LoadAssetAtPath<BuildingData>(Dir + "CoreTower.asset");

            var placement = Object.FindFirstObjectByType<PlacementController>();
            if (placement != null)
            {
                // Mine ถอดจาก hotbar — เหล็กมาจากการขุดแหล่งแร่ (OreDepositSetup) เท่านั้น
                // (เดิม AppendIfMissing — เปลี่ยนเป็นถอด กันรันซ้ำแล้ว Mine คืนชีพเข้า hotbar)
                bool changed = RemoveIfPresent(ref placement.buildingHotbar, mine);
                changed |= RemoveIfPresent(ref placement.buildingHotbar, conduit);
                changed |= RemoveIfPresent(ref placement.buildingHotbar, coreTower);
                if (changed)
                {
                    EditorUtility.SetDirty(placement);
                    Debug.Log($"[BuildingBalanceSetup] hotbar = {placement.buildingHotbar.Length} ช่อง (Mine+Conduit+CoreTower ถอด)");
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

            EnsurePrePlacedCoreTower(coreTower);

            if (placement == null || registry == null)
                Debug.LogWarning("[BuildingBalanceSetup] ไม่พบ PlacementController/BuildingRegistry ในซีน — " +
                                 "เปิด Gamescene.unity แล้วรันเมนูนี้อีกครั้งเพื่อ wire Mine เข้า hotbar");
            else
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>
        /// สร้าง GameObject "PrePlacedCoreTower" (PrePlacedBuilding) — วาง CORE TOWER กลางกริดตอนเริ่มเกม
        /// ตำแหน่งคำนวณจากขนาดกริดจริงในซีน (43×28, ตึก 3×3 → origin (20,12) กินช่อง col 20–22 / row 12–14)
        /// </summary>
        private static void EnsurePrePlacedCoreTower(BuildingData coreTower)
        {
            if (coreTower == null)
            {
                Debug.LogWarning("[BuildingBalanceSetup] ไม่พบ CoreTower.asset — ข้าม pre-place");
                return;
            }

            var grid = Object.FindFirstObjectByType<GridManager>();
            int columns = grid != null ? grid.columns : 43;
            int rows = grid != null ? grid.rows : 28;
            var origin = new Vector2Int((columns - coreTower.size.x) / 2, (rows - coreTower.size.y) / 2);

            var go = GameObject.Find("PrePlacedCoreTower");
            if (go == null) go = new GameObject("PrePlacedCoreTower");
            var pre = go.GetComponent<PrePlacedBuilding>() ?? go.AddComponent<PrePlacedBuilding>();
            pre.building = coreTower;
            pre.cell = origin;
            EditorUtility.SetDirty(pre);
            Debug.Log($"[BuildingBalanceSetup] PrePlacedCoreTower ที่ ({origin.x},{origin.y}) ขนาด {coreTower.size.x}×{coreTower.size.y}");
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
