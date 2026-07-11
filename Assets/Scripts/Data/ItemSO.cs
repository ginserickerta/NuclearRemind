using System;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// หมวดไอเทม (GDD §13) — Equipment/Medical/Agri/Emergency/Fuel
    /// (Fuel สำรองไว้ให้อนาคต — Deuterium/Tritium ปัจจุบันเป็น "ทรัพยากร" ใน ResourceManager ไม่ย้ายมา Inventory)
    /// </summary>
    public enum ItemCategory
    {
        Equipment,  // อุปกรณ์ (Rad-Gear)
        Medical,    // การแพทย์ (PET/SPECT, ไอโซโทปการแพทย์)
        Agri,       // เกษตร/ถนอมอาหาร (เมล็ดพันธุ์ฉายรังสี, เครื่องฉายโคบอลต์-60)
        Emergency,  // ฉุกเฉิน (น้ำหล่อเย็นฉุกเฉิน)
        Fuel        // เชื้อเพลิง (สำรอง — ยังไม่ใช้)
    }

    /// <summary>
    /// ต้นทุนทรัพยากร 1 รายการ (ชนิด + จำนวน) — ItemSO ใช้เป็น array ให้คราฟต์จ่ายหลายชนิดได้
    /// </summary>
    [Serializable]
    public struct ResourceCost
    {
        public ResourceType type;
        public float amount;
    }

    /// <summary>
    /// ข้อมูลไอเทมคราฟต์ 1 ชนิด (GDD §13 — ScriptableObject: ค่าเกมทั้งหมดอยู่ใน asset ห้าม hardcode)
    /// คราฟต์ที่อาคาร craftedAt (ต้องสร้างเสร็จ + มีคนงานประจำ) → เข้าคิวคราฟต์ (InventoryManager)
    /// ผลตอน "ใช้" กระจายผ่าน EventManager.OnItemUsed — manager ที่เกี่ยวข้อง subscribe เอง
    /// (CoreTowerManager: coreHeatReduction · CrisisEffectManager: stopsFoodSpoilage/foodYieldBonus)
    /// </summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "NuclearReMind/Item")]
    public class ItemSO : ScriptableObject
    {
        [Header("Identity")]
        public string id;               // key ใน inventory (เช่น "rad_gear") — ห้ามซ้ำ
        public string displayName;
        [TextArea]
        public string description;
        public Sprite icon;             // ว่างได้ — UI fallback เป็น emoji ตามหมวด
        public ItemCategory category;

        [Header("Craft (GDD §13)")]
        public ResourceCost[] craftCost;    // ต้นทุนคราฟต์ (หักครั้งเดียวตอนเริ่มคราฟต์)
        public BuildingType craftedAt;      // ต้องมีอาคารชนิดนี้สร้างเสร็จ + คนงานประจำ ≥1
        public int craftTicks;              // เวลาผลิต (tick · 5วิ) — 0 = ได้ทันที · 18 ≈ 1 วันเกม
        public bool requiresResearch;       // ต้องปลดวิจัยก่อน (เชื่อม ResearchManager ระบบ 2 ผ่าน InventoryManager.ResearchUnlockedQuery)
        public int maxStack;                // เพดานถือครอง (นับรวมที่กำลังคราฟต์ในคิว) · 0 = ไม่จำกัด

        [Header("Effect on use (ค่าใน asset — manager ที่เกี่ยวข้อง subscribe OnItemUsed)")]
        [Tooltip("true = ไอเทมถือไว้มีผล/ให้ระบบวิกฤตเช็ก HasItem (Rad-Gear, PET) — กดใช้เองจากแผงไม่ได้")]
        public bool isPassive;
        [Tooltip(">0 = ใช้แล้วลด HEAT เตาทันที (น้ำหล่อเย็นฉุกเฉิน — วิกฤต 1·C)")]
        public float coreHeatReduction;
        [Tooltip("true = ใช้แล้วอาหารหยุดเน่า spoil rate → 0 (เครื่องฉายโคบอลต์-60 — วิกฤต 3·B)")]
        public bool stopsFoodSpoilage;
        [Tooltip(">0 = ใช้แล้วผลผลิตอาหาร FoodYieldMultiplier +ค่านี้ ถาวร (เมล็ดพันธุ์ฉายรังสี — วิกฤต 3·A)")]
        public float foodYieldBonus;
        [Tooltip("true = ใช้แล้วรักษาคนป่วยหายสนิท sick → 0 (ไอโซโทปการแพทย์ — วิกฤต 2·B)")]
        public bool curesAllSick;
    }
}
