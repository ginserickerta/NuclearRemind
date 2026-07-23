using System.IO;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: สคริปต์ Editor สร้าง asset โน้ตวิจัยทั้ง 8 ใบ จากข้อความใน docs/NOTES.md แบบคำต่อคำ
    /// Generates the 8 ResearchNoteSO assets (GDD §19 / docs/NOTES.md) into
    /// Assets/Resources/ResearchNotes/ so KnowledgeDB auto-loads them at runtime.
    ///
    /// ★ knowledgeBody / leadHint / completionLine are VERBATIM from docs/NOTES.md — ห้ามแต่งใหม่.
    /// Idempotent: re-running updates existing assets in place (keeps GUIDs / scene refs).
    /// Run: menu NuclearReMind > Setup Research Notes.
    /// </summary>
    public static class ResearchNotesSetup
    {
        private const string Folder = "Assets/Resources/ResearchNotes";

        // เมนูนี้: สร้าง/อัปเดต ResearchNoteSO 8 ใบลง Resources/ResearchNotes (รันซ้ำได้ GUID ไม่เปลี่ยน)
        [MenuItem("NuclearReMind/Setup Research Notes")]
        public static void Apply()
        {
            Directory.CreateDirectory(Folder);

            // 1 · deuterium
            Write("deuterium", n =>
            {
                n.title = "เชื้อเพลิงที่ซ่อนอยู่ในน้ำ";
                n.category = "เชื้อเพลิง";
                n.researcherSlots = 2; n.daysRequired = 2;
                n.costPower = 80; n.costIron = 0; n.costLabMat = 0;
                n.requiredLead = "water_analysis";
                n.prerequisiteNotes = new string[0];
                n.unlocksBuildings = new[] { "deuterium_extractor" };
                n.unlocksCommands = new string[0];
                n.quizIds = new[] { "q_deuterium" };
                n.linkedRecordId = "record_02";
                n.leadHint = "คลังน้ำของเมืองอาจมีมากกว่าที่เราคิด";
                n.knowledgeBody =
"น้ำทั่วไปมีไอโซโทปของไฮโดรเจนชนิดหนึ่งชื่อ ดิวเทอเรียม (²H) ปนอยู่แล้ว\n" +
"ในน้ำทุกหยด มีดิวเทอเรียมประมาณ 1 ใน 6,400 อะตอมของไฮโดรเจน\n\n" +
"เราไม่ต้องผลิตมันขึ้นใหม่ แค่ แยกมันออกจากน้ำ ก็ได้เชื้อเพลิงป้อนเตาฟิวชันได้เลย\n\n" +
"→ ปลดล็อก: Deuterium Extractor (ต้องวางติดโรงน้ำ)";
                n.completionSpeaker = "KOVA";
                n.completionLine = "อยู่ในน้ำที่เราดื่มทุกวันเนี่ยนะ เอาเครื่องมาต่อโรงน้ำเลย";
            });

            // 2 · confinement
            Write("confinement", n =>
            {
                n.title = "ขังพลาสมาด้วยสนามแม่เหล็ก";
                n.category = "เตาปฏิกรณ์";
                n.researcherSlots = 3; n.daysRequired = 2;
                n.costPower = 40; n.costIron = 60; n.costLabMat = 0;
                n.requiredLead = "magnetic_theory";
                n.prerequisiteNotes = new string[0];
                n.unlocksBuildings = new[] { "toroidal_coil", "poloidal_coil" };
                n.unlocksCommands = new[] { "install_toroidal", "install_poloidal" };
                n.quizIds = new[] { "q_plasma", "q_magnetic_pair" };
                n.linkedRecordId = "record_03";
                n.leadHint = "ความร้อนขึ้นเร็วเกินที่หล่อเย็นจะรับไหว";
                n.knowledgeBody =
"เตา CORE TOWER คือโทคาแมก เตาฟิวชันที่ใช้สนามแม่เหล็กขังพลาสมาร้อนล้านองศาไว้กลางเตา ไม่ให้แตะผนังจนหลอมละลาย\n\n" +
"ต้องติด ขดลวด 2 ชนิด เพื่อสร้างสนามนั้น: Toroidal บีบพลาสมาให้เป็นวง · Poloidal กันไม่ให้ชนผนัง\n\n" +
"ขังพลาสมาได้นิ่ง = เตาเดินร้อนขึ้นได้โดยไม่ระเบิด → ดัน CORE% ต่อได้\n\n" +
"→ ปลดล็อก: Toroidal Coil · Poloidal Coil";
                n.completionSpeaker = "KOVA";
                n.completionLine = "คอยล์สองตัว ตัวหนึ่งกันชนผนัง อีกตัวรีดความร้อน ติดตัวเดียวไม่พอนะ";
            });

            // 3 · nuclear_medicine
            Write("nuclear_medicine", n =>
            {
                n.title = "รังสีกับร่างกายคน";
                n.category = "การแพทย์";
                n.researcherSlots = 2; n.daysRequired = 3;
                n.costPower = 0; n.costIron = 0; n.costLabMat = 60;
                n.requiredLead = "radiation_biology";
                n.prerequisiteNotes = new string[0];
                n.unlocksBuildings = new[] { "med_bay", "rad_suit" };
                n.unlocksCommands = new[] { "cmd_scan", "craft_radsuit" };
                n.quizIds = new[] { "q_nuclear_medicine", "q_alara" };
                n.linkedRecordId = "";
                n.leadHint = "คนงานล้มโดยไม่มีบาดแผล ไม่มีไข้";
                n.knowledgeBody =
"รังสีปริมาณสูงทำลายเซลล์ในร่างกาย ทำให้เนื้อเยื่อโตผิดปกติได้\n\n" +
"เวชศาสตร์นิวเคลียร์ (Nuclear Medicine) ทำงาน 2 ขั้น:\n" +
"• วินิจฉัย — ฉีดสารเภสัชรังสีเข้าไป \"ถ่ายภาพ\" หาตำแหน่งก่อน (PET / SPECT)\n" +
"• รักษา — ส่งรังสีไป \"ทำลาย\" เฉพาะจุด กระทบเนื้อดีน้อย\n\n" +
"หลัก ALARA (As Low As Reasonably Achievable) — ให้คนรับรังสีน้อยที่สุดเท่าที่ทำได้\n" +
"และปกป้องกลุ่มที่ไวต่อรังสีเป็นพิเศษ (ผู้ป่วย/เด็ก) ก่อนเสมอ\n\n" +
"→ ปลดล็อก: Med Bay · ชุดกันรังสี (Rad Suit)";
                n.completionSpeaker = "MIRA";
                n.completionLine = "ที่พวกเขาล้ม เพราะรังสี... ฉันวินิจฉัยผิดมาทั้งอาทิตย์";
            });

            // 4 · irradiation
            Write("irradiation", n =>
            {
                n.title = "รังสีช่วยเรื่องอาหารได้ 2 ทาง";
                n.category = "เกษตร";
                n.researcherSlots = 3; n.daysRequired = 2;
                n.costPower = 120; n.costIron = 0; n.costLabMat = 0;
                n.requiredLead = "food_preservation";
                n.prerequisiteNotes = new string[0];
                n.unlocksBuildings = new[] { "co60_chamber", "mutation_lab" };
                n.unlocksCommands = new string[0];
                n.quizIds = new[] { "q_food_irradiation", "q_mutation" };
                n.linkedRecordId = "";
                n.leadHint = "ข้าวเน่าเร็วกว่าปกติสามเท่า";
                n.knowledgeBody =
"ทางที่ 1 — ถนอมอาหาร: ฉายรังสีแกมมา (เช่น จากโคบอลต์-60) ทะลุผ่านอาหาร\n" +
"ฆ่าจุลินทรีย์และเชื้อรา อาหารเก็บได้นานขึ้น\n" +
"สำคัญ: อาหารฉายรังสี ≠ อาหารมีรังสี — รังสีทะลุผ่านไป ไม่ตกค้างในอาหาร\n\n" +
"ทางที่ 2 — ปรับปรุงพันธุ์: ฉายรังสีใส่เมล็ด กระตุ้นให้เกิดการกลายพันธุ์\n" +
"นักวิจัยคัดเฉพาะสายพันธุ์ที่ทนทาน/ผลผลิตสูงไว้ใช้ถาวร\n" +
"(ข้าว กข6 ของไทย มาจากวิธีนี้)\n\n" +
"→ ปลดล็อก: Co-60 Chamber · Mutation Lab";
                n.completionSpeaker = "DORN(angry)";
                n.completionLine = "คุณจะเอารังสีมายิงใส่ข้าวที่คนต้องกิน แล้วบอกว่ามันปลอดภัยเนี่ยนะ";
            });

            // 5 · tritium (ประตูชนะ)
            Write("tritium", n =>
            {
                n.title = "เชื้อเพลิงระยะสอง — ทริเทียม";
                n.category = "เชื้อเพลิง";
                n.researcherSlots = 4; n.daysRequired = 2;
                n.costPower = 140; n.costIron = 60; n.costLabMat = 0;
                n.requiredLead = "lithium_breeding";
                n.prerequisiteNotes = new[] { "deuterium" };
                n.unlocksBuildings = new[] { "zone_b" };
                n.unlocksCommands = new[] { "open_zone_b" };
                n.quizIds = new[] { "q_dt_fuel", "q_tritium_breeding" };
                n.linkedRecordId = "record_04";
                n.leadHint = "CORE ใกล้ตัน เชื้อเพลิงที่มีดันได้แค่นี้";
                n.knowledgeBody =
"เตาฟิวชันจุดติดง่ายที่สุดด้วยเชื้อเพลิงคู่ ดิวเทอเรียม–ทริเทียม (D–T)\n" +
"เพราะคู่นี้หลอมรวมกันได้ที่อุณหภูมิต่ำกว่าคู่อื่น\n\n" +
"ดิวเทอเรียมล้วนดันเตาได้ถึงระดับหนึ่ง แต่ปฏิกิริยาไม่แรงพอจะดันต่อถึงจุดติด\n" +
"ตั้งแต่ CORE 80% ขึ้นไป เตา ต้องการทริเทียม (³H) มาป้อนคู่กัน\n\n" +
"ทริเทียมแทบไม่มีในธรรมชาติ แต่ผลิตเองได้:\n" +
"นำ นิวตรอน ที่เตาปล่อยออกมา ยิงใส่ ลิเทียม (breeding blanket)\n" +
"ลิเทียมแตกตัวคายทริเทียมออกมา — เตาจึงเลี้ยงเชื้อเพลิงของตัวเองได้\n" +
"เรียกว่า tritium breeding\n\n" +
"→ ปลดล็อก: Zone B (โรงเพาะทริเทียม)\n" +
"⚠ Zone B มีรังสีสูงมาก — จัดคนให้น้อยที่สุด และต้องมีชุดกันรังสี";
                n.completionSpeaker = "KOVA";
                n.completionLine = "เตาเลี้ยงเชื้อเพลิงตัวเองได้... แต่ต้องมีคนเข้าไปในนั้น";
            });

            // 6 · storm_detection
            Write("storm_detection", n =>
            {
                n.title = "อ่านสัญญาณพายุรังสี";
                n.category = "ระบบ";
                n.researcherSlots = 2; n.daysRequired = 2;
                n.costPower = 60; n.costIron = 0; n.costLabMat = 30;
                n.requiredLead = "storm_detection";
                n.prerequisiteNotes = new string[0];
                n.unlocksBuildings = new[] { "sensor_array" };
                n.unlocksCommands = new string[0];
                n.quizIds = new string[0];
                n.linkedRecordId = "record_final";
                n.leadHint = "เครื่องวัดเพี้ยนอีกแล้ว ครั้งที่สามวันนี้";
                n.knowledgeBody =
"รังสีจากการระเบิดของ CORE TOWER ไม่ได้หายไปทันที\n" +
"มันเคลื่อนเป็นคลื่นความดันในชั้นบรรยากาศ แล้ววนกลับมาเป็นระลอก\n\n" +
"เครื่องวัดที่เพี้ยนบ่อยผิดปกติ คือสัญญาณแรก\n" +
"ถ้าวัดความดันคลื่นได้ต่อเนื่อง เราจะรู้ล่วงหน้าว่ามันจะมาถึงเมื่อไหร่\n\n" +
"→ ปลดล็อก: Sensor Array (เห็นเกจ Storm Pressure + คาดการณ์วันที่มาถึง)";
                n.completionSpeaker = "KOVA";
                n.completionLine = "เครื่องวัดไม่ได้เสีย มันพยายามบอกอะไรเราอยู่";
            });

            // 7 · food_logistics (★ ไม่มีควิซ)
            Write("food_logistics", n =>
            {
                n.title = "คลังเสบียงกับการจัดคน";
                n.category = "บริหาร";
                n.researcherSlots = 2; n.daysRequired = 2;
                n.costPower = 0; n.costIron = 40; n.costLabMat = 0;
                n.requiredLead = "food_logistics";
                n.prerequisiteNotes = new string[0];
                n.unlocksBuildings = new[] { "granary" };
                n.unlocksCommands = new string[0];
                n.quizIds = new string[0];
                n.linkedRecordId = "";
                n.leadHint = "ยุ้งเหลือไม่ถึงสามวัน";
                n.knowledgeBody =
"คนหนึ่งคนกินอาหาร 1 หน่วยต่อวัน ไม่มีข้อยกเว้น\n" +
"คนที่ไม่ได้กิน 2 วันติด จะทำงานได้แค่ 60% — และขวัญเมืองจะเริ่มตก\n\n" +
"ปัญหาไม่ใช่ว่าเราผลิตอาหารไม่พอ\n" +
"ปัญหาคือเราดึงคนออกจากฟาร์มไปทำอย่างอื่น แล้วลืมว่ายุ้งไม่เติมตัวเอง\n\n" +
"คลังเสบียงที่ปิดสนิทและมีระบบหมุนเวียน ช่วยยืดเวลาให้เราแก้ตัวได้\n\n" +
"→ ปลดล็อก: Granary (อัตราเน่า ×0.6 · เปิดคลังสำรองได้ตอนวิกฤต)";
                n.completionSpeaker = "DORN(serious)";
                n.completionLine = "ผมพูดมาสามอาทิตย์แล้วว่ายุ้งมันไม่เติมตัวเอง";
            });

            // 8 · shift_management (★ ไม่มีควิซ)
            Write("shift_management", n =>
            {
                n.title = "กะและการพัก";
                n.category = "บริหาร";
                n.researcherSlots = 2; n.daysRequired = 2;
                n.costPower = 0; n.costIron = 50; n.costLabMat = 0;
                n.requiredLead = "shift_management";
                n.prerequisiteNotes = new string[0];
                n.unlocksBuildings = new[] { "barracks" };
                n.unlocksCommands = new string[0];
                n.quizIds = new string[0];
                n.linkedRecordId = "";
                n.leadHint = "คนของนายยืนหลับคาเครื่องแล้ว";
                n.knowledgeBody =
"คนทำงานต่อเนื่องโดยไม่พัก ประสิทธิภาพจะตกลงเรื่อยๆ\n" +
"พอความล้าเกินระดับหนึ่ง เขาจะหยุดเอง ไม่ว่าเราจะสั่งอะไร\n\n" +
"การบังคับให้ทำงานต่อไม่ได้ทำให้ได้งานเพิ่ม — มันแค่ทำให้คนบาดเจ็บ\n\n" +
"ที่พักที่มีเตียงจริงและมืดสนิท ทำให้คนฟื้นตัวเร็วขึ้นเกือบเท่าตัว\n" +
"คนที่พักครบ กลับมาทำงานได้เต็มร้อย\n\n" +
"→ ปลดล็อก: Barracks (ฟื้นความล้า −45/วัน แทน −30)";
                n.completionSpeaker = "MIRA";
                n.completionLine = "ไม่ต้องวิจัยก็รู้ว่าคนต้องนอน แต่เอาเถอะ ตอนนี้มีเตียงแล้ว";
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ResearchNotesSetup] สร้าง/อัปเดต ResearchNoteSO 8 ใบ ที่ " + Folder);
        }

        private static void Write(string noteId, System.Action<ResearchNoteSO> fill)
        {
            string path = $"{Folder}/{noteId}.asset";
            var note = AssetDatabase.LoadAssetAtPath<ResearchNoteSO>(path);
            bool isNew = note == null;
            if (isNew) note = ScriptableObject.CreateInstance<ResearchNoteSO>();

            note.noteId = noteId;
            fill(note);

            if (isNew) AssetDatabase.CreateAsset(note, path);
            else EditorUtility.SetDirty(note);
        }
    }
}
