using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: สคริปต์ Editor สร้าง asset คำถามควิซ 12 ข้อ แล้วต่อสายเข้าระบบควิซในซีนเกม
    /// สร้าง 12 QuizQuestionSO assets (Q1–Q10 + QT1/QT2 ควิซ Tritium ใหม่ v8)
    /// + wire เข้า QuizManager.allQuizzes + ผูก linkedQuizIds ให้ crisis dilemma
    /// รันผ่านเมนู NuclearReMind / Setup Quiz System
    ///
    /// สีตามหมวด (§17): Q1,2,3,8,9,QT1,QT2 = Reactor · Q6,7 = Agriculture · Q4 = Medical · Q5,10 = Ethics
    ///
    /// 🔓 ตาราง codexUnlockId (Codex_Spec v8 — 11 entry · Q10 ชี้กลับ ALARA เดิม ไม่นับแยก):
    ///   Q1→codex_deuterium · Q2→codex_plasma_confinement · Q3→codex_magnetic_confinement
    ///   Q4→codex_nuclear_medicine · Q5,Q10→codex_alara · Q6→codex_mutation_breeding
    ///   Q7→codex_food_irradiation · Q8→codex_nuclear_fusion · Q9→codex_clean_energy
    ///   QT1→codex_dt_fusion_fuel · QT2→codex_tritium_breeding (เด้งตอนป้อน Tritium เข้าเตาครั้งแรก
    ///   — CoreTowerManager.TriggerTritiumQuizzes latch ครั้งเดียว)
    /// entry ทั้ง 11 สร้างโดย CodexSetup — รัน Setup Codex System ก่อน/หลังได้ (match ด้วย id ตอนเล่น)
    /// </summary>
    public static class QuizSetup
    {
        private const string Folder        = "Assets/ScriptableObjects/Quizzes";
        private const string DilemmaFolder = "Assets/ScriptableObjects/Dilemmas";
        private const string ScenePath     = "Assets/Scenes/Gamescene.unity";

        // เมนูนี้: สร้าง asset ควิซทั้ง 12 ข้อ + wire เข้า QuizManager และ crisis dilemma
        [MenuItem("NuclearReMind/Setup Quiz System")]
        public static void SetupAll()
        {
            System.IO.Directory.CreateDirectory(Folder);

            var quizzes = new List<QuizQuestionSO>();
            foreach (var def in BuildDefs())
                quizzes.Add(CreateOrUpdate(def));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            WireDilemmas();
            AssetDatabase.SaveAssets();

            WireManager(quizzes);

            Debug.Log($"[QuizSetup] สร้าง {quizzes.Count} quizzes + wire QuizManager.allQuizzes + ผูก linkedQuizIds ให้ crisis สำเร็จ");
        }

        private static QuizQuestionSO CreateOrUpdate(QuizDef def)
        {
            string path = $"{Folder}/{def.id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<QuizQuestionSO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<QuizQuestionSO>();
                AssetDatabase.CreateAsset(asset, path);
                Debug.Log($"[QuizSetup] สร้าง {path}");
            }

            asset.id              = def.id;
            asset.category        = def.category;
            asset.topicTitle      = def.topicTitle;
            asset.question        = def.question;
            asset.options         = def.options;
            asset.correctIndex    = def.correctIndex;
            asset.rewardKnowledge = 8;              // ถูก +8 · ผิด +3 (จัดการที่ QuizManager.SubmitAnswer)
            asset.explainText     = def.explainText;
            asset.speaker         = def.speaker;
            asset.codexUnlockId   = def.codexUnlockId;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        // wire QuizManager.allQuizzes (QuizManager GameObject สร้างโดย HUDCanvasSetup — Gap G9)
        private static void WireManager(List<QuizQuestionSO> quizzes)
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var mgr = Object.FindFirstObjectByType<QuizManager>();
            if (mgr == null)
            {
                Debug.LogWarning("[QuizSetup] ไม่พบ QuizManager ใน scene — รัน NuclearReMind/Setup HUD Canvas ก่อน แล้วรันใหม่");
                return;
            }

            mgr.allQuizzes = quizzes.ToArray();
            EditorUtility.SetDirty(mgr);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[QuizSetup] wire QuizManager.allQuizzes = {quizzes.Count} quizzes");
        }

        // ผูกควิซเข้ากับ crisis dilemma: Plasma→Q2,Q3 · Outbreak→Q4,Q5 (ทุกทางเลือก)
        // Food → รายทางเลือก (Story Guide quizRef): A=Q6 (mutation) · B=Q7 (irradiation) · C=ไม่มีควิซ
        //   (linkedQuizIds ต้องว่าง — ไม่งั้นทางเลือก C จะ fallback ไปเด้ง Q6,Q7 ทั้งคู่)
        private static void WireDilemmas()
        {
            WireDilemma("Crisis_PlasmaInstability", "Q2", "Q3");
            WireDilemma("Crisis_MalignantOutbreak", "Q4", "Q5");
            WireDilemmaPerChoice("Crisis_FoodShortage",
                a: new[] { "Q6" }, b: new[] { "Q7" }, c: new string[0]);
        }

        private static void WireDilemma(string dilemmaId, params string[] quizIds)
        {
            var d = LoadDilemma(dilemmaId);
            if (d == null) return;

            d.linkedQuizIds = quizIds;
            EditorUtility.SetDirty(d);
            Debug.Log($"[QuizSetup] wire {dilemmaId}.linkedQuizIds = [{string.Join(", ", quizIds)}]");
        }

        private static void WireDilemmaPerChoice(string dilemmaId, string[] a, string[] b, string[] c)
        {
            var d = LoadDilemma(dilemmaId);
            if (d == null) return;

            d.linkedQuizIds  = new string[0]; // fallback ว่าง — ทางเลือกที่ไม่มีรายการ = ไม่มีควิซ
            d.choiceA_QuizIds = a;
            d.choiceB_QuizIds = b;
            d.choiceC_QuizIds = c;
            EditorUtility.SetDirty(d);
            Debug.Log($"[QuizSetup] wire {dilemmaId} per-choice: A=[{string.Join(",", a)}] B=[{string.Join(",", b)}] C=[{string.Join(",", c)}]");
        }

        private static DilemmaData LoadDilemma(string dilemmaId)
        {
            string path = $"{DilemmaFolder}/{dilemmaId}.asset";
            var d = AssetDatabase.LoadAssetAtPath<DilemmaData>(path);
            if (d == null)
                Debug.LogWarning($"[QuizSetup] ไม่พบ dilemma {path} — รัน NuclearReMind/Setup Crisis Dilemmas ก่อน");
            return d;
        }

        // ─────────────────────────────────────────────
        //  เนื้อหาควิซ 10 ข้อ (Story Guide §4 — verbatim · ทุกข้อขึ้นต้น "จาก Info Card เมื่อกี้"
        //  ตอกย้ำธีม "ควิซ = ทบทวนสิ่งที่เพิ่งอ่าน" — เดิมใช้ฉบับ V4 §12 ความหมายเดียวกันแต่ถ้อยคำต่าง)
        // ─────────────────────────────────────────────
        private static QuizDef[] BuildDefs() => new[]
        {
            // ── Q1 · เชื้อเพลิงแรก: ดิวเทอเรียม (Reactor · VESTA) ──
            new QuizDef
            {
                id = "Q1", category = QuizCategory.Reactor, speaker = "VESTA",
                topicTitle = "เชื้อเพลิงแรกจากน้ำ",
                question = "เมื่อกี้เราสกัดเชื้อเพลิงตัวแรกจาก \"น้ำ\" ได้ เพราะอะไร?",
                options = new[]
                {
                    "เพราะน้ำเป็นสารกัมมันตรังสีที่ปลอดภัยที่สุด",
                    "เพราะน้ำมีดิวเทอเรียมอยู่แล้ว ไม่ต้องสร้างใหม่ จึงมีเชื้อเพลิงป้อนเตาได้ต่อเนื่อง",
                    "เพราะน้ำช่วยดับไฟในเตาไม่ให้ร้อน",
                },
                correctIndex = 1,
                explainText = "ดิวเทอเรียม (²H) เป็นไอโซโทปของไฮโดรเจนที่ปนอยู่ในน้ำทั่วไปอยู่แล้ว เราจึงแยกออกมาใช้เป็นเชื้อเพลิงฟิวชันได้เลย โดยไม่ต้องผลิตขึ้นใหม่",
                codexUnlockId = "codex_deuterium",   // ดิวเทอเรียม (Codex_Spec v8 #1)
            },

            // ── Q2 · ทำไมพลาสมาถึงพัง (Reactor · Dr. Auren Vasek) ──
            new QuizDef
            {
                id = "Q2", category = QuizCategory.Reactor, speaker = "Dr. Auren Vasek",
                topicTitle = "ทำไมพลาสมาถึงพัง",
                question = "จาก Info Card เมื่อกี้ อะไรคือสาเหตุที่ทำให้เตาหลอมละลายเมื่อพลาสมาไม่เสถียร?",
                options = new[]
                {
                    "พลาสมาที่ร้อนหลายล้านองศาหลุดไปชนผนังเตา ถ่ายเทความร้อนเข้าตัวอาคาร",
                    "พลาสมาเย็นเกินไปจนเตาหยุดทำงาน",
                    "มีน้ำมากเกินไปจนเตาจม",
                },
                correctIndex = 0,
                explainText = "ในโทคาแมก พลาสมาร้อนหลายล้านองศาถูกกักด้วยสนามแม่เหล็ก ถ้าสนามไม่นิ่ง พลาสมาหลุดไปชนผนัง จะถ่ายเทความร้อนเข้าตัวอาคารจนหลอมละลาย",
                codexUnlockId = "codex_plasma_confinement",   // การกักพลาสมา (v8 #2)
            },

            // ── Q3 · สนามแม่เหล็กคู่คืออะไร (Reactor · VESTA) ──
            new QuizDef
            {
                id = "Q3", category = QuizCategory.Reactor, speaker = "VESTA",
                topicTitle = "กันเตาไม่ให้ Meltdown",
                question = "เมื่อ HEAT ของเตาเริ่มไต่สูง ควรทำอะไรเพื่อกันไม่ให้ถึง Meltdown?",
                options = new[]
                {
                    "ป้อนอาหารเพิ่มให้คนงาน",
                    "ปิดโรงน้ำเพื่อประหยัดพลังงาน",
                    "เสริมสนามแม่เหล็ก/หล่อเย็น (Toroidal–Poloidal) เพื่อยกเพดานและรีดความร้อนออก",
                },
                correctIndex = 2,
                explainText = "สนามแม่เหล็กคู่ทำงานร่วมกัน — Toroidal บีบพลาสมาให้เป็นวง ส่วน Poloidal กันไม่ให้พลาสมาชนผนัง เมื่อเสริมให้แข็งแรงจะกักพลาสมาไว้กลางเตาและรีดความร้อนที่รั่วออก",
                codexUnlockId = "codex_magnetic_confinement",   // สนามแม่เหล็กคู่ (v8 #3)
            },

            // ── Q4 · เวชศาสตร์นิวเคลียร์: หาก่อน แล้วค่อยยิง (Medical · แพทย์ประจำเมือง) ──
            new QuizDef
            {
                id = "Q4", category = QuizCategory.Medical, speaker = "แพทย์ประจำเมือง",
                topicTitle = "เวชศาสตร์นิวเคลียร์",
                question = "จาก Info Card เมื่อกี้ เวชศาสตร์นิวเคลียร์จัดการเซลล์เนื้อร้ายเป็น 2 ขั้นตอน ข้อใดถูก?",
                options = new[]
                {
                    "PET/SPECT ยิงรังสีทำลายเนื้อร้ายทันทีตั้งแต่ตอนสแกน",
                    "PET/SPECT ใช้สารรังสี \"ถ่ายภาพ\" หาตำแหน่งก่อน จากนั้นยาเฉพาะจุดจึงส่งรังสีไป \"ทำลาย\" เป้าแม่นยำ",
                    "รังสีฆ่าทุกเซลล์เท่ากันหมด ไม่ว่าจะดีหรือร้าย",
                },
                correctIndex = 1,
                explainText = "เวชศาสตร์นิวเคลียร์ทำงาน 2 ขั้น — (1) วินิจฉัย: PET/SPECT ฉีดสารเภสัชรังสีถ่ายภาพหาตำแหน่งเซลล์ผิดปกติ (2) รักษา: ยาเฉพาะจุด (targeted therapy) ส่งรังสีไปทำลายเฉพาะเป้า กระทบเนื้อดีน้อย",
                codexUnlockId = "codex_nuclear_medicine",   // เวชศาสตร์นิวเคลียร์ (v8 #6)
            },

            // ── Q5 · ใครห้ามเข้าเขตรังสี (ALARA) (Ethics · VESTA) ──
            new QuizDef
            {
                id = "Q5", category = QuizCategory.Ethics, speaker = "VESTA",
                topicTitle = "หลัก ALARA",
                question = "ตามหลัก ALARA ที่เพิ่งอ่าน เมื่อต้องส่งคนเข้าพื้นที่เสี่ยงรังสี ควรจัดการอย่างไร?",
                options = new[]
                {
                    "จำกัดเวลา/ปริมาณรังสีต่อคนให้น้อยที่สุด และเลี่ยงส่งกลุ่มเปราะบาง (ผู้ป่วย/เด็ก)",
                    "ส่งใครก็ได้เข้าไปนานเท่าไรก็ได้ ถ้ามีชุดกันรังสี",
                    "ส่งผู้ป่วยเข้าไปก่อน เพราะป่วยอยู่แล้ว",
                },
                correctIndex = 0,
                explainText = "ALARA (As Low As Reasonably Achievable) คือ \"ให้คนรับรังสีน้อยที่สุดเท่าที่ทำได้\" และต้องปกป้องกลุ่มที่ไวต่อรังสีเป็นพิเศษ (ผู้ป่วย/เด็ก) ก่อนเสมอ",
                codexUnlockId = "codex_alara",   // หลัก ALARA (v8 #7 — Q10 ชี้กลับ entry เดิม ไม่นับแยก)
            },

            // ── Q6 · แก้ที่ต้นเหตุ ไม่ใช่ปลายเหตุ (Agriculture · Dr. Auren Vasek) ──
            new QuizDef
            {
                id = "Q6", category = QuizCategory.Agriculture, speaker = "Dr. Auren Vasek",
                topicTitle = "ปรับปรุงพันธุ์ด้วยรังสี",
                question = "จาก Info Card เมื่อกี้ การฉายรังสีใส่เมล็ดพันธุ์ช่วยแก้วิกฤตอาหารระยะยาวได้อย่างไร?",
                options = new[]
                {
                    "ทำให้พืชเรืองแสงจึงปลูกตอนกลางคืนได้",
                    "ทำให้พืชกลายเป็นกัมมันตรังสี กินแล้วแข็งแรง",
                    "กระตุ้นการกลายพันธุ์เชิงบวก คัดสายพันธุ์ที่ทนทาน/ผลผลิตสูงไว้ใช้ถาวร",
                },
                correctIndex = 2,
                explainText = "การฉายรังสีกระตุ้นให้เกิดการกลายพันธุ์ นักวิจัยคัดเลือกเฉพาะสายพันธุ์ที่ทนทานและให้ผลผลิตสูงไว้ใช้ถาวร เช่น ข้าว กข6 ของไทย เป็นการแก้ปัญหาที่ต้นเหตุ",
                codexUnlockId = "codex_mutation_breeding",   // ปรับปรุงพันธุ์ด้วยรังสี (v8 #8)
            },

            // ── Q7 · หยุดอาหารเน่าด้วยรังสี (Agriculture · VESTA) ──
            new QuizDef
            {
                id = "Q7", category = QuizCategory.Agriculture, speaker = "VESTA",
                topicTitle = "ฉายรังสีถนอมอาหาร",
                question = "จาก Info Card เมื่อกี้ การฉายรังสีแกมมาจากโคบอลต์-60 ช่วยถนอมอาหารด้วยกลไกใด?",
                options = new[]
                {
                    "รังสีเคลือบผิวอาหารด้วยโลหะกันบูด",
                    "รังสีทำลายจุลินทรีย์/เชื้อราในอาหาร ชะลอการเน่า โดยอาหารไม่กลายเป็นสารรังสี",
                    "รังสีทำให้อาหารแช่แข็งตลอดเวลา",
                },
                correctIndex = 1,
                explainText = "รังสีแกมมาทะลุผ่านอาหารและฆ่าจุลินทรีย์/เชื้อรา ทำให้เก็บได้นานขึ้น โดยอาหารไม่กลายเป็นสารกัมมันตรังสี (อาหารฉายรังสี ≠ อาหารมีรังสี)",
                codexUnlockId = "codex_food_irradiation",   // ฉายรังสีถนอมอาหาร (v8 #9)
            },

            // ── Q8 · ฟิวชันคืออะไร (Reactor · VESTA) ──
            new QuizDef
            {
                id = "Q8", category = QuizCategory.Reactor, speaker = "VESTA",
                topicTitle = "ฟิวชันคืออะไร",
                question = "จาก Info Card เมื่อกี้ \"ฟิวชันนิวเคลียร์\" ที่เพิ่งจุดติดในเตา แท้จริงคืออะไร?",
                options = new[]
                {
                    "การหลอมรวมนิวเคลียสเบา (ไฮโดรเจน + ไฮโดรเจน) ให้เป็นธาตุที่หนักกว่า แล้วปลดปล่อยพลังงานมหาศาล",
                    "การแตกตัวของนิวเคลียสหนักออกเป็นชิ้นเล็ก เหมือนเครื่องปฏิกรณ์ฟิชชัน",
                    "การเผาไหม้ถ่านหินด้วยความร้อนสูง",
                },
                correctIndex = 0,
                explainText = "ฟิวชันคือการหลอมรวมนิวเคลียสเบา (เช่น ไฮโดรเจน) ให้กลายเป็นธาตุที่หนักกว่า แล้วปลดปล่อยพลังงานมหาศาล \"ตรงข้าม\" กับฟิชชันที่เป็นการแตกตัวของนิวเคลียสหนัก",
                codexUnlockId = "codex_nuclear_fusion",   // ฟิวชันคืออะไร (v8 #10)
            },

            // ── Q9 · ทำไมฟิวชันถึงสะอาด (Reactor · Dr. Auren Vasek) ──
            new QuizDef
            {
                id = "Q9", category = QuizCategory.Reactor, speaker = "Dr. Auren Vasek",
                topicTitle = "ทำไมฟิวชันสะอาด",
                question = "จาก Info Card เมื่อกี้ ข้อใดอธิบายได้ถูกว่าทำไมพลังงานฟิวชันถึงสะอาดกว่าทางเลือกอื่น?",
                options = new[]
                {
                    "เพราะมันไม่เกี่ยวข้องกับรังสีหรือนิวเคลียร์เลย บริสุทธิ์ 100%",
                    "เพราะมันเผาถ่านหินที่สะอาดเป็นพิเศษ",
                    "เชื้อเพลิงมาจากน้ำทะเล ไม่ปล่อย CO₂ ไม่มีปฏิกิริยาลูกโซ่ที่คุมไม่ได้ และไม่มีกากรังสีอายุยืนแบบฟิชชัน",
                },
                correctIndex = 2,
                explainText = "ฟิวชันสะอาดกว่าเพราะเชื้อเพลิงหาได้จากน้ำ ไม่ปล่อย CO₂ ถ้าเสียสมดุลเตาจะดับเอง (ไม่ระเบิด) และไม่มีกากรังสีอายุยืนแบบฟิชชัน — แต่ \"สะอาดกว่า\" ไม่ได้แปลว่า \"ไม่มีรังสีเลย\" เพราะเชื้อเพลิง D-T ยังปล่อยนิวตรอน",
                codexUnlockId = "codex_clean_energy",   // ทำไมฟิวชันสะอาด (v8 #11)
            },

            // ── Q10 · จริยธรรม (Decree) (Ethics · Dr. Auren Vasek) ──
            new QuizDef
            {
                id = "Q10", category = QuizCategory.Ethics, speaker = "Dr. Auren Vasek",
                topicTitle = "จริยธรรมแรงงานฉุกเฉิน",
                question = "จาก Info Card เมื่อกี้ การเกณฑ์แรงงานฉุกเฉินให้คนเข้าทำงานในเขตเสี่ยงรังสีขณะวิกฤต ขัดกับหลักจริยธรรมอย่างไร?",
                options = new[]
                {
                    "ไม่มีปัญหาอะไร เพราะคนกลุ่มนี้แค่ทำงานช้ากว่าคนทั่วไป",
                    "กลุ่มเปราะบางไวต่อรังสีเป็นพิเศษ การบังคับให้เข้าไปเสี่ยงจึงขัดหลัก ALARA โดยตรง",
                    "ไม่ขัดอะไรเลย ถ้าทุกคนในเมืองตกลงร่วมมือกัน",
                },
                correctIndex = 1,
                explainText = "การส่งผู้ป่วย/เด็กเข้าเขตรังสีขัดหลัก ALARA เพราะคนกลุ่มนี้ไวต่อรังสีเป็นพิเศษ การตัดสินใจนี้เป็นของผู้เล่น เกมไม่ตัดสินถูก-ผิดแทน แต่ผลของมันคือ Hope และแรงงานในรอบนั้น",
                codexUnlockId = "codex_alara",   // หลัก ALARA (v8 #7 — Q10 ชี้กลับ entry เดิม ไม่นับแยก)
            },

            // ── QT1 · ควิซ #Tritium (ใหม่ v8) — เด้งตอนป้อน Tritium เข้าเตาครั้งแรก (~Day 21+) ──
            new QuizDef
            {
                id = "QT1", category = QuizCategory.Reactor, speaker = "VESTA",
                topicTitle = "เชื้อเพลิงคู่ D–T",
                question = "เราเพิ่งป้อนทริเทียมเข้าเตาคู่กับดิวเทอเรียม ทำไมต้องใช้เชื้อเพลิง \"คู่ D–T\" ถึงจะดันเตาถึงจุดติดเต็มร้อย?",
                options = new[]
                {
                    "เพราะทริเทียมถูกกว่าดิวเทอเรียม ประหยัดงบเมือง",
                    "เพราะคู่ D–T หลอมรวมได้ง่ายที่สุด จุดติดที่อุณหภูมิต่ำกว่าเชื้อเพลิงคู่อื่น",
                    "เพราะทริเทียมทำให้เตาเย็นลง ไม่ต้องหล่อเย็นอีก",
                },
                correctIndex = 1,
                explainText = "เชื้อเพลิงที่หลอมรวมได้ง่ายที่สุดคือคู่ดิวเทอเรียม–ทริเทียม (D–T) เพราะจุดติดที่อุณหภูมิต่ำกว่าคู่อื่น ดิวเทอเรียมจากน้ำพาเตาขึ้นมาได้ระดับหนึ่ง แต่การดันถึงจุดติดเต็มร้อยต้องมีทริเทียมป้อนคู่",
                codexUnlockId = "codex_dt_fusion_fuel",   // เชื้อเพลิงคู่ D–T (v8 #4)
            },

            // ── QT2 · ควิซ #Tritium-2 (ใหม่ v8) — เด้งต่อจาก QT1 ──
            new QuizDef
            {
                id = "QT2", category = QuizCategory.Reactor, speaker = "VESTA",
                topicTitle = "การเพาะทริเทียม",
                question = "ทริเทียมแทบไม่มีในธรรมชาติ แล้วเตาฟิวชันจริงจะเอาทริเทียมมาจากไหนได้อย่างยั่งยืน?",
                options = new[]
                {
                    "สั่งซื้อจากเมืองอื่น เพราะผลิตเองไม่ได้เลย",
                    "กลั่นจากน้ำทะเลเหมือนดิวเทอเรียม",
                    "เพาะเอง — ใช้นิวตรอนจากฟิวชันยิงใส่ลิเทียม (breeding blanket) ให้แตกตัวเป็นทริเทียม",
                },
                correctIndex = 2,
                explainText = "ทริเทียมผลิตได้ด้วยการนำนิวตรอนที่เกิดจากปฏิกิริยาฟิวชันไปยิงใส่ลิเทียม (breeding blanket) ลิเทียมจะแตกตัวให้ทริเทียม — เตาฟิวชันจึงผลิตเชื้อเพลิงส่วนหนึ่งของตัวเองได้",
                codexUnlockId = "codex_tritium_breeding",   // การเพาะทริเทียม (v8 #5)
            },
        };

        private struct QuizDef
        {
            public string id;
            public QuizCategory category;
            public string speaker;
            public string topicTitle;   // หัวข้อสั้น โชว์มุมซ้ายบนของ popup ควิซ
            public string question;
            public string[] options;
            public int correctIndex;
            public string explainText;
            public string codexUnlockId;
        }
    }
}
