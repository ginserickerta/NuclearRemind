using NUnit.Framework;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// ตรรกะล้วนระบบ Inventory (GDD §13) — InventoryMath
    /// deterministic: affordability / stack clamp / gate คราฟต์ / คิวคราฟต์คืบต่อ tick — ไม่ต้องมี scene/event
    /// </summary>
    public class InventoryMathTests
    {
        private static ResourceCost[] Cost(params (ResourceType type, float amount)[] costs)
        {
            var result = new ResourceCost[costs.Length];
            for (int i = 0; i < costs.Length; i++)
                result[i] = new ResourceCost { type = costs[i].type, amount = costs[i].amount };
            return result;
        }

        private static ResourceData Stock(float energy = 0, float water = 0, float food = 0, float iron = 0)
            => new ResourceData { energy = energy, water = water, food = food, iron = iron };

        // ── CanAfford: คราฟต์ได้เมื่อพอจ่าย / ไม่ได้เมื่อไม่พอ ──

        [Test]
        public void CanAfford_EnoughStock_ReturnsTrue()
        {
            // Rad-Gear: Iron 150 — คลังมี 200 → พอ
            Assert.IsTrue(InventoryMath.CanAfford(Stock(iron: 200f), Cost((ResourceType.Iron, 150f))));
        }

        [Test]
        public void CanAfford_NotEnough_ReturnsFalse()
        {
            Assert.IsFalse(InventoryMath.CanAfford(Stock(iron: 100f), Cost((ResourceType.Iron, 150f))),
                "Iron 100 < 150 → คราฟต์ไม่ได้");
        }

        [Test]
        public void CanAfford_ExactAmount_ReturnsTrue()
        {
            Assert.IsTrue(InventoryMath.CanAfford(Stock(iron: 150f), Cost((ResourceType.Iron, 150f))),
                "พอดีเป๊ะ = จ่ายได้");
        }

        [Test]
        public void CanAfford_MultiCost_ChecksEveryType()
        {
            // ไอโซโทปการแพทย์: Iron 50 + Energy 50
            var cost = Cost((ResourceType.Iron, 50f), (ResourceType.Energy, 50f));
            Assert.IsTrue(InventoryMath.CanAfford(Stock(energy: 60f, iron: 60f), cost));
            Assert.IsFalse(InventoryMath.CanAfford(Stock(energy: 60f, iron: 10f), cost), "Iron ขาด → ไม่ผ่านแม้ Energy พอ");
            Assert.IsFalse(InventoryMath.CanAfford(Stock(energy: 10f, iron: 60f), cost), "Energy ขาด → ไม่ผ่านแม้ Iron พอ");
        }

        [Test]
        public void CanAfford_DuplicateType_SumsBeforeComparing()
        {
            // cost ซ้ำชนิด Iron 100 + Iron 100 — คลัง 150 ต้องไม่ผ่าน (รวม = 200)
            var cost = Cost((ResourceType.Iron, 100f), (ResourceType.Iron, 100f));
            Assert.IsFalse(InventoryMath.CanAfford(Stock(iron: 150f), cost));
            Assert.IsTrue(InventoryMath.CanAfford(Stock(iron: 200f), cost));
        }

        [Test]
        public void CanAfford_EmptyOrNullCost_IsFree()
        {
            Assert.IsTrue(InventoryMath.CanAfford(Stock(), null));
            Assert.IsTrue(InventoryMath.CanAfford(Stock(), new ResourceCost[0]));
        }

        // ── AddClamped: maxStack clamp ──

        [Test]
        public void AddClamped_UnderCap_Adds()
        {
            Assert.AreEqual(3, InventoryMath.AddClamped(2, 1, 5));
        }

        [Test]
        public void AddClamped_AtCap_Clamps()
        {
            Assert.AreEqual(5, InventoryMath.AddClamped(5, 1, 5), "เต็มเพดานแล้ว → คงที่ 5");
            Assert.AreEqual(3, InventoryMath.AddClamped(2, 10, 3), "เพิ่มเกิน → clamp ที่เพดาน");
        }

        [Test]
        public void AddClamped_ZeroMaxStack_IsUnlimited()
        {
            Assert.AreEqual(101, InventoryMath.AddClamped(100, 1, 0), "maxStack 0 = ไม่จำกัด");
        }

        [Test]
        public void AddClamped_NeverNegative()
        {
            Assert.AreEqual(0, InventoryMath.AddClamped(1, -5, 0));
        }

        [Test]
        public void RemoveClamped_FloorsAtZero()
        {
            Assert.AreEqual(1, InventoryMath.RemoveClamped(2, 1));
            Assert.AreEqual(0, InventoryMath.RemoveClamped(0, 1), "ไม่มีของ → 0 ไม่ติดลบ");
        }

        // ── HasStackRoom: นับรวมของที่ค้างในคิวคราฟต์ ──

        [Test]
        public void HasStackRoom_CountsQueuedItems()
        {
            Assert.IsTrue(InventoryMath.HasStackRoom(1, 0, 3), "ถือ 1/3 → คราฟต์ได้");
            Assert.IsFalse(InventoryMath.HasStackRoom(2, 1, 3), "ถือ 2 + คิว 1 = 3/3 → เต็ม");
            Assert.IsFalse(InventoryMath.HasStackRoom(3, 0, 3), "ถือเต็มเพดาน → คราฟต์ไม่ได้");
            Assert.IsTrue(InventoryMath.HasStackRoom(99, 99, 0), "maxStack 0 = ไม่จำกัด");
        }

        // ── CanStartCraft: gate ครบ 4 เงื่อนไข ──

        [Test]
        public void CanStartCraft_AllConditionsMet_ReturnsTrue()
        {
            Assert.IsTrue(InventoryMath.CanStartCraft(true, true, true, true));
        }

        [Test]
        public void CanStartCraft_AnyConditionFails_ReturnsFalse()
        {
            Assert.IsFalse(InventoryMath.CanStartCraft(false, true, true, true), "ยังไม่วิจัย");
            Assert.IsFalse(InventoryMath.CanStartCraft(true, false, true, true), "ไม่มีอาคาร/คนประจำ");
            Assert.IsFalse(InventoryMath.CanStartCraft(true, true, false, true), "ทรัพยากรไม่พอ");
            Assert.IsFalse(InventoryMath.CanStartCraft(true, true, true, false), "เต็มเพดานถือครอง");
        }

        // ── คิวคราฟต์: คืบตาม tick · gate ปิด = ค้าง · deterministic ──

        [Test]
        public void StepProgress_GateOpen_AdvancesOneTick()
        {
            Assert.AreEqual(1, InventoryMath.StepProgress(0, true));
            Assert.AreEqual(5, InventoryMath.StepProgress(4, true));
        }

        [Test]
        public void StepProgress_GateClosed_Stalls()
        {
            Assert.AreEqual(4, InventoryMath.StepProgress(4, false), "อาคารไม่พร้อม/ไม่มีคน → ค้าง ไม่ถอยหลัง");
        }

        [Test]
        public void IsComplete_AtOrPastCraftTicks()
        {
            Assert.IsFalse(InventoryMath.IsComplete(5, 6));
            Assert.IsTrue(InventoryMath.IsComplete(6, 6));
            Assert.IsTrue(InventoryMath.IsComplete(7, 6));
        }

        [Test]
        public void IsComplete_ZeroTicks_IsInstant()
        {
            Assert.IsTrue(InventoryMath.IsComplete(0, 0), "craftTicks 0 = ได้ทันที (น้ำหล่อเย็นฉุกเฉิน)");
        }

        [Test]
        public void CraftQueue_Simulation_CompletesAtExactTick_Deterministic()
        {
            // จำลองคิวคราฟต์ Rad-Gear (6 tick): gate เปิดตลอด → เสร็จบน tick ที่ 6 เป๊ะ
            const int craftTicks = 6;
            int progress = 0;
            int ticksTaken = 0;
            while (!InventoryMath.IsComplete(progress, craftTicks))
            {
                progress = InventoryMath.StepProgress(progress, true);
                ticksTaken++;
                Assert.LessOrEqual(ticksTaken, 100, "ต้องเสร็จ — กันลูปไม่จบ");
            }
            Assert.AreEqual(6, ticksTaken);
        }

        [Test]
        public void CraftQueue_Simulation_StallsWhileGateClosed_ThenResumes()
        {
            // จำลอง: คืบ 3 tick → คนงานถูกถอน 5 tick (ค้าง) → คนกลับมา → เสร็จรวมงานจริง 6 tick
            const int craftTicks = 6;
            int progress = 0;

            for (int i = 0; i < 3; i++) progress = InventoryMath.StepProgress(progress, true);
            Assert.AreEqual(3, progress);

            for (int i = 0; i < 5; i++) progress = InventoryMath.StepProgress(progress, false);
            Assert.AreEqual(3, progress, "gate ปิด 5 tick → progress ค้างที่ 3");

            for (int i = 0; i < 3; i++) progress = InventoryMath.StepProgress(progress, true);
            Assert.IsTrue(InventoryMath.IsComplete(progress, craftTicks), "กลับมาคืบต่อจนเสร็จ");
        }

        // ── ไอโซโทปการแพทย์: 18 tick = 1 วันเกม (90s ÷ 5s) ──

        [Test]
        public void MedicalIsotope_EighteenTicks_EqualsOneGameDay()
        {
            // GDD §13: "เตา Idle 1 วัน" — วันเกม 90s ÷ tick 5s = 18 tick
            const int craftTicks = 18;
            int progress = 0;
            for (int i = 0; i < 17; i++) progress = InventoryMath.StepProgress(progress, true);
            Assert.IsFalse(InventoryMath.IsComplete(progress, craftTicks), "tick ที่ 17 ยังไม่เสร็จ");
            progress = InventoryMath.StepProgress(progress, true);
            Assert.IsTrue(InventoryMath.IsComplete(progress, craftTicks), "ครบ 18 tick = 1 วันเกม → เสร็จ");
        }

        // ── StockOf: map ครบทุกชนิด ──

        [Test]
        public void StockOf_MapsEveryResourceType()
        {
            var stock = new ResourceData
            {
                energy = 1f, water = 2f, food = 3f, iron = 4f,
                deuterium = 5f, tritium = 6f, knowledge = 7f,
            };
            Assert.AreEqual(1f, InventoryMath.StockOf(stock, ResourceType.Energy));
            Assert.AreEqual(2f, InventoryMath.StockOf(stock, ResourceType.Water));
            Assert.AreEqual(3f, InventoryMath.StockOf(stock, ResourceType.Food));
            Assert.AreEqual(4f, InventoryMath.StockOf(stock, ResourceType.Iron));
            Assert.AreEqual(5f, InventoryMath.StockOf(stock, ResourceType.Deuterium));
            Assert.AreEqual(6f, InventoryMath.StockOf(stock, ResourceType.Tritium));
            Assert.AreEqual(7f, InventoryMath.StockOf(stock, ResourceType.Knowledge));
        }
    }
}
