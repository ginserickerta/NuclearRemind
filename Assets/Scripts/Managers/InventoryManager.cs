using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// คลังไอเทมคราฟต์ (GDD §13) — แยกจากคลังทรัพยากรบัลก์ (ResourceManager)
    /// - รับคำสั่งผ่าน event เท่านั้น: OnCraftItemRequested / OnUseItemRequested (UI ยิง)
    /// - คราฟต์: gate ผ่าน InventoryMath (วิจัย·อาคาร craftedAt สร้างเสร็จ+คนประจำ·คลังพอ·เพดาน stack)
    ///   → หักต้นทุนผ่าน RaiseResourceDelta → craftTicks 0 = ได้ทันที · >0 = เข้าคิวคืบต่อ tick
    ///   (แบบเดียวกับ ConstructionController — gate ปิด = ค้าง ไม่ยกเลิก)
    /// - ใช้ไอเทม: ลด count → RaiseItemUsed(ItemSO) — ผลกระจายให้ manager ที่ subscribe:
    ///   CoreTowerManager (coreHeatReduction) · CrisisEffectManager (stopsFoodSpoilage/foodYieldBonus)
    ///   · curesAllSick ยิง RaisePopulationSickSet(0) จากที่นี่ (event เดิมของ PopulationManager)
    /// - อ่านคลัง/อาคาร/คนงานแบบ read-only query ข้าม manager (แพทเทิร์นเดียวกับ ResourceManager)
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Item Catalog — ItemSO assets ทั้งหมด (wire โดยเมนู Setup Inventory)")]
        public ItemSO[] allItems;

        /// <summary>
        /// hook วิจัย (ระบบ 2 — ResearchManager ยังไม่ถูกสร้าง): เมื่อระบบวิจัยมาให้ assign
        ///   InventoryManager.ResearchUnlockedQuery = id => ResearchManager.Instance.IsUnlocked(id);
        /// null = ยังไม่มีระบบวิจัย → ไอเทม requiresResearch คราฟต์ได้ไปก่อน (ไม่บล็อกเกม)
        /// </summary>
        public static Func<string, bool> ResearchUnlockedQuery;

        // itemId → จำนวนที่ถือ
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

        // คิวคราฟต์ (รักษาลำดับ — แพทเทิร์น ConstructionController): itemId + progress ปัจจุบัน
        private class CraftJob
        {
            public string itemId;
            public int progress;
        }
        private readonly List<CraftJob> _craftQueue = new List<CraftJob>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnCraftItemRequested += HandleCraftRequested;
            EventManager.Instance.OnUseItemRequested   += HandleUseRequested;
            EventManager.Instance.OnDiscardItemRequested += HandleDiscardRequested;
            EventManager.Instance.OnGameTick           += HandleGameTick;
            EventManager.Instance.OnSaveLoaded         += HandleSaveLoaded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnCraftItemRequested -= HandleCraftRequested;
            EventManager.Instance.OnUseItemRequested   -= HandleUseRequested;
            EventManager.Instance.OnDiscardItemRequested -= HandleDiscardRequested;
            EventManager.Instance.OnGameTick           -= HandleGameTick;
            EventManager.Instance.OnSaveLoaded         -= HandleSaveLoaded;
        }

        // ─────────────────────────────────────────
        //  Public queries (read-only — UI/ระบบวิกฤตใช้)
        // ─────────────────────────────────────────

        /// <summary>ItemSO จาก id (null ถ้าไม่พบ) — scan ตรง allItems (N≤10 แบบ QuizManager.GetById)</summary>
        public ItemSO GetItem(string id)
        {
            if (string.IsNullOrEmpty(id) || allItems == null) return null;
            foreach (var item in allItems)
                if (item != null && item.id == id)
                    return item;
            return null;
        }

        /// <summary>จำนวนไอเทมที่ถืออยู่ (0 ถ้าไม่มี)</summary>
        public int GetCount(string id) => _counts.TryGetValue(id, out int n) ? n : 0;

        /// <summary>ถือไอเทมนี้อย่างน้อย 1 ชิ้นไหม — ให้ระบบวิกฤตเช็ก (เช่น 2·A ต้องมี PET scanner)</summary>
        public bool HasItem(string id) => GetCount(id) > 0;

        /// <summary>จำนวนชิ้นที่กำลังคราฟต์ค้างในคิว (นับรวมกับ count ตอนเช็กเพดาน maxStack)</summary>
        public int GetQueuedCount(string id)
        {
            int n = 0;
            for (int i = 0; i < _craftQueue.Count; i++)
                if (_craftQueue[i].itemId == id) n++;
            return n;
        }

        /// <summary>คลังพอจ่ายต้นทุนคราฟต์ไหม (เฉพาะเงื่อนไขทรัพยากร — UI ใช้แต่งสีปุ่ม)</summary>
        public bool CanAffordCraft(ItemSO item)
        {
            if (item == null) return false;
            var rm = ResourceManager.Instance;
            if (rm == null) return true; // ไม่มีคลัง (test harness) → ไม่บล็อก
            return InventoryMath.CanAfford(rm.Current, item.craftCost);
        }

        /// <summary>เริ่มคราฟต์ได้ไหม — gate ครบทุกเงื่อนไข (UI ใช้ enable/disable ปุ่มคราฟต์)</summary>
        public bool CanCraft(ItemSO item)
        {
            if (item == null) return false;
            return InventoryMath.CanStartCraft(
                IsResearchUnlocked(item),
                HasStaffedBuilding(item.craftedAt),
                CanAffordCraft(item),
                InventoryMath.HasStackRoom(GetCount(item.id), GetQueuedCount(item.id), item.maxStack));
        }

        /// <summary>ไอเทมนี้ปลดวิจัยแล้วไหม — ผ่าน hook ResearchUnlockedQuery (null = ปลดล็อกทั้งหมด)</summary>
        public bool IsResearchUnlocked(ItemSO item)
        {
            if (item == null || !item.requiresResearch) return true;
            return ResearchUnlockedQuery == null || ResearchUnlockedQuery(item.id);
        }

        /// <summary>
        /// มีอาคารชนิด craftedAt ที่สร้างเสร็จ + คนงานประจำ ≥1 ไหม (จุดตัดสินใจ #3)
        /// read-only query: BuildingRegistry / ConstructionController / WorkerAssignmentManager
        /// ไม่มี registry (test harness) → ไม่บล็อก
        /// </summary>
        public bool HasStaffedBuilding(BuildingType type)
        {
            var reg = BuildingRegistry.Instance;
            if (reg == null) return true;

            foreach (var kvp in reg.PlacedBuildings)
            {
                var data = kvp.Value;
                if (data == null || data.buildingType != type) continue;
                if (ConstructionController.Instance != null &&
                    ConstructionController.Instance.IsUnderConstruction(kvp.Key)) continue;

                // ไม่มี WAM → ถือว่าคนครบ (แพทเทิร์น ResourceManager.ComputeProductionDelta)
                int assigned = WorkerAssignmentManager.Instance != null
                    ? WorkerAssignmentManager.Instance.GetAssigned(kvp.Key)
                    : data.workerRequired;
                if (data.workerRequired > 0 && assigned <= 0) continue;

                return true;
            }
            return false;
        }

        // ─────────────────────────────────────────
        //  Craft (event → gate → หักต้นทุน → คิว/ได้ทันที)
        // ─────────────────────────────────────────

        private void HandleCraftRequested(string itemId)
        {
            var item = GetItem(itemId);
            if (item == null)
            {
                Debug.LogWarning($"[InventoryManager] ไม่รู้จักไอเทม '{itemId}'");
                return;
            }

            // แจ้งเหตุผลที่คราฟต์ไม่ได้เป็น toast (แพทเทิร์น RaiseNotice — AlertController แสดง)
            if (!IsResearchUnlocked(item))
            {
                EventManager.Instance.RaiseNotice($"ยังไม่ได้วิจัย {item.displayName}");
                return;
            }
            if (!HasStaffedBuilding(item.craftedAt))
            {
                EventManager.Instance.RaiseNotice($"ต้องมี{BuildingLabel(item.craftedAt)}ที่สร้างเสร็จ + คนประจำ ถึงจะผลิต {item.displayName} ได้");
                return;
            }
            if (!CanAffordCraft(item))
            {
                EventManager.Instance.RaiseNotice($"ทรัพยากรไม่พอผลิต {item.displayName}");
                return;
            }
            if (!InventoryMath.HasStackRoom(GetCount(item.id), GetQueuedCount(item.id), item.maxStack))
            {
                EventManager.Instance.RaiseNotice($"{item.displayName} เต็มเพดานถือครองแล้ว ({item.maxStack})");
                return;
            }

            // หักต้นทุน (ผ่าน event — ResourceManager clamp เอง)
            if (item.craftCost != null)
                foreach (var cost in item.craftCost)
                    if (cost.amount > 0f)
                        EventManager.Instance.RaiseResourceDelta(cost.type, -cost.amount);

            if (item.craftTicks <= 0)
            {
                CompleteCraft(item); // ของฉุกเฉิน (น้ำหล่อเย็น) — ได้ทันที
                return;
            }

            _craftQueue.Add(new CraftJob { itemId = item.id, progress = 0 });
            EventManager.Instance.RaiseCraftProgressChanged(item.id, 0, item.craftTicks);
        }

        /// <summary>คิวคราฟต์คืบต่อ tick — gate ต่อ job: อาคาร craftedAt ยังพร้อม+มีคน (ไม่พร้อม = ค้าง ไม่ยกเลิก)</summary>
        private void HandleGameTick()
        {
            if (_craftQueue.Count == 0) return;

            // snapshot กัน CompleteCraft ลบ entry ระหว่างลูป (แพทเทิร์น ConstructionController)
            var snapshot = new List<CraftJob>(_craftQueue);
            foreach (var job in snapshot)
            {
                var item = GetItem(job.itemId);
                if (item == null) { _craftQueue.Remove(job); continue; } // catalog เปลี่ยน — ทิ้ง job กำพร้า

                bool gateOpen = HasStaffedBuilding(item.craftedAt);
                int next = InventoryMath.StepProgress(job.progress, gateOpen);
                if (next == job.progress) continue; // gate ปิด — ไม่ยิง event ซ้ำทุก tick

                if (InventoryMath.IsComplete(next, item.craftTicks))
                {
                    _craftQueue.Remove(job);
                    CompleteCraft(item);
                }
                else
                {
                    job.progress = next;
                    EventManager.Instance.RaiseCraftProgressChanged(item.id, next, item.craftTicks);
                }
            }
        }

        private void CompleteCraft(ItemSO item)
        {
            _counts[item.id] = InventoryMath.AddClamped(GetCount(item.id), 1, item.maxStack);
            EventManager.Instance.RaiseItemCrafted(item);
            EventManager.Instance.RaiseInventoryChanged();
        }

        // ─────────────────────────────────────────
        //  Use (event → ลด count → กระจายผลผ่าน OnItemUsed)
        // ─────────────────────────────────────────

        private void HandleUseRequested(string itemId)
        {
            var item = GetItem(itemId);
            if (item == null || item.isPassive) return; // passive (Rad-Gear/PET) ถือไว้มีผล — กดใช้ไม่ได้
            if (!HasItem(itemId))
            {
                EventManager.Instance.RaiseNotice($"ไม่มี {item.displayName} ในคลัง");
                return;
            }

            _counts[itemId] = InventoryMath.RemoveClamped(GetCount(itemId), 1);
            EventManager.Instance.RaiseInventoryChanged();

            // ผลที่ยิงผ่าน event เดิมของระบบอื่นได้เลย — ทำจากที่นี่ (ไม่เรียก manager ตรง)
            if (item.curesAllSick)
                EventManager.Instance.RaisePopulationSickSet(0); // ไอโซโทปการแพทย์ — รักษาหายสนิท (วิกฤต 2·B)

            // ผลที่เหลือ (ลด HEAT / หยุดเน่า / โบนัสอาหาร) — manager เจ้าของ state subscribe OnItemUsed เอง
            EventManager.Instance.RaiseItemUsed(item);
        }

        // ทิ้งไอเทมทั้งสแต็ก (แผงกริด §13) — ไม่มีผลใช้งาน (ต่างจาก Use) แค่เอาออกจากคลัง
        private void HandleDiscardRequested(string itemId)
        {
            if (!_counts.ContainsKey(itemId)) return;
            _counts.Remove(itemId);
            EventManager.Instance.RaiseInventoryChanged();
        }

        // ─────────────────────────────────────────
        //  Save / Load
        // ─────────────────────────────────────────

        /// <summary>export สถานะสำหรับ SaveManager (read-only query — แพทเทิร์น ConstructionController.GetSaveState)</summary>
        public InventoryData GetSaveState()
        {
            var data = new InventoryData();
            foreach (var kvp in _counts)
            {
                if (kvp.Value <= 0) continue;
                data.itemIds.Add(kvp.Key);
                data.itemCounts.Add(kvp.Value);
            }
            foreach (var job in _craftQueue)
            {
                data.craftQueueIds.Add(job.itemId);
                data.craftQueueProgress.Add(job.progress);
            }
            return data;
        }

        private void HandleSaveLoaded(SaveData save)
        {
            _counts.Clear();
            _craftQueue.Clear();

            var inv = save.inventory;
            if (inv != null)
            {
                if (inv.itemIds != null)
                    for (int i = 0; i < inv.itemIds.Count; i++)
                    {
                        int count = (inv.itemCounts != null && i < inv.itemCounts.Count) ? inv.itemCounts[i] : 0;
                        if (count > 0) _counts[inv.itemIds[i]] = count;
                    }

                if (inv.craftQueueIds != null)
                    for (int i = 0; i < inv.craftQueueIds.Count; i++)
                    {
                        int prog = (inv.craftQueueProgress != null && i < inv.craftQueueProgress.Count)
                            ? inv.craftQueueProgress[i] : 0;
                        _craftQueue.Add(new CraftJob { itemId = inv.craftQueueIds[i], progress = prog });
                    }
            }

            EventManager.Instance.RaiseInventoryChanged();
        }

        // ─────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────

        // ป้ายชื่ออาคารสำหรับข้อความ toast (เฉพาะชนิดที่เป็นจุดคราฟต์ §13)
        private static string BuildingLabel(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Laboratory: return "ห้องวิจัย";
                case BuildingType.Hospital:   return "โรงพยาบาล";
                case BuildingType.WaterPlant: return "โรงกรองน้ำ";
                default:                      return type.ToString();
            }
        }
    }
}
