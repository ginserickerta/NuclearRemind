using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: สคริปต์ Editor สร้างอนุสรณ์สถานในซีนเกม (asset + วางในฉาก + ต่อสาย UI)
    /// อนุสรณ์ทีมสร้างหอคอย (STORY.md §② MEMORIAL) — MemorialSO + BuildingData + PrePlacedBuilding
    /// + wire เข้า MemorialPanelController · คลิกได้ตั้งแต่ Day 1 · คลิกแรก +2 Hope และยิง Inner Voice V02
    ///
    /// แยกออกมาจาก StorySetup (v6.3 cutover): StorySetup ถูก archive เพราะ beats ผูก `day == X` ซึ่งขัด
    /// กฎข้อ 1 — แต่อนุสรณ์ไม่ได้ผูกวันเลยและยังเป็นสเปกปัจจุบัน ถ้าไม่แยกออกมา เชน Run All Setups
    /// จะไม่สร้างอนุสรณ์อีกเลย (เมนูเดิมย้ายไป Legacy แล้ว ExecuteMenuItem จะข้ามเงียบ)
    /// </summary>
    public static class MemorialSetup
    {
        private const string StoryFolder    = "Assets/ScriptableObjects/Story";
        private const string BuildingFolder = "Assets/ScriptableObjects/Buildings";
        private const string ScenePath      = "Assets/Scenes/Gamescene.unity";

        // ตำแหน่งเดิมจาก StorySetup.WireScene — ไม่ผูกสูตรขนาดกริด (เคาะไว้ที่ (17,17))
        private static readonly Vector2Int MemorialCell = new Vector2Int(17, 17);

        // เมนูนี้: สร้างอนุสรณ์สถานในซีนเกม (asset + วางที่ช่อง 17,17 + ต่อสายเข้า UI)
        [MenuItem("NuclearReMind/Setup Memorial (STORY.md §2)")]
        public static void Setup()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var memorial = CreateMemorialAssets(out var memorialBuilding);
            WireScene(memorial, memorialBuilding);
        }

        private static MemorialSO CreateMemorialAssets(out BuildingData building)
        {
            var memorial = CreateOrLoad<MemorialSO>($"{StoryFolder}/Memorial_veltara.asset");
            memorial.headerTH = "เพื่อจดจำทีมสร้างหอคอย — Veltara Core Project";
            // Elara คือ 1 ใน 6 ชื่อ — เกมไม่ชี้ (ปมเชื่อมกับบันทึกกู้คืน) · อีก 5 ชื่อทีมแก้ได้ใน asset
            memorial.names = new[]
            {
                "DARIUS KOHL — Chief Systems Engineer — 2149–2157",
                "ELARA VANE — Lead Reactor Physicist — 2151–2157",
                "SELENE MARSH — Medical Physicist — 2152–2157",
                "TOMAS REVIK — Plasma Diagnostics — 2150–2157",
                "ANYA PETROVA — Fuel Cycle Specialist — 2151–2157",
                "JUN OKADA — Cooling Systems Architect — 2148–2157",
            };
            // fallback เท่านั้น — บทจริงคือ V02 ใน Resources/InnerVoice ที่ MemorialPanelController ยิง
            memorial.innerVoiceOnFirstOpen = "คนพวกนี้เคยอยู่ที่นี่... ก่อนผมมาถึง";
            EditorUtility.SetDirty(memorial);

            // BuildingData "อนุสรณ์" — ราคา 0 (PrePlacedBuilding หักค่าสร้างตามปกติ) ไม่เข้า hotbar
            building = CreateOrLoad<BuildingData>($"{BuildingFolder}/Memorial.asset");
            building.buildingName = "อนุสรณ์";
            building.description = "อนุสรณ์ทีมสร้างหอคอยทั้ง 6 คน — คลิกที่ตัวอาคารเพื่อเปิดแผงรายชื่อ";
            building.nuclearKnowledge = "Veltara Core Project คือทีมที่จุดเตาฟิวชันครั้งแรกเมื่อปี 2157 " +
                                        "ความผิดพลาดของพวกเขาไม่ใช่วิทยาศาสตร์ แต่คือการเดินเครื่องเกินขีดที่ระบบหล่อเย็นรับไหว";
            building.size = new Vector2Int(3, 3);
            building.spriteScale = 0.7f;
            building.buildingType = BuildingType.Memorial;
            building.ironCost = 0;
            building.energyCost = 0;
            building.workerRequired = 0;
            if (building.sprite == null)
                building.sprite = PlaceholderSpriteGenerator.EnsureBuildingSprite("Memorial");
            EditorUtility.SetDirty(building);

            return memorial;
        }

        private static void WireScene(MemorialSO memorial, BuildingData memorialBuilding)
        {
            var memorialPanel = Object.FindFirstObjectByType<MemorialPanelController>();
            if (memorialPanel != null)
            {
                memorialPanel.memorialData = memorial;
                EditorUtility.SetDirty(memorialPanel);
            }
            else
            {
                Debug.LogWarning("[MemorialSetup] ไม่พบ MemorialPanelController — รัน NuclearReMind/Setup Story UI ก่อน (แผงอนุสรณ์จะไม่มีข้อมูล)");
            }

            var go = GameObject.Find("PrePlacedMemorial") ?? new GameObject("PrePlacedMemorial");
            var pre = go.GetComponent<PrePlacedBuilding>() ?? go.AddComponent<PrePlacedBuilding>();
            pre.building = memorialBuilding;
            pre.cell = MemorialCell;
            EditorUtility.SetDirty(pre);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[MemorialSetup] อนุสรณ์ 6 ชื่อที่ ({MemorialCell.x},{MemorialCell.y}) + wire แผงเรียบร้อย");
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
                Debug.Log($"[MemorialSetup] สร้าง {path}");
            }
            return asset;
        }
    }
}
