using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// โรงพยาบาล (GDD §6 อาคารสนับสนุน): สร้าง Hospital.asset + sprite + ต่อเข้า hotbar/registry
    ///   • รักษาคนป่วยเพิ่ม (PopulationManager.hospitalHealPerDay) + ลดรังสี (RadiationManager.hospitalMitigationPerDay)
    ///   • มีผลเฉพาะเมื่อ Medic ประจำครบ (requiredClass = Medic, workerRequired 2)
    ///   • ไม่ปลดล็อกการฝึกใดๆ — Lab เป็นศูนย์ฝึกทุกคลาส (Phase3PopulationSetup)
    ///   • RadiationShelter ยังอยู่ — ทั้งคู่ลดรังสี คนละเงื่อนไข (shelter แค่วาง / hospital ต้องมีคน)
    ///
    /// รัน: เมนู NuclearReMind/Setup Hospital (GDD §6) — หรือรวมใน Run All Setups (หลัง Building Balance)
    /// </summary>
    public static class HospitalSetup
    {
        private const string Dir = "Assets/ScriptableObjects/Buildings/";
        private const string HospitalPath = Dir + "Hospital.asset";
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";

        [MenuItem("NuclearReMind/Setup Hospital (GDD §6)")]
        public static void SetupAll()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var hospital = EnsureHospitalAsset();
            WireScene(hospital);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[HospitalSetup] ✅ Hospital พร้อม (asset + hotbar + registry) — เซฟซีนแล้ว");
        }

        /// <summary>สร้าง/อัปเดต Hospital.asset — ตัวเลข GDD §6: ⛏100+⚡150 / Medic 2 / upkeep ⚡15</summary>
        private static BuildingData EnsureHospitalAsset()
        {
            var data = AssetDatabase.LoadAssetAtPath<BuildingData>(HospitalPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<BuildingData>();
                AssetDatabase.CreateAsset(data, HospitalPath);
                Debug.Log($"[HospitalSetup] สร้าง {HospitalPath}");
            }

            data.buildingName = "โรงพยาบาล"; // restore-by-name key — ห้ามซ้ำกับ asset อื่น
            data.description = "รักษาคนป่วยจากรังสีและลดรังสีสะสมของเมือง — ต้องมีแพทย์ประจำครบ 2 คนจึงทำงาน " +
                               "(ฝึกแพทย์ได้ที่ห้องปฏิบัติการ)";
            data.nuclearKnowledge = "หลัก ALARA (As Low As Reasonably Achievable): ลดการรับรังสีให้ต่ำที่สุดเท่าที่ทำได้ " +
                                    "ด้วยเวลา-ระยะห่าง-เกราะกำบัง · แพทย์เวชศาสตร์นิวเคลียร์ใช้ไอโอดีน-131 รักษาไทรอยด์ " +
                                    "และ Tc-99m สแกนอวัยวะ — รังสีรักษาชีวิตได้เมื่อใช้ถูกวิธี";
            data.buildingType = BuildingType.Hospital;
            data.size = new Vector2Int(2, 2);
            data.ironCost = 100;
            data.energyCost = 150;
            data.workerRequired = 2;
            data.requiredClass = WorkerClass.Medic;
            data.energyConsumption = 15f;
            data.waterConsumption = 0f;
            data.buildTicks = 8; // ตึกใหญ่ 2×2 — นานกว่าค่าตั้งต้น 6 เล็กน้อย
            data.unlockPhase = 3; // GDD §6 "ปลดล็อก Phase 3" (วัน 11+)
            data.upgradeIronCost = 50;
            data.upgradeEnergyCost = 150;
            // ไม่ตั้ง unlocks*Training ใดๆ — Lab เป็นศูนย์ฝึก (ยืนยันแล้ว)

            if (data.sprite == null)
                data.sprite = PlaceholderSpriteGenerator.EnsureBuildingSprite("Hospital");

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        // ต่อเข้า hotbar + registry + sync BuildingSelectionUI + ปรับความกว้าง panel (idiom OreDepositSetup)
        private static void WireScene(BuildingData hospital)
        {
            var registry = Object.FindFirstObjectByType<BuildingRegistry>();
            if (registry != null && AppendIfMissing(ref registry.allBuildingData, hospital))
            {
                EditorUtility.SetDirty(registry);
                Debug.Log($"[HospitalSetup] เพิ่ม Hospital เข้า BuildingRegistry.allBuildingData ({registry.allBuildingData.Length} รายการ)");
            }

            var placement = Object.FindFirstObjectByType<PlacementController>();
            if (placement == null)
            {
                Debug.LogWarning("[HospitalSetup] ไม่พบ PlacementController ในซีน — เปิด Gamescene แล้วรันใหม่");
                return;
            }

            if (AppendIfMissing(ref placement.buildingHotbar, hospital))
            {
                EditorUtility.SetDirty(placement);
                Debug.Log($"[HospitalSetup] เพิ่ม Hospital เข้า hotbar — {placement.buildingHotbar.Length} ช่อง");
            }

            var selUI = Object.FindFirstObjectByType<BuildingSelectionUI>();
            if (selUI != null)
            {
                selUI.buildings = (BuildingData[])placement.buildingHotbar.Clone();
                EditorUtility.SetDirty(selUI);
            }

            // ปรับความกว้าง panel ตามจำนวนช่องใหม่ (สูตรเดียวกับ Day11Setup/OreDepositSetup)
            var panelGO = GameObject.Find("BuildingSelectionPanel");
            var panelRect = panelGO != null ? panelGO.GetComponent<RectTransform>() : null;
            if (panelRect != null)
            {
                int count = placement.buildingHotbar.Length;
                panelRect.sizeDelta = new Vector2((count + 1) * 94f + 8f, 118f); // +1 = ปุ่มทุบอาคาร
                EditorUtility.SetDirty(panelRect);
            }
        }

        private static bool AppendIfMissing(ref BuildingData[] array, BuildingData item)
        {
            var list = array != null ? new List<BuildingData>(array) : new List<BuildingData>();
            if (list.Contains(item)) return false;
            list.Add(item);
            array = list.ToArray();
            return true;
        }
    }
}
