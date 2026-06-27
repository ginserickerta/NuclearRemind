using UnityEditor;
using UnityEngine;

namespace NuclearReMind.Editor
{
    /// <summary>
    /// เติมค่าฐาน Level-1 ตาม GDD v2.1 §2.4 ลง BuildingData assets
    /// (production + workerRequired + per-tick consumption "เดินระบบ")
    ///
    /// ปัจจุบันยังไม่มีระบบ level บน BuildingData (1 asset = 1 อาคาร flat) จึงเติมค่า L1 base
    /// อาคารที่ map: Power Plant / Water Plant / Food Plant (Farm)
    /// ⚠ ข้าม Mine — ยังไม่มี asset และ ResourceData ยังไม่มี field "material"
    /// ⚠ L2–L5 + ค่าอัป (upgrade) ยังไม่รองรับ เพราะยังไม่มีระบบ level
    ///
    /// รันได้ 2 ทาง:
    ///   • เมนู Unity: NuclearReMind/Apply 2.4 Building Balance (L1)
    ///   • batch: -executeMethod NuclearReMind.Editor.BuildingBalanceSetup.ApplyFromBatch
    /// </summary>
    public static class BuildingBalanceSetup
    {
        private const string Dir = "Assets/ScriptableObjects/Buildings/";

        [MenuItem("NuclearReMind/Apply 2.4 Building Balance (L1)")]
        public static void Apply()
        {
            int n = ApplyCore();
            EditorUtility.DisplayDialog("§2.4 Building Balance",
                $"อัปเดต {n} อาคารตามค่า Level-1 ใน GDD §2.4:\n\n" +
                "  • Power Plant : +60⚡ / 1 คน / เดินระบบ 2💧\n" +
                "  • Water Plant : +50💧 / 1 คน / เดินระบบ 10⚡\n" +
                "  • Food Plant  : +40🌿 / 1 คน / เดินระบบ 8💧 + 5⚡\n\n" +
                "ยังไม่ได้ทำ: Mine (ไม่มี asset + ไม่มี resource material), L2–L5\n\n" +
                "อย่าลืม Save Project (Ctrl+S)", "OK");
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

            // Power Plant L1: +60⚡ / 1 คน / 2💧
            n += SetBuilding("PowerPlant",
                energyProduction: 60f, waterProduction: 0f, foodProduction: 0f,
                workerRequired: 1, energyConsumption: 0f, waterConsumption: 2f);

            // Water Plant L1: +50💧 / 1 คน / เดินระบบ 10⚡
            n += SetBuilding("WaterPlant",
                energyProduction: 0f, waterProduction: 50f, foodProduction: 0f,
                workerRequired: 1, energyConsumption: 10f, waterConsumption: 0f);

            // Food Plant (Farm) L1: +40🌿 / 1 คน / เดินระบบ 8💧 + 5⚡
            n += SetBuilding("Farm",
                energyProduction: 0f, waterProduction: 0f, foodProduction: 40f,
                workerRequired: 1, energyConsumption: 5f, waterConsumption: 8f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return n;
        }

        private static int SetBuilding(string assetName,
            float energyProduction, float waterProduction, float foodProduction,
            int workerRequired, float energyConsumption, float waterConsumption)
        {
            var data = AssetDatabase.LoadAssetAtPath<BuildingData>(Dir + assetName + ".asset");
            if (data == null)
            {
                Debug.LogWarning($"[BuildingBalanceSetup] ไม่พบ {assetName}.asset — ข้าม");
                return 0;
            }

            data.energyProduction   = energyProduction;
            data.waterProduction    = waterProduction;
            data.foodProduction     = foodProduction;
            data.workerRequired     = workerRequired;
            data.energyConsumption  = energyConsumption;
            data.waterConsumption   = waterConsumption;
            EditorUtility.SetDirty(data);

            Debug.Log($"[BuildingBalanceSetup] {assetName}: " +
                      $"prod(E{energyProduction}/W{waterProduction}/F{foodProduction}) " +
                      $"worker={workerRequired} consume(E{energyConsumption}/W{waterConsumption})");
            return 1;
        }
    }
}
