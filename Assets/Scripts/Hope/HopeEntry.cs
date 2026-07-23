using System;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: หมวดของแหล่งที่มา Hope — ใช้จัดกลุ่ม/เลือกสีในแผง HopeBreakdownPanel
    /// Category of a hope source — drives grouping/colors in HopeBreakdownPanel (GDD §18).
    /// </summary>
    public enum HopeCategory { Worker, Food, Water, Power, Reactor, Storm, Research, Story, Card }

    /// <summary>
    /// [TH] หน้าที่: รายการบวก/ลบ Hope 1 รายการที่ระบบใดระบบหนึ่งส่งเข้าบัญชี — Hope ห้ามเขียนตรงเด็ดขาด
    /// [TH] ทุกระบบต้องส่ง HopeEntry เข้า HopeLedger เท่านั้น เพื่อ audit ย้อนหลังได้ว่าบวก/ลบเพราะอะไร
    /// One hope delta reported by a system (GDD §18). Hope is NEVER written directly —
    /// every system submits entries to the HopeLedger and the ledger applies the daily sum.
    /// </summary>
    [Serializable]
    public struct HopeEntry
    {
        public string sourceKey;   // e.g. "worker.hungry" — stable key per CONFIG.md Hope Sources table
        public string text;        // player-facing line, e.g. "คนหิว 4 คน"
        public float value;        // signed delta (aggregated: 4 hungry × −2 = −8)
        public HopeCategory category;

        public HopeEntry(string sourceKey, string text, float value, HopeCategory category)
        {
            this.sourceKey = sourceKey;
            this.text = text;
            this.value = value;
            this.category = category;
        }
    }
}
