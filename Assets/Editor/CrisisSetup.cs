using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Block C — สร้าง 3 crisis DilemmaData assets + wire เข้า DilemmaManager.dilemmaPool
    /// รันผ่านเมนู NuclearReMind / Setup Crisis Dilemmas
    ///
    /// crisis เหล่านี้ trigger ด้วยเงื่อนไข day-end ที่ DilemmaManager ประเมินตอน OnDayEnded:
    ///   heat_above_70 / day_reached_18 / food_below_120
    /// เนื้อหาผูกกับวิทยาศาสตร์นิวเคลียร์ (cooling/meltdown, เวชศาสตร์นิวเคลียร์, food irradiation)
    /// </summary>
    public static class CrisisSetup
    {
        private const string Folder = "Assets/ScriptableObjects/Dilemmas";
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";

        [MenuItem("NuclearReMind/Setup Crisis Dilemmas")]
        public static void SetupAll()
        {
            System.IO.Directory.CreateDirectory(Folder);

            foreach (var def in BuildDefs())
                CreateOrUpdate(def);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            WirePool();

            Debug.Log("[CrisisSetup] สร้าง 3 crisis dilemmas + wire DilemmaManager.dilemmaPool สำเร็จ");
        }

        private static void CreateOrUpdate(CrisisDef def)
        {
            string path = $"{Folder}/{def.id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<DilemmaData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DilemmaData>();
                AssetDatabase.CreateAsset(asset, path);
                Debug.Log($"[CrisisSetup] สร้าง {path}");
            }

            asset.dilemmaId        = def.id;
            asset.scenarioText     = def.scenario;
            asset.choiceAText      = def.aText;
            asset.choiceBText      = def.bText;
            asset.triggerCondition = def.trigger;

            asset.choiceA_FoodChange   = def.aFood;
            asset.choiceA_EnergyChange = def.aEnergy;
            asset.choiceA_WaterChange  = def.aWater;
            asset.choiceA_TrustChange  = def.aTrust;
            asset.choiceA_AethonRelationChange = def.aAethon;
            asset.choiceA_KeranRelationChange  = def.aKeran;

            asset.choiceB_FoodChange   = def.bFood;
            asset.choiceB_EnergyChange = def.bEnergy;
            asset.choiceB_WaterChange  = def.bWater;
            asset.choiceB_TrustChange  = def.bTrust;
            asset.choiceB_AethonRelationChange = def.bAethon;
            asset.choiceB_KeranRelationChange  = def.bKeran;

            EditorUtility.SetDirty(asset);
        }

        // assign DilemmaData ทั้งหมดใน folder (เดิม + ใหม่) เข้า DilemmaManager.dilemmaPool
        private static void WirePool()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var mgr = Object.FindFirstObjectByType<DilemmaManager>();
            if (mgr == null)
            {
                Debug.LogWarning("[CrisisSetup] ไม่พบ DilemmaManager ใน scene — เปิด Gamescene แล้วรันใหม่");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:DilemmaData", new[] { Folder });
            var pool = new List<DilemmaData>();
            foreach (var g in guids)
            {
                var d = AssetDatabase.LoadAssetAtPath<DilemmaData>(AssetDatabase.GUIDToAssetPath(g));
                if (d != null) pool.Add(d);
            }

            mgr.dilemmaPool = pool.ToArray();
            EditorUtility.SetDirty(mgr);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[CrisisSetup] wire dilemmaPool = {pool.Count} dilemmas");
        }

        private static CrisisDef[] BuildDefs() => new[]
        {
            // ── Crisis 1: Plasma Instability (HEAT พุ่ง) ──
            new CrisisDef
            {
                id = "Crisis_PlasmaInstability",
                trigger = "heat_above_70",
                scenario =
@"⚠️ วิกฤต: พลาสมาไม่เสถียร

CORE HEAT พุ่งเกิน 70 — พลาสมาในเตาฟิวชันเริ่มสั่นไหว Aethon วิศวกรหัวหน้าเตือนว่า
ถ้าปล่อยไว้ความร้อนจะทะลุ heat cap และเกิด meltdown

จะจัดการความร้อนนี้อย่างไร?",
                aText = "ระบายความร้อนฉุกเฉิน (ทุ่มพลังงาน+น้ำหล่อเย็น)",
                aEnergy = -300, aWater = -60, aTrust = 5, aAethon = 1,
                bText = "เดินเครื่องต่อ เร่งสร้าง CORE (เสี่ยง)",
                bTrust = -12, bAethon = -2,
            },

            // ── Crisis 2: Malignant Outbreak (รังสีสะสม) ──
            new CrisisDef
            {
                id = "Crisis_MalignantOutbreak",
                trigger = "day_reached_18",
                scenario =
@"⚠️ วิกฤต: การระบาดของเซลล์กลายพันธุ์

คนงานหลายคนที่ทำงานใกล้เขตรังสีเริ่มมีอาการป่วย — เซลล์กลายพันธุ์จากรังสีสะสมลุกลาม
Keran หมอประจำเมืองขอใช้เภสัชรังสี (เวชศาสตร์นิวเคลียร์) รักษาอย่างเร่งด่วน

จะรับมืออย่างไร?",
                aText = "ส่งทีมแพทย์ + เภสัชรังสี รักษาเต็มที่",
                aEnergy = -200, aFood = -100, aTrust = 8, aKeran = 2,
                bText = "กักตัวผู้ป่วย ประหยัดทรัพยากร",
                bTrust = -12, bKeran = -2,
            },

            // ── Crisis 3: Food Crisis (อาหารร่อยหรอ) ──
            new CrisisDef
            {
                id = "Crisis_FoodShortage",
                trigger = "food_below_120",
                scenario =
@"⚠️ วิกฤต: เสบียงอาหารใกล้หมด

คลังอาหารเหลือต่ำกว่า 120 ประชาชนเริ่มอดอยาก มีทางเลือกใช้เทคโนโลยีฉายรังสี
ถนอมอาหาร (food irradiation) เร่งยืดอายุเสบียง แต่ต้องใช้พลังงานมาก

จะแก้วิกฤตนี้อย่างไร?",
                aText = "ปันส่วนอาหารอย่างเข้มงวด",
                aTrust = -10, aAethon = 1,
                bText = "เร่งผลิต + ฉายรังสีถนอมอาหาร (ใช้พลังงาน)",
                bEnergy = -150, bFood = 250, bTrust = 3, bKeran = 1,
            },
        };

        private struct CrisisDef
        {
            public string id, trigger, scenario, aText, bText;
            public float aFood, aEnergy, aWater, aTrust;
            public int aAethon, aKeran;
            public float bFood, bEnergy, bWater, bTrust;
            public int bAethon, bKeran;
        }
    }
}
