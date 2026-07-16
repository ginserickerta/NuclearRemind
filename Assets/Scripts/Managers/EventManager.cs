using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// จัดการ event กลางของเกมทั้งหมด (Observer pattern, Singleton)
    /// ระบบอื่นต้อง subscribe/raise ผ่าน EventManager.Instance เท่านั้น ห้าม direct reference ข้าม Manager
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class EventManager : MonoBehaviour
    {
        public static EventManager Instance { get; private set; }

        // ===== Building =====
        public event Action<Cell, BuildingData> OnBuildingPlaced;
        public event Action<Vector2Int> OnBuildingRemoved;
        public event Action OnPlacementCancelled;
        public event Action<BuildingData> OnBuildingSelected;

        // ===== Building Upgrade / Coils (V4 §6/§7 — เฟส 6) =====
        public event Action<Vector2Int> OnUpgradeBuildingRequested; // UI → BuildingRegistry (อัประดับอาคาร)
        public event Action<Vector2Int, int> OnBuildingUpgraded;    // (cell, ระดับใหม่)
        public event Action OnUpgradeToroidalRequested;             // UI → CoreTower (Toroidal Coils +1)
        public event Action OnInstallPoloidalRequested;             // UI → CoreTower (Poloidal Coils)
        public event Action<int, bool> OnCoilsChanged;              // CoreTower → Story (Toroidal level, hasPoloidal) — v8.5 record เสริม

        // ===== Resources =====
        public event Action<ResourceData> OnResourceChanged;
        public event Action<ResourceType> OnResourceCritical;
        public event Action<ResourceType> OnResourceDepleted;
        public event Action<ResourceType, float> OnResourceDelta;

        // ===== Population / Morale (Hope เดี่ยว, V4 §9) =====
        public event Action<float> OnMoraleChanged;              // (hope) — สำหรับ HUD
        public event Action<PopulationData> OnPopulationChanged;
        public event Action OnWorkerStrike;                      // legacy (ไม่ใช้แล้ว — คงไว้กัน reference)
        public event Action<float> OnMoraleDelta;               // (hopeDelta) — จาก dilemma/decree
        public event Action OnTrainEngineerRequested;           // UI → PopulationManager (ฝึกวิศวกร)
        public event Action OnTrainMedicRequested;              // UI → PopulationManager (ฝึกแพทย์)
        public event Action OnTrainFarmerRequested;             // UI → PopulationManager (ฝึกเกษตรกร)
        public event Action<bool> OnClassTrained;               // (isEngineer) ฝึกสำเร็จ (จ่ายแล้ว รอจบวัน) → tutorial/feedback
        public event Action<int> OnEnactDecreeRequested;        // UI → DecreeManager (ประกาศฉุกเฉิน index)

        // ===== Worker Assignment (V4 §5 — จัดสรร Worker ประจำอาคาร) =====
        public event Action<Vector2Int, int> OnWorkerAssignRequested;   // UI → WorkerAssignmentManager (cell, ±1)
        public event Action<Vector2Int, int> OnWorkerAssignmentChanged; // manager → UI/visual (cell, จำนวนที่ประจำใหม่)
        public event Action<int, int> OnWorkerPoolChanged;              // manager → HUD/visual (idle, totalWorkers)

        // ===== Radiation (Story Guide §4 — วิกฤตโรครังสี "Zone A") =====
        public event Action<float> OnRadiationExposureChanged;  // RadiationManager → StoryDirector/HUD (exposure สะสม)
        public event Action<float> OnRadiationExposureDelta;    // OreDepositManager (ขุดโซน B) → RadiationManager (+exposure · ไม่ gate เตา)
        public event Action OnZoneBUnlocked;                    // StoryDirector (beat.unlocksZoneB) → ZoneBarrierRenderer/WAM (เปิดประตูโซน B ให้เดินเข้า/จัดคนได้)

        // ===== CORE TOWER =====
        public event Action<TowerData> OnTowerProgressChanged;
        public event Action<int> OnTowerPhaseComplete;
        public event Action OnTowerComplete;
        public event Action<int> OnOverclockModeRequested; // UI → CoreTowerManager (0..3)
        public event Action<int> OnOverclockModeChanged;   // CoreTowerManager → UI (โหมดปัจจุบัน)
        public event Action OnScramRequested;              // UI → CoreTowerManager (กดปุ่ม SCRAM)
        public event Action<ReactorAllocation, int> OnReactorAllocationAdjust; // UI → CoreTowerManager (จัดสรรเชื้อเพลิง/หล่อเย็น ±)

        // ===== Game State =====
        public event Action<GameManager.GameState> OnGameStateChanged;
        public event Action<GameEndType> OnGameOver;

        // ===== Day Cycle (§2.3 / §3) =====
        public event Action<int, bool> OnDayStarted; // (day, isTimed) — Day 1 = tutorial ไม่จับเวลา
        public event Action<int> OnDayProduction;    // (day ที่เพิ่งจบ) — batch ผลิต+บริโภค ยิงก่อน OnDayEnded เสมอ (V4 §3)
        public event Action<int> OnDayEnded;         // (day ที่เพิ่งจบ) — hook สำหรับ EndOfDay resolve / crisis
        public event Action<GameManager.DayPhase> OnDayPhaseChanged; // Planning 30s → Live 60s (V4 §3)

        // ===== Speed Control =====
        public event Action<float> OnSpeedChangeRequested; // UI → GameManager (0=pause, 1=normal, 2=fast)
        public event Action<float> OnSpeedChanged;         // GameManager → UI (effective timeScale ปัจจุบัน)

        // ===== Save/Load =====
        public event Action<SaveData> OnSaveLoaded;
        public event Action OnSaveRequested;
        public event Action OnLoadRequested;

        // ===== Narrative / Dilemma =====
        public event Action<DilemmaData> OnDilemmaTriggered;
        public event Action<DilemmaData, int> OnDilemmaResolved;  // (dilemma, choiceIndex 0=A/1=B/2=C)
        public event Action<int, int> OnRelationshipChanged; // (aethon, keran)

        // ===== Codex =====
        public event Action<CodexEntry> OnCodexEntryUnlocked;
        public event Action<CodexEntry> OnCodexUnlockFailed; // RP ไม่พอ
        public event Action<string> OnCodexUnlockRequested;  // Quiz (ตอบถูก) → CodexManager ปลดล็อกด้วย entryId (ไม่เรียก manager ตรง)

        // ===== Quiz / Knowledge (V4 §16 — เฟส 1) =====
        public event Action<float> OnKnowledgeChanged;        // (knowledge 0–100) — สำหรับ HUD/tier
        public event Action<QuizQuestionSO> OnQuizShown;      // QuizManager → QuizPopupController
        public event Action<string, bool> OnQuizAnswered;     // (quizId, correct) — ตอบเสร็จแล้ว

        // ===== Building Selection (UI → PlacementController) =====
        public event Action<BuildingData> OnBuildingSelectRequested; // UI กด → PlacementController เริ่มวาง
        public event Action<BuildingData> OnBuildingDragStarted;     // ลากช่อง hotbar → เริ่มวางแบบ drag (drop=วาง)
        public event Action OnBuildingDragDropped;                   // ปล่อยเมาส์จบ drag → วาง/ยกเลิกตามตำแหน่ง

        // ===== Game Tick =====
        public event Action OnGameTick; // raised by ResourceManager ทุก tickInterval

        // ===== Construction =====
        public event Action<Vector2Int, BuildingData> OnConstructionComplete;
        public event Action<Vector2Int, int> OnConstructionProgressChanged; // (cell, progress 0-10)
        public event Action<Vector2Int> OnConstructionCancelRequested;    // BuildingQueueUI → ConstructionController
        public event Action<Vector2Int> OnConstructionPrioritizeRequested; // BuildingQueueUI → ConstructionController

        // ===== Tutorial =====
        public event Action OnTutorialComplete;

        // ===== Overdrive =====
        public event Action<bool> OnOverdriveToggled;  // true = activate
        public event Action<float> OnTowerDamaged;     // remaining durability

        // ===== Demolition =====
        public event Action<bool> OnDemolishModeToggled; // true = enter demolish mode

        // ===== Power Grid =====
        public event Action<HashSet<Vector2Int>> OnPowerGridChanged; // ชุด cell ทั้งหมดที่ได้รับพลังงาน

        // ===== Notice (toast แจ้งเหตุผลจากการกระทำผู้เล่น เช่น ฝึกคลาสไม่ได้ — AlertController แสดง) =====
        public event Action<string> OnNotice;

        // ===== Population Deaths (V4 §9: คนตาย → Hope −5/คน · จากทางเลือกวิกฤต/Decree) =====
        public event Action<int> OnPopulationDeaths; // จำนวนคนที่เสียชีวิต

        // ===== Population Sick (Story Guide §4 วิกฤตโรครังสี — CrisisEffectManager → PopulationManager) =====
        public event Action<int> OnPopulationSickInjected; // +n คนป่วย (เช่น Plasma B วิศวกร 2 คน)
        public event Action<int> OnPopulationSickSet;      // ตั้งจำนวนป่วยเป็น n (เช่น Outbreak A→5, B→0)
        public event Action<int> OnPopulationSickCured;    // รักษาป่วยสูงสุด n คน (ResearchLab_Spec: ยาไอโซโทป ≤15)

        // ===== Research (ResearchLab_Spec — โครงการวิจัย 3 อันของห้องวิจัย · ResearchManager) =====
        public event Action<string> OnResearchRequested; // UI → ResearchManager (projectId: seeds/isotope/core_tower)
        public event Action<string> OnResearchCompleted; // ResearchManager → CoreTower(gate)/CrisisEffect(ผลผลิต)/UI

        // ===== Pre-placed (ตึกที่มากับแมพ เช่น CORE TOWER กลางเมือง — สร้างเสร็จทันที ไม่เข้าคิวก่อสร้าง) =====
        public event Action<Vector2Int> OnConstructionCompleteRequested;

        // ===== Inventory (GDD §13 — ไอเทมคราฟต์ · InventoryManager) =====
        public event Action<string> OnCraftItemRequested;   // UI → InventoryManager (itemId)
        public event Action<string> OnUseItemRequested;     // UI → InventoryManager (itemId)
        public event Action<string> OnDiscardItemRequested; // UI → InventoryManager (ทิ้งไอเทมทั้งสแต็ก)
        public event Action<ItemSO> OnItemCrafted;          // InventoryManager → UI/feedback (ได้ของแล้ว)
        public event Action<ItemSO> OnItemUsed;             // InventoryManager → CoreTower/CrisisEffect (ผลไอเทมตาม field ใน asset)
        public event Action OnInventoryChanged;             // จำนวนถือครองเปลี่ยน → UI refresh
        public event Action<string, int, int> OnCraftProgressChanged; // (itemId, progress, craftTicks) — คิวคราฟต์คืบ

        // ===== Story (Story Guide — StoryDirector คุมลำดับ record → infoCard → crisis → outcome → quiz) =====
        public event Action<DilemmaData> OnDilemmaTriggerRequested; // StoryDirector → DilemmaManager (วิกฤตเข้า pipeline ปกติ)
        public event Action<RecordCardSO> OnStoryRecordShown;       // StoryDirector → Card UI (การ์ดบันทึกกู้คืน)
        public event Action<InfoCardSO> OnStoryInfoShown;           // StoryDirector → Card UI (การ์ดความรู้ — ก่อนควิซเสมอ)
        public event Action<string> OnStoryOutcomeShown;            // StoryDirector → Card UI (บทหลังเลือก afterText)
        public event Action<DialogueLine[]> OnStoryDialogueShown;   // StoryDirector → Dialogue UI (บทสนทนาหลายตัวละคร v8.5)
        public event Action OnStoryCardDismissed;                   // Card UI / Dialogue UI → StoryDirector (ปิดการ์ด/จบบท → เดินลำดับต่อ)
        public event Action<RecordCardSO> OnRecordArchived;         // StoryDirector → RecordsPanel (บันทึกเข้าแผงย้อนอ่าน)
        public event Action<RecordCardSO> OnRecordArchiveRequested; // RecordCardUI → StoryDirector (กดปุ่ม "เก็บเข้าแผง Record")

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ===== Building =====
        public void RaiseBuildingPlaced(Cell cell, BuildingData data) => OnBuildingPlaced?.Invoke(cell, data);
        public void RaiseBuildingRemoved(Vector2Int position) => OnBuildingRemoved?.Invoke(position);
        public void RaisePlacementCancelled() => OnPlacementCancelled?.Invoke();
        public void RaiseBuildingSelected(BuildingData data) => OnBuildingSelected?.Invoke(data);
        public void RaiseUpgradeBuildingRequested(Vector2Int cell) => OnUpgradeBuildingRequested?.Invoke(cell);
        public void RaiseBuildingUpgraded(Vector2Int cell, int level) => OnBuildingUpgraded?.Invoke(cell, level);
        public void RaiseUpgradeToroidalRequested() => OnUpgradeToroidalRequested?.Invoke();
        public void RaiseInstallPoloidalRequested() => OnInstallPoloidalRequested?.Invoke();
        public void RaiseCoilsChanged(int toroidalLevel, bool hasPoloidal) => OnCoilsChanged?.Invoke(toroidalLevel, hasPoloidal);

        // ===== Resources =====
        public void RaiseResourceChanged(ResourceData data) => OnResourceChanged?.Invoke(data);
        public void RaiseResourceCritical(ResourceType type) => OnResourceCritical?.Invoke(type);
        public void RaiseResourceDepleted(ResourceType type) => OnResourceDepleted?.Invoke(type);
        public void RaiseResourceDelta(ResourceType type, float amount) => OnResourceDelta?.Invoke(type, amount);

        // ===== Population =====
        public void RaiseMoraleChanged(float hope) => OnMoraleChanged?.Invoke(hope);
        public void RaisePopulationChanged(PopulationData data) => OnPopulationChanged?.Invoke(data);
        public void RaiseWorkerStrike() => OnWorkerStrike?.Invoke();
        public void RaiseZoneBUnlocked() => OnZoneBUnlocked?.Invoke();
        public void RaiseMoraleDelta(float hopeDelta) => OnMoraleDelta?.Invoke(hopeDelta);
        public void RaiseTrainEngineerRequested() => OnTrainEngineerRequested?.Invoke();
        public void RaiseTrainMedicRequested() => OnTrainMedicRequested?.Invoke();
        public void RaiseTrainFarmerRequested() => OnTrainFarmerRequested?.Invoke();
        public void RaiseClassTrained(bool isEngineer) => OnClassTrained?.Invoke(isEngineer);
        public void RaiseEnactDecreeRequested(int index) => OnEnactDecreeRequested?.Invoke(index);

        // ===== Worker Assignment =====
        public void RaiseWorkerAssignRequested(Vector2Int cell, int delta) => OnWorkerAssignRequested?.Invoke(cell, delta);
        public void RaiseWorkerAssignmentChanged(Vector2Int cell, int newCount) => OnWorkerAssignmentChanged?.Invoke(cell, newCount);
        public void RaiseWorkerPoolChanged(int idle, int total) => OnWorkerPoolChanged?.Invoke(idle, total);

        // ===== Radiation =====
        public void RaiseRadiationExposureChanged(float exposure) => OnRadiationExposureChanged?.Invoke(exposure);
        public void RaiseRadiationExposureDelta(float amount) => OnRadiationExposureDelta?.Invoke(amount);

        // ===== CORE TOWER =====
        public void RaiseTowerProgressChanged(TowerData data) => OnTowerProgressChanged?.Invoke(data);
        public void RaiseTowerPhaseComplete(int phase) => OnTowerPhaseComplete?.Invoke(phase);
        public void RaiseTowerComplete() => OnTowerComplete?.Invoke();
        public void RaiseOverclockModeRequested(int mode) => OnOverclockModeRequested?.Invoke(mode);
        public void RaiseOverclockModeChanged(int mode) => OnOverclockModeChanged?.Invoke(mode);
        public void RaiseScramRequested() => OnScramRequested?.Invoke();
        public void RaiseReactorAllocationAdjust(ReactorAllocation kind, int delta) => OnReactorAllocationAdjust?.Invoke(kind, delta);

        // ===== Game State =====
        public void RaiseGameStateChanged(GameManager.GameState newState) => OnGameStateChanged?.Invoke(newState);
        public void RaiseGameOver(GameEndType endType) => OnGameOver?.Invoke(endType);

        // ===== Day Cycle =====
        public void RaiseDayStarted(int day, bool timed) => OnDayStarted?.Invoke(day, timed);
        public void RaiseDayProduction(int day) => OnDayProduction?.Invoke(day);
        public void RaiseDayEnded(int day) => OnDayEnded?.Invoke(day);
        public void RaiseDayPhaseChanged(GameManager.DayPhase phase) => OnDayPhaseChanged?.Invoke(phase);

        // ===== Speed Control =====
        public void RaiseSpeedChangeRequested(float speed) => OnSpeedChangeRequested?.Invoke(speed);
        public void RaiseSpeedChanged(float speed) => OnSpeedChanged?.Invoke(speed);

        // ===== Save/Load =====
        public void RaiseSaveLoaded(SaveData data) => OnSaveLoaded?.Invoke(data);
        public void RaiseSaveRequested() => OnSaveRequested?.Invoke();
        public void RaiseLoadRequested() => OnLoadRequested?.Invoke();

        // ===== Narrative / Dilemma =====
        public void RaiseDilemmaTriggered(DilemmaData data) => OnDilemmaTriggered?.Invoke(data);
        public void RaiseDilemmaResolved(DilemmaData data, int choiceIndex) => OnDilemmaResolved?.Invoke(data, choiceIndex);
        public void RaiseRelationshipChanged(int aethon, int keran) => OnRelationshipChanged?.Invoke(aethon, keran);

        // ===== Codex =====
        public void RaiseCodexEntryUnlocked(CodexEntry entry) => OnCodexEntryUnlocked?.Invoke(entry);
        public void RaiseCodexUnlockFailed(CodexEntry entry) => OnCodexUnlockFailed?.Invoke(entry);
        public void RaiseCodexUnlockRequested(string entryId) => OnCodexUnlockRequested?.Invoke(entryId);

        // ===== Quiz / Knowledge =====
        public void RaiseKnowledgeChanged(float knowledge) => OnKnowledgeChanged?.Invoke(knowledge);
        public void RaiseQuizShown(QuizQuestionSO quiz) => OnQuizShown?.Invoke(quiz);
        public void RaiseQuizAnswered(string quizId, bool correct) => OnQuizAnswered?.Invoke(quizId, correct);

        // ===== Building Selection =====
        public void RaiseBuildingSelectRequested(BuildingData data) => OnBuildingSelectRequested?.Invoke(data);
        public void RaiseBuildingDragStarted(BuildingData data) => OnBuildingDragStarted?.Invoke(data);
        public void RaiseBuildingDragDropped() => OnBuildingDragDropped?.Invoke();

        // ===== Game Tick =====
        public void RaiseGameTick() => OnGameTick?.Invoke();

        // ===== Construction =====
        public void RaiseConstructionComplete(Vector2Int cell, BuildingData data) => OnConstructionComplete?.Invoke(cell, data);
        public void RaiseConstructionProgressChanged(Vector2Int cell, int progress) => OnConstructionProgressChanged?.Invoke(cell, progress);
        public void RaiseConstructionCancelRequested(Vector2Int cell) => OnConstructionCancelRequested?.Invoke(cell);
        public void RaiseConstructionPrioritizeRequested(Vector2Int cell) => OnConstructionPrioritizeRequested?.Invoke(cell);

        // ===== Tutorial =====
        public void RaiseTutorialComplete() => OnTutorialComplete?.Invoke();

        // ===== Overdrive =====
        public void RaiseOverdriveToggled(bool active) => OnOverdriveToggled?.Invoke(active);
        public void RaiseTowerDamaged(float durability) => OnTowerDamaged?.Invoke(durability);

        // ===== Demolition =====
        public void RaiseDemolishModeToggled(bool active) => OnDemolishModeToggled?.Invoke(active);

        // ===== Power Grid =====
        public void RaisePowerGridChanged(HashSet<Vector2Int> powered) => OnPowerGridChanged?.Invoke(powered);

        // ===== Notice =====
        public void RaiseNotice(string message) => OnNotice?.Invoke(message);

        // ===== Population Deaths =====
        public void RaisePopulationDeaths(int count) => OnPopulationDeaths?.Invoke(count);

        // ===== Population Sick =====
        public void RaisePopulationSickInjected(int count) => OnPopulationSickInjected?.Invoke(count);
        public void RaisePopulationSickSet(int count) => OnPopulationSickSet?.Invoke(count);
        public void RaisePopulationSickCured(int count) => OnPopulationSickCured?.Invoke(count);

        // ===== Research =====
        public void RaiseResearchRequested(string projectId) => OnResearchRequested?.Invoke(projectId);
        public void RaiseResearchCompleted(string projectId) => OnResearchCompleted?.Invoke(projectId);

        // ===== Pre-placed =====
        public void RaiseConstructionCompleteRequested(Vector2Int cell) => OnConstructionCompleteRequested?.Invoke(cell);

        // ===== Inventory =====
        public void RaiseCraftItemRequested(string itemId) => OnCraftItemRequested?.Invoke(itemId);
        public void RaiseUseItemRequested(string itemId) => OnUseItemRequested?.Invoke(itemId);
        public void RaiseDiscardItemRequested(string itemId) => OnDiscardItemRequested?.Invoke(itemId);
        public void RaiseItemCrafted(ItemSO item) => OnItemCrafted?.Invoke(item);
        public void RaiseItemUsed(ItemSO item) => OnItemUsed?.Invoke(item);
        public void RaiseInventoryChanged() => OnInventoryChanged?.Invoke();
        public void RaiseCraftProgressChanged(string itemId, int progress, int total) => OnCraftProgressChanged?.Invoke(itemId, progress, total);

        // ===== Story =====
        public void RaiseDilemmaTriggerRequested(DilemmaData dilemma) => OnDilemmaTriggerRequested?.Invoke(dilemma);
        public void RaiseStoryRecordShown(RecordCardSO record) => OnStoryRecordShown?.Invoke(record);
        public void RaiseStoryInfoShown(InfoCardSO infoCard) => OnStoryInfoShown?.Invoke(infoCard);
        public void RaiseStoryOutcomeShown(string afterText) => OnStoryOutcomeShown?.Invoke(afterText);
        public void RaiseStoryDialogueShown(DialogueLine[] lines) => OnStoryDialogueShown?.Invoke(lines);
        public void RaiseStoryCardDismissed() => OnStoryCardDismissed?.Invoke();
        public void RaiseRecordArchived(RecordCardSO record) => OnRecordArchived?.Invoke(record);
        public void RaiseRecordArchiveRequested(RecordCardSO record) => OnRecordArchiveRequested?.Invoke(record);
    }
}
