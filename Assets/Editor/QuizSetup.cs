using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// เฟส 1 (T1.A3 / V4 §12) — สร้าง 10 QuizQuestionSO assets จากเนื้อหาควิซฉบับเต็ม
    /// + wire เข้า QuizManager.allQuizzes + ผูก linkedQuizIds ให้ crisis dilemma
    /// รันผ่านเมนู NuclearReMind / Setup Quiz System
    ///
    /// สีตามหมวด (§17): Q1,2,3,8,9 = Reactor · Q6,7 = Agriculture · Q4 = Medical · Q5,10 = Ethics
    ///
    /// 🔓 ตาราง codexUnlockId (map "codexName" ในสคริปต์ → CodexEntry.entryId ที่มีจริงบนดิสก์):
    ///   Q4 "Nuclear Medicine"  → "med_pet_scan"           (ดูหมายเหตุ Q4 ด้านล่าง — เปลี่ยนได้)
    ///   Q5 "ALARA"             → "env_alara"
    ///   Q6 "Mutation Breeding" → "agri_mutation_breeding"
    ///   Q7 "Food Irradiation"  → "agri_irradiation"
    ///   Q10 "ALARA"            → "env_alara"
    ///   Q1 "Deuterium"          → "fusion_deuterium"      ✅ (Gap G1 ปิดแล้ว — เฟส 2)
    ///   Q2 "Plasma Confinement" → "fusion_plasma"
    ///   Q3 "Magnetic Confinement"→ "fusion_magnetic"
    ///   Q8 "Nuclear Fusion"     → "fusion_reaction"
    ///   Q9 "Clean Energy"       → "fusion_clean_energy"
    /// (5 entry สาขา Fusion สร้างโดย CodexSetup.BuildEntryDefs — รัน Setup Codex System)
    /// หมายเหตุ Q4: ไม่มี CodexEntry ชื่อ "Nuclear Medicine" ตรง ๆ (ถูกแตกเป็น PET/SPECT/TRT)
    ///   เลือก "med_pet_scan" เป็นตัวแทน (เวชศาสตร์นิวเคลียร์ระดับเริ่มต้น "หาก่อน แล้วค่อยยิง")
    ///   ทางเลือกอื่นที่รับได้: "med_spect_scan" (ผูกวิกฤต Outbreak) หรือ "med_radionuclide_therapy"
    ///   (ยาเฉพาะจุด/targeted therapy) — reviewer ปรับได้จากตารางด้านบน
    /// </summary>
    public static class QuizSetup
    {
        private const string Folder        = "Assets/ScriptableObjects/Quizzes";
        private const string DilemmaFolder = "Assets/ScriptableObjects/Dilemmas";
        private const string ScenePath     = "Assets/Scenes/Gamescene.unity";

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
        //  เนื้อหาควิซ 10 ข้อ (V4 §12 — verbatim)
        // ─────────────────────────────────────────────
        private static QuizDef[] BuildDefs() => new[]
        {
            // ── Q1 · เชื้อเพลิงแรก: ดิวเทอเรียม (Reactor · VESTA) ──
            new QuizDef
            {
                id = "Q1", category = QuizCategory.Reactor, speaker = "VESTA",
                question = "ทำไมเราจึงสกัดเชื้อเพลิงตัวแรกได้จาก \"น้ำ\"?",
                options = new[]
                {
                    "เพราะน้ำเป็นสารกัมมันตรังสีที่ปลอดภัยที่สุด",
                    "เพราะน้ำมีดิวเทอเรียมอยู่แล้ว ไม่ต้องสร้างใหม่ จึงมีเชื้อเพลิงฟิวชันป้อนเตาได้ต่อเนื่อง",
                    "เพราะน้ำช่วยดับไฟในเตาไม่ให้ร้อน",
                },
                correctIndex = 1,
                explainText = "ดิวเทอเรียม (²H) เป็นไอโซโทปของไฮโดรเจนที่มีปนอยู่ในน้ำทั่วไปอยู่แล้ว เราจึงแยกออกมาใช้เป็นเชื้อเพลิงฟิวชันได้เลยโดยไม่ต้องผลิตขึ้นใหม่",
                codexUnlockId = "fusion_deuterium",   // Deuterium (ปิด Gap G1)
            },

            // ── Q2 · ทำไมพลาสมาถึงพัง (Reactor · Dr. Auren Vasek) ──
            new QuizDef
            {
                id = "Q2", category = QuizCategory.Reactor, speaker = "Dr. Auren Vasek",
                question = "อะไรคือสาเหตุที่ทำให้เตาเสี่ยงหลอมละลาย (Meltdown) เมื่อพลาสมาไม่เสถียร?",
                options = new[]
                {
                    "พลาสมาที่ร้อนหลายล้านองศาหลุดออกไปชนผนังเตา ถ่ายเทความร้อนเข้าตัวอาคาร",
                    "พลาสมาเย็นเกินไปจนเตาหยุดทำงาน",
                    "มีน้ำมากเกินไปจนเตาจม",
                },
                correctIndex = 0,
                explainText = "ในโทคาแมก พลาสมาร้อนหลายล้านองศาถูกกักไว้ด้วยสนามแม่เหล็ก ถ้าสนามไม่นิ่งจนพลาสมาหลุดไปชนผนัง จะถ่ายเทความร้อนเข้าตัวอาคารจนเสี่ยงหลอมละลาย",
                codexUnlockId = "fusion_plasma",   // Plasma Confinement (ปิด Gap G1)
            },

            // ── Q3 · สนามแม่เหล็กคู่คืออะไร (Reactor · VESTA) ──
            new QuizDef
            {
                id = "Q3", category = QuizCategory.Reactor, speaker = "VESTA",
                question = "เมื่อ HEAT ของเตาเริ่มไต่สูง ควรทำอะไรเพื่อกันไม่ให้ถึง Meltdown?",
                options = new[]
                {
                    "ป้อนอาหารเพิ่มให้คนงาน",
                    "ปิดโรงน้ำเพื่อประหยัดพลังงาน",
                    "เสริมสนามแม่เหล็ก/หล่อเย็น (Toroidal–Poloidal) เพื่อยกเพดานและรีดความร้อนออก",
                },
                correctIndex = 2,
                explainText = "สนามแม่เหล็กคู่ทำงานร่วมกัน — Toroidal บีบพลาสมาให้วิ่งเป็นวง ส่วน Poloidal กันไม่ให้พลาสมาชนผนัง เมื่อเสริมให้แข็งแรงจะกักพลาสมาไว้กลางเตาและลดความร้อนที่รั่วออก",
                codexUnlockId = "fusion_magnetic",   // Magnetic Confinement (ปิด Gap G1)
            },

            // ── Q4 · เวชศาสตร์นิวเคลียร์: หาก่อน แล้วค่อยยิง (Medical · แพทย์ประจำเมือง) ──
            new QuizDef
            {
                id = "Q4", category = QuizCategory.Medical, speaker = "แพทย์ประจำเมือง",
                question = "เวชศาสตร์นิวเคลียร์จัดการเซลล์เนื้อร้ายเป็น 2 ขั้นตอน — ข้อใดอธิบายได้ถูกต้อง?",
                options = new[]
                {
                    "PET/SPECT ยิงรังสีทำลายเนื้อร้ายทันทีตั้งแต่ตอนสแกน",
                    "PET/SPECT ใช้สารรังสี \"ถ่ายภาพ\" หาตำแหน่งเซลล์ผิดปกติก่อน จากนั้นยาเฉพาะจุดจึงส่งรังสีไป \"ทำลาย\" เป้าได้แม่นยำ กระทบเนื้อดีน้อย",
                    "รังสีฆ่าทุกเซลล์ในร่างกายเท่ากันหมด ไม่ว่าจะดีหรือร้าย",
                },
                correctIndex = 1,
                explainText = "เวชศาสตร์นิวเคลียร์ทำงาน 2 ขั้น — (1) **วินิจฉัย:** PET/SPECT ฉีดสารเภสัชรังสีเพื่อ \"ถ่ายภาพ\" หาตำแหน่งเซลล์ผิดปกติ (ยังไม่ได้รักษา) (2) **รักษา:** ยาเฉพาะจุด (targeted therapy) จึงส่งรังสีไปทำลายเฉพาะเป้า กระทบเนื้อดีรอบ ๆ น้อย — ความแม่นยำคือหัวใจของทั้งสองขั้น",
                codexUnlockId = "med_pet_scan",   // Nuclear Medicine (ดูหมายเหตุ Q4 ด้านบน)
            },

            // ── Q5 · ใครห้ามเข้าเขตรังสี (ALARA) (Ethics · VESTA) ──
            new QuizDef
            {
                id = "Q5", category = QuizCategory.Ethics, speaker = "VESTA",
                question = "ตามหลัก ALARA เมื่อต้องส่งคนเข้าพื้นที่เสี่ยงรังสี ควรจัดการอย่างไร?",
                options = new[]
                {
                    "จำกัดเวลา/ปริมาณรังสีต่อคนให้น้อยที่สุด และเลี่ยงส่งกลุ่มเปราะบาง (ผู้ป่วย/เด็ก)",
                    "ส่งใครก็ได้เข้าไปนานเท่าไรก็ได้ ถ้ามีชุดกันรังสี",
                    "ส่งผู้ป่วยเข้าไปก่อน เพราะป่วยอยู่แล้ว",
                },
                correctIndex = 0,
                explainText = "ALARA (As Low As Reasonably Achievable) คือ \"ให้คนรับรังสีน้อยที่สุดเท่าที่ทำได้\" และต้องปกป้องกลุ่มที่ไวต่อรังสีเป็นพิเศษ (ผู้ป่วย/เด็ก) ก่อนเสมอ",
                codexUnlockId = "env_alara",   // ALARA
            },

            // ── Q6 · แก้ที่ต้นเหตุ ไม่ใช่ปลายเหตุ (Agriculture · Dr. Auren Vasek) ──
            new QuizDef
            {
                id = "Q6", category = QuizCategory.Agriculture, speaker = "Dr. Auren Vasek",
                question = "การฉายรังสีใส่เมล็ดพันธุ์ช่วยแก้วิกฤตอาหารระยะยาวได้อย่างไร?",
                options = new[]
                {
                    "ทำให้พืชเรืองแสงจึงปลูกตอนกลางคืนได้",
                    "ทำให้พืชกลายเป็นกัมมันตรังสี กินแล้วแข็งแรง",
                    "กระตุ้นการกลายพันธุ์เชิงบวก คัดสายพันธุ์ที่ทนทาน/ผลผลิตสูงไว้ใช้ถาวร",
                },
                correctIndex = 2,
                explainText = "การฉายรังสีกระตุ้นให้เกิดการกลายพันธุ์ นักวิจัยคัดเลือกเฉพาะสายพันธุ์ที่ทนทานและให้ผลผลิตสูงไว้ใช้ถาวร เช่น ข้าว กข6 ของไทย — เป็นการแก้ปัญหาที่ต้นเหตุ",
                codexUnlockId = "agri_mutation_breeding",   // Mutation Breeding
            },

            // ── Q7 · หยุดอาหารเน่าด้วยรังสี (Agriculture · VESTA) ──
            new QuizDef
            {
                id = "Q7", category = QuizCategory.Agriculture, speaker = "VESTA",
                question = "การฉายรังสีแกมมาจากโคบอลต์-60 ช่วยถนอมอาหารด้วยกลไกใด?",
                options = new[]
                {
                    "รังสีเคลือบผิวอาหารด้วยโลหะกันบูด",
                    "รังสีทำลายจุลินทรีย์/เชื้อราในอาหาร ชะลอการเน่า โดยอาหารไม่กลายเป็นสารรังสี",
                    "รังสีทำให้อาหารแช่แข็งตลอดเวลา",
                },
                correctIndex = 1,
                explainText = "รังสีแกมมาทะลุผ่านอาหารและฆ่าจุลินทรีย์/เชื้อรา ทำให้เก็บได้นานขึ้น โดยอาหารไม่กลายเป็นสารกัมมันตรังสี (อาหารฉายรังสี ≠ อาหารมีรังสี)",
                codexUnlockId = "agri_irradiation",   // Food Irradiation
            },

            // ── Q8 · ฟิวชันคืออะไร (Reactor · VESTA) ──
            new QuizDef
            {
                id = "Q8", category = QuizCategory.Reactor, speaker = "VESTA",
                question = "\"ฟิวชันนิวเคลียร์\" ที่เพิ่งจุดติดในเตา — แท้จริงคืออะไร?",
                options = new[]
                {
                    "การหลอมรวมนิวเคลียสเบาเข้าด้วยกัน (ไฮโดรเจน + ไฮโดรเจน) ให้เป็นธาตุที่หนักกว่า แล้วปล่อยพลังงานมหาศาล",
                    "การแตกตัวของนิวเคลียสหนักออกเป็นชิ้นเล็ก เหมือนเครื่องปฏิกรณ์ฟิชชัน",
                    "การเผาไหม้ถ่านหินด้วยความร้อนสูง",
                },
                correctIndex = 0,
                explainText = "ฟิวชันคือการหลอมรวมนิวเคลียสเบา (เช่น ไฮโดรเจน) ให้กลายเป็นธาตุที่หนักกว่า แล้วปลดปล่อยพลังงานมหาศาล — ตรงข้ามกับฟิชชันที่เป็นการ \"แตกตัว\" ของนิวเคลียสหนัก",
                codexUnlockId = "fusion_reaction",   // Nuclear Fusion (ปิด Gap G1)
            },

            // ── Q9 · ทำไมฟิวชันถึงสะอาด (Reactor · Dr. Auren Vasek) ──
            new QuizDef
            {
                id = "Q9", category = QuizCategory.Reactor, speaker = "Dr. Auren Vasek",
                question = "ข้อใดอธิบายได้ถูกต้องว่าทำไมพลังงานฟิวชันถึงสะอาดกว่าทางเลือกอื่น?",
                options = new[]
                {
                    "เพราะมันไม่เกี่ยวข้องกับรังสีหรือนิวเคลียร์เลย บริสุทธิ์ 100%",
                    "เพราะมันเผาถ่านหินที่สะอาดเป็นพิเศษ",
                    "เชื้อเพลิงมาจากน้ำทะเล ไม่ปล่อย CO₂ ไม่มีปฏิกิริยาลูกโซ่ที่คุมไม่ได้ และไม่มีกากรังสีอายุยืนแบบฟิชชัน",
                },
                correctIndex = 2,
                explainText = "ฟิวชันสะอาดกว่าเพราะเชื้อเพลิงหาได้จากน้ำ ไม่ปล่อย CO₂ ถ้าเสียสมดุลเตาจะดับเอง (ไม่ระเบิด) และไม่มีกากรังสีอายุยืนแบบฟิชชัน — แต่ \"สะอาดกว่า\" ไม่ได้แปลว่า \"ไม่มีรังสีเลย\" เพราะเชื้อเพลิง D-T ยังปล่อยนิวตรอน",
                codexUnlockId = "fusion_clean_energy",   // Clean Energy (ปิด Gap G1)
            },

            // ── Q10 · จริยธรรม (Decree) (Ethics · Dr. Auren Vasek) ──
            new QuizDef
            {
                id = "Q10", category = QuizCategory.Ethics, speaker = "Dr. Auren Vasek",
                question = "การเกณฑ์แรงงานฉุกเฉินให้คนเข้าทำงานในเขตเสี่ยงรังสีขณะวิกฤต — กับดักทางจริยธรรมอยู่ตรงไหน?",
                options = new[]
                {
                    "ไม่มีปัญหาอะไร เพราะคนกลุ่มนี้แค่ทำงานช้ากว่าคนทั่วไป",
                    "กลุ่มเปราะบางไวต่อรังสีเป็นพิเศษ การบังคับให้เข้าไปเสี่ยงจึงขัดหลัก ALARA โดยตรง",
                    "ไม่ขัดอะไรเลย ถ้าทุกคนในเมืองตกลงร่วมมือกัน",
                },
                correctIndex = 1,
                explainText = "การส่งผู้ป่วย/เด็กเข้าเขตรังสีขัดหลัก ALARA เพราะคนกลุ่มนี้ไวต่อรังสีเป็นพิเศษ การตัดสินใจนี้เป็นของผู้เล่น เกมไม่ตัดสินถูก-ผิดแทน แต่ผลของมันคือ Hope และแรงงานในรอบนั้น",
                codexUnlockId = "env_alara",   // ALARA
            },
        };

        private struct QuizDef
        {
            public string id;
            public QuizCategory category;
            public string speaker;
            public string question;
            public string[] options;
            public int correctIndex;
            public string explainText;
            public string codexUnlockId;
        }
    }
}
