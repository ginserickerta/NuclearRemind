using System;

namespace NuclearReMind
{
    /// <summary>[TH] แท็บกรองของแผงคลัง v6.3: ทั้งหมด/ทรัพยากร/เชื้อเพลิง/อาหาร/การแพทย์/เกษตร
    /// Filter tabs of the v6.3 inventory panel (docs/INVENTORY.md ข้อ 2).</summary>
    public enum InventoryTab { All, Resource, Fuel, Food, Medical, Agriculture }

    /// <summary>
    /// [TH] หน้าที่: ข้อมูลแสดงผลหนึ่งช่องของแผงคลัง — สร้างใหม่จาก ResourceManager ทุกครั้งที่ refresh
    /// [TH] เป็น view ล้วน ห้ามใช้เก็บ state (กติกาข้อ 9: Inventory ห้ามเก็บ state ซ้ำ)
    /// One display slot of the inventory panel (docs/INVENTORY.md ข้อ 5-6).
    /// Pure view data built fresh from ResourceManager each refresh — never a state store.
    /// </summary>
    [Serializable]
    public struct InventorySlotView
    {
        public string itemId;        // power / water / iron / labmat / fuel / tritium / food / radsuit
        public string displayName;
        public string icon;          // emoji fallback until real sprites land
        public float count;
        public float cap;            // 999 = "แทบไม่จำกัด" for bulk goods
        public float deltaPerDay;    // ★ +เขียว / −แดง — the teaching number of the whole game · [TH] อัตราสุทธิ/วัน = ตัวเลขสอนหลักของทั้งเกม
        public bool isCraftable;     // Rad Suit only (Sprint 4 wires crafting UI fully) · [TH] ช่องนี้คราฟต์ได้ (ตอนนี้มีแค่ Rad Suit)
        public bool craftEnabled;    // gate result — gray button when false · [TH] false = ปุ่มคราฟต์เทา (เงื่อนไขไม่ครบ)
        public string lockedHint;    // non-null → show 🔒 + "ต้องวิจัย [note]" (same rule as card options) · [TH] ไม่ null = โชว์ล็อก+เหตุผล ห้ามซ่อน
    }

    /// <summary>
    /// [TH] หน้าที่: สูตรคำนวณอัตราสุทธิ/วัน ของแต่ละทรัพยากร (ค่าจาก CONFIG.md) ให้แผงคลังแสดง
    /// [TH] ★ ทุกอัตราคูณ Σ GetEfficiency (ผลรวมประสิทธิภาพคนงาน) ไม่ใช่นับหัว — กติกาข้อ 7
    /// [TH] static + pure ให้ EditMode test ตรวจเลขได้โดยไม่ต้องมีซีน
    /// Per-day delta formulas (CONFIG.md ECONOMY) for the inventory panel.
    /// CRITICAL: every rate multiplies Σ GetEfficiency (passed in as eff sums), never headcount.
    /// Static + pure so EditMode tests verify the math without a scene.
    /// Sprint 1 covers base draw only — extractor/medbay/zoneB/co60 draws join in later sprints.
    /// </summary>
    public static class InventoryDeltaMath
    {
        public static float PowerPerDay(GameConfigSO cfg, float powerEff) =>
            powerEff * cfg.powerPerPowerWorker - cfg.drawBase;

        public static float WaterPerDay(GameConfigSO cfg, float waterEff, int pop) =>
            waterEff * cfg.waterPerWaterWorker - pop * cfg.waterPerPopPerDay;

        public static float FoodPerDay(GameConfigSO cfg, float farmEff, int pop) =>
            farmEff * cfg.foodPerFarmWorker - pop * cfg.foodPerWorkerPerDay;

        public static float IronPerDay(GameConfigSO cfg, float mineEff) =>
            mineEff * cfg.ironPerMineWorker;
    }
}
