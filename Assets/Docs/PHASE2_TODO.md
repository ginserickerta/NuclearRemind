# Phase 2 — Core Systems (Day 5–9, ตาม ImprovePlan1.csv)

> เขียนเมื่อจบ Day 7 (Review & Buffer — Phase 1), ปรับปรุงหลัง migration สถาปัตยกรรม Original -> Improve (2026-06-14)
> อ้างอิงแผนเต็ม: `Assets/Docs/ImprovePlan1.csv` (canonical), `Assets/Docs/ImproveCLAUDE.md` (convention)

## สถานะเมื่อจบ Phase 1 (Day 1–7)

| ระบบ | ไฟล์ | สถานะ |
|---|---|---|
| GameManager (state + pause) | `Scripts/Managers/GameManager.cs` | ✅ |
| GridManager (12×9 iso + Gizmos) | `Scripts/Systems/GridManager.cs` | ✅ |
| Camera (WASD + zoom) | `Scripts/Systems/CameraController.cs` | ✅ |
| Input (click → grid) | `Scripts/Systems/InputManager.cs` | ✅ |
| Tilemap (Ground/Buildings/FogOfWar + sorting layers) | `Scenes/Gamescene.unity` | ⚠️ รอ sprite จริง |
| Placement (ghost + footprint) | `Scripts/Systems/PlacementController.cs` | ✅ |
| BuildingData (SO) | `Scripts/Data/BuildingData.cs` | ✅, schema ใหม่ |
| EventManager (Observer, Singleton) | `Scripts/Managers/EventManager.cs` | ✅, converted |
| ResourceManager (stub) | `Scripts/Managers/ResourceManager.cs` | stub, Day 5 |
| UIManagerHUD (stub) | `Scripts/UI/UIManagerHUD.cs` | stub, Day 7 |
| EditMode Tests (GridManager) | `Tests/EditMode/GridManagerTests.cs` | ✅ |
| Assembly Definitions | `Scripts/NuclearReMind.asmdef` | ✅ |

## ค้างจาก Phase 1 (ทำเองใน Editor)
- [ ] Import sprite tiles/buildings → วาด map Veltara 12×9 บน Tilemap "Ground"
- [x] สร้าง `BuildingData` asset จริง (`Assets/ScriptableObjects/Buildings/`: Habitat, Farm, WaterPlant, PowerPlant, RadiationShelter, Laboratory, CoreTower) — ยังไม่มี sprite รอ sprite จริง
- [x] รัน EditMode tests ใน Test Runner ให้เขียวครบ — ผ่านครบ 8/8 (รันผ่าน Unity batchmode)
- [ ] ปรับ tileWidth/tileHeight/originOffset ให้ตรง sprite จริง

## Migration completed (2026-06-14)

- โครงสร้างโฟลเดอร์: `Assets/Script/{Core,Grid,Buildings,Resource,UI}` -> `Assets/Scripts/{Managers,Systems,UI,Data,Utils}`
- `EventManager` เปลี่ยนจาก static class เป็น `MonoBehaviour` Singleton (`[DefaultExecutionOrder(-100)]`) — เรียกผ่าน `EventManager.Instance.RaiseXxx()` / `EventManager.Instance.OnXxx`
- `BuildingData` schema ใหม่: `icon` -> `sprite`, ลบ `ResourceCost` class (รวมเป็น `materialCost`/`energyCost`/`workerRequired`), เพิ่ม production fields (`foodProduction`, `waterProduction`, `radiationProtectionBonus`, `energyProduction`, `researchPointsPerTick`), เพิ่ม `nuclearKnowledge` (tooltip layer 3), เพิ่ม `isCoreTowerPart`/`towerPhaseRequired`
- `Assets/ScriptableObjects/BuildingData/` -> `Assets/ScriptableObjects/Buildings/` (7 assets, GUID เดิม, ปรับ field ตาม schema ใหม่ + เพิ่ม nuclearKnowledge ภาษาไทย)
- เพิ่ม Data files ใหม่ใน `Scripts/Data/`: `ResourceData`, `PopulationData`, `TowerData`, `SaveData`, enum `ResourceType`, `GameEndType`
- `Narrative/` folder ยังไม่สร้าง — เลื่อนไป Phase 3 (สร้างเมื่อมีไฟล์แรก)

## Phase 2 Tasks (Day 5–9)

### Day 5 — ResourceManager + PopulationManager
- ResourceManager: Singleton, ติดตาม Food / Water / RadiationProtection / Energy / Workers ผ่าน `ResourceData`, `Tick()` ทุก game turn, subscribe `EventManager.Instance.OnBuildingPlaced` → หักต้นทุนตาม `BuildingData.materialCost/energyCost/workerRequired`, บวก production ตาม `foodProduction`/`waterProduction`/...
- PopulationManager: `PopulationData` (total/trust/isOnStrike), Trust decay เมื่อทรัพยากรขาด; Trust < 20 → `EventManager.Instance.RaiseWorkerStrike()`

### Day 6 — CoreTowerManager + SaveManager ✅ Milestone
- CoreTowerManager: `TowerData` (currentPhase/phaseProgress), 3 phase (Foundation / Reactor Core / Fusion Activation) + progress bar + win condition ผ่าน `EventManager.Instance.RaiseTowerPhaseComplete`/`RaiseTowerComplete`
- SaveManager: JSON save/load ของ `SaveData` (resources, population, tower, placedBuildings, unlockedCodexEntries, ฯลฯ)

### Day 7 — HUD (UIManagerHUD + data binding)
- resource bar, CORE TOWER progress, trust meter; subscribe `EventManager.Instance.OnResourceChanged`/`OnTrustChanged`/`OnTowerPhaseComplete` อัปเดต real-time

### Day 8 — Tooltip (3 ชั้น) + Moral Dilemma
- TooltipController: ชั้น 1 ชื่อ+cost / ชั้น 2 `description` (gameplay) / ชั้น 3 `nuclearKnowledge` (ความรู้จริง)
- MoralDilemmaManager: `DilemmaData` SO + Popup UI + consequence ต่อ Trust/Resources

### Day 9 — Integration milestone ✅ Milestone
- ทดสอบ flow รวมทั้งหมด (placement -> resource -> population -> tower -> save/load -> HUD -> tooltip -> dilemma), commit + tag `v0.1-prototype`

Day 10 เป็นต้นไป: ต่อตาม `Assets/Docs/ImprovePlan1.csv` (Content phase, เลขวันเดิม ไม่เปลี่ยน)

## หมายเหตุสถาปัตยกรรม (จาก Day 7, อัปเดตหลัง migration)
- โค้ดเกมทั้งหมดอยู่ใน assembly `NuclearReMind` (asmdef) — Manager ใหม่ทุกตัววางใน `Assets/Scripts/Managers|Systems|UI|Data|Utils/...` ตามหน้าที่ เพื่อให้อยู่ assembly เดียวกัน
- ใช้ `EventManager.Instance` ส่ง event เสมอ ห้าม direct reference ข้าม Manager
- Manager ที่ subscribe event ต้อง +=/-= ใน `OnEnable`/`OnDisable` พร้อม null-guard `EventManager.Instance == null` ใน `OnDisable` (ดู `EventTestSubscriber.cs` เป็นแบบ)
- ข้อมูลที่ปรับได้ (stats/dilemma/codex) ต้องเป็น ScriptableObject
