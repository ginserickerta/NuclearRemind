using UnityEditor;
using UnityEngine;

namespace NuclearReMind.Editor
{
    /// <summary>
    /// [TH] หน้าที่: สคริปต์ Editor ตั้งค่าการผลิตเชื้อเพลิง (Deuterium/Tritium) ของอาคารระดับสูงใน asset
    /// เฟส 6 (V4 §4/§6) — ตั้งค่าเชื้อเพลิงที่ผลิตเมื่ออาคารถึงระดับสูงสุด (L3):
    ///   • Water Plant L3 → Deuterium (แหล่งเชื้อเพลิงเฟส 2 ของเตา)
    ///   • Laboratory → ไม่ผลิตเชื้อเพลิงแล้ว (สะพาน Tritium ปลดระวาง — Tritium ขุดจากแหล่งแร่โซน B เท่านั้น
    ///     ดู OreDepositSetup · ตั้ง 0 ที่นี่กันรันซ้ำแล้วค่าเก่าคืนชีพ)
    /// รันผ่านเมนู NuclearReMind / Setup Phase 6 Buildings
    /// </summary>
    public static class Phase6BuildingSetup
    {
        private const string Dir = "Assets/ScriptableObjects/Buildings/";

        // เมนูนี้: เขียนค่าการผลิตเชื้อเพลิงลง BuildingData asset (WaterPlant/Laboratory)
        [MenuItem("NuclearReMind/Setup Phase 6 Buildings")]
        public static void Apply()
        {
            int n = 0;
            // minLevel 2 / rate 4 = the early tier from ResearchLab_System_Spec §3. Re-stated here because
            // this menu rewrites the asset — leaving it out would silently undo the L2 tier next run.
            n += SetFuel("WaterPlant", deuterium: 8f, tritium: 0f, minLevel: 2, deuteriumAtMinLevel: 4f);
            n += SetFuel("Laboratory", deuterium: 0f, tritium: 0f); // Tritium ย้ายไปขุดโซน B เท่านั้น
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Phase6BuildingSetup] ตั้งเชื้อเพลิงให้ {n} อาคาร");
            EditorUtility.DisplayDialog("Phase 6 Buildings",
                $"ตั้งเชื้อเพลิงให้ {n} อาคาร:\n  • WaterPlant → Deuterium 4/วัน ที่ Lv.2 · 8/วัน ที่ Lv.3\n" +
                "  • Laboratory → ไม่ผลิตเชื้อเพลิง (Tritium ขุดจากแหล่งแร่โซน B)\n\n" +
                "การสกัดต้องวิจัย \"การสกัดดิวเทอเรียม\" ก่อน แล้วเปิดสวิตช์เองที่แผงโรงน้ำ", "OK");
        }

        private static int SetFuel(string assetName, float deuterium, float tritium,
                                   int minLevel = 0, float deuteriumAtMinLevel = 0f)
        {
            var data = AssetDatabase.LoadAssetAtPath<BuildingData>(Dir + assetName + ".asset");
            if (data == null) { Debug.LogWarning($"[Phase6BuildingSetup] ไม่พบ {assetName}.asset"); return 0; }
            data.deuteriumProduction = deuterium;
            data.tritiumProduction = tritium;
            data.deuteriumMinLevel = minLevel;
            data.deuteriumProductionMinLevel = deuteriumAtMinLevel;
            EditorUtility.SetDirty(data);
            return 1;
        }
    }
}
