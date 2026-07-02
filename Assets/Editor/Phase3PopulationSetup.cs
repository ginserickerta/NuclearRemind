using UnityEditor;
using UnityEngine;

namespace NuclearReMind.Editor
{
    /// <summary>
    /// เฟส 3 (V4 §5) — ตั้งธงปลดล็อกการฝึกคลาสบน BuildingData
    ///   • Laboratory → ฝึก Engineer (Research Lab)
    ///   • Laboratory → ฝึก Medic ชั่วคราว (ยังไม่มี Hospital building; เฟส 6 จะเพิ่ม Hospital แล้วย้าย Medic ไป)
    /// รันผ่านเมนู NuclearReMind / Setup Phase 3 Population
    /// </summary>
    public static class Phase3PopulationSetup
    {
        private const string Dir = "Assets/ScriptableObjects/Buildings/";

        [MenuItem("NuclearReMind/Setup Phase 3 Population")]
        public static void Apply()
        {
            var lab = AssetDatabase.LoadAssetAtPath<BuildingData>(Dir + "Laboratory.asset");
            if (lab == null)
            {
                EditorUtility.DisplayDialog("Phase 3 Population", "ไม่พบ Laboratory.asset — ข้าม", "OK");
                return;
            }

            lab.unlocksEngineerTraining = true;
            lab.unlocksMedicTraining = true; // ชั่วคราว จน Hospital มา (เฟส 6)
            EditorUtility.SetDirty(lab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Phase3PopulationSetup] Laboratory: unlocksEngineerTraining=true, unlocksMedicTraining=true (ชั่วคราว)");
            EditorUtility.DisplayDialog("Phase 3 Population",
                "ตั้งธง Laboratory:\n  • unlocksEngineerTraining = true\n  • unlocksMedicTraining = true (ชั่วคราว)\n\n" +
                "หมายเหตุ: เฟส 6 เพิ่ม Hospital building แล้วย้าย Medic ไปที่ Hospital", "OK");
        }
    }
}
