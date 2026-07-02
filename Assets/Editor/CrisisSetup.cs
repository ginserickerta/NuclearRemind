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
            asset.choiceCText      = def.cText;
            asset.triggerCondition = def.trigger;

            asset.choiceA_FoodChange   = def.aFood;
            asset.choiceA_EnergyChange = def.aEnergy;
            asset.choiceA_WaterChange  = def.aWater;
            asset.choiceA_IronChange   = def.aIron;
            asset.choiceA_HopeChange   = def.aHope;
            asset.choiceA_AethonRelationChange = def.aAethon;
            asset.choiceA_KeranRelationChange  = def.aKeran;
            asset.choiceA_ForceReactorIdleDays = def.aIdle;

            asset.choiceB_FoodChange   = def.bFood;
            asset.choiceB_EnergyChange = def.bEnergy;
            asset.choiceB_WaterChange  = def.bWater;
            asset.choiceB_IronChange   = def.bIron;
            asset.choiceB_HopeChange   = def.bHope;
            asset.choiceB_AethonRelationChange = def.bAethon;
            asset.choiceB_KeranRelationChange  = def.bKeran;
            asset.choiceB_ForceReactorIdleDays = def.bIdle;

            asset.choiceC_FoodChange   = def.cFood;
            asset.choiceC_EnergyChange = def.cEnergy;
            asset.choiceC_WaterChange  = def.cWater;
            asset.choiceC_IronChange   = def.cIron;
            asset.choiceC_HopeChange   = def.cHope;
            asset.choiceC_AethonRelationChange = def.cAethon;
            asset.choiceC_KeranRelationChange  = def.cKeran;
            asset.choiceC_ForceReactorIdleDays = def.cIdle;

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

        // เนื้อหา 3 วิกฤต A/B/C ตาม V4 §10 · ผูกควิซ (linkedQuizIds) ตั้งโดย QuizSetup:
        //   Plasma→Q2,Q3 · Outbreak→Q4,Q5 · Food→Q6,Q7
        private static CrisisDef[] BuildDefs() => new[]
        {
            // ── วิกฤต 1: Plasma Instability (สนามแม่เหล็กคู่) ──
            new CrisisDef
            {
                id = "Crisis_PlasmaInstability",
                trigger = "heat_above_70",
                scenario =
@"⚠️ วิกฤต 1: เสถียรภาพพลาสมา

พลาสมาหลายร้อยล้านองศาในเตาเริ่มบิดเบี้ยว เสี่ยงหลุดชนผนังและถ่ายเทความร้อนเข้าตัวอาคาร
มีเวลาไม่กี่วันก่อนเตาจะเข้าสู่ภาวะ Meltdown — จะเสริมการกักพลาสมาอย่างไร?",
                aText = "A · เร่งสนามแม่เหล็กวงแหวน (Overdrive Toroidal) — เปลืองพลังงานหนัก",
                aEnergy = -300, aHope = 5, aAethon = 1,
                bText = "B · ซ่อมขดลวดด้วยมือ — ใช้แร่เหล็ก เสี่ยงคนป่วย",
                bIron = -150, bHope = -8, bAethon = -1,
                cText = "C · ฉีดสารหล่อเย็นฉุกเฉิน — ผ่านง่าย แต่เปลืองน้ำ เสี่ยงวิกฤตน้ำตามมา",
                cWater = -200, cHope = 2,
            },

            // ── วิกฤต 2: Malignant Outbreak (เวชศาสตร์นิวเคลียร์) ──
            new CrisisDef
            {
                id = "Crisis_MalignantOutbreak",
                trigger = "day_reached_18",
                scenario =
@"⚠️ วิกฤต 2: โรคกลายพันธุ์

ฝุ่นรังสีทำให้คนงานเขตเหมืองลึกเกิดเซลล์กลายพันธุ์ ป่วยพร้อมกันหลายคน
เวชศาสตร์นิวเคลียร์ทำงาน 2 ขั้น — วินิจฉัย (PET/SPECT) แล้วจึงรักษา — จะจัดการอย่างไร?",
                aText = "A · สแกน PET/SPECT คัดกรอง — รักษาบางส่วน (ใช้พลังงาน)",
                aEnergy = -200, aHope = 5, aKeran = 1,
                bText = "B · ผลิตไอโซโทปการแพทย์จากเตา — เตาเดิน Idle 1 วัน ผลิตยา รักษาครบ",
                bIron = -200, bHope = 8, bKeran = 2, bIdle = 1,
                cText = "C · ฆ่าเชื้อแกมมา + กักตัว — ประหยัด แต่เสี่ยงเสียชีวิต อาหารหมด",
                cFood = -100, cHope = -15, cKeran = -2,
            },

            // ── วิกฤต 3: Food Crisis (พันธุ์พืช/ถนอมอาหาร) ──
            new CrisisDef
            {
                id = "Crisis_FoodShortage",
                trigger = "food_below_120",
                scenario =
@"⚠️ วิกฤต 3: เสบียงเน่า/ขาดแคลน

อาหารร่อยหรอและเน่าเร็ว เทคโนโลยีนิวเคลียร์ช่วยได้ 2 ทาง — ปรับปรุงพันธุ์ด้วยรังสี
หรือฉายรังสีถนอมอาหาร — หรือจะรัดเข็มขัดด้วยการลดปันส่วน?",
                aText = "A · เพาะเมล็ดกลายพันธุ์ (รังสี) — แก้ต้นเหตุ ใช้แร่เหล็ก",
                aIron = -250, aHope = 5, aAethon = 1,
                bText = "B · ฉายรังสีถนอมด้วยโคบอลต์-60 — หยุดเน่า ใช้พลังงานมาก",
                bEnergy = -300, bHope = 5, bKeran = 1,
                cText = "C · ลดปันส่วนอาหาร — ประหยัด แต่ Hope ดิ่ง เสี่ยงจลาจล",
                cHope = -12,
            },
        };

        private struct CrisisDef
        {
            public string id, trigger, scenario, aText, bText, cText;
            public float aFood, aEnergy, aWater, aIron, aHope; public int aAethon, aKeran, aIdle;
            public float bFood, bEnergy, bWater, bIron, bHope; public int bAethon, bKeran, bIdle;
            public float cFood, cEnergy, cWater, cIron, cHope; public int cAethon, cKeran, cIdle;
        }
    }
}
