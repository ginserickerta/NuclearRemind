using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.Editor
{
    /// <summary>
    /// [TH] หน้าที่: สคริปต์ Editor ตั้งค่าระบบประชากร (ธงฝึกอาชีพ + ระบบจัดสรรคนงาน) ให้ซีนเกม
    /// เฟส 3 (V4 §5) — ตั้งค่าระบบประชากร:
    ///   • ธงปลดล็อกฝึกคลาสบน Laboratory (Engineer + Farmer — Medic อยู่โรงพยาบาล ตามสเปกโรงวิจัย)
    ///   • ระบบจัดสรรคนงานรายอาคาร (Worker sprites): WorkerAssignmentManager + WorkerVisualSpawner + sprite คนงาน
    /// รันผ่านเมนู NuclearReMind / Setup Phase 3 Population (ต้องเปิด Gamescene ก่อนเพื่อ wire object ในซีน)
    /// </summary>
    public static class Phase3PopulationSetup
    {
        private const string Dir = "Assets/ScriptableObjects/Buildings/";

        // เมนูนี้: ตั้งธงปลดล็อกฝึกอาชีพ + ติดตั้งระบบจัดสรรคนงานในซีนเกม
        [MenuItem("NuclearReMind/Setup Phase 3 Population")]
        public static void Apply()
        {
            // 1) ธงปลดล็อกฝึกคลาส — สเปกโรงวิจัย (ResearchLab_Spec): Lab ฝึก Engineer/Farmer ·
            //    Medic ย้ายไปโรงพยาบาล (Hospital → unlocksMedicTraining ตั้งใน HospitalSetup)
            var lab = AssetDatabase.LoadAssetAtPath<BuildingData>(Dir + "Laboratory.asset");
            if (lab != null)
            {
                lab.unlocksEngineerTraining = true;
                lab.unlocksMedicTraining = false; // ★ สเปก §6: ฝึกแพทย์อยู่โรงพยาบาล ไม่ใช่ห้องวิจัย
                lab.unlocksFarmerTraining = true;
                EditorUtility.SetDirty(lab);
                AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.LogWarning("[Phase3PopulationSetup] ไม่พบ Laboratory.asset — ข้ามการตั้งธงฝึกคลาส");
            }

            // 2) ระบบจัดสรรคนงาน (V4 §5) — manager + visual spawner + sprite คนงาน ในซีน
            SetupWorkerAssignmentSystem();

            // 3) migrate TrainPanel ใน scene เดิมให้มีปุ่มฝึกครบ 3 คลาส (scene สร้างใหม่ได้จาก HUDCanvasSetup อยู่แล้ว)
            MigrateTrainPanel();

            AssetDatabase.Refresh();
            Debug.Log("[Phase3PopulationSetup] Lab flags + Worker assignment system พร้อม (กด Ctrl+S บันทึกซีน)");
        }

        // migrate TrainPanel เดิม (2 ปุ่ม −64/+64 กว้าง 122) → 3 ปุ่ม (−86/0/+86 กว้าง 80) + ปุ่มฝึกเกษตรกร
        // idempotent: มี TrainFarmerBtn แล้ว → แค่จัดตำแหน่ง + rewire
        private static void MigrateTrainPanel()
        {
            var hud = Object.FindFirstObjectByType<UIManagerHUD>();
            var panelGO = GameObject.Find("TrainPanel");
            if (hud == null || panelGO == null)
            {
                Debug.LogWarning("[Phase3PopulationSetup] ไม่พบ UIManagerHUD/TrainPanel — ข้าม migrate ปุ่มฝึก " +
                                 "(HUD ที่สร้างใหม่ด้วย Setup HUD Canvas มีครบ 3 ปุ่มแล้ว)");
                return;
            }

            var panel = panelGO.transform;
            ReLayoutTrainButton(panel, "TrainEngineerBtn", new Vector2(-86, 0));
            ReLayoutTrainButton(panel, "TrainMedicBtn", new Vector2(0, 0));

            var farmerTf = panel.Find("TrainFarmerBtn");
            UnityEngine.UI.Button farmerBtn;
            if (farmerTf != null)
            {
                farmerBtn = farmerTf.GetComponent<UnityEngine.UI.Button>();
                ReLayoutTrainButton(panel, "TrainFarmerBtn", new Vector2(86, 0));
            }
            else
            {
                var anyLabel = panelGO.GetComponentInChildren<UnityEngine.UI.Text>();
                var font = anyLabel != null ? anyLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                farmerBtn = CreateTrainButton(panel, font, "TrainFarmerBtn", "ฝึกเกษตรกร", new Vector2(86, 0));
            }

            hud.trainFarmerButton = farmerBtn;
            EditorUtility.SetDirty(hud);
            Debug.Log("[Phase3PopulationSetup] TrainPanel: ปุ่มฝึกครบ 3 คลาส (วิศวกร/แพทย์/เกษตรกร)");
        }

        private static void ReLayoutTrainButton(Transform panel, string name, Vector2 pos)
        {
            var tf = panel.Find(name);
            if (tf == null) return;
            var rect = tf.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(80, 36);

            var label = tf.GetComponentInChildren<UnityEngine.UI.Text>();
            if (label != null) label.fontSize = 14; // ปุ่มแคบลง (3 ปุ่มใน 260px) — กันข้อความล้น
        }

        // ปุ่มโครงเดียวกับ HUDCanvasSetup.CreateButton (private ที่ต้นทาง — คัดลอกมา local)
        private static UnityEngine.UI.Button CreateTrainButton(Transform parent, Font font, string name, string label, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(80, 36);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = Color.white;
            var btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            btn.transition = UnityEngine.UI.Selectable.Transition.None; // UIManagerHUD คุมสีเอง (ตรงต้นแบบ)

            var textGO = new GameObject("Label", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var trect = textGO.GetComponent<RectTransform>();
            trect.anchorMin = Vector2.zero;
            trect.anchorMax = Vector2.one;
            trect.offsetMin = Vector2.zero;
            trect.offsetMax = Vector2.zero;
            var t = textGO.AddComponent<UnityEngine.UI.Text>();
            t.font = font;
            t.fontSize = 14; // ปุ่มแคบ (80px) — กันข้อความไทยล้น
            t.fontStyle = FontStyle.Bold;
            t.color = Color.black;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = label;

            return btn;
        }

        // สร้าง GameObject ของ WorkerAssignmentManager + WorkerVisualSpawner ในซีน (idempotent — find-or-create)
        private static void SetupWorkerAssignmentSystem()
        {
            // manager จัดสรร — MonoBehaviour ล้วน ไม่มี field ต้อง wire
            var wamGo = GameObject.Find("WorkerAssignmentManager") ?? new GameObject("WorkerAssignmentManager");
            if (wamGo.GetComponent<WorkerAssignmentManager>() == null)
                wamGo.AddComponent<WorkerAssignmentManager>();

            // parent ของ sprite คนงาน (จัดกลุ่มใน hierarchy)
            var parentGo = GameObject.Find("WorkersParent") ?? new GameObject("WorkersParent");

            // visual spawner — wire sprite คนงาน + parent
            var spawnerGo = GameObject.Find("WorkerVisualSpawner") ?? new GameObject("WorkerVisualSpawner");
            var spawner = spawnerGo.GetComponent<WorkerVisualSpawner>() ?? spawnerGo.AddComponent<WorkerVisualSpawner>();
            spawner.workersParent = parentGo.transform;
            // ใส่ placeholder แค่ตอนยังไม่มี sprite เลย (ซีนใหม่เอี่ยม) — ห้ามทับอาร์ตจริงที่
            // "Setup/Character Sprites" wire ไว้ (ปัญหาเดิม: รัน Run All Setups ทีไร worker กลับไปเป็น placeholder ทุกที)
            if (spawner.workerSprite == null)
                spawner.workerSprite = EditorTools.PlaceholderSpriteGenerator.EnsureWorkerSprite("Worker");
            EditorUtility.SetDirty(spawner);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
