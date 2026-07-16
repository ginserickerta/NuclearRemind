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
        public List<int> archivedRecordDays = new List<int>();      // วันเกมที่กู้คืนบันทึกแต่ละใบ (คู่ index กับ archivedRecords · เซฟเก่าไม่มี = ว่าง → ไม่โชว์วัน)
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

        // ===== Worker Assignment (V4 §5 — จัดสรร Worker ประจำอาคาร) — default ว่าง: เซฟเก่าโหลดได้ (ทุกคน idle) =====
        public List<Vector2Int> workerAssignmentCells = new List<Vector2Int>();  // คู่ index กับ workerAssignmentCounts
        public List<int> workerAssignmentCounts = new List<int>();               // จำนวน Worker ประจำอาคารที่ cell นั้น

        // ===== Inventory (GDD §13 — ไอเทมคราฟต์) — default ว่าง: เซฟเก่าโหลดได้ (คลังว่าง) =====
        public InventoryData inventory = new InventoryData();

        // ===== Research (ResearchLab_Spec — โครงการวิจัย 3 อัน) — default false: เซฟเก่า = ยังไม่วิจัย =====
        public bool researchSeedsDone = false;      // เมล็ดพันธุ์ฉายรังสี (ผลผลิตฟาร์ม +100% ถาวร)
        public bool researchIsotopeDone = false;    // ยาไอโซโทปการแพทย์ (รักษาป่วย ≤15)
        public bool researchCoreUnlockDone = false; // ปลดล็อก CORE TOWER (เงื่อนไขก่อนเดินเตา)
        public bool researchIsotopePending = false; // จ่ายค่ายาไอโซโทปแล้ว — รอวันที่เตาโหมด Idle
    }
}
