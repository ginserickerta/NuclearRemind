using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// จัดการ Learning Codex: ติดตาม entry ที่ unlock แล้ว, ตรวจสอบ Research Points
    /// entry unlock ได้สองทาง: (1) ใช้ RP ซื้อ, (2) auto-unlock เมื่อ event ตรงกับ unlockedByEvent
    /// </summary>
    public class CodexManager : MonoBehaviour
    {
        public static CodexManager Instance { get; private set; }

        [Header("Codex Content — ใส่ CodexEntry assets ทั้งหมดที่นี่")]
        public CodexEntry[] allCodexEntries;

        private readonly HashSet<string> _unlockedIds = new HashSet<string>();
        private readonly Dictionary<string, CodexEntry> _entryById = new Dictionary<string, CodexEntry>();

        public IReadOnlyCollection<string> UnlockedIds => _unlockedIds;

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
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnTowerPhaseComplete -= HandleTowerPhaseComplete;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
        }

        /// <summary>
        /// ผู้เล่นกด unlock entry โดยใช้ Research Points
        /// ถ้า RP ไม่พอ จะ raise OnCodexUnlockFailed
        /// </summary>
        public void TryUnlock(CodexEntry entry)
        {
            if (entry == null || _unlockedIds.Contains(entry.entryId))
                return;

            float currentRP = ResourceManager.Instance.Current.researchPoints;

            if (entry.researchPointCost > 0 && currentRP < entry.researchPointCost)
            {
                EventManager.Instance.RaiseCodexUnlockFailed(entry);
                return;
            }

            if (entry.researchPointCost > 0)
                EventManager.Instance.RaiseResourceDelta(ResourceType.ResearchPoints, -entry.researchPointCost);

            Unlock(entry);
        }

        private void Unlock(CodexEntry entry)
        {
            if (_unlockedIds.Add(entry.entryId))
                EventManager.Instance.RaiseCodexEntryUnlocked(entry);
        }

        private void HandleTowerPhaseComplete(int phase)
        {
            AutoUnlockByEvent($"phase_{phase}_complete");
        }

        private void AutoUnlockByEvent(string eventId)
        {
            foreach (var entry in allCodexEntries)
            {
                if (entry == null) continue;
                if (entry.unlockedByEvent == eventId && entry.researchPointCost == 0)
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
