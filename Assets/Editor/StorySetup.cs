using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// เฟส 4 — สร้างเนื้อหาเนื้อเรื่องทั้งหมดจาก Story Guide §4 (ข้อความไทย verbatim ห้ามแปล)
    /// รันผ่านเมนู NuclearReMind / Setup Story Content (หลัง Setup Story UI — ต้องมี StoryDirector ในซีน)
    ///
    /// สิ่งที่สร้าง:
    ///   1. RecordCardSO 4 ใบ (บันทึก Dr. Elara Vane #01/#02/#03/#สุดท้าย)
    ///   2. InfoCardSO 6 ใบ (ความรู้ก่อนควิซ/วิกฤต — Energy/Reactor/Medical/Food/Fusion/Ethics)
    ///   3. MemorialSO + BuildingData "อนุสรณ์" + PrePlacedBuilding ในฐาน (คลิกเปิดแผงรายชื่อ)
    ///   4. Crisis_DecreeEmergency + วิกฤตซ้อน Water/FoodAftermath (DilemmaData ใหม่ · ไม่เข้า dilemmaPool)
    ///   5. StoryBeatSO 14 beat เรียงตามไทม์ไลน์ → wire เข้า StoryDirector.beats
    ///
    /// การแมปที่ต่างจาก guide (จดไว้ใน noteTH ของ beat ด้วย):
    ///   - วิกฤต 3 ใบ: เงื่อนไข/วันอ่านจาก CrisisSchedule (runtime) — GDD §10 + Victory Loop §14 (Day 17/20/24)
    ///   - crisis_radiation_disease: trigger "ZoneA_workers>threshold" → exposure สะสม (RadiationManager) + เพดาน Day 20
    ///   - decree_emergency: "coolingWorkerShortage" = จบวันระหว่างพายุที่ HEAT ≥ 70 (นิยามใน StoryDirector)
    ///     · effects coolingWorkers +1 ของ guide map เป็นแต้มหล่อเย็น +6/+12 (สเกลเดียวกับ decree ปุ่ม ①②)
    ///   - deferredCrisis water/food: guide ระบุแค่คีย์ — เนื้อหาวิกฤตซ้อนแต่งเพิ่มตามโทน guide
    ///   - tutorial_day1: บทสนทนา/เควสต์แรก/ปฏิกิริยาโรงไฟฟ้าแรก = beat (toast) · tutorialSteps ยังอยู่ที่ TutorialManager popup เดิม
    /// </summary>
    public static class StorySetup
    {
        private const string StoryFolder    = "Assets/ScriptableObjects/Story";
        private const string DilemmaFolder  = "Assets/ScriptableObjects/Dilemmas";
        private const string QuizFolder     = "Assets/ScriptableObjects/Quizzes";
        private const string BuildingFolder = "Assets/ScriptableObjects/Buildings";
        private const string ScenePath      = "Assets/Scenes/Gamescene.unity";

        [MenuItem("NuclearReMind/Setup Story Content")]
        public static void SetupAll()
        {
            System.IO.Directory.CreateDirectory(StoryFolder);

            var records = CreateRecords();
            var infos = CreateInfoCards();
            var memorial = CreateMemorialAssets(out var memorialBuilding);
            var decreeCrisis = CreateDecreeCrisis();
            CreateAftermathCrises(out var waterAftermath, out var foodAftermath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var beats = CreateBeats(records, infos, decreeCrisis, waterAftermath, foodAftermath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            WireScene(beats, memorial, memorialBuilding);
            AssetDatabase.SaveAssets();

            Debug.Log($"[StorySetup] สร้างเนื้อหาเนื้อเรื่องครบ: {records.Count} records · {infos.Count} info cards · "
                      + $"{beats.Length} beats · อนุสรณ์ + ประกาศฉุกเฉิน — กด Save Scene (Ctrl+S)");
        }

        // ─────────────────────────────────────────────
        //  1. RecordCards — บันทึกกู้คืนของ Dr. Elara Vane (§4 verbatim)
        // ─────────────────────────────────────────────
        private static Dictionary<string, RecordCardSO> CreateRecords()
        {
            var records = new Dictionary<string, RecordCardSO>();

            records["elara_01"] = Record("elara_01",
                author: "ระบบกู้คืนข้อมูลจากเครือข่ายเก่า · ผู้บันทึก: Dr. Elara Vane",
                body:
@"ถ้ามีใครได้อ่านข้อความนี้ แปลว่าห้องวิจัยยังไม่พังหมด
เชื้อเพลิงตัวแรกอยู่ในที่ที่นายคาดไม่ถึง — มันอยู่ในน้ำ
ยังมีบันทึกอีกหลายส่วนที่กู้ไม่สำเร็จ ระบบจะถอดรหัสต่อเมื่อเมืองเดินหน้า",
                archiveTitle: "บันทึก #01 — เชื้อเพลิงในน้ำ");

            records["elara_02"] = Record("elara_02",
                author: "ระบบถอดรหัสข้อมูลเพิ่มเติมสำเร็จ · ผู้บันทึก: Dr. Elara Vane",
                body:
@"เชื้อเพลิงตัวแรกอยู่ในน้ำมาตลอด เราแค่ไม่เคยมองมัน
ดิวเทอเรียมมีอยู่ในน้ำทุกหยด รอแค่เครื่องแยก",
                archiveTitle: "บันทึก #02 — ดิวเทอเรียมในน้ำ");

            records["elara_03"] = Record("elara_03",
                author: "ระบบถอดรหัสข้อมูลเพิ่มเติมสำเร็จ · ผู้บันทึก: Dr. Elara Vane",
                body:
@"ความร้อนในเตาไม่ใช่ศัตรู มันบอกว่าเราใกล้ความจริง
แต่ถ้าคุมมันไม่อยู่... นายจะเข้าใจว่าทำไมทีมเราถึงไม่เหลือใคร",
                archiveTitle: "บันทึก #03 — ความร้อนไม่ใช่ศัตรู");

            records["elara_final"] = Record("elara_final",
                author: "ถอดรหัสไฟล์รายงานฉบับเต็มสำเร็จ · ผู้บันทึก: Dr. Elara Vane",
                body:
@"ก่อนหอคอยระเบิด — เราตรวจพบบางอย่าง คลื่นรังสีที่จะตามมาหลังการระเบิด
เราเขียนมันลงในรายงาน รายงานที่ควรจะถึงมือทุกคน
ถ้ามันกำลังจะมา เตาต้องติดเต็มร้อยก่อนมันจะถึง นั่นคือทางเดียว",
                archiveTitle: "บันทึก #สุดท้าย — รายงานเตือนพายุ");

            return records;
        }

        private static RecordCardSO Record(string id, string author, string body, string archiveTitle)
        {
            var r = CreateOrLoad<RecordCardSO>($"{StoryFolder}/Record_{id}.asset");
            r.recordId = id;
            r.authorLabel = author;
            r.bodyTH = body;
            r.buttonLabel = "รับทราบ · เก็บเข้าแผง Records";
            r.archiveTitle = archiveTitle;
            EditorUtility.SetDirty(r);
            return r;
        }

        // ─────────────────────────────────────────────
        //  2. InfoCards — ความรู้ก่อนควิซ/วิกฤต (§4 verbatim)
        // ─────────────────────────────────────────────
        private static Dictionary<string, InfoCardSO> CreateInfoCards()
        {
            var infos = new Dictionary<string, InfoCardSO>();

            infos["deuterium"] = Info("deuterium", "เชื้อเพลิงที่ซ่อนอยู่ในน้ำ", CardCategory.Energy,
@"น้ำทะเลทั่วไปมีไอโซโทปของไฮโดรเจนชนิดหนึ่งชื่อ ดิวเทอเรียม (²H) ปนอยู่แล้ว
เราไม่ต้องผลิตใหม่ แค่แยกมันออกจากน้ำ ก็ได้เชื้อเพลิงป้อนเตาฟิวชันได้เลย
→ กำลังสกัดดิวเทอเรียมจากคลังน้ำของเมือง",
                button: "เข้าใจแล้ว");

            infos["plasma"] = Info("plasma", "พลาสมาถูกขังด้วยอะไร", CardCategory.Reactor,
@"ในเตาฟิวชัน เชื้อเพลิงร้อนเป็นพลาสมาหลายล้านองศา ร้อนเกินกว่าผนังใดจะทนได้
เราจึงใช้สนามแม่เหล็ก ขังพลาสมาไว้กลางเตา ไม่ให้แตะผนัง
ถ้าสนามอ่อนลง พลาสมาจะหลุดชนผนัง แล้วเตาจะหลอมละลาย",
                button: "รับทราบ");

            infos["medicine"] = Info("medicine", "รังสีกับร่างกายคน", CardCategory.Medical,
@"รังสีปริมาณสูงทำลายเซลล์ในร่างกาย ทำให้เกิดเนื้อร้ายได้
การรักษาสมัยใหม่ใช้เวชศาสตร์นิวเคลียร์ — ฉีดสารเภสัชรังสีเข้าไป ""ถ่ายภาพ"" หาตำแหน่งก่อน
แล้วค่อยส่งรังสีไป ""ทำลาย"" เฉพาะจุด แม่นยำ ไม่กระทบเนื้อดี
หลักสำคัญ: ALARA — ให้คนรับรังสีน้อยที่สุดเท่าที่ทำได้ และปกป้องกลุ่มเปราะบาง (ผู้ป่วย/เด็ก) ก่อน",
                button: "รับทราบ");

            infos["food"] = Info("food", "รังสีช่วยเรื่องอาหารได้ 2 ทาง", CardCategory.Food,
@"ทางที่ 1 — ถนอมอาหาร: ฉายรังสีแกมมา (เช่น จากโคบอลต์-60) ทะลุผ่านอาหาร ฆ่าจุลินทรีย์และเชื้อรา
อาหารเก็บได้นานขึ้น โดยอาหารไม่กลายเป็นสารรังสี (อาหารฉายรังสี ≠ อาหารมีรังสี)
ทางที่ 2 — ปรับปรุงพันธุ์: ฉายรังสีกระตุ้นให้พืชกลายพันธุ์ นักวิจัยคัดเฉพาะสายพันธุ์ที่ทนทาน/ผลผลิตสูงไว้ใช้ถาวร (เช่น ข้าว กข6 ของไทย)",
                button: "รับทราบ");

            infos["fusion"] = Info("fusion", "จุดที่เรากำลังจะไปให้ถึง: ฟิวชัน", CardCategory.Fusion,
@"สิ่งที่เตากำลังทำคือฟิวชันนิวเคลียร์ — การหลอมรวมนิวเคลียสเบา (ไฮโดรเจน + ไฮโดรเจน)
ให้เป็นธาตุที่หนักกว่า แล้วปลดปล่อยพลังงานมหาศาล
ตรงข้ามกับฟิชชัน ที่เป็นการแตกนิวเคลียสหนัก (แบบโรงไฟฟ้านิวเคลียร์เก่า)
ฟิวชันสะอาดกว่าเพราะเชื้อเพลิงมาจากน้ำ ไม่ปล่อย CO₂ ถ้าเสียสมดุลเตาจะดับเอง (ไม่ระเบิด) และไม่มีกากรังสีอายุยืนแบบฟิชชัน",
                button: "ดันเตาต่อ");

            infos["alara"] = Info("alara", "ทบทวน: ALARA กับการเกณฑ์แรงงาน", CardCategory.Ethics,
@"อย่าลืมหลัก ALARA — คนที่ไวต่อรังสีเป็นพิเศษคือกลุ่มเปราะบาง (ผู้ป่วย/เด็ก)
การบังคับให้พวกเขาเข้าเขตเสี่ยงรังสี ขัดหลัก ALARA โดยตรง
การตัดสินใจนี้เป็นของคุณ เกมไม่ตัดสินถูก-ผิดแทน แต่ผลของมันคือ Hope ของเมือง",
                button: "ตัดสินใจ");

            return infos;
        }

        private static InfoCardSO Info(string id, string title, CardCategory category, string body, string button)
        {
            var info = CreateOrLoad<InfoCardSO>($"{StoryFolder}/Info_{id}.asset");
            info.title = title;
            info.category = category;
            info.bodyTH = body;
            info.buttonLabel = button;
            EditorUtility.SetDirty(info);
            return info;
        }

        // ─────────────────────────────────────────────
        //  3. อนุสรณ์ (§4 MEMORIAL) — SO + BuildingData + pre-placed ในฐาน
        // ─────────────────────────────────────────────
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
            memorial.innerVoiceOnFirstOpen = "คนพวกนี้เคยอยู่ที่นี่... ก่อนผมมาถึง";
            EditorUtility.SetDirty(memorial);

            // BuildingData "อนุสรณ์" — ราคา 0 (PrePlacedBuilding หักค่าสร้างตามปกติ) ไม่เข้า hotbar
            building = CreateOrLoad<BuildingData>($"{BuildingFolder}/Memorial.asset");
            building.buildingName = "อนุสรณ์";
            building.description = "อนุสรณ์ทีมสร้างหอคอยทั้ง 6 คน — คลิกที่ตัวอาคารเพื่อเปิดแผงรายชื่อ";
            building.nuclearKnowledge = "Veltara Core Project คือทีมที่จุดเตาฟิวชันครั้งแรกเมื่อปี 2157 " +
                                        "ความผิดพลาดของพวกเขาไม่ใช่วิทยาศาสตร์ แต่คือการเดินเครื่องเกินขีดที่ระบบหล่อเย็นรับไหว";
            building.size = new Vector2Int(2, 2);
            building.buildingType = BuildingType.Memorial;
            building.ironCost = 0;
            building.energyCost = 0;
            building.workerRequired = 0;
            if (building.sprite == null)
                building.sprite = PlaceholderSpriteGenerator.EnsureBuildingSprite("Memorial");
            EditorUtility.SetDirty(building);

            return memorial;
        }

        // ─────────────────────────────────────────────
        //  4. ประกาศฉุกเฉิน (§4 decree_emergency) — DilemmaData ใหม่ · ไม่เข้า pool
        //     (CrisisSetup.StoryDrivenIds กันไว้แล้ว) · B/C → Q10 · A ไม่มีควิซ
        //     effects coolingWorkers +1 → แต้มหล่อเย็น +6/+12 ผ่าน DecreeManager (สเกลเดียวกับปุ่ม ①②)
        //     effect hopePerDay/patientDeathRisk เต็มระบบแล้วผ่าน choiceB_Effects → CrisisEffectManager (§4)
        // ─────────────────────────────────────────────
        private static DilemmaData CreateDecreeCrisis()
        {
            var d = CreateOrLoad<DilemmaData>($"{DilemmaFolder}/Crisis_DecreeEmergency.asset");
            d.dilemmaId = "Crisis_DecreeEmergency";
            d.triggerCondition = ""; // ยิงผ่าน StoryBeat decree_emergency เท่านั้น
            d.scenarioText =
@"⚠️ ประกาศฉุกเฉิน

แรงงานไม่พอรับมือพายุ
ระบบหล่อเย็นต้องการคนเพิ่มด่วน ไม่งั้นเตาร้อนเกิน
มีทางที่ได้ผล แต่ขัดกับสิ่งที่ควรทำ — จะออกประกาศไหม";

            d.choiceAText = "A · ไม่ออกประกาศ — หาทางอื่น";
            d.choiceA_HopeChange = 0;
            d.choiceA_AfterText =
@"[ระบบ] ไม่เกณฑ์กลุ่มเปราะบาง · เมืองต้องบีบทรัพยากรให้พอ
ชาวเมือง: ขอบคุณที่ไม่ทิ้งพวกเรา แม้ในวันที่ยากที่สุด";

            d.choiceBText = "B · เกณฑ์ผู้ป่วยร่วมงาน (หล่อเย็น +6 · Hope −8)";
            d.choiceB_HopeChange = -8;
            d.choiceB_CoolingWorkers = 6;  // guide: coolingWorkers +1 — สเกลเดียวกับ Decree1_SickLabor
            // guide effects: hopePerDay -3 (ดาบสองคม Hope ดิ่งต่อวัน 4 วัน) + patientDeathRisk 0.2 (ผู้ป่วยเสี่ยงตายในเขตรังสี)
            d.choiceB_Effects = new CrisisChoiceEffects { hopePerDay = -3f, hopePerDayDays = 4, patientDeathRisk = 0.2f };
            d.choiceB_AfterText =
@"[ระบบ] ผู้ป่วยถูกเรียกออกมาทำงานในเขตเสี่ยงรังสี
Mira: เราชนะพายุไปทำไม ถ้าไม่เหลือใครให้ช่วย";

            d.choiceCText = "C · ดึงแรงงานเด็ก (หล่อเย็น +12 · Hope −15)";
            d.choiceC_HopeChange = -15;
            d.choiceC_CoolingWorkers = 12; // guide: coolingWorkers +1 — สเกลเดียวกับ Decree2_ChildLabor
            d.choiceC_AfterText =
@"[ระบบ] เด็กถูกส่งไปทำงานเบาในเขตหล่อเย็น
ชาวเมือง: นี่คือสิ่งที่เราหนีมา ไม่ใช่สิ่งที่เราอยากสร้าง";

            // ★ ดาบสองคม (guide warning): เลือก B/C ซ้ำ ๆ Hope อาจร่วงถึง 0 = แพ้ — ALARA ในรูปการตัดสินใจ
            d.linkedQuizIds = new string[0];
            d.choiceA_QuizIds = new string[0];       // A ไม่ให้ความรู้ใหม่ — ไม่มีควิซ
            d.choiceB_QuizIds = new[] { "Q10" };     // alara_price
            d.choiceC_QuizIds = new[] { "Q10" };

            EditorUtility.SetDirty(d);
            return d;
        }

        // ─────────────────────────────────────────────
        //  4b. วิกฤตซ้อน (เฟส 5 — guide ระบุแค่คีย์ deferredCrisis: water/food · เนื้อหาแต่งเพิ่มตามโทน guide)
        //      2 ทางเลือก (ปุ่ม C ซ่อนเองเมื่อ choiceCText ว่าง) · ไม่มีควิซ (ไม่มีความรู้ใหม่) · ไม่เข้า pool
        // ─────────────────────────────────────────────
        private static void CreateAftermathCrises(out DilemmaData water, out DilemmaData food)
        {
            water = CreateOrLoad<DilemmaData>($"{DilemmaFolder}/Crisis_WaterAftermath.asset");
            water.dilemmaId = "Crisis_WaterAftermath";
            water.triggerCondition = ""; // ยิงผ่าน beat deferred_water_crisis เท่านั้น
            water.scenarioText =
@"⚠️ วิกฤตซ้อน: น้ำไม่พอ

คลังน้ำที่หายไปครึ่งหนึ่งจากการฉีดสารหล่อเย็นเริ่มส่งผล
ประชาชนต่อแถวรับปันส่วนน้ำ โรงอาหารต้องหยุดครึ่งวัน
ต้องหาน้ำกลับมาก่อนที่คนจะล้ม";
            water.choiceAText = "A · เร่งกำลังโรงผลิตน้ำ (พลังงาน −150)";
            water.choiceA_EnergyChange = -150;
            water.choiceA_AfterText =
@"[ระบบ] โรงน้ำเดินเครื่องเต็มกำลัง · คลังน้ำเริ่มฟื้น
Kova: ไฟที่เหลือน้อยลงอีก แต่คนต้องมีน้ำก่อน";
            water.choiceBText = "B · ปันส่วนน้ำอย่างเข้มงวด (Hope −5)";
            water.choiceB_HopeChange = -5;
            water.choiceB_AfterText =
@"[ระบบ] จำกัดน้ำคนละครึ่งส่วน · เมืองเงียบลง
▸ ความคิด: เข้าแถวรับน้ำ... ภาพที่ผมไม่อยากเห็นอีกแล้ว";
            water.choiceCText = "";
            water.linkedQuizIds = new string[0];
            EditorUtility.SetDirty(water);

            food = CreateOrLoad<DilemmaData>($"{DilemmaFolder}/Crisis_FoodAftermath.asset");
            food.dilemmaId = "Crisis_FoodAftermath";
            food.triggerCondition = "";
            food.scenarioText =
@"⚠️ วิกฤตซ้อน: แรงงานฟาร์มขาด

คนที่เสียไปคือแรงงานฟาร์ม
ผลผลิตอาหารตกต่อเนื่อง คลังเสบียงลดลงทุกวัน
ต้องอุดช่องว่างก่อนฤดูพายุจะมาถึง";
            food.choiceAText = "A · ดึงคนจากงานก่อสร้างไปฟาร์ม (พลังงาน −100)";
            food.choiceA_EnergyChange = -100;
            food.choiceA_AfterText =
@"[ระบบ] ฟาร์มกลับมาเดินเต็มกำลัง · งานก่อสร้างช้าลง
Kova: คนเท่าเดิม งานเท่าเดิม ต้องมีอะไรช้าลงสักอย่าง";
            food.choiceBText = "B · ลดปันส่วนอาหารชั่วคราว (Hope −5)";
            food.choiceB_HopeChange = -5;
            food.choiceB_AfterText =
@"[ระบบ] ทุกคนได้อาหารน้อยลงจนกว่าฟาร์มจะฟื้น
▸ ความคิด: ผมสัญญากับพวกเขาไว้ว่ามันจะดีขึ้น...";
            food.choiceCText = "";
            food.linkedQuizIds = new string[0];
            EditorUtility.SetDirty(food);
        }

        // ─────────────────────────────────────────────
        //  5. StoryBeats — เรียงตามไทม์ไลน์ §4
        // ─────────────────────────────────────────────
        private static StoryBeatSO[] CreateBeats(
            Dictionary<string, RecordCardSO> records,
            Dictionary<string, InfoCardSO> infos,
            DilemmaData decreeCrisis,
            DilemmaData waterAftermath,
            DilemmaData foodAftermath)
        {
            var plasmaCrisis  = LoadAsset<DilemmaData>($"{DilemmaFolder}/Crisis_PlasmaInstability.asset");
            var outbreakCrisis = LoadAsset<DilemmaData>($"{DilemmaFolder}/Crisis_MalignantOutbreak.asset");
            var foodCrisis    = LoadAsset<DilemmaData>($"{DilemmaFolder}/Crisis_FoodShortage.asset");
            var q1 = LoadAsset<QuizQuestionSO>($"{QuizFolder}/Q1.asset");
            var q8 = LoadAsset<QuizQuestionSO>($"{QuizFolder}/Q8.asset");
            var q9 = LoadAsset<QuizQuestionSO>($"{QuizFolder}/Q9.asset");

            var beats = new List<StoryBeatSO>();

            // ── PHASE 1 · tutorial_day1 (§4) — จุดเดียวที่ไกด์ หลังจากนี้ปล่อยผู้เล่นเรียนรู้เอง ──
            beats.Add(Beat("tutorial_day1", StoryTriggerType.OnDay, "1", b =>
            {
                b.npcLinePre = "Kova: วิศวกรใหม่สินะ ที่นี่เหลือแค่นี้แหละ เริ่มจากไฟ น้ำ อาหาร";
                b.logLines = new[]
                {
                    "[เควสต์] ทำให้เมืองมีไฟ — ไม่มีพลังงาน สร้างหรืออัปเกรดเครื่องกำเนิดไฟฟ้า",
                };
                b.innerVoiceAfter = "ที่นี่มืดสนิท... เริ่มจากไฟก่อน";
                b.noteTH = "guide firstQuest แสดงเป็น log (ไม่มีระบบเควสต์แยก) · tutorialSteps 3 ข้อ " +
                           "อยู่ที่ป๊อปอัป TutorialManager เดิม (guide: ใช้ระบบ tutorial ที่มีอยู่)";
            }));

            // ── tutorial_first_power (§4 onFirstPowerPlantBuilt) — ปฏิกิริยาโรงไฟฟ้าหลังแรก ──
            beats.Add(Beat("tutorial_first_power", StoryTriggerType.OnBuildingBuilt, "โรงไฟฟ้า", b =>
            {
                b.logLines = new[]
                {
                    "[ระบบ] เครื่องกำเนิดไฟฟ้าเริ่มทำงาน · พลังงาน +60/วัน",
                    "Kova: ดี ที่เหลือคิดเองเป็นแล้ว",
                };
                b.innerVoiceAfter = "เมืองนี้ยังไม่ตายซะทีเดียว";
                b.noteTH = "logLines โชว์รวดเดียว (logLinesDaily=false) · triggerParam = buildingName ไทยของ PowerPlant.asset";
            }));

            // ── PHASE 2 · recover_record_01 — สร้างห้องวิจัย → เริ่มกู้คืนข้อมูล ──
            beats.Add(Beat("recover_record_01", StoryTriggerType.OnBuildingBuilt, "ห้องปฏิบัติการ", b =>
            {
                b.record = records["elara_01"];
                b.innerVoiceAfter = "Elara... ชื่อนี้อยู่บนอนุสรณ์ในฐาน";
                b.noteTH = "การ์ดเด้งกลางจอเอง ไม่ต้องเข้าห้อง · เก็บเข้าแผง Records ให้ย้อนอ่าน · " +
                           "triggerParam = buildingName ไทยเป๊ะของห้องปฏิบัติการ (guide: ResearchLab)";
            }));

            // ── PHASE 2→3 · deuterium_ignition — ★ ความรู้มาก่อนควิซ (InfoCard → Record → Q1) ──
            beats.Add(Beat("deuterium_ignition", StoryTriggerType.OnDeuteriumExtracted, "", b =>
            {
                b.npcLinePre = "Kova: เตานี่ไม่เหมือนโรงไฟบ้าๆ นะ จ้อง CORE% กับ HEAT ให้ดี";
                b.infoCard = infos["deuterium"];
                b.record = records["elara_02"];
                b.quiz = NonNull(q1);
                b.innerVoiceAfter = "อยู่ในน้ำมาตลอด... ก็อย่างที่รายงานเขียนไว้";
                b.noteTH = "ลำดับเล่นจริง record → info (ตาม pseudocode §3 ของ guide) — ทั้งคู่มาก่อนควิซตามกฎเหล็ก";
            }));

            // ── recover_record_03 — เตาเริ่มเดินเครื่อง ──
            beats.Add(Beat("recover_record_03", StoryTriggerType.OnReactorStart, "", b =>
            {
                b.record = records["elara_03"];
            }));

            // ── PHASE 3 · วิกฤต 3 ใบ (InfoCard → Crisis → Outcome → Quiz ผ่าน DilemmaManager) ──
            // ★ เงื่อนไข/วันทั้งหมดมาจาก CrisisSchedule (runtime) — GDD §10 + Victory Loop §14 · EditMode test คุมวันเด้ง
            beats.Add(Beat("crisis_plasma_stability", StoryTriggerType.OnStatThreshold, CrisisSchedule.PlasmaTrigger, b =>
            {
                b.infoCard = infos["plasma"];
                b.crisis = plasmaCrisis;
                b.noteTH = "Day 17 (§14) · §10 ระบุ \"HEAT>80 หรือ Q>0.3 (จบเฟส 1)\" — จบเฟส 1 = CORE% 50 (§8) " +
                           "จึงใช้ core_above_50 ไม่ใช่ q_above_0.3 (Q=CORE%/100 → 0.3 = CORE% 30 = ค่าเริ่มต้นตอนปลดล็อก Day 11 " +
                           "เงื่อนไขเดิมทำให้วิกฤตเด้ง Day 11) · ควิซ Q2,Q3 ผูกที่ crisis.linkedQuizIds";
            }));

            beats.Add(Beat("crisis_radiation_disease", StoryTriggerType.OnStatThreshold, CrisisSchedule.OutbreakTrigger, b =>
            {
                b.infoCard = infos["medicine"];
                b.crisis = outbreakCrisis;
                b.noteTH = "Day 20 (§14) · §10: ส่งคนขุดโซนเสี่ยงมากเกินไป — จำลองด้วย RadiationManager exposure สะสม " +
                           "(ลดด้วย Shelter/Medic ตาม ALARA) · เพดานวัน day_reached_20 (เดิม 23 ไม่ตรง §14) · ควิซ Q4,Q5";
            }));

            beats.Add(Beat("crisis_food_spoilage", StoryTriggerType.OnStatThreshold, CrisisSchedule.FoodTrigger, b =>
            {
                b.infoCard = infos["food"];
                b.crisis = foodCrisis;
                b.noteTH = "Day 24 (§14) · §10: \"อาหาร>500 หรือไม่มี Agri Dome\" — ยังไม่มี Agri Dome วงเล็บหลังจึงจริงเสมอ " +
                           "และ food_above_500 ใช้ไม่ได้ (เพดานคลังอาหาร = 500 → ชนเพดาน ~Day 12 วิกฤตเด้งก่อนกำหนด 12 วัน) " +
                           "· ควิซรายทางเลือก: A→Q6 (mutation) · B→Q7 (irradiation) · C→ไม่มี";
            }));

            // ── วิกฤตซ้อน (เฟส 5 — deferredCrisis §4): ยิง 2 วันหลังเลือกทาง C ของวิกฤตแม่ ──
            beats.Add(Beat("deferred_water_crisis", StoryTriggerType.OnDeferredCrisis, "water", b =>
            {
                b.crisis = waterAftermath;
                b.noteTH = "ตามหลังพลาสมา C (ฉีดสารหล่อเย็น — น้ำหายครึ่งคลัง) · guide ระบุแค่คีย์ water — เนื้อหาแต่งเพิ่มตามโทน";
            }));

            beats.Add(Beat("deferred_food_crisis", StoryTriggerType.OnDeferredCrisis, "food", b =>
            {
                b.crisis = foodAftermath;
                b.noteTH = "ตามหลังโรครังสี C (กักตัว — แรงงานฟาร์มขาด) · guide ระบุแค่คีย์ food — เนื้อหาแต่งเพิ่มตามโทน";
            }));

            // ── PHASE 4 · foreshadow_storm — ลางพายุ กระจายวันละบรรทัด Day 20→23 ──
            beats.Add(Beat("foreshadow_storm", StoryTriggerType.OnDay, "20", b =>
            {
                b.logLines = new[]
                {
                    "[ระบบ] เซนเซอร์อ่านค่ารังสีพื้นหลังสูงผิดปกติ",
                    "Kova: เครื่องวัดเพี้ยนอีกแล้ว ครั้งที่สามวันนี้ ไม่เคยเพี้ยนโดยไม่มีเหตุ",
                    "▸ ความคิด: ท้องฟ้ากลางคืนสีแปลกๆ... หรือผมคิดไปเอง",
                    "[ระบบ] ตรวจพบความผิดปกติของสภาพอากาศเหนือ Veltara",
                };
                b.logLinesDaily = true; // §4 sequence: "เด้งกระจายหลายวัน ไม่รวบ"
                b.noteTH = "พายุไม่เฉลย ค่อยๆ ปูให้ผู้เล่นเอะใจเอง (บรรทัดแรก Day 20 · ที่เหลือวันละบรรทัด)";
            }));

            // ── recover_record_final — เฉลยปมเงียบๆ (~Day 23) ──
            beats.Add(Beat("recover_record_final", StoryTriggerType.OnDay, "23", b =>
            {
                b.record = records["elara_final"];
                b.innerVoiceAfter = "รายงานที่ผมเอาไปส่ง... มันเขียนเรื่องนี้ไว้ตั้งแต่แรก";
                b.noteTH = "ปมเชื่อม: ผู้เล่นที่คลิกอนุสรณ์ (เห็นชื่อ Elara) + กู้บันทึกครบ จะเข้าใจเองว่า " +
                           "รายงานที่ Auren ส่ง = รายงานเตือนพายุนี้ เกมไม่ตอกย้ำ";
            }));

            // ── storm_first_light — พายุมาถึง Day 25 (InfoCard ฟิวชัน → Q8,Q9) ──
            beats.Add(Beat("storm_first_light", StoryTriggerType.OnStormApproach, "", b =>
            {
                b.npcLinePre = "Kova: มันมาจริงๆ นั่นแหละที่เครื่องฉันพยายามเตือน! เตาต้องติดเต็มร้อย ไม่งั้นละลาย!";
                b.logLines = new[] // crisisAlert §4 verbatim — โชว์รวดเดียว (ไม่ daily)
                {
                    "[ระบบ] พายุรังสีเคลื่อนเข้า Veltara · ท้องฟ้าเปลี่ยนเป็นสีม่วง เซนเซอร์ทั่วเมืองร้องเตือน",
                    "[ระบบ] อุณหภูมิแกนเตาจะเพิ่มต่อเนื่องตราบที่พายุยังอยู่ · ทางเดียวที่จะรอด: ดันเตาให้ถึง 100%",
                };
                b.infoCard = infos["fusion"];
                b.quiz = NonNull(q8, q9);
                b.noteTH = "climax: พายุ +12 heat/วัน Day 25–30 (มีแล้วใน CoreTowerManager) · " +
                           "Q8,Q9 ย้ายมาจาก CoreTowerManager phase-complete เพื่อให้ InfoCard นำก่อน";
            }));

            // ── decree_emergency — การ์ดจริยธรรม (เฟส 5 ปลุกแล้ว) ──
            beats.Add(Beat("decree_emergency", StoryTriggerType.OnStormActive, "coolingWorkerShortage", b =>
            {
                b.infoCard = infos["alara"];
                b.crisis = decreeCrisis;
                b.noteTH = "coolingWorkerShortage = จบวันระหว่างพายุที่ HEAT ≥ 70 (หล่อเย็นตามพายุ +12/วันไม่ทัน) · " +
                           "ดาบสองคม: B/C ทำ Hope ร่วง อาจถึง 0 = แพ้ — ALARA ในรูปการตัดสินใจ";
            }));

            return beats.ToArray();
        }

        private static StoryBeatSO Beat(string beatId, StoryTriggerType type, string param, System.Action<StoryBeatSO> fill)
        {
            var beat = CreateOrLoad<StoryBeatSO>($"{StoryFolder}/Beat_{beatId}.asset");
            beat.beatId = beatId;
            beat.triggerType = type;
            beat.triggerParam = param;
            // ล้างชิ้นส่วนก่อน fill — รันซ้ำแล้วค่าเก่าที่เลิกใช้ไม่ค้าง
            beat.record = null;
            beat.infoCard = null;
            beat.crisis = null;
            beat.quiz = new QuizQuestionSO[0];
            beat.npcLinePre = "";
            beat.innerVoiceAfter = "";
            beat.logLines = new string[0];
            beat.logLinesDaily = false;
            beat.noteTH = "";
            fill(beat);
            EditorUtility.SetDirty(beat);
            return beat;
        }

        private static QuizQuestionSO[] NonNull(params QuizQuestionSO[] quizzes)
        {
            var list = new List<QuizQuestionSO>();
            foreach (var q in quizzes)
                if (q != null) list.Add(q);
            return list.ToArray();
        }

        // ─────────────────────────────────────────────
        //  6. Wire เข้าซีน — StoryDirector.beats + MemorialPanel + PrePlacedMemorial
        // ─────────────────────────────────────────────
        private static void WireScene(StoryBeatSO[] beats, MemorialSO memorial, BuildingData memorialBuilding)
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var director = Object.FindFirstObjectByType<StoryDirector>();
            if (director == null)
            {
                Debug.LogWarning("[StorySetup] ไม่พบ StoryDirector ในซีน — รัน NuclearReMind/Setup Story UI ก่อน แล้วรันใหม่");
                return;
            }
            director.beats = beats;
            EditorUtility.SetDirty(director);

            var memorialPanel = Object.FindFirstObjectByType<MemorialPanelController>();
            if (memorialPanel != null)
            {
                memorialPanel.memorialData = memorial;
                EditorUtility.SetDirty(memorialPanel);
            }
            else
            {
                Debug.LogWarning("[StorySetup] ไม่พบ MemorialPanelController — รัน Setup Story UI ก่อน (แผงอนุสรณ์จะไม่มีข้อมูล)");
            }

            // ตึกอนุสรณ์ pre-placed ข้างขวา CORE TOWER (ทาวเวอร์ 3×3 กลางกริด — เว้น 1 ช่อง)
            var grid = Object.FindFirstObjectByType<GridManager>();
            int columns = grid != null ? grid.columns : 43;
            int rows    = grid != null ? grid.rows    : 28;
            var origin = new Vector2Int(columns / 2 + 4, rows / 2 - 1);

            var go = GameObject.Find("PrePlacedMemorial") ?? new GameObject("PrePlacedMemorial");
            var pre = go.GetComponent<PrePlacedBuilding>() ?? go.AddComponent<PrePlacedBuilding>();
            pre.building = memorialBuilding;
            pre.cell = origin;
            EditorUtility.SetDirty(pre);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[StorySetup] wire StoryDirector.beats = {beats.Length} · อนุสรณ์ที่ ({origin.x},{origin.y})");
        }

        // ─────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────
        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
                Debug.Log($"[StorySetup] สร้าง {path}");
            }
            return asset;
        }

        private static T LoadAsset<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                Debug.LogWarning($"[StorySetup] ไม่พบ {path} — รัน setup ก่อนหน้า (Crisis/Quiz) แล้วรันใหม่");
            return asset;
        }
    }
}
