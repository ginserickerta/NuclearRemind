using UnityEditor;
using UnityEngine;

namespace NuclearReMind.Editor
{
    public static class PowerGridSetup
    {
        [MenuItem("NuclearReMind/Setup Power Grid System")]
        public static void SetupPowerGrid()
        {
            bool dirty = false;

            // ── 1. ตั้ง powerRange ใน BuildingData assets ──────────────────────
            dirty |= SetPowerRange("PowerPlant", 2, false);
            dirty |= SetPowerRange("CoreTower",  2, false); // phase 0 base; PowerGridManager scale ต่อเอง

            if (dirty) AssetDatabase.SaveAssets();

            // ── 2. สร้าง PowerConduit BuildingData asset (ถ้ายังไม่มี) ─────────
            EnsurePowerConduitAsset();

            // ── 3. สร้าง GameObjects ใน scene ───────────────────────────────────
            EnsureComponent<PowerGridManager>("PowerGridManager");
            EnsureComponent<PowerGridVisual>("PowerGridVisual");

            // ── 4. เพิ่ม PowerConduit เข้า BuildingSelectionUI ────────────────
            AddConduitToHotbar();

            EditorUtility.DisplayDialog("Power Grid Setup",
                "Setup เสร็จแล้ว!\n\n" +
                "ระบบที่สร้าง:\n" +
                "  • PowerGridManager — BFS coverage calculation\n" +
                "  • PowerGridVisual  — overlay บน grid (กด P toggle)\n\n" +
                "อาคารที่เป็น power source:\n" +
                "  • PowerPlant  → range 2 cells\n" +
                "  • CoreTower   → range 2/4/6/8 (scale ตาม phase)\n" +
                "  • PowerConduit → relay ขยาย chain ต่อ (range 3)\n\n" +
                "อาคารนอก range จะ dim ลงและหยุด produce resource\n\n" +
                "อย่าลืม Save Scene (Ctrl+S)", "OK");
        }

        private static bool SetPowerRange(string buildingName, int range, bool isRelay)
        {
            bool changed = false;
            foreach (var guid in AssetDatabase.FindAssets("t:BuildingData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
                if (data == null || data.buildingName != buildingName) continue;

                if (data.powerRange == range && data.isPowerRelay == isRelay) continue;

                data.powerRange    = range;
                data.isPowerRelay  = isRelay;
                EditorUtility.SetDirty(data);
                changed = true;
                Debug.Log($"[PowerGridSetup] {buildingName}: powerRange={range}, isPowerRelay={isRelay}");
            }
            return changed;
        }

        private static void EnsurePowerConduitAsset()
        {
            const string path = "Assets/ScriptableObjects/Buildings/PowerConduit.asset";
            if (AssetDatabase.LoadAssetAtPath<BuildingData>(path) != null) return;

            var conduit              = ScriptableObject.CreateInstance<BuildingData>();
            conduit.buildingName     = "Power Conduit";
            conduit.buildingType     = BuildingType.PowerConduit;
            conduit.size             = Vector2Int.one;
            conduit.powerRange       = 3;
            conduit.isPowerRelay     = true;
            conduit.materialCost     = 30;
            conduit.energyCost       = 15;
            conduit.workerRequired   = 2;
            conduit.energyProduction = -2f; // ดูด energy เล็กน้อยต่อ tick
            conduit.description      = "ตัวส่งต่อพลังงานจาก Core Tower / Power Plant\n" +
                                       "ต้องอยู่ในระยะ power source จึงจะทำงาน\n" +
                                       "ขยายพื้นที่รับพลังงาน 3 ช่องจากตำแหน่งที่วาง";
            conduit.nuclearKnowledge = "ระบบส่งพลังงานไฟฟ้าจากโรงไฟฟ้านิวเคลียร์ใช้สายส่งไฟฟ้า\n" +
                                       "แรงดันสูง (High-Voltage Transmission) เพื่อลดการสูญเสีย\n" +
                                       "พลังงานในระยะทางไกล ก่อนแปลงแรงดันลงที่ substation\n" +
                                       "เพื่อจ่ายให้กับบ้านเรือนและโรงงาน";

            AssetDatabase.CreateAsset(conduit, path);
            AssetDatabase.SaveAssets();
            Debug.Log("[PowerGridSetup] Created PowerConduit.asset");
        }

        private static void EnsureComponent<T>(string goName) where T : Component
        {
            if (Object.FindFirstObjectByType<T>() != null) return;
            var go = new GameObject(goName);
            go.AddComponent<T>();
            Debug.Log($"[PowerGridSetup] Created {goName}");
        }

        private static void AddConduitToHotbar()
        {
            var ui = Object.FindFirstObjectByType<BuildingSelectionUI>();
            if (ui == null)
            {
                Debug.LogWarning("[PowerGridSetup] BuildingSelectionUI ไม่พบใน scene — ข้ามการเพิ่ม PowerConduit ใน hotbar");
                return;
            }

            var conduit = AssetDatabase.LoadAssetAtPath<BuildingData>(
                "Assets/ScriptableObjects/Buildings/PowerConduit.asset");
            if (conduit == null) return;

            var list = new System.Collections.Generic.List<BuildingData>(ui.buildings ?? new BuildingData[0]);
            if (list.Contains(conduit)) return;

            list.Add(conduit);
            ui.buildings = list.ToArray();
            EditorUtility.SetDirty(ui);
            Debug.Log("[PowerGridSetup] เพิ่ม PowerConduit เข้า BuildingSelectionUI.buildings");
        }
    }
}
