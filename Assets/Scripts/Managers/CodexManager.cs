using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// จัดการ Learning Codex (V4 §9 — D2): entry ปลดล็อกด้วย "event" หรือการอ่าน (ไม่จ่าย Knowledge)
    /// ทุกครั้งที่ปลดล็อก → +2 Knowledge (สะสม ไม่ใช้จ่าย) — Knowledge มาจากควิซ/อ่าน Codex
    /// </summary>
    public class CodexManager : MonoBehaviour
    {
        public static CodexManager Instance { get; private set; }

        [Header("Codex Content — ใส่ CodexEntry assets ทั้งหมดที่นี่")]
        public CodexEntry[] allCodexEntries;

        private readonly HashSet<string> _unlockedIds = new HashSet<string>();
        private readonly Dictionary<string, CodexEntry> _entryById = new Dictionary<string, CodexEntry>();

        public IReadOnlyCollection<string> UnlockedIds => _unlockedIds;

        public bool IsUnlocked(string entryId) => _unlockedIds.Contains(entryId);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            foreach (var entry in allCodexEntries)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.entryId))
                    _entryById[entry.entryId] = entry;
            }
        }

        private void OnEnable()
        {
            EventManager.Instance.OnTowerPhaseComplete += HandleTowerPhaseComplete;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
            EventManager.Instance.OnBuildingPlaced += HandleBuildingPlaced;
            EventManager.Instance.OnBuildingUpgraded += HandleBuildingUpgraded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnTowerPhaseComplete -= HandleTowerPhaseComplete;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            EventManager.Instance.OnBuildingPlaced -= HandleBuildingPlaced;
            EventManager.Instance.OnBuildingUpgraded -= HandleBuildingUpgraded;
        }

        /// <summary>
        /// ผู้เล่นเปิด/อ่าน entry → ปลดล็อก (V4: ไม่มีค่าใช้จ่าย Knowledge)
        /// </summary>
        public void TryUnlock(CodexEntry entry)
        {
            if (entry == null || _unlockedIds.Contains(entry.entryId))
                return;

            Unlock(entry);
        }

        /// <summary>ปลดล็อกด้วย id (เรียกจาก QuizManager เมื่อตอบควิซ — เฟส 1)</summary>
        public void UnlockById(string entryId)
        {
            if (string.IsNullOrEmpty(entryId)) return;
            if (_entryById.TryGetValue(entryId, out var entry))
                Unlock(entry);
        }

        /// <summary>คืน Codex ที่ปลดล็อกข้ามรอบ (MetaProgress §9) — เงียบ ไม่ raise event / ไม่บวก Knowledge</summary>
        public void RestoreUnlocked(IEnumerable<string> ids)
        {
            if (ids == null) return;
            foreach (var id in ids)
                if (!string.IsNullOrEmpty(id) && _entryById.ContainsKey(id))
                    _unlockedIds.Add(id);
        }

        private void Unlock(CodexEntry entry)
        {
            if (_unlockedIds.Add(entry.entryId))
            {
                EventManager.Instance.RaiseCodexEntryUnlocked(entry);
                EventManager.Instance.RaiseResourceDelta(ResourceType.Knowledge, 2f); // V4 §9: อ่าน Codex +2 (ครั้งแรกครั้งเดียว)
                if (Application.isPlaying) // กัน EditMode tests เขียน PlayerPrefs จริง
                    MetaProgress.AddCodex(entry.entryId); // Codex_Spec §5: เข้าคลังถาวรทันที ไม่รอจบเกม (§16)
            }
        }

        private void HandleTowerPhaseComplete(int phase)
        {
            AutoUnlockByEvent($"phase_{phase}_complete");
        }

        // ── ใบความรู้จากห้องวิจัย (V4 §6): สร้าง Lab → ปลดชุดแรก · อัป L2/L3 → ปลดชุดลึกขึ้น ──
        private void HandleBuildingPlaced(Cell cell, BuildingData data)
        {
            if (data != null && data.buildingType == BuildingType.Laboratory)
                AutoUnlockByEvent("lab_built");
        }

        private void HandleBuildingUpgraded(Vector2Int cellPos, int newLevel)
        {
            var registry = BuildingRegistry.Instance;
            if (registry == null || !registry.PlacedBuildings.TryGetValue(cellPos, out var data) || data == null)
                return;
            if (data.buildingType != BuildingType.Laboratory) return;

            AutoUnlockByEvent($"lab_l{newLevel}");
        }

        private void AutoUnlockByEvent(string eventId)
        {
            foreach (var entry in allCodexEntries)
            {
                if (entry == null) continue;
                if (entry.unlockedByEvent == eventId)
                    Unlock(entry);
            }
        }

        private void HandleSaveLoaded(SaveData save)
        {
            _unlockedIds.Clear();
            if (save.unlockedCodexEntries == null) return;

            foreach (var id in save.unlockedCodexEntries)
            {
                if (_entryById.TryGetValue(id, out var entry))
                    _unlockedIds.Add(entry.entryId);
            }
        }

        /// <summary>
        /// คืน CodexEntry ที่ unlock แล้วทั้งหมด (ใช้โดย CodexUIController)
        /// </summary>
        public IEnumerable<CodexEntry> GetUnlockedEntries()
        {
            foreach (var id in _unlockedIds)
            {
                if (_entryById.TryGetValue(id, out var entry))
                    yield return entry;
            }
        }

        /// <summary>
        /// คืน CodexEntry ทั้งหมด (ทั้ง locked และ unlocked) — ใช้แสดง "preview" ใน Codex panel
        /// </summary>
        public IReadOnlyDictionary<string, CodexEntry> AllEntries => _entryById;
    }
}
