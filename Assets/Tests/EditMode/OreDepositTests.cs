using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// ระบบแหล่งแร่ (OreDepositManager + OreMath — V4 §5, งานขุดมีเวลา):
    /// - งานขุด: สะสม work = คน × วินาที · ครบ baseMineSeconds → ได้แร่เต็มก้อน โหนดหาย (เวลา = base ÷ คน)
    /// - โซน B ตอนขุดเสร็จ: รังสีสะสม +ค่า×คน + สุ่มป่วย · Q5 (ALARA) เด้งครั้งแรกครั้งเดียว
    /// - วันใหม่: ถอนโหนดเก่าทั้งหมดแล้วสุ่มใหม่ · PickPositionsInRect: อยู่ในกรอบโซน + เว้นระยะ + ไม่ค้าง
    /// </summary>
    public class OreDepositTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private BuildingRegistry registry;
        private ResourceManager resources;
        private PopulationManager population;
        private WorkerAssignmentManager assignment;
        private RadiationManager radiation;
        private OreDepositManager ore;
        private QuizManager quiz;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
            registry = NewComponent<BuildingRegistry>("BuildingRegistry");
            registry.allBuildingData = new BuildingData[0];
            resources = NewComponent<ResourceManager>("ResourceManager");
            population = NewComponent<PopulationManager>("PopulationManager");
            assignment = NewComponent<WorkerAssignmentManager>("WorkerAssignmentManager");
            radiation = NewComponent<RadiationManager>("RadiationManager");
            ore = NewComponent<OreDepositManager>("OreDepositManager"); // ไม่เรียก Start → ไม่ scatter (grid ไม่มีในเทสต์)
            quiz = NewComponent<QuizManager>("QuizManager");
            quiz.allQuizzes = new QuizQuestionSO[0];
            InvokePrivate(resources, "Start");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        // ─────────────── OreMath (pure — ไม่ต้องมี scene) ───────────────

        [Test]
        public void RollQuota_WithinBounds_ManySeeds()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                float q = OreMath.RollQuota(15f, 35f, new System.Random(seed));
                Assert.GreaterOrEqual(q, 15f, $"seed {seed}: โควตาต่ำกว่าขั้นต่ำ");
                Assert.LessOrEqual(q, 35f, $"seed {seed}: โควตาเกินเพดาน");
                Assert.AreEqual(q, Mathf.Round(q), 1e-4f, $"seed {seed}: ต้องปัดจำนวนเต็ม");
            }
        }

        [Test]
        public void RollQuota_MinEqualsMax_ReturnsMin()
        {
            Assert.AreEqual(60f, OreMath.RollQuota(60f, 60f, new System.Random(1)), 1e-4f);
            Assert.AreEqual(0f, OreMath.RollQuota(-5f, -1f, new System.Random(1)), 1e-4f, "ค่าติดลบ → clamp 0");
        }

        [Test]
        public void SickCount_ChanceZero_IsZero()
        {
            Assert.AreEqual(0, OreMath.SickCount(10, 0f, new System.Random(1)));
            Assert.AreEqual(0, OreMath.SickCount(0, 0.5f, new System.Random(1)), "ไม่มีคน → ไม่มีป่วย");
        }

        [Test]
        public void SickCount_ChanceOne_EqualsWorkers()
        {
            Assert.AreEqual(7, OreMath.SickCount(7, 1f, new System.Random(1)));
        }

        [Test]
        public void SickCount_Seeded_Reproducible()
        {
            int a = OreMath.SickCount(100, 0.3f, new System.Random(42));
            int b = OreMath.SickCount(100, 0.3f, new System.Random(42));
            Assert.AreEqual(a, b, "seed เดียวกันต้องได้ผลเท่ากัน (เทสต์ balance ได้)");
            Assert.Greater(a, 0, "chance 30% × 100 คน — ควรมีป่วยบ้าง");
            Assert.Less(a, 100, "ไม่ควรป่วยหมดทุกคน");
        }

        [Test]
        public void PickPositionsInRect_AllInZoneB_SpacedAndFree()
        {
            // โซน B บนกริด 43×28 = คอลัมน์ 29..42 × แถว 0..27 (14×28)
            var occupied = new HashSet<Vector2Int> { new Vector2Int(32, 10), new Vector2Int(38, 20) };
            var picked = OreMath.PickPositionsInRect(3, 29, 42, 0, 27, 3,
                pos => !occupied.Contains(pos), new System.Random(42));

            Assert.AreEqual(3, picked.Count, "พื้นที่โซน B (14×28) กว้างพอ — ต้องวางครบ 3 จุด");
            for (int i = 0; i < picked.Count; i++)
            {
                Assert.GreaterOrEqual(picked[i].x, 29, $"จุด {picked[i]} หลุดเข้าโซน A");
                Assert.LessOrEqual(picked[i].x, 42, $"จุด {picked[i]} หลุดขอบกริด");
                Assert.GreaterOrEqual(picked[i].y, 0, $"จุด {picked[i]} หลุดขอบล่าง");
                Assert.LessOrEqual(picked[i].y, 27, $"จุด {picked[i]} หลุดขอบบน");
                Assert.IsFalse(occupied.Contains(picked[i]), $"จุด {picked[i]} ทับช่องไม่ว่าง");

                for (int j = i + 1; j < picked.Count; j++)
                {
                    int gap = Mathf.Max(Mathf.Abs(picked[i].x - picked[j].x), Mathf.Abs(picked[i].y - picked[j].y));
                    Assert.GreaterOrEqual(gap, 3, $"จุด {picked[i]} กับ {picked[j]} ชิดกันเกิน");
                }
            }
        }

        [Test]
        public void PickPositionsInRect_NoSpace_ReturnsFewerWithoutHanging()
        {
            var picked = OreMath.PickPositionsInRect(5, 29, 42, 0, 27, 3,
                _ => false, new System.Random(1), maxAttempts: 50);
            Assert.AreEqual(0, picked.Count, "ไม่มีช่องว่างเลย → คืนว่าง (ไม่ throw/ไม่ค้าง)");

            var inverted = OreMath.PickPositionsInRect(3, 10, 5, 0, 27, 3,
                _ => true, new System.Random(1), maxAttempts: 50);
            Assert.AreEqual(0, inverted.Count, "กรอบกลับด้าน (xMax < xMin) → คืนว่าง");
        }

        // ─────────────── งานขุดมีเวลา (ผ่าน AdvanceMining) ───────────────

        [Test]
        public void Mining_CompletesWhenWorkReachesBase_GrantsFullPayload()
        {
            // payload เหล็ก 60 · baseMineSeconds 60 · 2 คน × 30 วิ = 60 worker-sec → เสร็จ · ได้เต็มก้อน (คนไม่หารแร่)
            SetResources(energy: 100f, water: 100f, workers: 20);
            ore.baseMineSeconds = 60f;
            Place(MakeOreNode("OreA", workerRequired: 3, quotaMin: 60f, quotaMax: 60f), 1, 1);
            Assign(1, 1, 2);

            ore.AdvanceMining(30f);

            Assert.AreEqual(60f, resources.Current.iron, 1e-3f, "ขุดเสร็จ → ได้แร่เต็มก้อน (จำนวนคงที่ ไม่ขึ้นกับจำนวนคน)");
            Assert.IsFalse(registry.PlacedBuildings.ContainsKey(new Vector2Int(1, 1)), "ขุดเสร็จ → โหนดหายจากแมพ");
        }

        [Test]
        public void Mining_MoreWorkers_FasterCompletion()
        {
            // เวลา = base ÷ คน — 3 คนเสร็จใน 20 วิ · 1 คนยังไม่เสร็จใน 20 วิ (แปรผกผัน)
            SetResources(energy: 100f, water: 100f, workers: 20);
            ore.baseMineSeconds = 60f;
            Place(MakeOreNode("Fast", workerRequired: 3, quotaMin: 60f, quotaMax: 60f), 1, 1);
            Place(MakeOreNode("Slow", workerRequired: 3, quotaMin: 60f, quotaMax: 60f), 5, 5);
            Assign(1, 1, 3);
            Assign(5, 5, 1);

            ore.AdvanceMining(20f);

            Assert.IsFalse(registry.PlacedBuildings.ContainsKey(new Vector2Int(1, 1)), "3 คน × 20 = 60 → เสร็จ");
            Assert.IsTrue(registry.PlacedBuildings.ContainsKey(new Vector2Int(5, 5)), "1 คน × 20 = 20 < 60 → ยังไม่เสร็จ");
            Assert.AreEqual(60f, resources.Current.iron, 1e-3f, "ได้แร่จากโหนดที่เสร็จก้อนเดียว");
        }

        [Test]
        public void Mining_InsufficientTime_KeepsNodeAndProgress()
        {
            SetResources(energy: 100f, water: 100f, workers: 20);
            ore.baseMineSeconds = 60f;
            Place(MakeOreNode("OreA", workerRequired: 3, quotaMin: 60f, quotaMax: 60f), 1, 1);
            Assign(1, 1, 1);

            ore.AdvanceMining(30f); // 1 คน × 30 = 30 < 60

            Assert.AreEqual(0f, resources.Current.iron, 1e-4f, "ยังไม่เสร็จ → ยังไม่ได้แร่");
            Assert.IsTrue(registry.PlacedBuildings.ContainsKey(new Vector2Int(1, 1)), "ยังไม่เสร็จ → โหนดยังอยู่");
            Assert.AreEqual(0.5f, ore.GetMineProgress01(new Vector2Int(1, 1)), 1e-3f, "ความคืบหน้า 30/60 = 50%");
        }

        [Test]
        public void Mining_Unstaffed_NoProgress()
        {
            SetResources(energy: 100f, water: 100f, workers: 20);
            ore.baseMineSeconds = 60f;
            Place(MakeOreNode("OreA", workerRequired: 3, quotaMin: 30f, quotaMax: 30f), 1, 1);
            // ไม่ Assign

            ore.AdvanceMining(120f);

            Assert.AreEqual(0f, resources.Current.iron, 1e-4f, "ไม่มีคน → งานไม่เดิน ไม่ได้แร่");
            Assert.IsTrue(registry.PlacedBuildings.ContainsKey(new Vector2Int(1, 1)), "ไม่มีคน → โหนดยังอยู่");
        }

        // ─────────────── โซน B: รังสี + สุ่มป่วย ตอนขุดเสร็จ ───────────────

        [Test]
        public void ZoneB_MiningComplete_YieldsTritiumExposureAndSick()
        {
            // โซน B: เหล็ก 60 + Tritium 6 · exposure 1 · sickChance 1 · 3 คนเสร็จ → รังสี +3, ป่วย 3
            SetResources(energy: 100f, water: 100f, food: 100f, workers: 20);
            ore.baseMineSeconds = 60f;
            Place(MakeOreNode("OreB", workerRequired: 3, quotaMin: 60f, quotaMax: 60f,
                exposure: 1f, sickChance: 1f, tritMin: 6f, tritMax: 6f), 1, 1);
            Assign(1, 1, 3);

            ore.AdvanceMining(20f); // 3 คน × 20 = 60 → เสร็จ

            Assert.AreEqual(60f, resources.Current.iron, 1e-3f, "เหล็กเต็มก้อน");
            Assert.AreEqual(6f, resources.Current.tritium, 1e-3f, "Tritium เต็มก้อน (โซน B)");
            Assert.AreEqual(3f, radiation.CurrentExposure, 1e-3f, "รังสี = 1 × 3 คน ตอนขุดเสร็จ");
            Assert.AreEqual(3, population.Current.sick, "chance 100% × 3 คน → ป่วย 3");
        }

        [Test]
        public void ZoneA_MiningComplete_NoTritiumNoExposure()
        {
            SetResources(energy: 100f, water: 100f, food: 100f, workers: 20);
            ore.baseMineSeconds = 60f;
            Place(MakeOreNode("OreA", workerRequired: 2, quotaMin: 30f, quotaMax: 30f), 1, 1);
            Assign(1, 1, 2);

            ore.AdvanceMining(30f); // 2 คน × 30 = 60 → เสร็จ

            Assert.AreEqual(30f, resources.Current.iron, 1e-3f);
            Assert.AreEqual(0f, resources.Current.tritium, 1e-4f, "โซน A ไม่ให้ Tritium");
            Assert.AreEqual(0f, radiation.CurrentExposure, 1e-4f, "โซน A ปลอดภัย — ไม่เพิ่มรังสี");
        }

        [Test]
        public void NewDay_RemovesExistingNodes()
        {
            // ขึ้นวันใหม่ = ถอนโหนดเก่าทั้งหมด (แล้ว scatter ใหม่ — ตำแหน่งใหม่ต้องมี grid จริง เทสต์นี้เช็คการถอน)
            SetResources(energy: 100f, water: 100f, workers: 20);
            Place(MakeOreNode("OreA", workerRequired: 2, quotaMin: 30f, quotaMax: 30f), 1, 1);
            Assert.IsTrue(registry.PlacedBuildings.ContainsKey(new Vector2Int(1, 1)));

            eventManager.RaiseDayStarted(2, true);

            Assert.IsFalse(registry.PlacedBuildings.ContainsKey(new Vector2Int(1, 1)),
                "วันใหม่ → โหนดเก่าถูกถอน (ขุดอีกต้องรอ scatter รอบใหม่)");
        }

        // ─────────────── Q5 (ALARA) — เด้งครั้งแรกครั้งเดียว ───────────────

        [Test]
        public void Q5_FirstZoneBAssignment_ShowsOnce()
        {
            var q5 = ScriptableObject.CreateInstance<QuizQuestionSO>();
            q5.id = "Q5";
            _spawned.Add(q5);
            quiz.allQuizzes = new[] { q5 };

            int shown = 0;
            eventManager.OnQuizShown += _ => shown++;

            SetResources(energy: 100f, water: 100f, workers: 20);
            Place(MakeOreNode("OreB1", workerRequired: 3, quotaMin: 60f, quotaMax: 60f, exposure: 1f), 1, 1);
            Place(MakeOreNode("OreB2", workerRequired: 3, quotaMin: 60f, quotaMax: 60f, exposure: 1f), 2, 2);

            Assign(1, 1, 1);
            InvokePrivate(ore, "Update"); // Q5 หน่วง 1 เฟรม (กันเด้งตอนโหลดเซฟ) — เทสต์เดินเฟรมเอง
            Assert.AreEqual(1, shown, "จ่ายคนเข้าโซน B ครั้งแรก → Q5 เด้ง");

            Assign(2, 2, 1); // โหนด B อีกแห่ง
            InvokePrivate(ore, "Update");
            Assert.AreEqual(1, shown, "Q5 เด้งครั้งเดียว — ครั้งต่อไปไม่ซ้ำ");
        }

        [Test]
        public void Q5_ZoneAAssignment_DoesNotShow()
        {
            var q5 = ScriptableObject.CreateInstance<QuizQuestionSO>();
            q5.id = "Q5";
            _spawned.Add(q5);
            quiz.allQuizzes = new[] { q5 };

            int shown = 0;
            eventManager.OnQuizShown += _ => shown++;

            SetResources(energy: 100f, water: 100f, workers: 20);
            Place(MakeOreNode("OreA", workerRequired: 2, quotaMin: 30f, quotaMax: 30f), 1, 1);

            Assign(1, 1, 1);
            InvokePrivate(ore, "Update");
            Assert.AreEqual(0, shown, "โซน A ปลอดภัย — ไม่ต้องสอน ALARA");
        }

        // ─────────────── save/load — payload rebuild จาก registry ───────────────

        [Test]
        public void SaveLoaded_RollsPayloadFromRegistry()
        {
            var nodeData = MakeOreNode("แหล่งแร่เหล็ก (โซน A)", workerRequired: 2, quotaMin: 25f, quotaMax: 25f);
            registry.allBuildingData = new[] { nodeData };

            var save = new SaveData
            {
                resources = new ResourceData { energy = 100f, water = 100f },
                population = new PopulationData { workers = 10, hope = 100f },
                placedBuildings = new List<Vector2Int> { new Vector2Int(3, 3) },
                buildingTypes = new List<string> { "แหล่งแร่เหล็ก (โซน A)" },
            };
            eventManager.RaiseSaveLoaded(save);

            Assert.AreEqual(25f, ore.GetIronPayload(new Vector2Int(3, 3)), 1e-3f,
                "โหลดเซฟแล้วโหนดใน registry ต้องได้ payload ใหม่ (สุ่มในช่วง — min==max deterministic)");
        }

        // ─────────────── helpers (idiom เดียวกับ ResourceManagerTests) ───────────────

        private void SetResources(float energy, float water, float food = 0f, int workers = 20)
        {
            var save = new SaveData
            {
                resources = new ResourceData { energy = energy, water = water, food = food },
                population = new PopulationData { workers = workers, hope = 100f }
            };
            eventManager.RaiseSaveLoaded(save);
        }

        private BuildingData MakeOreNode(string name, int workerRequired, float quotaMin, float quotaMax,
            float exposure = 0f, float sickChance = 0f, float tritMin = 0f, float tritMax = 0f)
        {
            var data = ScriptableObject.CreateInstance<BuildingData>();
            data.buildingName = name;
            data.size = Vector2Int.one;
            data.buildingType = BuildingType.OreDeposit;
            data.isOreNode = true;
            data.workerRequired = workerRequired;
            data.oreQuotaMin = quotaMin;
            data.oreQuotaMax = quotaMax;
            data.oreTritiumMin = tritMin;
            data.oreTritiumMax = tritMax;
            data.oreExposurePerWorkerDay = exposure;
            data.oreSickChancePerWorkerDay = sickChance;
            _spawned.Add(data);
            return data;
        }

        private void Place(BuildingData data, int col, int row)
        {
            eventManager.RaiseBuildingPlaced(new Cell(col, row), data);
        }

        private void Assign(int col, int row, int n)
        {
            eventManager.RaiseWorkerAssignRequested(new Vector2Int(col, row), n);
        }

        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var component = go.AddComponent<T>();
            TryInvokePrivate(component, "Awake");
            TryInvokePrivate(component, "OnEnable");
            return component;
        }

        private static void TryInvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            try { method?.Invoke(target, null); }
            catch (TargetInvocationException) { }
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method, $"ไม่พบ method '{methodName}' บน {target.GetType().Name}");
            method.Invoke(target, null);
        }
    }
}
