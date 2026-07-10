using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// นำเข้าอาร์ตตัวละครจริง (คนงาน/วิศวกร/หมอ) เป็น sprite แล้ว wire เข้ากับ WorkerVisualSpawner ในซีน
    /// แทน placeholder ที่วาดด้วยโค้ด · idempotent — รันซ้ำได้
    ///
    /// ต้นทางไฟล์อาร์ต (คัดลอกเข้ามาแล้วใน Assets/Sprites/Characters/):
    ///   Worker_Art.png = คนงาน · Engineer.png = วิศวะ · Medic.png = หมอ
    ///   (Scientist.png = นักวิทยาศาสตร์, Patient.png = คนป่วย — นำเข้าไว้ให้ใช้ต่อ ยังไม่ wire)
    ///
    /// ปรับขนาดตัวละครบนแมพได้ที่ CharacterPixelsPerUnit — ค่ามากขึ้น = ตัวเล็กลง
    /// (อาร์ตสูง ~1600px · 1600/3200 ≈ 0.5 unit สูง ~1.4 เท่าของ placeholder เดิม 0.35)
    /// </summary>
    public static class CharacterSpriteSetup
    {
        private const string Dir = "Assets/Sprites/Characters/";
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const float CharacterPixelsPerUnit = 3200f;

        private const string WorkerArt = Dir + "Worker_Art.png";
        private const string EngineerArt = Dir + "Engineer.png";
        private const string MedicArt = Dir + "Medic.png";
        private const string ScientistArt = Dir + "Scientist.png";
        private const string PatientArt = Dir + "Patient.png";

        [MenuItem("NuclearReMind/Setup/Character Sprites (คนงาน·วิศวกร·หมอ)")]
        public static void Run()
        {
            // 1) ตั้งค่า import ทุกไฟล์เป็น Sprite (pivot ล่างกลาง, ขนาดตาม PPU)
            ConfigureSprite(WorkerArt);
            ConfigureSprite(EngineerArt);
            ConfigureSprite(MedicArt);
            ConfigureSprite(ScientistArt);
            ConfigureSprite(PatientArt);
            AssetDatabase.SaveAssets();

            // 2) wire เข้า WorkerVisualSpawner ในซีน
            WireSpawner();

            Debug.Log("[CharacterSpriteSetup] เสร็จ — คนงาน/วิศวกร/หมอ ใช้อาร์ตจริงแล้ว");
        }

        private static void ConfigureSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[CharacterSpriteSetup] ไม่พบไฟล์อาร์ต: {path}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = CharacterPixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter; // ยืนบนพื้น (pivot ล่างกลาง)
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        private static void WireSpawner()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // find-or-create (mirror Phase3PopulationSetup) — เผื่อรันก่อน Phase3
            var parentGo = GameObject.Find("WorkersParent") ?? new GameObject("WorkersParent");
            var spawnerGo = GameObject.Find("WorkerVisualSpawner") ?? new GameObject("WorkerVisualSpawner");
            var spawner = spawnerGo.GetComponent<WorkerVisualSpawner>()
                          ?? spawnerGo.AddComponent<WorkerVisualSpawner>();

            spawner.workersParent = parentGo.transform;
            spawner.workerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WorkerArt);
            spawner.engineerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EngineerArt);
            spawner.medicSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MedicArt);
            // farmerSprite ปล่อยว่าง → fallback เป็นคนงาน (ตามที่ตกลง)

            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
