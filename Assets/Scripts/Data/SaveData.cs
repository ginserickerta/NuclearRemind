using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ข้อมูลทั้งหมดที่ใช้ save/load เกม (JSON ผ่าน SaveManager - Phase 2 Day 6)
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public ResourceData resources;
        public PopulationData population;
        public TowerData tower;
        public List<Vector2Int> placedBuildings;
        public List<string> buildingTypes;
        public List<string> unlockedCodexEntries;
        public float gameTime;
        public int aethonRelationship;
        public int keranRelationship;
        public List<Vector2Int> underConstructionCells = new List<Vector2Int>();
        public List<int> constructionProgress = new List<int>();

        // ===== Story (Story Guide) — มีค่า default เสมอ: เซฟรุ่นเก่ายังโหลดได้ =====
        public List<string> firedStoryBeats = new List<string>();  // beatId ที่เล่นไปแล้ว (ยิงครั้งเดียวต่อรอบ)
        public List<string> archivedRecords = new List<string>();  // recordId ที่กู้คืนแล้ว (แผง Records)
        public List<string> deferredCrisisKeys = new List<string>(); // วิกฤตซ้อนที่รอวันยิง (คู่ index กับ deferredCrisisDays)
        public List<int> deferredCrisisDays = new List<int>();       // วันที่จะยิง (เลขวันเกม)

        public float radiationExposure; // ค่าเสี่ยงรังสีสะสม (RadiationManager §4) — default 0: เซฟเก่าโหลดได้

        // ===== Crisis Effects (Story Guide §4 — CrisisEffectManager) — default ปลอดภัย: เซฟเก่าโหลดได้ =====
        // ★ multiplier ต้องเริ่ม 1f (ไม่ใช่ 0) ไม่งั้นเซฟเก่าจะทำให้ผลผลิตอาหารเป็น 0
        public float foodYieldMultiplier = 1f;
        public float foodSpoilRatePerDay = 0f;
        public float workerEfficiencyMultiplier = 1f;
        public int workerEfficiencyDaysRemaining = 0;
        public List<int> busyWorkerCounts = new List<int>();   // คู่ index กับ busyWorkerDays
        public List<int> busyWorkerDays = new List<int>();
        public List<float> hopeDrainPerDay = new List<float>(); // คู่ index กับ hopeDrainDays
        public List<int> hopeDrainDays = new List<int>();
    }
}
