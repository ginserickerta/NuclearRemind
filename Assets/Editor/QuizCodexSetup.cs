using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: สคริปต์ Editor สร้างเนื้อหาควิซ-Codex ทั้งชุด (11 ควิซ + 11 Codex + 11 Mastery)
    /// เป็น ScriptableObject assets จากข้อความในเอกสาร QUIZZES.md / CODEX.md แบบคำต่อคำ
    /// Generates the Sprint 3 Codex-quiz content (GDD §21 / QUIZZES.md / CODEX.md):
    ///   • 11 MasteryBonusSO  → Assets/Resources/Mastery
    ///   • 11 CodexEntrySO    → Assets/Resources/Codex
    ///   • 11 QuizQuestionSO  → Assets/Resources/Quizzes
    /// so CodexQuizManager auto-loads them at runtime (Resources.LoadAll).
    ///
    /// ★ question / options / explanation are VERBATIM from QUIZZES.md and Codex bodyText VERBATIM
    /// from CODEX.md §7 — ห้ามแต่งใหม่. Each quiz references its MasteryBonusSO and links its Codex
    /// entry via codexUnlockId (== entryId). Idempotent: re-running updates assets in place (GUIDs
    /// preserved). Run: menu NuclearReMind > Setup Quiz + Codex.
    /// </summary>
    public static class QuizCodexSetup
    {
        private const string MasteryFolder = "Assets/Resources/Mastery";
        private const string CodexFolder = "Assets/Resources/Codex";
        private const string QuizFolder = "Assets/Resources/Quizzes";

        // one row = one quiz + its 1:1 Codex entry + its Mastery bonus (QUIZZES.md / CODEX.md)
        private class Def
        {
            public string quizId, topicTitle, question, explanation, linkedNoteId;
            public string[] options;
            public int correctIndex;
            public QuizCategory category;
            public MasteryTarget target;
            public float bonusValue;
            public string bonusLabel;
            public string entryId, titleTh, titleEn, iconName, bodyText;
        }

        // เมนูนี้: สร้าง/อัปเดต asset ควิซ+Codex+Mastery ทั้ง 11 ชุดลง Resources (รันซ้ำได้ GUID ไม่เปลี่ยน)
        [MenuItem("NuclearReMind/Setup Quiz + Codex")]
        public static void Apply()
        {
            Directory.CreateDirectory(MasteryFolder);
            Directory.CreateDirectory(CodexFolder);
            Directory.CreateDirectory(QuizFolder);

            foreach (var d in Defs())
            {
                var bonus = WriteBonus(d);
                WriteCodex(d);
                WriteQuiz(d, bonus);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[QuizCodexSetup] สร้าง/อัปเดต Quiz + Codex + Mastery 11 ชุด (Resources/Quizzes · Codex · Mastery)");
        }

        // ─────────────────────────────────────────
        //  Writers (idempotent — load-or-create, keep GUIDs)
        // ─────────────────────────────────────────

        private static MasteryBonusSO WriteBonus(Def d)
        {
            string path = $"{MasteryFolder}/{d.quizId}.asset";
            var a = AssetDatabase.LoadAssetAtPath<MasteryBonusSO>(path);
            bool isNew = a == null;
            if (isNew) a = ScriptableObject.CreateInstance<MasteryBonusSO>();

            a.bonusId = d.quizId;
            a.targetSystem = d.target;
            a.value = d.bonusValue;
            a.label = d.bonusLabel;

            if (isNew) AssetDatabase.CreateAsset(a, path);
            else EditorUtility.SetDirty(a);
            return a;
        }

        private static void WriteCodex(Def d)
        {
            string path = $"{CodexFolder}/{d.entryId}.asset";
            var a = AssetDatabase.LoadAssetAtPath<CodexEntrySO>(path);
            bool isNew = a == null;
            if (isNew) a = ScriptableObject.CreateInstance<CodexEntrySO>();

            a.entryId = d.entryId;
            a.titleTh = d.titleTh;
            a.titleEn = d.titleEn;
            a.category = d.category;
            a.bodyText = d.bodyText;
            a.iconName = d.iconName;
            a.unlockedFromQuiz = d.quizId;

            if (isNew) AssetDatabase.CreateAsset(a, path);
            else EditorUtility.SetDirty(a);
        }

        private static void WriteQuiz(Def d, MasteryBonusSO bonus)
        {
            string path = $"{QuizFolder}/{d.quizId}.asset";
            var a = AssetDatabase.LoadAssetAtPath<QuizQuestionSO>(path);
            bool isNew = a == null;
            if (isNew) a = ScriptableObject.CreateInstance<QuizQuestionSO>();

            a.id = d.quizId;               // legacy field kept in sync (new path keys on quizId)
            a.quizId = d.quizId;
            a.category = d.category;
            a.topicTitle = d.topicTitle;
            a.question = d.question;
            a.options = d.options;
            a.correctIndex = d.correctIndex;
            a.explainText = d.explanation; // explanation shown after every answer (QUIZZES.md)
            a.codexUnlockId = d.entryId;   // link quiz → Codex entry
            a.linkedNoteId = d.linkedNoteId;
            a.requiresApplied = true;      // ★ every v6.3 quiz requires the knowledge USED first
            a.bonus = bonus;               // display metadata (None-target carries its "(Codex only)" label)

            if (isNew) AssetDatabase.CreateAsset(a, path);
            else EditorUtility.SetDirty(a);
        }

        // ─────────────────────────────────────────
        //  Content — VERBATIM (QUIZZES.md · CODEX.md §7). ห้ามแต่งใหม่.
        // ─────────────────────────────────────────

        private static List<Def> Defs() => new List<Def>
        {
            // Q1 · q_deuterium
            new Def {
                quizId = QuizIds.Deuterium, category = QuizCategory.Reactor, linkedNoteId = "deuterium",
                topicTitle = "เชื้อเพลิงจากน้ำ",
                question = "เมื่อกี้เราสกัดเชื้อเพลิงตัวแรกจาก \"น้ำ\" ได้ เพราะอะไร?",
                options = new[] {
                    "เพราะน้ำเป็นสารกัมมันตรังสีที่ปลอดภัยที่สุด",
                    "เพราะน้ำมีดิวเทอเรียมอยู่แล้ว ไม่ต้องสร้างใหม่ จึงแยกออกมาใช้เป็นเชื้อเพลิงได้เลย",
                    "เพราะน้ำช่วยดับไฟในเตาไม่ให้ร้อน",
                },
                correctIndex = 1,
                explanation =
"ดิวเทอเรียม (²H) เป็นไอโซโทปของไฮโดรเจนที่ปนอยู่ในน้ำทั่วไปอยู่แล้ว\n" +
"เราจึงแยกออกมาใช้เป็นเชื้อเพลิงฟิวชันได้เลย โดยไม่ต้องผลิตขึ้นใหม่",
                target = MasteryTarget.FuelEfficiency, bonusValue = 0.08f, bonusLabel = "fuelEfficiency +0.08",
                entryId = "codex_deuterium", titleTh = "ดิวเทอเรียม", titleEn = "Deuterium", iconName = "droplet",
                bodyText =
"ดิวเทอเรียม (²H) เป็นไอโซโทปของไฮโดรเจนที่ปนอยู่ในน้ำทั่วไปอยู่แล้ว\n" +
"เราจึงแยกออกมาใช้เป็นเชื้อเพลิงฟิวชันได้เลย โดยไม่ต้องผลิตขึ้นใหม่",
            },

            // Q2 · q_plasma
            new Def {
                quizId = QuizIds.Plasma, category = QuizCategory.Reactor, linkedNoteId = "confinement",
                topicTitle = "ทำไมพลาสมาถึงพัง",
                question = "อะไรคือสาเหตุที่ทำให้เตาหลอมละลายเมื่อพลาสมาไม่เสถียร?",
                options = new[] {
                    "พลาสมาที่ร้อนหลายล้านองศาหลุดไปชนผนังเตา ถ่ายเทความร้อนเข้าตัวอาคาร",
                    "พลาสมาเย็นเกินไปจนเตาหยุดทำงาน",
                    "มีน้ำมากเกินไปจนเตาจม",
                },
                correctIndex = 0,
                explanation =
"ในโทคาแมก พลาสมาร้อนหลายล้านองศาถูกกักด้วยสนามแม่เหล็ก\n" +
"ถ้าสนามไม่นิ่ง พลาสมาหลุดไปชนผนัง จะถ่ายเทความร้อนเข้าตัวอาคารจนหลอมละลาย",
                target = MasteryTarget.Cooling, bonusValue = 5f, bonusLabel = "cooling +5",
                entryId = "codex_plasma_confinement", titleTh = "การกักพลาสมา", titleEn = "Plasma Confinement", iconName = "flame",
                bodyText =
"ในโทคาแมกพลาสมาร้อนหลายล้านองศาถูกกักด้วยสนามแม่เหล็ก\n" +
"ถ้าสนามไม่นิ่ง พลาสมาหลุดไปชนผนัง จะถ่ายเทความร้อนเข้าตัวอาคารจนหลอมละลาย",
            },

            // Q3 · q_magnetic_pair
            new Def {
                quizId = QuizIds.MagneticPair, category = QuizCategory.Reactor, linkedNoteId = "confinement",
                topicTitle = "สนามแม่เหล็กคู่",
                question = "เมื่อ HEAT ของเตาเริ่มไต่สูง ควรทำอะไรเพื่อกันไม่ให้ถึง Meltdown?",
                options = new[] {
                    "ป้อนอาหารเพิ่มให้คนงาน",
                    "ปิดโรงน้ำเพื่อประหยัดพลังงาน",
                    "เสริมขดลวด Toroidal–Poloidal เพื่อยกเพดานหล่อเย็นและรีดความร้อนออก",
                },
                correctIndex = 2,
                explanation =
"สนามแม่เหล็กคู่ทำงานร่วมกัน — Toroidal บีบพลาสมาให้เป็นวง\n" +
"ส่วน Poloidal กันไม่ให้พลาสมาชนผนัง\n" +
"เมื่อเสริมให้แข็งแรงจะกักพลาสมาไว้กลางเตาและรีดความร้อนที่รั่วออก",
                target = MasteryTarget.PoloidalDamp, bonusValue = 1.15f, bonusLabel = "poloidalDamp ×1.15",
                entryId = "codex_magnetic_confinement", titleTh = "สนามแม่เหล็กคู่", titleEn = "Magnetic Confinement", iconName = "magnet",
                bodyText =
"สนามแม่เหล็กคู่ทำงานร่วมกัน — Toroidal บีบพลาสมาให้เป็นวง\n" +
"ส่วน Poloidal กันไม่ให้พลาสมาชนผนัง\n" +
"เมื่อเสริมให้แข็งแรงจะกักพลาสมาไว้กลางเตาและรีดความร้อนที่รั่วออก",
            },

            // Q4 · q_nuclear_medicine
            new Def {
                quizId = QuizIds.NuclearMedicine, category = QuizCategory.Medical, linkedNoteId = "nuclear_medicine",
                topicTitle = "หาก่อน แล้วค่อยยิง",
                question = "เวชศาสตร์นิวเคลียร์จัดการเซลล์เนื้อร้ายเป็น 2 ขั้นตอน ข้อใดถูก?",
                options = new[] {
                    "PET/SPECT ยิงรังสีทำลายเนื้อร้ายทันทีตั้งแต่ตอนสแกน",
                    "PET/SPECT ใช้สารรังสี \"ถ่ายภาพ\" หาตำแหน่งก่อน จากนั้นยาเฉพาะจุดจึงส่งรังสีไป \"ทำลาย\" เป้าแม่นยำ",
                    "รังสีฆ่าทุกเซลล์เท่ากันหมด ไม่ว่าจะดีหรือร้าย",
                },
                correctIndex = 1,
                explanation =
"เวชศาสตร์นิวเคลียร์ทำงาน 2 ขั้น —\n" +
"(1) วินิจฉัย: PET/SPECT ฉีดสารเภสัชรังสีถ่ายภาพหาตำแหน่งเซลล์ผิดปกติ\n" +
"(2) รักษา: ยาเฉพาะจุด (targeted therapy) ส่งรังสีไปทำลายเฉพาะเป้า กระทบเนื้อดีน้อย",
                target = MasteryTarget.MedBayHeal, bonusValue = 35f, bonusLabel = "Med Bay heal 25→35 · radiation ×0.8 ทุกโซน",
                entryId = "codex_nuclear_medicine", titleTh = "เวชศาสตร์นิวเคลียร์", titleEn = "Nuclear Medicine", iconName = "stethoscope",
                bodyText =
"เวชศาสตร์นิวเคลียร์ทำงาน 2 ขั้น —\n" +
"(1) วินิจฉัย: PET/SPECT ฉีดสารเภสัชรังสีถ่ายภาพหาตำแหน่งเซลล์ผิดปกติ\n" +
"(2) รักษา: ยาเฉพาะจุด (targeted therapy) ส่งรังสีไปทำลายเฉพาะเป้า กระทบเนื้อดีน้อย",
            },

            // Q5 · q_alara (Codex only)
            new Def {
                quizId = QuizIds.Alara, category = QuizCategory.Ethics, linkedNoteId = "nuclear_medicine",
                topicTitle = "ให้รับรังสีน้อยที่สุด",
                question = "ตามหลัก ALARA เมื่อต้องส่งคนเข้าพื้นที่เสี่ยงรังสี ควรจัดการอย่างไร?",
                options = new[] {
                    "จำกัดเวลา/ปริมาณรังสีต่อคนให้น้อยที่สุด และเลี่ยงส่งกลุ่มเปราะบาง (ผู้ป่วย/เด็ก)",
                    "ส่งใครก็ได้เข้าไปนานเท่าไรก็ได้ ถ้ามีชุดกันรังสี",
                    "ส่งผู้ป่วยเข้าไปก่อน เพราะป่วยอยู่แล้ว",
                },
                correctIndex = 0,
                explanation =
"ALARA (As Low As Reasonably Achievable) คือ \"ให้คนรับรังสีน้อยที่สุดเท่าที่ทำได้\"\n" +
"และต้องปกป้องกลุ่มที่ไวต่อรังสีเป็นพิเศษ (ผู้ป่วย/เด็ก) ก่อนเสมอ",
                target = MasteryTarget.None, bonusValue = 0f, bonusLabel = "(ผูกกับ q_nuclear_medicine — ปลด Codex อย่างเดียว)",
                entryId = "codex_alara", titleTh = "หลัก ALARA", titleEn = "ALARA", iconName = "shield",
                bodyText =
"ALARA (As Low As Reasonably Achievable) คือ \"ให้คนรับรังสีน้อยที่สุดเท่าที่ทำได้\"\n" +
"และต้องปกป้องกลุ่มที่ไวต่อรังสีเป็นพิเศษ (ผู้ป่วย/เด็ก) ก่อนเสมอ",
            },

            // Q6 · q_mutation
            new Def {
                quizId = QuizIds.Mutation, category = QuizCategory.Agriculture, linkedNoteId = "irradiation",
                topicTitle = "ปรับปรุงพันธุ์พืช",
                question = "การฉายรังสีใส่เมล็ดพันธุ์ช่วยแก้วิกฤตอาหารระยะยาวได้อย่างไร?",
                options = new[] {
                    "ทำให้พืชเรืองแสงจึงปลูกตอนกลางคืนได้",
                    "ทำให้พืชกลายเป็นกัมมันตรังสี กินแล้วแข็งแรง",
                    "กระตุ้นการกลายพันธุ์เชิงบวก คัดสายพันธุ์ที่ทนทาน/ผลผลิตสูงไว้ใช้ถาวร",
                },
                correctIndex = 2,
                explanation =
"การฉายรังสีกระตุ้นให้เกิดการกลายพันธุ์\n" +
"นักวิจัยคัดเลือกเฉพาะสายพันธุ์ที่ทนทานและให้ผลผลิตสูงไว้ใช้ถาวร\n" +
"เช่น ข้าว กข6 ของไทย — เป็นการแก้ปัญหาที่ต้นเหตุ",
                target = MasteryTarget.FarmYield, bonusValue = 1.15f, bonusLabel = "ผลผลิตฟาร์ม +15%",
                entryId = "codex_mutation_breeding", titleTh = "ปรับปรุงพันธุ์ด้วยรังสี", titleEn = "Mutation Breeding", iconName = "seeding",
                bodyText =
"การฉายรังสีกระตุ้นให้เกิดการกลายพันธุ์\n" +
"นักวิจัยคัดเลือกเฉพาะสายพันธุ์ที่ทนทานและให้ผลผลิตสูงไว้ใช้ถาวร\n" +
"เช่น ข้าว กข 6 ของไทย — เป็นการแก้ปัญหาที่ต้นเหตุ",
            },

            // Q7 · q_food_irradiation
            new Def {
                quizId = QuizIds.FoodIrradiation, category = QuizCategory.Agriculture, linkedNoteId = "irradiation",
                topicTitle = "ฉายรังสีถนอมอาหาร",
                question = "การฉายรังสีแกมมาจากโคบอลต์-60 ช่วยถนอมอาหารด้วยกลไกใด?",
                options = new[] {
                    "รังสีเคลือบผิวอาหารด้วยโลหะกันบูด",
                    "รังสีทำลายจุลินทรีย์/เชื้อราในอาหาร ชะลอการเน่า โดยอาหารไม่กลายเป็นสารรังสี",
                    "รังสีทำให้อาหารแช่แข็งตลอดเวลา",
                },
                correctIndex = 1,
                explanation =
"รังสีแกมมาทะลุผ่านอาหารและฆ่าจุลินทรีย์/เชื้อรา ทำให้เก็บได้นานขึ้น\n" +
"โดยอาหารไม่กลายเป็นสารกัมมันตรังสี (อาหารฉายรังสี ≠ อาหารมีรังสี)",
                target = MasteryTarget.SpoilRate, bonusValue = 0.5f, bonusLabel = "อัตราเน่า −50% เพิ่มเติม",
                entryId = "codex_food_irradiation", titleTh = "ฉายรังสีถนอมอาหาร", titleEn = "Food Irradiation", iconName = "meat",
                bodyText =
"รังสีแกมมาทะลุผ่านอาหารและฆ่าจุลินทรีย์/เชื้อรา ทำให้เก็บได้นานขึ้น\n" +
"โดยอาหารไม่กลายเป็นสารกัมมันตรังสี (อาหารฉายรังสี ≠ อาหารมีรังสี)",
            },

            // Q8 · q_dt_fuel
            new Def {
                quizId = QuizIds.DtFuel, category = QuizCategory.Reactor, linkedNoteId = "tritium",
                topicTitle = "เชื้อเพลิงคู่ D–T",
                question = "ทำไมพอ CORE% ถึง 80% เตาถึงต้องการทริเทียมมาป้อนเพิ่ม ทั้งที่ยังมีดิวเทอเรียมอยู่?",
                options = new[] {
                    "เพราะดิวเทอเรียมหมดคลังพอดีที่ 80% ต้องหาเชื้อเพลิงอะไรก็ได้มาแทน",
                    "เพราะเตาฟิวชันจุดติดง่ายที่สุดด้วยคู่ D–T ดิวเทอเรียมล้วนดันได้ถึงจุดหนึ่ง แล้วต้องมีทริเทียมป้อนคู่ปฏิกิริยาถึงจะแรงพอดันต่อ",
                    "เพราะทริเทียมเย็นกว่า ช่วยลดความร้อนในเตาลง",
                },
                correctIndex = 1,
                explanation =
"เชื้อเพลิงที่หลอมรวมได้ง่ายที่สุดคือคู่ ดิวเทอเรียม–ทริเทียม (D–T)\n" +
"เพราะจุดติดที่อุณหภูมิต่ำกว่าคู่อื่น\n" +
"ดิวเทอเรียมจากน้ำพาเตาขึ้นมาได้ระดับหนึ่ง\n" +
"แต่การจะดันถึงจุดติดเต็มร้อยต้องมีทริเทียมป้อนคู่",
                target = MasteryTarget.FuelEfficiency, bonusValue = 0.08f, bonusLabel = "fuelEfficiency +0.08",
                entryId = "codex_dt_fusion_fuel", titleTh = "เชื้อเพลิงคู่ D–T", titleEn = "D–T Fusion Fuel", iconName = "atom-2",
                bodyText =
"เชื้อเพลิงที่หลอมรวมได้ง่ายที่สุดคือคู่ ดิวเทอเรียม–ทริเทียม (D–T)\n" +
"เพราะจุดติดที่อุณหภูมิต่ำกว่าคู่อื่น\n" +
"ดิวเทอเรียมจากน้ำพาเตาขึ้นมาได้ระดับหนึ่ง\n" +
"แต่การจะดันถึงจุดติดเต็มรอบต้องมีทริเทียมป้อนคู่",
            },

            // Q9 · ★ q_tritium_breeding
            new Def {
                quizId = QuizIds.TritiumBreeding, category = QuizCategory.Reactor, linkedNoteId = "tritium",
                topicTitle = "เตาเลี้ยงเชื้อเพลิงตัวเอง",
                question = "Zone B ผลิตทริเทียมขึ้นมาได้อย่างไร?",
                options = new[] {
                    "ขุดทริเทียมสำเร็จรูปขึ้นมาจากใต้ดินโดยตรง",
                    "ใช้นิวตรอนที่เตาปล่อยออกมายิงใส่ลิเทียม ทำให้ลิเทียมแตกตัวคายทริเทียมออกมา เตาจึงเลี้ยงเชื้อเพลิงของตัวเองได้",
                    "กลั่นทริเทียมออกจากน้ำทะเลเหมือนดิวเทอเรียมทุกประการ",
                },
                correctIndex = 1,
                explanation =
"ทริเทียมแทบไม่มีในธรรมชาติ แต่ผลิตได้ด้วยการนำ นิวตรอน ที่เกิดจากปฏิกิริยาฟิวชัน\n" +
"ไปยิงใส่ ลิเทียม (breeding blanket) ลิเทียมจะแตกตัวให้ทริเทียม\n" +
"เตาฟิวชันจึงสามารถผลิตเชื้อเพลิงส่วนหนึ่งของตัวเองได้ เรียกว่า tritium breeding",
                target = MasteryTarget.ZoneBTritium, bonusValue = 8.0f, bonusLabel = "★ Zone B: 3.0 → 8.0 ทริเทียม/วัน",
                entryId = "codex_tritium_breeding", titleTh = "การเพาะทริเทียม", titleEn = "Tritium Breeding", iconName = "atom",
                bodyText =
"ทริเทียมแทบไม่มีในธรรมชาติ แต่ผลิตได้ด้วยการนำนิวตรอนที่เกิดจากปฏิกิริยาฟิวชัน\n" +
"ไปยิงใส่ลิเทียม (breeding blanket) ลิเทียมจะแตกตัวให้ทริเทียม\n" +
"เตาฟิวชันจึงสามารถผลิตเชื้อเพลิงส่วนหนึ่งของตัวเองได้\n" +
"เรียกว่า tritium breeding",
            },

            // Q10 · q_fusion (milestone)
            new Def {
                quizId = QuizIds.Fusion, category = QuizCategory.Reactor, linkedNoteId = "",
                topicTitle = "ฟิวชันคืออะไร",
                question = "\"ฟิวชันนิวเคลียร์\" ที่กำลังจะจุดติดในเตา แท้จริงคืออะไร?",
                options = new[] {
                    "การหลอมรวมนิวเคลียสเบา (ไฮโดรเจน + ไฮโดรเจน) ให้เป็นธาตุที่หนักกว่า แล้วปลดปล่อยพลังงานมหาศาล",
                    "การแตกตัวของนิวเคลียสหนักออกเป็นชิ้นเล็ก เหมือนเครื่องปฏิกรณ์ฟิชชัน",
                    "การเผาไหม้ถ่านหินด้วยความร้อนสูง",
                },
                correctIndex = 0,
                explanation =
"ฟิวชันคือการหลอมรวมนิวเคลียสเบา (เช่น ไฮโดรเจน) ให้กลายเป็นธาตุที่หนักกว่า\n" +
"แล้วปลดปล่อยพลังงานมหาศาล\n" +
"\"ตรงข้าม\" กับฟิชชันที่เป็นการแตกตัวของนิวเคลียสหนัก",
                target = MasteryTarget.BoostHeat, bonusValue = 0.9f, bonusLabel = "Boost heat cost −10%",
                entryId = "codex_nuclear_fusion", titleTh = "ฟิวชันคืออะไร", titleEn = "Nuclear Fusion", iconName = "atom-2",
                bodyText =
"ฟิวชันคือการหลอมรวมนิวเคลียสเบา (เช่น ไฮโดรเจน) ให้กลายเป็นธาตุที่หนักกว่า\n" +
"แล้วปลดปล่อยพลังงานมหาศาล\n" +
"\"ตรงข้าม\" กับฟิชชันที่เป็นการแตกตัวของนิวเคลียสหนัก",
            },

            // Q11 · q_clean_energy (milestone / ending)
            new Def {
                quizId = QuizIds.CleanEnergy, category = QuizCategory.Reactor, linkedNoteId = "",
                topicTitle = "ทำไมฟิวชันถึงสะอาด",
                question = "ข้อใดอธิบายได้ถูกว่าทำไมพลังงานฟิวชันถึงสะอาดกว่าทางเลือกอื่น?",
                options = new[] {
                    "เพราะมันไม่เกี่ยวข้องกับรังสีหรือนิวเคลียร์เลย บริสุทธิ์ 100%",
                    "เพราะมันเผาถ่านหินที่สะอาดเป็นพิเศษ",
                    "เชื้อเพลิงมาจากน้ำ ไม่ปล่อย CO₂ ไม่มีปฏิกิริยาลูกโซ่ที่คุมไม่ได้ และไม่มีกากรังสีอายุยืนแบบฟิชชัน",
                },
                correctIndex = 2,
                explanation =
"ฟิวชันสะอาดกว่าเพราะเชื้อเพลิงหาได้จากน้ำ ไม่ปล่อย CO₂\n" +
"ถ้าเสียสมดุลเตาจะดับเอง (ไม่ระเบิด) และไม่มีกากรังสีอายุยืนแบบฟิชชัน\n" +
"— แต่ \"สะอาดกว่า\" ไม่ได้แปลว่า \"ไม่มีรังสีเลย\" เพราะเชื้อเพลิง D-T ยังปล่อยนิวตรอน",
                target = MasteryTarget.None, bonusValue = 0f, bonusLabel = "(ending stat เท่านั้น)",
                entryId = "codex_clean_energy", titleTh = "ทำไมฟิวชันสะอาด", titleEn = "Clean Energy", iconName = "leaf",
                bodyText =
"ฟิวชันสะอาดกว่าเพราะเชื้อเพลิงหาได้จากน้ำ ไม่ปล่อย CO₂\n" +
"ถ้าเสียสมดุลเตาจะดับเอง (ไม่ระเบิด)\n" +
"และไม่มีกากรังสีอายุยืนแบบฟิชชัน\n" +
"— แต่ \"สะอาดกว่า\" ไม่ได้แปลว่า \"ไม่มีรังสีเลย\" เพราะเชื้อเพลิง D-T ยังปล่อยนิวตรอน",
            },
        };
    }
}
