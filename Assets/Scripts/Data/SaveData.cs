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

        // ===== Food spoilage (CARDS.md การ์ด 3) — default ปิด/×1: เซฟเก่า = วิกฤตเสบียงยังไม่เกิด =====
        public bool spoilageActive = false;      // วิกฤตเสบียงเกิดแล้วหรือยัง (ก่อนหน้านั้นอาหารไม่เน่า)
        public float spoilMultiplier = 1f;       // ★ default 1 ไม่ใช่ 0 — 0 = หยุดเน่าถาวรในเซฟเก่า
        public bool co60Active = false;          // เลือกทางเลือก Co-60 แล้ว (Dorn D04)

        // ===== Data Recovery (STORY.md §3 — บันทึก Elara 4 ใบ) — default 0: เซฟเก่า = ยังไม่กู้ใบไหน =====
        public float dataRecoveryProgress = 0f;      // ความคืบหน้าใบถัดไป (0..dataRecoveryTarget)
        public int dataRecoveryRecords = 0;          // กู้ได้กี่ใบแล้ว (0..4) — restore ต้อง replay UnlockLead ด้วย

        // ===== Run stats (STORY.md §④ สรุปการเล่น) — เก็บเฉพาะค่าที่หายไปถ้าไม่จด =====
        public float statLowestHope = -1f;           // ★ default −1 ไม่ใช่ 0 — 0 = "Hope เคยตกถึงศูนย์" ในเซฟเก่า
        public int statLowestHopeDay = 0;            // วันที่ Hope ต่ำสุด (0 = ยังไม่เคยเก็บค่า)
        public int statDecreeOption = 0;             // 0 = ไม่ออกประกาศ · 1 = B · 2 = C
        public bool statTriageEncountered = false;   // เคยเจอการ์ด Triage ไหม (achievement "ไม่มีใครต้องเลือก")
        public int statZoneBWorkerDays = 0;          // วัน-คนที่อยู่ใน Zone B รวมทั้งรอบ (ตัวหารของ ALARA)
        public int statZoneBSuitedDays = 0;          // ในจำนวนนั้น ใส่ชุดกันรังสีกี่วัน-คน (ตัวตั้ง)

        // ===== Deuterium extraction (ResearchLab_System_Spec — ปุ่มสกัด) — default ว่าง = ปิดทุกโรง =====
        public List<Vector2Int> deuteriumExtractCells = new List<Vector2Int>(); // โรงน้ำที่เปิดสวิตช์สกัดไว้
        public bool deuteriumUnlockAnnounced = false;                            // แจ้งเตือน "ปลดล็อกแล้ว" ไปหรือยัง
    }
}
