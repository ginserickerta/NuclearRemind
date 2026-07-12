using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// ตรวจ "เนื้อหา" เนื้อเรื่องบนดิสก์ (asset ที่ StorySetup สร้าง) ตาม checklist §6 ของ Story Guide:
    /// - ทุก beat ที่มีควิซ (ของ beat เอง หรือผ่าน crisis) ต้องมี infoCard/record นำก่อน — กฎเหล็ก
    /// - beatId ไม่ซ้ำ · trigger param ของ OnDay parse เป็นตัวเลขได้
    /// - วิกฤตอาหาร: ควิซรายทางเลือก A=Q6/B=Q7/C=ไม่มี (linkedQuizIds ต้องว่าง — กัน C fallback)
    /// ยังไม่ได้รัน Setup Story Content → Ignore (ไม่ใช่ fail — content เกิดจาก editor menu)
    /// </summary>
    public class StoryContentTests
    {
        private const string StoryFolder = "Assets/ScriptableObjects/Story";

        private static StoryBeatSO[] LoadBeats()
        {
            if (!AssetDatabase.IsValidFolder(StoryFolder))
                return null;

            var beats = new List<StoryBeatSO>();
            foreach (var guid in AssetDatabase.FindAssets("t:StoryBeatSO", new[] { StoryFolder }))
            {
                var beat = AssetDatabase.LoadAssetAtPath<StoryBeatSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (beat != null) beats.Add(beat);
            }
            return beats.ToArray();
        }

        private static StoryBeatSO[] RequireBeats()
        {
            var beats = LoadBeats();
            if (beats == null || beats.Length == 0)
                Assert.Ignore("ยังไม่ได้รัน NuclearReMind → Setup Story Content — ข้ามการตรวจเนื้อหา");
            return beats;
        }

        private static bool HasAnyQuiz(StoryBeatSO beat)
        {
            if (beat.quiz != null && beat.quiz.Length > 0) return true;
            if (beat.crisis == null) return false;

            bool NonEmpty(string[] ids) => ids != null && ids.Length > 0;
            return NonEmpty(beat.crisis.linkedQuizIds)
                || NonEmpty(beat.crisis.choiceA_QuizIds)
                || NonEmpty(beat.crisis.choiceB_QuizIds)
                || NonEmpty(beat.crisis.choiceC_QuizIds);
        }

        [Test]
        public void IronRule_EveryQuizBeat_HasInfoCardOrRecordBeforeQuiz()
        {
            foreach (var beat in RequireBeats())
            {
                if (!HasAnyQuiz(beat)) continue;
                Assert.IsTrue(beat.infoCard != null || beat.record != null,
                    $"beat '{beat.beatId}' มีควิซแต่ไม่มี infoCard/record นำ — ผิดกฎเหล็ก Story Guide (ความรู้มาก่อนควิซ)");
            }
        }

        [Test]
        public void BeatIds_UniqueAndNonEmpty()
        {
            var seen = new HashSet<string>();
            foreach (var beat in RequireBeats())
            {
                Assert.IsFalse(string.IsNullOrEmpty(beat.beatId), $"asset {beat.name} ไม่มี beatId");
                Assert.IsTrue(seen.Add(beat.beatId), $"beatId ซ้ำ: {beat.beatId}");
            }
        }

        [Test]
        public void OnDayBeats_HaveParseableDayParam()
        {
            foreach (var beat in RequireBeats())
            {
                if (beat.triggerType != StoryTriggerType.OnDay) continue;
                Assert.IsTrue(int.TryParse(beat.triggerParam, out int day) && day >= 1 && day <= 30,
                    $"beat '{beat.beatId}' OnDay param '{beat.triggerParam}' ต้องเป็นวัน 1–30");
            }
        }

        [Test]
        public void CrisisBeats_TriggerParams_AreValidStatConditions()
        {
            // เงื่อนไขที่รู้จักต้องประเมินได้ (เทียบ snapshot late-game deep crisis ที่เข้าเกณฑ์ต้องเป็น true)
            // core=85 ครอบคลุม beat v8.5 ที่ผูก CORE% ล้วน (tritium_warning core_above_78 / tritium_teach core_above_80)
            var resources = new ResourceData { food = 600f, energy = 50f, water = 50f };
            var tower = new TowerData { coreHeat = 90f, corePercent = 85f };

            foreach (var beat in RequireBeats())
            {
                if (beat.triggerType != StoryTriggerType.OnStatThreshold) continue;
                if (beat.beatId == "decree_emergency") continue; // param เฟส 5 — หลับโดยตั้งใจ

                Assert.IsTrue(StatCondition.Matches(beat.triggerParam, 25, resources, tower),
                    $"beat '{beat.beatId}' param '{beat.triggerParam}' ประเมินไม่ได้ด้วย StatCondition (สะกดผิด?)");
            }
        }

        [Test]
        public void StoryDrivenCrises_HaveEmptyTriggerCondition_ToAvoidDoubleFire()
        {
            // วิกฤต story-driven ยิงผ่าน StoryBeat (OnDilemmaTriggerRequested) เท่านั้น
            // triggerCondition ต้องว่าง — ถ้าเผลอเข้า DilemmaManager.dilemmaPool ทั้งที่ยังมี condition
            // จะยิงซ้ำสองทาง (double-fire) · ตัวจริงอยู่ที่ StoryBeatSO.triggerParam
            string[] storyDrivenIds = { "Crisis_PlasmaInstability", "Crisis_MalignantOutbreak", "Crisis_FoodShortage" };

            foreach (var id in storyDrivenIds)
            {
                var crisis = AssetDatabase.LoadAssetAtPath<DilemmaData>(
                    $"Assets/ScriptableObjects/Dilemmas/{id}.asset");
                if (crisis == null)
                    Assert.Ignore("ยังไม่ได้รัน Setup Crisis Dilemmas — ข้ามการตรวจ");

                Assert.IsTrue(string.IsNullOrEmpty(crisis.triggerCondition),
                    $"{id} เป็น story-driven ต้องไม่มี triggerCondition (ยิงผ่าน StoryBeat เท่านั้น — กันหลุดเข้า pool แล้วยิงซ้ำ)");
            }
        }

        [Test]
        public void FoodCrisis_PerChoiceQuizzes_NoFallbackForChoiceC()
        {
            var food = AssetDatabase.LoadAssetAtPath<DilemmaData>(
                "Assets/ScriptableObjects/Dilemmas/Crisis_FoodShortage.asset");
            if (food == null)
                Assert.Ignore("ยังไม่ได้รัน Setup Crisis Dilemmas + Setup Quiz System");

            CollectionAssert.AreEqual(new[] { "Q6" }, food.GetQuizIdsForChoice(0), "A → mutation_breeding");
            CollectionAssert.AreEqual(new[] { "Q7" }, food.GetQuizIdsForChoice(1), "B → food_irradiation");
            Assert.IsEmpty(food.GetQuizIdsForChoice(2) ?? new string[0],
                "C ไม่ให้ความรู้ใหม่ ต้องไม่มีควิซ (linkedQuizIds ต้องว่าง — กัน fallback เด้ง Q6,Q7)");
        }

        [Test]
        public void DeferredKeys_UsedByCrises_HaveMatchingDeferredBeats()
        {
            var beats = RequireBeats();

            var beatKeys = new HashSet<string>();
            foreach (var beat in beats)
                if (beat.triggerType == StoryTriggerType.OnDeferredCrisis && !string.IsNullOrEmpty(beat.triggerParam))
                    beatKeys.Add(beat.triggerParam);

            foreach (var guid in AssetDatabase.FindAssets("t:DilemmaData", new[] { "Assets/ScriptableObjects/Dilemmas" }))
            {
                var d = AssetDatabase.LoadAssetAtPath<DilemmaData>(AssetDatabase.GUIDToAssetPath(guid));
                if (d == null) continue;

                for (int choice = 0; choice < 3; choice++)
                {
                    string key = d.GetDeferredCrisis(choice);
                    if (string.IsNullOrEmpty(key)) continue;
                    Assert.IsTrue(beatKeys.Contains(key),
                        $"{d.dilemmaId} ทางเลือก {(char)('A' + choice)} ตั้ง deferredCrisis '{key}' " +
                        "แต่ไม่มี beat OnDeferredCrisis ที่ param ตรงกัน — วิกฤตซ้อนจะหายเงียบ");
                }
            }
        }

        // v8.5 · knowledge-before-quiz ของ Tritium: QT1/QT2 ยิงจาก CoreTowerManager ตอนป้อน Tritium (นอก beat)
        // จึงไม่โดน IronRule — ล็อกด้วยเทสต์นี้ว่า Info_tritium เด้งก่อน (beat tritium_teach ที่ CORE 80%)
        // + tritium UI ล็อกถึง Day 25 (ป้อนไม่ได้ก่อน) → info (~Day 21) มาก่อนควิซเสมอ
        [Test]
        public void Tritium_TeachBeat_HasInfoCard_SoQuizHasKnowledgeFirst()
        {
            var beats = RequireBeats();
            StoryBeatSO teach = null;
            foreach (var b in beats) if (b.beatId == "tritium_teach") teach = b;

            Assert.IsNotNull(teach, "ไม่พบ beat 'tritium_teach' — v8.5 ต้องมี (รัน Setup Story Content)");
            Assert.IsNotNull(teach.infoCard, "tritium_teach ต้องมี InfoCard (Info_tritium) นำก่อนควิซ QT1/QT2");
            Assert.AreEqual("core_above_80", teach.triggerParam,
                "tritium_teach ต้อง trigger ที่ CORE 80% (info เด้งก่อนป้อน Tritium ที่ปลดล็อก Day 25)");
        }

        // v8.5 · กัน content บทสนทนาหายเงียบตอนรัน Setup Story Content ซ้ำ (Beat() ล้าง field ก่อน fill)
        [Test]
        public void V85_DialogueBeats_HaveDialogueLines()
        {
            var byId = new Dictionary<string, StoryBeatSO>();
            foreach (var b in RequireBeats()) byId[b.beatId] = b;

            string[] mustHavePre =
            {
                "reactor_preview", "crisis_plasma_stability", "crisis_radiation_disease",
                "crisis_food_spoilage", "tritium_warning", "tritium_teach", "storm_first_light",
            };
            foreach (var id in mustHavePre)
            {
                Assert.IsTrue(byId.TryGetValue(id, out var beat), $"ไม่พบ beat '{id}' (v8.5)");
                Assert.IsTrue(beat.dialoguePre != null && beat.dialoguePre.Length > 0,
                    $"beat '{id}' ต้องมี dialoguePre (v8.5) — ว่างแปลว่า Setup ล้าง content หาย");
            }

            Assert.IsTrue(byId.TryGetValue("crisis_food_spoilage", out var food)
                && food.dialoguePost != null && food.dialoguePost.Length > 0,
                "crisis_food_spoilage ต้องมี dialoguePost (บทปิดวิกฤต 3 · Dorn)");
        }

        [Test]
        public void DecreeCrisis_NotWiredIntoAnyPoolTrigger_AndQ10OnBAndCOnly()
        {
            var decree = AssetDatabase.LoadAssetAtPath<DilemmaData>(
                "Assets/ScriptableObjects/Dilemmas/Crisis_DecreeEmergency.asset");
            if (decree == null)
                Assert.Ignore("ยังไม่ได้รัน Setup Story Content");

            Assert.IsTrue(string.IsNullOrEmpty(decree.triggerCondition),
                "ประกาศฉุกเฉินต้องไม่มี triggerCondition (ยิงผ่าน StoryBeat เท่านั้น — กันหลุดเข้า pool)");
            Assert.IsEmpty(decree.GetQuizIdsForChoice(0) ?? new string[0], "A ไม่ออกประกาศ → ไม่มีควิซ");
            CollectionAssert.AreEqual(new[] { "Q10" }, decree.GetQuizIdsForChoice(1), "B → alara_price");
            CollectionAssert.AreEqual(new[] { "Q10" }, decree.GetQuizIdsForChoice(2), "C → alara_price");

            // guide effects coolingWorkers +1 — ผู้เล่นจ่าย Hope ต้อง "ได้ผล" จริง (แต้มหล่อเย็นสเกลปุ่ม ①②)
            Assert.AreEqual(0, decree.GetCoolingWorkers(0), "A ไม่ออกประกาศ → ไม่ได้หล่อเย็น");
            Assert.Greater(decree.GetCoolingWorkers(1), 0, "B ต้องได้แรงงานหล่อเย็น — ไม่งั้นเสีย Hope ฟรี");
            Assert.Greater(decree.GetCoolingWorkers(2), 0, "C ต้องได้แรงงานหล่อเย็น — ไม่งั้นเสีย Hope ฟรี");
        }
    }
}
