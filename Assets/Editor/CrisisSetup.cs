using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Block C — สร้าง 3 crisis DilemmaData assets (เนื้อหา Story Guide §4 — เฟส 4 story content)
    /// รันผ่านเมนู NuclearReMind / Setup Crisis Dilemmas
    ///
    /// ★ วิกฤตทั้ง 3 เป็น "story-driven" แล้ว: StoryBeat (OnStatThreshold) เป็นคนยิงผ่าน
    ///   OnDilemmaTriggerRequested เพื่อให้ InfoCard เด้งก่อนวิกฤต (ความรู้มาก่อนควิซ)
    ///   → WirePool จึง "ไม่ใส่" วิกฤตเหล่านี้เข้า DilemmaManager.dilemmaPool (กันยิงซ้ำสองทาง)
    ///   triggerCondition บน asset ถูกเคลียร์ให้ว่าง (กัน double-fire ถ้าเผลอเข้า pool) — ตัวจริงอยู่ที่ StoryBeatSO.triggerParam (StorySetup)
    /// </summary>
    public static class CrisisSetup
    {
        private const string Folder = "Assets/ScriptableObjects/Dilemmas";
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";

        // dilemma ที่ StoryDirector เป็นเจ้าของ (ยิงผ่าน beat) — ห้ามเข้า pool ของ DilemmaManager
        internal static readonly string[] StoryDrivenIds =
        {
            "Crisis_PlasmaInstability",
            "Crisis_MalignantOutbreak",
            "Crisis_FoodShortage",
            "Crisis_DecreeEmergency", // สร้างโดย StorySetup (เฟส 4) — กันหลุดเข้า pool ตอนรัน CrisisSetup ซ้ำ
            "Crisis_WaterAftermath",  // วิกฤตซ้อน (เฟส 5) — ยิงผ่าน beat OnDeferredCrisis เท่านั้น
            "Crisis_FoodAftermath",
        };

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
            // ★ story-driven: triggerCondition ต้องว่างเสมอ — DilemmaManager.HandleDayEnded ยิงทุก dilemma
            //   ใน pool ที่ condition ตรงตอนสิ้นวัน ถ้าเผลอใส่วิกฤตนี้เข้า pool ทั้งที่ยังมี condition
            //   = ยิงซ้ำกับ StoryBeat (double-fire) · ตัวจริงอยู่ที่ StoryBeatSO.triggerParam
            //   def.trigger เก็บไว้อ้างอิง guide ในโค้ดเท่านั้น ไม่เขียนลง live field
            asset.triggerCondition = "";

            asset.choiceA_FoodChange   = def.aFood;
            asset.choiceA_EnergyChange = def.aEnergy;
            asset.choiceA_WaterChange  = def.aWater;
            asset.choiceA_IronChange   = def.aIron;
            asset.choiceA_HopeChange   = def.aHope;
            asset.choiceA_AethonRelationChange = def.aAethon;
            asset.choiceA_KeranRelationChange  = def.aKeran;
            asset.choiceA_ForceReactorIdleDays = def.aIdle;
            asset.choiceA_Deaths               = def.aDeaths;

            asset.choiceB_FoodChange   = def.bFood;
            asset.choiceB_EnergyChange = def.bEnergy;
            asset.choiceB_WaterChange  = def.bWater;
            asset.choiceB_IronChange   = def.bIron;
            asset.choiceB_HopeChange   = def.bHope;
            asset.choiceB_AethonRelationChange = def.bAethon;
            asset.choiceB_KeranRelationChange  = def.bKeran;
            asset.choiceB_ForceReactorIdleDays = def.bIdle;
            asset.choiceB_Deaths               = def.bDeaths;

            asset.choiceC_FoodChange   = def.cFood;
            asset.choiceC_EnergyChange = def.cEnergy;
            asset.choiceC_WaterChange  = def.cWater;
            asset.choiceC_IronChange   = def.cIron;
            asset.choiceC_HopeChange   = def.cHope;
            asset.choiceC_AethonRelationChange = def.cAethon;
            asset.choiceC_KeranRelationChange  = def.cKeran;
            asset.choiceC_ForceReactorIdleDays = def.cIdle;
            asset.choiceC_Deaths               = def.cDeaths;

            // บทหลังเลือก (Story Guide afterTextTH) — StoryDirector โชว์เป็นการ์ดบทสรุปก่อนควิซ
            asset.choiceA_AfterText = def.aAfter;
            asset.choiceB_AfterText = def.bAfter;
            asset.choiceC_AfterText = def.cAfter;

            // วิกฤตซ้อน (deferredCrisis §4) — StoryDirector ยิง beat OnDeferredCrisis หลังหน่วง 2 วัน
            asset.choiceA_DeferredCrisis = def.aDeferred;
            asset.choiceB_DeferredCrisis = def.bDeferred;
            asset.choiceC_DeferredCrisis = def.cDeferred;

            EditorUtility.SetDirty(asset);
        }

        // assign DilemmaData ใน folder เข้า DilemmaManager.dilemmaPool — ยกเว้น story-driven
        // (วิกฤตเนื้อเรื่องยิงผ่าน StoryBeat → OnDilemmaTriggerRequested เท่านั้น กันเด้งซ้ำสองทาง)
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
                if (d != null && System.Array.IndexOf(StoryDrivenIds, d.dilemmaId) < 0)
                    pool.Add(d);
            }

            mgr.dilemmaPool = pool.ToArray();
            EditorUtility.SetDirty(mgr);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[CrisisSetup] wire dilemmaPool = {pool.Count} dilemmas");
        }

        // เนื้อหา 3 วิกฤต A/B/C — ข้อความไทย verbatim จาก Story Guide §4 (ห้ามแปล/แต่งใหม่)
        // ควิซ: Plasma→Q2,Q3 · Outbreak→Q4,Q5 (linkedQuizIds) · Food→รายทางเลือก A=Q6/B=Q7/C=ไม่มี (QuizSetup)
        // effect ที่ guide ระบุแต่เกมไม่มีระบบรองรับ (workersReassigned/radSickRisk/yieldPct/
        // spoilRate/quarantineDays/deferredCrisis) — ข้ามไว้ก่อน · deferredCrisis น้ำ/อาหาร = เฟส 5
        private static CrisisDef[] BuildDefs() => new[]
        {
            // ── วิกฤต 1: เสถียรภาพพลาสมา (~Day 17 — beat crisis_plasma_stability) ──
            new CrisisDef
            {
                id = "Crisis_PlasmaInstability",
                trigger = "heat_above_80|q_above_0.3", // เอกสาร — ตัวจริงอยู่ที่ StoryBeat.triggerParam
                scenario =
@"⚠️ วิกฤต: เสถียรภาพพลาสมา

สนามแม่เหล็กเริ่มเอาไม่อยู่
พลาสมาในเตาร้อนหลายล้านองศา ถูกกักด้วยสนามแม่เหล็ก
สนามเริ่มไม่นิ่ง ถ้าพลาสมาหลุดชนผนัง เตาจะหลอมละลาย",
                aText = "A · เร่งสนามแม่เหล็กเติมกำลัง (พลังงาน −300)",
                aEnergy = -300, aAethon = 1,
                aAfter =
@"[ระบบ] สนามแม่เหล็กเสถียร · HEAT กลับสู่ระดับปลอดภัย
[ระบบ] พลังงานสำรองหมด · ไฟทั้งเมืองดับชั่วคราว
Kova: รอดแล้ว แต่คืนนี้มืดทั้งเมือง หวังว่าคุ้มนะ
▸ ความคิด: แลกไฟทั้งเมืองกับเตาหนึ่งคืน... คุ้มไหม",
                bText = "B · ซ่อมขดลวดด้วยมือ (แร่เหล็ก −150 · เสี่ยงคนป่วยรังสี)",
                bIron = -150, bHope = -1, bAethon = -1,
                bAfter =
@"[ระบบ] ขดลวดซ่อมเสร็จใน 2 วัน · เตากลับมาเสถียร
[ระบบ] วิศวกร 2 คนได้รับรังสีเกินขนาด
Kova: ซ่อมได้ แต่คนของเราไม่ใช่อะไหล่
▸ ความคิด: สองคน... ที่ผมส่งลงไปเอง",
                cText = "C · ฉีดสารหล่อเย็นฉุกเฉิน (น้ำ −200)",
                cWater = -200, cDeferred = "water", // guide: deferredCrisis "water" — วิกฤตน้ำตามมาอีก 2 วัน
                cAfter =
@"[ระบบ] HEAT ลดฮวบทันที · เตาปลอดภัยชั่วคราว
[ระบบ] คลังน้ำลดลง 50%
▸ ความคิด: น้ำหายไปครึ่งคลัง... เดี๋ยวได้เจอปัญหาใหม่แน่",
            },

            // ── วิกฤต 2: โรคจากรังสี (~Day 20 — beat crisis_radiation_disease) ──
            new CrisisDef
            {
                id = "Crisis_MalignantOutbreak",
                trigger = "day_reached_20", // เอกสารเฉยๆ (triggerCondition ถูกเคลียร์) — beat crisis_radiation_disease ยิงด้วย exposure_above_60 (RadiationManager)
                scenario =
@"⚠️ วิกฤต: โรคจากรังสี

คนงานล้มป่วยพร้อมกัน
คนงานที่ขุดแร่ 15 คนเกิดเนื้อร้าย เนื้อเยื่อโตผิดปกติ
ฟาร์มกับโรงน้ำชะงักเพราะคนล้ม",
                aText = "A · สแกนคัดกรอง PET / SPECT (พลังงาน −200)",
                aEnergy = -200, aKeran = 1,
                aAfter =
@"[ระบบ] สแกนพบตำแหน่งเนื้อร้าย · รักษาเฉพาะจุด 10 คนหาย
Mira: เห็นก่อนถึงรักษาถูกจุด แต่อีกห้าคนหนักเกินไปแล้ว",
                bText = "B · บำบัดด้วยสารเภสัชรังสี (แร่ −200 · เตา Idle 1 วัน)",
                bIron = -200, bKeran = 2, bIdle = 1,
                bAfter =
@"[ระบบ] เตาเปลี่ยนโหมดผลิตไอโซโทปการแพทย์ 1 วัน
[ระบบ] คนงานทั้ง 15 คนฟื้น กลับมาทำงาน
Mira: รังสีที่คนกลัวกันนี่ วันนี้มันช่วยชีวิตคนสิบห้าคน",
                cText = "C · กักตัว รอให้หายเอง (เสี่ยงเสียชีวิต)",
                cHope = -3, cKeran = -2, cDeaths = 3, // Hope −5/คนตาย หักเพิ่มโดย PopulationManager
                cDeferred = "food", // guide: deferredCrisis "food" — แรงงานฟาร์มขาด วิกฤตอาหารตามมา
                cAfter =
@"[ระบบ] ไม่มีการรักษา · 4 วันผ่านไป เสียชีวิต 3 คน
[ระบบ] แรงงานฟาร์มขาด · อาหารเริ่มหมด (วิกฤตซ้อน)
▸ ความคิด: ผมเลือกไม่รักษาพวกเขา...",
            },

            // ── วิกฤต 3: วิกฤตอาหาร (~Day 24 — beat crisis_food_spoilage) ──
            new CrisisDef
            {
                id = "Crisis_FoodShortage",
                trigger = "food_above_500", // เอกสาร — guide: foodStored>500||noAgriDome
                scenario =
@"⚠️ วิกฤต: วิกฤตอาหาร

เสบียงเน่าเพราะรังสี
คลังอาหารเน่าเร็วกว่าปกติสามเท่า เพราะรังสีปนเปื้อน
ถ้าคนอดตาย หอคอยก็ไม่มีความหมาย",
                aText = "A · เพาะเมล็ดกลายพันธุ์ (แร่เหล็ก −250)",
                aIron = -250, aAethon = 1,
                aAfter =
@"[ระบบ] ปลดล็อกแปลงพืชสายพันธุ์ทนรังสี · ผลผลิต +100%
▸ ความคิด: พืชโตในดินที่เป็นพิษได้... ใครจะเชื่อ",
                bText = "B · ฉายรังสีถนอมด้วยโคบอลต์-60 (พลังงาน −300)",
                bEnergy = -300, bKeran = 1,
                bAfter =
@"[ระบบ] เสบียงผ่านห้องฉายรังสีแกมมา · หยุดเน่าทันที
Kova: ฉายรังสีอาหาร ไม่ได้แปลว่าอาหารมีรังสีนะ คนละเรื่อง",
                cText = "C · ลดปันส่วนอาหาร (Hope ดิ่ง · เสี่ยงจลาจล)",
                cHope = -3,
                cAfter =
@"[ระบบ] ทุกคนได้อาหารครึ่งเดียว · แรงงานอ่อนแรง งานช้าลง 50%
▸ ความคิด: พวกเขาหิว... แต่เราไม่มีทางเลือกอื่นแล้วเหรอ",
            },
        };

        private struct CrisisDef
        {
            public string id, trigger, scenario, aText, bText, cText, aAfter, bAfter, cAfter;
            public string aDeferred, bDeferred, cDeferred;
            public float aFood, aEnergy, aWater, aIron, aHope; public int aAethon, aKeran, aIdle, aDeaths;
            public float bFood, bEnergy, bWater, bIron, bHope; public int bAethon, bKeran, bIdle, bDeaths;
            public float cFood, cEnergy, cWater, cIron, cHope; public int cAethon, cKeran, cIdle, cDeaths;
        }
    }
}
