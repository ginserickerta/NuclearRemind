using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// ติดตั้งระบบแหล่งแร่เหล็ก (V4 §5 — โซน A ปลอดภัย/โซน B เสี่ยงรังสี):
    ///   1. สร้าง sprite ก้อนแร่ A/B (วาดสีจริง — ล้อ EnsureWorkerSprite ไม่ผ่าน silhouette ให้แยกโซนออกด้วยตา)
    ///   2. สร้าง/อัปเดต BuildingData assets (OreDepositA/B — isOreNode + โควตา/ความเสี่ยง)
    ///   3. ต่อทั้งคู่เข้า BuildingRegistry.allBuildingData (ให้เซฟ restore ได้ — **คง Mine ไว้** เซฟเก่าต้องใช้)
    ///   4. ถอด Mine ออกจาก hotbar (เหล็กมาจากการขุดแหล่งแร่เท่านั้น — การตัดสินใจออกแบบ)
    ///   5. สร้าง GameObject "OreDepositManager" + wire assets (root เดี่ยว — idiom RadiationSetup)
    ///   6. ตั้งเส้นแบ่งโซน zoneAColumns — โซน A = คอลัมน์ 0..28 (29×28) · โซน B = 29..42 (14×28)
    ///
    /// ★ ที่นี่คือ "แหล่งความจริงเดียว" ของเส้นแบ่งโซน — ZoneBarrierSetup อ่านค่านี้ไปวางรั้ว/ประตูให้ตรงแนว
    /// รัน: เมนู NuclearReMind/Setup Ore Deposits (Zone A-B) — หรือรวมใน Run All Setups
    /// </summary>
    public static class OreDepositSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const string BuildingDir = "Assets/ScriptableObjects/Buildings/";
        private const string SpriteFolder = "Assets/Sprites/Buildings";
        private const int PixelsPerUnit = 64; // เท่าอาคาร (BuildingPixelsPerUnit)

        // คอลัมน์แรกของโซน B บนกริด 43×28 → โซน A กว้าง 29 ช่อง · โซน B กว้าง 14 ช่อง
        private const int ZoneAColumns = 29;

        [MenuItem("NuclearReMind/Setup Ore Deposits (Zone A-B)")]
        public static void SetupAll()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var spriteA = EnsureOreSprite("OreDepositA", risky: false);
            var spriteB = EnsureOreSprite("OreDepositB", risky: true);

            // โซน A: ใกล้เมือง โควตาน้อย ปลอดภัย (2 คน)
            var nodeA = EnsureNodeAsset("OreDepositA", spriteA, b =>
            {
                b.buildingName = "แหล่งแร่เหล็ก (โซน A)";
                b.description = "แหล่งแร่ผิวดินใกล้เมือง — ส่งคนงานมาขุดได้ทันที ปลอดภัย " +
                                "ปริมาณที่ขุดได้สุ่มใหม่ทุกวัน (น้อยแต่ชัวร์)";
                b.nuclearKnowledge = "แร่ผิวดินรับรังสีพื้นหลังธรรมชาติ (background radiation) เท่าที่มนุษย์เจอทุกวันอยู่แล้ว " +
                                     "~2-3 mSv/ปี จากดิน หิน และรังสีคอสมิก — การขุดตื้นจึงไม่เพิ่มความเสี่ยงอย่างมีนัยสำคัญ";
                b.workerRequired = 2;
                b.oreQuotaMin = 15f;
                b.oreQuotaMax = 35f;
                b.oreExposurePerWorkerDay = 0f;
                b.oreSickChancePerWorkerDay = 0f;
            });

            // โซน B: ไกล โควตาสูง + Tritium (แหล่งเดียวในเกม — V4 §4) เสี่ยงรังสี+ป่วย (3 คน)
            var nodeB = EnsureNodeAsset("OreDepositB", spriteB, b =>
            {
                b.buildingName = "แหล่งแร่เหล็ก (โซน B)";
                b.description = "สายแร่ลึกชานเมือง — เหล็กเยอะ + แร่ลิเธียมสำหรับสกัด Tritium (เชื้อเพลิงเตาเฟส 3) " +
                                "แต่ฝุ่นแร่ปนเปื้อนรังสี: รังสีสะสมของเมืองเพิ่มทุกวันที่ขุด และคนงานเสี่ยงล้มป่วย " +
                                "(หลัก ALARA: อย่าส่งคนเกินจำเป็น)";
                b.nuclearKnowledge = "Tritium (³H) ไม่ได้ขุดได้ตรง ๆ — สกัดจากลิเธียมในหิน: Li-6 จับนิวตรอนแล้วแตกตัว " +
                                     "เป็น Tritium + ฮีเลียม (Li-6 + n → T + He-4) · หลัก ALARA: รับรังสีน้อยที่สุดด้วย " +
                                     "ลดเวลา (Time) เพิ่มระยะห่าง (Distance) ใช้เครื่องกำบัง (Shielding) — " +
                                     "เหมืองลึกจริงยังเสี่ยงก๊าซเรดอน (Rn-222) ปล่อยอนุภาคแอลฟาทำลายเนื้อเยื่อปอด";
                b.workerRequired = 3;
                b.oreQuotaMin = 50f;
                b.oreQuotaMax = 100f;
                b.oreTritiumMin = 3f;  // เตาเฟส 3 เผา 10/20/30 ต่อวันตามโหมด — 3 โหนดเต็มสูบ ≈ 16.5/วัน
                b.oreTritiumMax = 8f;
                b.oreExposurePerWorkerDay = 1.0f;
                b.oreSickChancePerWorkerDay = 0.05f;
            });

            WireScene(nodeA, nodeB);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[OreDepositSetup] ✅ ติดตั้งระบบแหล่งแร่สำเร็จ — โหนดจะ scatter ตอนกด Play (OreDepositManager.Start)");
        }

        // ─────────────────────────────────────────
        //  BuildingData assets
        // ─────────────────────────────────────────

        private static BuildingData EnsureNodeAsset(string assetName, Sprite sprite, System.Action<BuildingData> configure)
        {
            string path = BuildingDir + assetName + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<BuildingData>();
                AssetDatabase.CreateAsset(data, path);
                Debug.Log($"[OreDepositSetup] สร้าง {path}");
            }

            // ค่าคงที่ของภูมิประเทศ (รันซ้ำ = อัปเดตค่า idempotent เหมือน SetBuilding ของ BuildingBalanceSetup)
            data.size = Vector2Int.one;
            data.buildingType = BuildingType.OreDeposit;
            data.ironCost = 0;      // pre-placed ต้องราคา 0 (ResourceManager หักตอนรับ event)
            data.energyCost = 0;
            data.buildTicks = 1;    // ข้ามคิวก่อสร้างอยู่แล้ว (CompleteRequested) — กันเหนียว
            data.ironProduction = 0f; // เหล็กมาจากโควตา (isOreNode) ไม่ใช่ ironProduction
            data.isOreNode = true;

            configure(data);

            if (data.sprite == null) data.sprite = sprite;
            EditorUtility.SetDirty(data);

            Debug.Log($"[OreDepositSetup] {assetName}: worker={data.workerRequired} " +
                      $"เหล็ก {data.oreQuotaMin}-{data.oreQuotaMax}/วัน " +
                      (data.oreTritiumMax > 0f ? $"Tritium {data.oreTritiumMin}-{data.oreTritiumMax}/วัน " : "") +
                      $"exposure {data.oreExposurePerWorkerDay}/คน/วัน sick {data.oreSickChancePerWorkerDay:P0}/คน/วัน");
            return data;
        }

        // ─────────────────────────────────────────
        //  Scene wiring (registry + hotbar + manager)
        // ─────────────────────────────────────────

        private static void WireScene(BuildingData nodeA, BuildingData nodeB)
        {
            // registry: เพิ่มโหนดทั้งคู่ (restore จากเซฟ) — คง Mine ไว้ให้เซฟเก่า
            var registry = Object.FindFirstObjectByType<BuildingRegistry>();
            if (registry != null)
            {
                bool changed = AppendIfMissing(ref registry.allBuildingData, nodeA);
                changed |= AppendIfMissing(ref registry.allBuildingData, nodeB);
                if (changed)
                {
                    EditorUtility.SetDirty(registry);
                    Debug.Log($"[OreDepositSetup] BuildingRegistry.allBuildingData = {registry.allBuildingData.Length} รายการ (เพิ่มโหนด A/B)");
                }
            }
            else
                Debug.LogWarning("[OreDepositSetup] ไม่พบ BuildingRegistry ในซีน — โหนดจะ restore จากเซฟไม่ได้");

            // hotbar: ถอด Mine (เหล็กมาจากแหล่งแร่เท่านั้น) — sync BuildingSelectionUI ให้ตรงเสมอ
            var mine = AssetDatabase.LoadAssetAtPath<BuildingData>(BuildingDir + "Mine.asset");
            var placement = Object.FindFirstObjectByType<PlacementController>();
            if (placement != null)
            {
                if (RemoveIfPresent(ref placement.buildingHotbar, mine))
                {
                    EditorUtility.SetDirty(placement);
                    Debug.Log($"[OreDepositSetup] ถอด Mine ออกจาก hotbar — เหลือ {placement.buildingHotbar.Length} ช่อง");
                }

                var selUI = Object.FindFirstObjectByType<BuildingSelectionUI>();
                if (selUI != null)
                {
                    selUI.buildings = (BuildingData[])placement.buildingHotbar.Clone();
                    EditorUtility.SetDirty(selUI);
                }

                // ปรับความกว้าง panel ตามจำนวนช่องใหม่ (สูตรเดียวกับ Day11Setup)
                var panelGO = GameObject.Find("BuildingSelectionPanel");
                var panelRect = panelGO != null ? panelGO.GetComponent<RectTransform>() : null;
                if (panelRect != null)
                {
                    int count = placement.buildingHotbar.Length;
                    panelRect.sizeDelta = new Vector2((count + 1) * 94f + 8f, 118f); // +1 = ปุ่มทุบอาคาร
                    EditorUtility.SetDirty(panelRect);
                }
            }
            else
                Debug.LogWarning("[OreDepositSetup] ไม่พบ PlacementController ในซีน — ข้ามการถอด Mine");

            // manager: root GO เดี่ยว (ซีนไม่มี parent "Managers" — idiom RadiationSetup)
            var go = GameObject.Find("OreDepositManager");
            if (go == null)
            {
                go = new GameObject("OreDepositManager");
                Debug.Log("[OreDepositSetup] สร้าง OreDepositManager ใน scene");
            }
            var mgr = go.GetComponent<OreDepositManager>();
            if (mgr == null) mgr = go.AddComponent<OreDepositManager>();
            mgr.zoneANode = nodeA;
            mgr.zoneBNode = nodeB;
            mgr.zoneAColumns = ZoneAColumns; // เส้นแบ่งโซน — ZoneBarrierSetup วางรั้วตามค่านี้
            EditorUtility.SetDirty(mgr);

            var grid = Object.FindFirstObjectByType<GridManager>();
            if (grid != null && ZoneAColumns >= grid.columns)
                Debug.LogWarning($"[OreDepositSetup] zoneAColumns ({ZoneAColumns}) ≥ กริด {grid.columns} คอลัมน์ " +
                                 "— โซน B จะไม่เหลือพื้นที่ (รัน Setup Grid ก่อน)");
        }

        private static bool AppendIfMissing(ref BuildingData[] array, BuildingData item)
        {
            if (item == null) return false;
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

        // ─────────────────────────────────────────
        //  Sprites — กองหินแร่ วาดสีจริง (ไม่ผ่าน silhouette — แยกโซนด้วยสี)
        //  helpers คัดลอกจาก PlaceholderSpriteGenerator (เป็น private ที่นั่น — ไม่แตะไฟล์เดิม)
        // ─────────────────────────────────────────

        private static Sprite EnsureOreSprite(string name, bool risky)
        {
            string path = Path.Combine(SpriteFolder, name + ".png");
            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
            {
                Directory.CreateDirectory(SpriteFolder);
                WritePng(path, risky ? DrawOreB() : DrawOreA());
                AssetDatabase.Refresh();
                ConfigureOreSprite(path);
                Debug.Log($"[OreDepositSetup] สร้าง sprite {path}");
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // โซน A: กองหินเทาน้ำตาล + ประกายแร่เงิน (โทนอบอุ่น ปลอดภัย)
        private static Texture2D DrawOreA()
        {
            var tex = NewCanvas(48, 36);
            Color baseRock = new Color(0.42f, 0.38f, 0.34f);
            Color rock = new Color(0.56f, 0.51f, 0.45f);
            Color rockLight = new Color(0.66f, 0.61f, 0.54f);
            Color glint = new Color(0.80f, 0.81f, 0.86f); // ประกายโลหะ

            // กองหินซ้อน 3 ก้อน (เงาก้อนฐานก่อน)
            FillEllipse(tex, 24, 8, 20, 8, baseRock);
            FillCircle(tex, 15, 12, 8, rock);
            FillCircle(tex, 31, 11, 9, rock);
            FillCircle(tex, 23, 17, 9, rockLight);

            // ประกายแร่
            FillRect(tex, 21, 18, 22, 19, glint);
            FillRect(tex, 30, 12, 31, 13, glint);
            FillRect(tex, 13, 12, 14, 13, glint);
            FillRect(tex, 25, 9, 26, 10, glint);

            tex.Apply();
            return tex;
        }

        // โซน B: หินเข้ม + ผลึกแดงส้มเรือง (โทนอันตราย — สื่อรังสี)
        private static Texture2D DrawOreB()
        {
            var tex = NewCanvas(48, 40);
            Color baseRock = new Color(0.26f, 0.23f, 0.27f);
            Color rock = new Color(0.36f, 0.32f, 0.37f);
            Color crystal = new Color(0.90f, 0.30f, 0.16f); // ผลึกแดงส้ม
            Color crystalGlow = new Color(1.00f, 0.58f, 0.24f);

            // กองหินเข้ม
            FillEllipse(tex, 24, 8, 20, 8, baseRock);
            FillCircle(tex, 16, 12, 8, rock);
            FillCircle(tex, 31, 11, 9, rock);
            FillCircle(tex, 23, 16, 8, rock);

            // ผลึกแหลมโผล่จากกองหิน 3 แท่ง
            FillTriangle(tex, new Vector2(12, 14), new Vector2(20, 14), new Vector2(16, 30), crystal);
            FillTriangle(tex, new Vector2(20, 16), new Vector2(28, 16), new Vector2(24, 36), crystal);
            FillTriangle(tex, new Vector2(28, 13), new Vector2(35, 13), new Vector2(32, 27), crystal);

            // ไฮไลต์เรืองบนผลึก
            FillTriangle(tex, new Vector2(22, 18), new Vector2(25, 18), new Vector2(24, 32), crystalGlow);
            FillRect(tex, 15, 20, 16, 26, crystalGlow);

            tex.Apply();
            return tex;
        }

        // ── drawing primitives (คัดลอกจาก PlaceholderSpriteGenerator — private ที่ต้นทาง) ──

        private static Texture2D NewCanvas(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var clear = new Color(0, 0, 0, 0);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;
            tex.SetPixels(pixels);
            return tex;
        }

        private static void SetPixelSafe(Texture2D tex, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= tex.width || y >= tex.height) return;
            tex.SetPixel(x, y, c);
        }

        private static void FillRect(Texture2D tex, int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    SetPixelSafe(tex, x, y, c);
        }

        private static void FillCircle(Texture2D tex, int cx, int cy, int r, Color c)
        {
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r)
                        SetPixelSafe(tex, x, y, c);
        }

        private static void FillEllipse(Texture2D tex, int cx, int cy, int rx, int ry, Color c)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
            {
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    float nx = (x - cx) / (float)rx;
                    float ny = (y - cy) / (float)ry;
                    if (nx * nx + ny * ny <= 1f)
                        SetPixelSafe(tex, x, y, c);
                }
            }
        }

        private static void FillTriangle(Texture2D tex, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int minX = Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x));
            int maxX = Mathf.CeilToInt(Mathf.Max(a.x, b.x, c.x));
            int minY = Mathf.FloorToInt(Mathf.Min(a.y, b.y, c.y));
            int maxY = Mathf.CeilToInt(Mathf.Max(a.y, b.y, c.y));

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    if (PointInTriangle(new Vector2(x + 0.5f, y + 0.5f), a, b, c))
                        SetPixelSafe(tex, x, y, color);
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
            => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

        private static void WritePng(string path, Texture2D tex)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        // pivot bottom-center + PPU 64 — เหมือน ConfigureBuildingSprite ของ generator
        private static void ConfigureOreSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spritePivot = new Vector2(0.5f, 0f);
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }
    }
}
