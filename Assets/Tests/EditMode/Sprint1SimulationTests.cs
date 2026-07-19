using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// Sprint 1 acceptance (GDD §32): run D1-30 headless →
    ///   · Hope must NOT sit at 100 the whole run
    ///   · hungry/sick workers must actually appear
    ///   · inventory delta/day math must reflect Σ efficiency (never headcount)
    /// Simulates the food economy test-side (production → tick → consumption per §2 End of Day).
    /// </summary>
    public class Sprint1SimulationTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private WorkerManager wm;
        private GameConfigSO cfg;

        [SetUp]
        public void SetUp()
        {
            cfg = ScriptableObject.CreateInstance<GameConfigSO>();
            _spawned.Add(cfg);
            var go = new GameObject("WorkerManager");
            _spawned.Add(go);
            wm = go.AddComponent<WorkerManager>();
            wm.Initialize(cfg);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        /// <summary>One full in-game day, mirroring WorkerManager.HandleDayEnded without a scene.</summary>
        private float SimulateDay(float food, System.Random rng, out WorkerTickContext ctx)
        {
            // End of Day batch (§2): ผลิต → หักบริโภค — production multiplies Σ efficiency (rule #7)
            food += wm.SumEfficiency(WorkerJobs.Farm) * cfg.foodPerFarmWorker;

            ctx = new WorkerTickContext { foodStock = food, rng = rng, medBayHeal = cfg.medBayHeal };
            wm.RunDailyTick(ctx);
            food = Mathf.Max(0f, food - ctx.foodConsumed); // floor 0 (บั๊ก #17/#18 หลักเดียวกัน)

            // resource-level hope (mirror of ReportResourceHope)
            int pop = wm.AliveCount;
            if (food > pop * cfg.foodSurplusPopMult)
                wm.Hope.Report("food.surplus", "อาหารเต็มคลัง", cfg.hopeFoodSurplus, HopeCategory.Food);
            else if (food <= 0f)
                wm.Hope.Report("food.empty", "อาหารหมด", cfg.hopeFoodEmpty, HopeCategory.Food);

            ShiftSystem.TickShifts(wm.Workers, labBusy: false, cfg);
            wm.TickStrikes();
            wm.CommitDay();
            return food;
        }

        // ── Acceptance: เร่งเตาแบบบริหารพลาด — ต้องเห็นคนหิว + คนป่วย ──
        [Test]
        public void Rush30Days_HungerAndSicknessAppear_HopeFalls()
        {
            wm.AssignJobCounts(new Dictionary<string, int>
            {
                { WorkerJobs.Farm, 1 },   // ฟาร์มขาดคน → ผลิต 9 < กิน 14 → อดแน่
                { WorkerJobs.Power, 2 },
                { WorkerJobs.Water, 2 },
                { WorkerJobs.Mine, 6 },   // rad 4/วัน → ป่วย ~D13
                { WorkerJobs.Cool, 3 },   // rad 6/วัน → ป่วย ~D9
            });

            var rng = new System.Random(2569);
            float food = cfg.startFood;
            int maxHungry = 0, maxSick = 0;
            var hopeHistory = new List<float>();

            for (int day = 2; day <= 30; day++)
            {
                food = SimulateDay(food, rng, out _);
                maxHungry = Mathf.Max(maxHungry, wm.HungryCount);
                maxSick = Mathf.Max(maxSick, wm.SickCount + wm.DyingCount);
                hopeHistory.Add(wm.Hope.Current);

                // invariants ต้องไม่หลุดสักวัน
                Assert.IsFalse(float.IsNaN(wm.Hope.Current), $"D{day}: Hope เป็น NaN");
                Assert.GreaterOrEqual(wm.Hope.Current, 0f, $"D{day}: Hope ต่ำกว่า 0");
                Assert.LessOrEqual(wm.Hope.Current, 100f, $"D{day}: Hope เกิน 100");
                Assert.GreaterOrEqual(food, 0f, $"D{day}: food ติดลบ (floor 0)");
            }

            Assert.GreaterOrEqual(maxHungry, 3, "ต้องมีวันที่คนหิว ≥ 3 (เงื่อนไข S7/การ์ด #4 ต้องถึงได้)");
            Assert.GreaterOrEqual(maxSick, 1, "เหมือง/หอเตาไม่มีการรักษา → ต้องมีคนป่วยจริง");
            Assert.Less(hopeHistory.Min(), 70f, "Hope ต้องร่วงจากค่าเริ่ม 70 — เมืองที่บริหารพลาดต้องรู้สึก");
            Assert.Greater(hopeHistory.Distinct().Count(), 1, "Hope ไม่ค้างค่าเดียวตลอดเกม");
        }

        // ── Acceptance: สมดุล — ระบบเดิน 30 วันโดย invariant ไม่แตก ──
        [Test]
        public void Balanced30Days_HopeMoves_NeverStuckAt100()
        {
            wm.AssignJobCounts(new Dictionary<string, int>
            {
                { WorkerJobs.Farm, 3 },
                { WorkerJobs.Power, 2 },
                { WorkerJobs.Water, 2 },
                { WorkerJobs.Mine, 4 },
                { WorkerJobs.Lab, 3 },
            });

            var rng = new System.Random(1337);
            float food = cfg.startFood;
            var hopeHistory = new List<float>();
            int maxSick = 0;

            for (int day = 2; day <= 30; day++)
            {
                food = SimulateDay(food, rng, out _);
                hopeHistory.Add(wm.Hope.Current);
                maxSick = Mathf.Max(maxSick, wm.SickCount + wm.DyingCount);
            }

            Assert.Greater(hopeHistory.Distinct().Count(), 1, "Hope ต้องขยับ — ไม่ค้างค่าเดียว");
            Assert.IsFalse(hopeHistory.All(h => Mathf.Approximately(h, 100f)),
                "Sprint 1 spec: Hope ไม่ 100 ตลอด");
            Assert.GreaterOrEqual(maxSick, 1,
                "ไม่มี Med Bay ใน Sprint 1 → คนเหมือง (rad 4/วัน) ต้องป่วยราว D13+");
            // ยังไม่มีระบบรักษา/วิจัย — ไม่ assert ว่ารอด 30 วัน (นั่นคืองาน Sprint 2-3)
        }

        // ── Shift ป้องกันเมือง Exhausted ถาวร (บั๊ก sim #5) ──────────
        [Test]
        public void ShiftSystem_PreventsPermanentExhaustion()
        {
            wm.AssignJobCounts(new Dictionary<string, int> { { WorkerJobs.Farm, 14 } });
            var rng = new System.Random(99);
            float food = 999f;

            // ★ วัด "ถาวรไหม" ไม่ใช่ "กี่คน" — บั๊ก #5 คือ *ทั้งเมือง Exhausted **ถาวร** D8* คำสำคัญคือถาวร
            //   เดิมเทสต์นี้ผูกกับ maxExhausted <= 4 ซึ่งใช้ได้ตอนที่ Exhausted แปลว่า "ล้าเกิน 85 = ทำงาน
            //   ไม่ได้เลย" (ซึ่งไม่เคยเกิดจริง เพราะกะดึงคนออกที่ 70 — ตัวเลขจึงเป็น 0 ตลอดและเทสต์ผ่าน
            //   แบบว่างเปล่า) ตอนนี้ Exhausted แปลว่า "ล้าเกิน 68 = ถึงจุดที่ต้องถูกดึงออก" คนทั้งกะที่เข้างาน
            //   พร้อมกันจึงติดป้ายพร้อมกันหนึ่งวันเป็นเรื่องปกติ — สิ่งที่ต้องห้ามคือมันค้างข้ามวัน
            var prevExhausted = new HashSet<int>();
            for (int day = 2; day <= 20; day++)
            {
                food += 200f; // อาหารล้น — แยกตัวแปรความล้าออกมาดูอย่างเดียว
                food = SimulateDay(food, rng, out _);

                var now = new HashSet<int>(wm.Workers.Where(w => w.alive && w.status == WorkerStatus.Exhausted)
                                                     .Select(w => w.id));
                var stuck = now.Intersect(prevExhausted).ToList();
                Assert.IsEmpty(stuck,
                    $"วันที่ {day}: คนงาน id {string.Join(",", stuck)} หมดแรงติดกันสองวัน — " +
                    "ระบบกะต้องดึงออกไปพักทุกครั้ง (บั๊ก #5: ไม่มีกะ → ทั้งเมือง Exhausted ถาวร D8)");
                prevExhausted = now;
            }

            Assert.IsTrue(wm.Workers.Any(w => w.fatigue < 70f), "ต้องมีคนได้พักจริง");
        }

        // ── Inventory delta/วัน (docs/INVENTORY.md — ★ ต้องคูณ eff) ──
        [Test]
        public void InventoryDelta_UsesEfficiencySum_NotHeadcount()
        {
            Assert.AreEqual(13f, InventoryDeltaMath.FoodPerDay(cfg, farmEff: 3f, pop: 14), 1e-3f,
                "3 ชาวนาเต็มประสิทธิภาพ: 3×9 − 14×1 = +13");
            Assert.AreEqual(4.9f, InventoryDeltaMath.FoodPerDay(cfg, farmEff: 2.1f, pop: 14), 1e-3f,
                "ชาวนาเหนื่อย (Σeff 2.1): 18.9 − 14 = +4.9 — ตัวเลขต้องสะท้อน eff ไม่ใช่หัว");
            Assert.AreEqual(70f, InventoryDeltaMath.PowerPerDay(cfg, powerEff: 2f), 1e-3f,
                "2×45 − draw 20 = +70");
            Assert.AreEqual(46f, InventoryDeltaMath.WaterPerDay(cfg, waterEff: 2f, pop: 14), 1e-3f,
                "2×30 − 14 = +46");
            Assert.AreEqual(56f, InventoryDeltaMath.IronPerDay(cfg, mineEff: 4f), 1e-3f, "4×14 = +56");
        }

        [Test]
        public void InventorySlot_Format_ShowsDeltaAndLock()
        {
            var slot = new InventorySlotView
            {
                itemId = "power", displayName = "Power", icon = "⚡",
                count = 240f, cap = 400f, deltaPerDay = 45f,
            };
            StringAssert.Contains("240/400", InventoryPanel.FormatSlot(slot));
            StringAssert.Contains("+45/วัน", InventoryPanel.FormatSlot(slot));

            slot.deltaPerDay = -6f;
            slot.cap = 999f;
            StringAssert.Contains("−6/วัน", InventoryPanel.FormatSlot(slot), "ติดลบต้องเห็นชัด (จุดสอน Tritium)");

            slot.lockedHint = "ต้องวิจัย รังสีกับร่างกายคน";
            // marker changed 🔒 → [ล็อก] (legacy uGUI Text cannot draw astral glyphs — it showed blank)
            StringAssert.Contains("[ล็อก]", InventoryPanel.FormatSlot(slot), "ของล็อกต้องแสดง ห้ามซ่อน (กติกาข้อ 6)");
        }
    }
}
