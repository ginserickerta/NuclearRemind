namespace NuclearReMind
{
    /// <summary>
    /// ตรรกะล้วนของระบบ Inventory (GDD §13) — deterministic ไม่มี Unity scene/event
    /// (แพทเทิร์นเดียวกับ OreMath/CrisisEffectMath/StatCondition — EditMode test ตรวจได้ตรง ๆ)
    /// InventoryManager เป็นคนรวบรวม input (คลัง/อาคาร/วิจัย) แล้วเรียกฟังก์ชันที่นี่ตัดสิน
    /// </summary>
    public static class InventoryMath
    {
        /// <summary>จำนวนในคลังของทรัพยากรชนิดนี้ (map ResourceType → field ของ ResourceData)</summary>
        public static float StockOf(ResourceData stock, ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Energy:    return stock.energy;
                case ResourceType.Water:     return stock.water;
                case ResourceType.Food:      return stock.food;
                case ResourceType.Iron:      return stock.iron;
                case ResourceType.Deuterium: return stock.deuterium;
                case ResourceType.Tritium:   return stock.tritium;
                case ResourceType.Knowledge: return stock.knowledge;
                default:                     return 0f;
            }
        }

        /// <summary>
        /// คลังพอจ่ายต้นทุนทั้งชุดไหม — รวมยอดต่อชนิดก่อนเทียบ
        /// (cost ซ้ำชนิด เช่น Iron 100 + Iron 100 ต้องมี Iron ≥ 200 ไม่ใช่แค่ 100)
        /// costs ว่าง/null = ฟรี → true
        /// </summary>
        public static bool CanAfford(ResourceData stock, ResourceCost[] costs)
        {
            if (costs == null || costs.Length == 0) return true;

            // ResourceType มี 7 ค่า (Energy..Knowledge) — สะสมยอดที่ต้องใช้ต่อชนิด
            var required = new float[7];
            foreach (var cost in costs)
            {
                int i = (int)cost.type;
                if (i < 0 || i >= required.Length) continue;
                required[i] += cost.amount;
            }

            for (int i = 0; i < required.Length; i++)
                if (required[i] > 0f && StockOf(stock, (ResourceType)i) < required[i])
                    return false;
            return true;
        }

        /// <summary>เพิ่มจำนวนแบบ clamp เพดานถือครอง — maxStack ≤ 0 = ไม่จำกัด · ไม่ติดลบ</summary>
        public static int AddClamped(int current, int amount, int maxStack)
        {
            long next = (long)current + amount;
            if (next < 0) next = 0;
            if (maxStack > 0 && next > maxStack) next = maxStack;
            return (int)next;
        }

        /// <summary>ลดจำนวน (ตอนใช้ไอเทม) — ไม่ต่ำกว่า 0</summary>
        public static int RemoveClamped(int current, int amount)
        {
            int next = current - amount;
            return next < 0 ? 0 : next;
        }

        /// <summary>
        /// ยังมีที่ให้คราฟต์เพิ่มไหม — นับรวม "ที่กำลังคราฟต์ในคิว" กันคิวเสร็จแล้วชน maxStack ของหาย
        /// maxStack ≤ 0 = ไม่จำกัด → true เสมอ
        /// </summary>
        public static bool HasStackRoom(int current, int queued, int maxStack)
        {
            if (maxStack <= 0) return true;
            return current + queued < maxStack;
        }

        /// <summary>
        /// เริ่มคราฟต์ได้ไหม — gate ครบ 4 เงื่อนไข (GDD §13 + จุดตัดสินใจ):
        /// วิจัยปลดแล้ว · มีอาคาร craftedAt สร้างเสร็จ+คนประจำ · คลังพอจ่าย · ยังไม่ชนเพดานถือครอง
        /// </summary>
        public static bool CanStartCraft(bool researchUnlocked, bool hasStaffedBuilding,
                                         bool canAfford, bool hasStackRoom)
        {
            return researchUnlocked && hasStaffedBuilding && canAfford && hasStackRoom;
        }

        /// <summary>
        /// คืบหน้าคิวคราฟต์ 1 tick — gateOpen (อาคารพร้อม+คนประจำ) = +1 · gate ปิด = ค้างเท่าเดิม
        /// (แบบเดียวกับ ConstructionController: ไม่มีคน = ไม่คืบ ไม่ยกเลิก)
        /// </summary>
        public static int StepProgress(int progress, bool gateOpen)
        {
            return gateOpen ? progress + 1 : progress;
        }

        /// <summary>คราฟต์เสร็จหรือยัง — craftTicks ≤ 0 = ทันที (เสร็จตั้งแต่ progress 0)</summary>
        public static bool IsComplete(int progress, int craftTicks)
        {
            return progress >= craftTicks;
        }
    }
}
