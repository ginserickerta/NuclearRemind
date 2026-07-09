using UnityEditor;
using UnityEngine;

namespace NuclearReMind.Editor
{
    /// <summary>
    /// เฟส 6 (V4 §4/§6) — ตั้งค่าเชื้อเพลิงที่ผลิตเมื่ออาคารถึงระดับสูงสุด (L3):
    ///   • Water Plant L3 → Deuterium (แหล่งเชื้อเพลิงเฟส 2 ของเตา)
    ///   • Laboratory → ไม่ผลิตเชื้อเพลิงแล้ว (สะพาน Tritium ปลดระวาง — Tritium ขุดจากแหล่งแร่โซน B เท่านั้น
    ///     ดู OreDepositSetup · ตั้ง 0 ที่นี่กันรันซ้ำแล้วค่าเก่าคืนชีพ)
    /// รันผ่านเมนู NuclearReMind / Setup Phase 6 Buildings
    /// </summary>
    public static class Phase6BuildingSetup
    {
        private const string Dir = "Assets/ScriptableObjects/Buildings/";

        [MenuItem("NuclearReMind/Setup Phase 6 Buildings")]
        public static void Apply()
        {
            int n = 0;
            n += SetFuel("WaterPlant", deuterium: 8f, tritium: 0f);
            n += SetFuel("Laboratory", deuterium: 0f, tritium: 0f); // Tritium ย้ายไปขุดโซน B เท่านั้น
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Phase6BuildingSetup] ตั้งเชื้อเพลิง L3 ให้ {n} อาคาร");
            EditorUtility.DisplayDialog("Phase 6 Buildings",
                $"ตั้งเชื้อเพลิง L3 ให้ {n} อาคาร:\n  • WaterPlant → Deuterium 8/tick\n  • Laboratory → ไม่ผลิตเชื้อเพลิง (Tritium ขุดจากแหล่งแร่โซน B)\n\n" +
                "อัปอาคารถึง L3 (กด U ที่อาคาร) แล้วจะเริ่มผลิตเชื้อเพลิงป้อนเตา", "OK");
        }

        private static int SetFuel(string assetName, float deuterium, float tritium)
        {
            var data = AssetDatabase.LoadAssetAtPath<BuildingData>(Dir + assetName + ".asset");
            if (data == null) { Debug.LogWarning($"[Phase6BuildingSetup] ไม่พบ {assetName}.asset"); return 0; }
            data.deuteriumProduction = deuterium;
            data.tritiumProduction = tritium;
            EditorUtility.SetDirty(data);
            return 1;
        }
    }
}
