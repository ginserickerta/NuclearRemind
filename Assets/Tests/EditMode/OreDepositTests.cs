using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// ระบบแหล่งแร่ (OreDepositManager + OreMath — V4 §5):
    /// - โควตา/วันสุ่มในช่วง [min..max] · ผลิตเหล็ก = โควตา × กำลังคน (ไม่มี upkeep)
    /// - โซน B จบวัน: รังสีสะสม +ค่า×คน + สุ่มป่วย · Q5 (ALARA) เด้งครั้งแรกครั้งเดียว
    /// - PickPositionsInRect: อยู่ในกรอบโซน + เว้นระยะ + ช่องว่างเท่านั้น + พื้นที่ไม่พอไม่ค้าง
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

        // ─────────────── ผลิตเหล็กจากโควตา (ผ่าน ResourceManager) ───────────────

        [Test]
        public void OreProduction_PartialStaff_ScalesQuota_NoUpkeep()
        {
            // โควตา min==max=60 → roll ได้ 60 แน่นอน · จัด 2/3 คน → เหล็ก +40 · ไม่มี upkeep ไฟ
            SetResources(energy: 100f, water: 100f, workers: 20);
            Place(MakeOreNode("OreB", workerRequired: 3, quotaMin: 60f, quotaMax: 60f), 1, 1);
            Assign(1, 1, 2);

            resources.ApplyDailyProduction();

            Assert.AreEqual(40f, resources.Current.iron, 1e-3f, "โควตา 60 × 2/3 = 40");
            Assert.AreEqual(100f, resources.Current.energy, 1e-4f, "แหล่งแร่ไม่มีค่าเดินระบบ — ไฟต้องไม่ถูกหัก");
        }

        [Test]
        public void ZoneB_Production_YieldsTritiumByQuota()
        {
            // โซน B: เหล็ก 60 + Tritium 6 (min==max deterministic) จัดครบ 3/3 → ได้เต็มทั้งคู่
            SetResources(energy: 100f, water: 100f, workers: 20);
            Place(MakeOreNode("OreB", workerRequired: 3, quotaMin: 60f, quotaMax: 60f,
                exposure: 1f, tritMin: 6f, tritMax: 6f), 1, 1);
            Assign(1, 1, 3);

            resources.ApplyDailyProduction();

            Assert.AreEqual(60f, resources.Current.iron, 1e-3f, "เหล็กเต็มโควตา");
            Assert.AreEqual(6f, resources.Current.tritium, 1e-3f, "Tritium เต็มโควตา (โซน B)");
        }

        [Test]
        public void ZoneA_Production_NoTritium()
        {
            // โซน A ไม่ตั้งช่วง Tritium → ขุดได้แต่เหล็ก
            SetResources(energy: 100f, water: 100f, workers: 20);
            Place(MakeOreNode("OreA", workerRequired: 2, quotaMin: 30f, quotaMax: 30f), 1, 1);
            Assign(1, 1, 2);

            resources.ApplyDailyProduction();

            Assert.AreEqual(30f, resources.Current.iron, 1e-3f);
            Assert.AreEqual(0f, resources.Current.tritium, 1e-4f, "โซน A ต้องไม่ให้ Tritium");
        }

        [Test]
        public void OreProduction_Unstaffed_ProducesZero()
        {
            SetResources(energy: 100f, water: 100f, workers: 20);
            Place(MakeOreNode("OreA", workerRequired: 2, quotaMin: 30f, quotaMax: 30f), 1, 1);
            // ไม่ Assign

            resources.ApplyDailyProduction();

            Assert.AreEqual(0f, resources.Current.iron, 1e-4f, "ไม่มีคนขุด → เหล็ก 0");
        }

        // ─────────────── โซน B: รังสีสะสม + สุ่มป่วย (จบวัน) ───────────────

        [Test]
        public void ZoneB_DayProduction_AddsExposureAndSick()
        {
            // exposure 1/คน/วัน + chance 1 (deterministic) · จัด 3 คน → exposure +3, ป่วย 3
            SetResources(energy: 100f, water: 100f, food: 100f, workers: 20);
            Place(MakeOreNode("OreB", workerRequired: 3, quotaMin: 60f, quotaMax: 60f,
                exposure: 1f, sickChance: 1f), 1, 1);
            Assign(1, 1, 3);

            eventManager.RaiseDayProduction(2);

            Assert.AreEqual(3f, radiation.CurrentExposure, 1e-3f, "รังสีสะสม = 1 × 3 คน");
            Assert.AreEqual(3, population.Current.sick, "chance 100% × 3 คน → ป่วย 3");
        }

        [Test]
        public void ZoneB_ChanceZero_ExposureOnly_NoSick()
        {
            SetResources(energy: 100f, water: 100f, food: 100f, workers: 20);
            Place(MakeOreNode("OreB", workerRequired: 3, quotaMin: 60f, quotaMax: 60f,
                exposure: 1f, sickChance: 0f), 1, 1);
            Assign(1, 1, 3);

            eventManager.RaiseDayProduction(2);

            Assert.AreEqual(3f, radiation.CurrentExposure, 1e-3f);
            Assert.AreEqual(0, population.Current.sick, "chance 0 → ไม่มีป่วย");
        }

        [Test]
        public void ZoneA_DayProduction_NoExposure()
        {
            SetResources(energy: 100f, water: 100f, food: 100f, workers: 20);
            Place(MakeOreNode("OreA", workerRequired: 2, quotaMin: 30f, quotaMax: 30f), 1, 1);
            Assign(1, 1, 2);

            eventManager.RaiseDayProduction(2);

            Assert.AreEqual(0f, radiation.CurrentExposure, 1e-4f, "โซน A ปลอดภัย — ไม่เพิ่มรังสี");
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

        // ─────────────── save/load — โควตา rebuild จาก registry ───────────────

        [Test]
        public void SaveLoaded_RebuildsQuotaFromRegistry()
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

            Assert.AreEqual(25f, ore.GetDailyQuota(new Vector2Int(3, 3)), 1e-3f,
                "โหลดเซฟแล้วโหนดใน registry ต้องได้โควตาใหม่ (สุ่มในช่วง — min==max deterministic)");
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
