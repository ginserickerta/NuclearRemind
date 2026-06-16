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
- [x] Import sprite tiles/buildings → วาด map Veltara 12×9 บน Tilemap "Ground" — `PlaceholderSpriteGenerator`
      สร้าง `Ground.png` (128×64, iso diamond) + `Ground.asset` (Tile) แล้ววาดเต็ม 12×9 (108 ทไทล์) บน
      Tilemap "Ground" ใน `Gamescene.unity` (placeholder รอ sprite จริงจากทีม Art)
- [x] สร้าง `BuildingData` asset จริง (`Assets/ScriptableObjects/Buildings/`: Habitat, Farm, WaterPlant, PowerPlant, RadiationShelter, Laboratory, CoreTower) — ใส่ placeholder sprite ต่ออาคารแล้ว (ดูหัวข้อ "Building visuals" ด้านล่าง) รอ sprite จริง
- [x] รัน EditMode tests ใน Test Runner ให้เขียวครบ — ผ่านครบ 8/8 (รันผ่าน Unity batchmode)
- [x] ปรับ tileWidth/tileHeight/originOffset ให้ตรง sprite จริง — `GridManager` ใช้ `tileWidth=1, tileHeight=0.5,
      originOffset=(0,0,0)`; Grid component ตั้ง `cellSize=(1,0.5,1)` + `cellLayout=Isometric` ตรงกับ
      `Ground.png` (128×64 @ 128 PPU = 1×0.5 unit) แล้ว — ค่าเหล่านี้จะถูก re-tune อีกครั้งเมื่อได้ sprite จริง

## Building visuals (เพิ่มหลัง migration, 2026-06-14)
- Placeholder sprite รายอาคาร (ทรง/สี iso เฉพาะตัวต่ออาคาร, pivot bottom-center) สำหรับ Habitat, Farm,
  WaterPlant, PowerPlant, RadiationShelter, Laboratory, CoreTower — ผูกเข้า `BuildingData.sprite` แล้ว
- `BuildingVisualSpawner` (`Scripts/Systems/BuildingVisualSpawner.cs`) subscribe
  `EventManager.Instance.OnBuildingPlaced` เพื่อ spawn sprite ตึกที่วางแล้วให้ติดอยู่ในฉาก (ไม่หายไปพร้อม ghost preview)
- ทั้งหมดสร้าง/วาดผ่าน `Assets/Editor/PlaceholderSpriteGenerator.cs` (menu: `NuclearReMind/Generate Placeholder Sprites`)
  ให้รันใหม่ได้ทุกครั้งที่ schema เปลี่ยน หรือเมื่อได้ sprite จริงมาแทน

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

### Day 9 — Integration milestone ✅ Milestone — เสร็จแล้ว (2026-06-15)
- `Tests/EditMode/IntegrationFlowTests.cs` ทดสอบ flow รวมทั้งหมดผ่าน `EventManager`:
  placement -> resource/registry/grid/visual -> tower phase complete -> dilemma trigger/resolve ->
  tower completion -> victory/game over -> save/load round trip (resource, population, tower, grid, visual)
  รันผ่าน Unity batchmode ครบ 12/12 (8 GridManagerTests + 4 IntegrationFlowTests)
- แก้ gap ที่ค้างไว้: `GridManager`/`BuildingVisualSpawner` เพิ่ม `OnSaveLoaded` handler เพื่อ re-occupy cell
  และ respawn visual จาก save (อ่าน footprint/sprite ผ่าน `BuildingRegistry.GetBuildingDataByName` ซึ่งเป็น
  read-only lookup), `BuildingVisualSpawner` ใช้ `DestroyImmediate` นอก Play mode เพื่อให้ทำงานถูกใน EditMode tests ด้วย
- commit + tag `v0.1-prototype`

### Day 10 — CodexManager + Research Point system ✅ เสร็จแล้ว (2026-06-15)
- `Scripts/Data/CodexEntry.cs` — ScriptableObject: entryId, title, branch, content, illustration, researchPointCost, unlockedByEvent
- `Scripts/Data/ResourceData.cs` — เพิ่ม `researchPoints` field (default 0, backward-compatible)
- `Scripts/Data/ResourceType.cs` — เพิ่ม `ResearchPoints` enum value
- `Scripts/Managers/EventManager.cs` — เพิ่ม OnCodexEntryUnlocked, OnCodexUnlockFailed + RaiseXxx methods
- `Scripts/Managers/ResourceManager.cs` — accumulate researchPoints per Tick, handle ResearchDelta
- `Scripts/Managers/CodexManager.cs` — Singleton, TryUnlock (RP check), AutoUnlockByEvent (phase events), IsUnlocked()
- `Scripts/UI/CodexUIController.cs` — Codex panel UI, RefreshList, ShowEntry, Toggle
- `Scripts/Managers/SaveManager.cs` — Save() อ่าน CodexManager.Instance.UnlockedIds ลง unlockedCodexEntries
- `Editor/CodexSetup.cs` — menu "NuclearReMind/Setup Codex System" สร้าง 5 CodexEntry assets + wire CodexManager +
  สร้าง Codex Canvas UI (panel, scrollable list, detail view, RP counter) + เพิ่มปุ่ม Codex ใน HUDCanvas ทั้งหมดในคลิกเดียว
- เนื้อหาภาษาไทย 5 entries (ระดับมัธยม): ฟิชชัน, ปฏิกิริยาลูกโซ่, ครึ่งชีวิต, สารหล่อเย็น, รังสีและการป้องกัน
- ขั้นตอนสุดท้าย (ต้องทำใน Editor): เปิด Gamescene → NuclearReMind → Setup Codex System → Save Scene

Day 11 เป็นต้นไป: ต่อตาม `Assets/Docs/ImprovePlan1.csv` (Content phase, เลขวันเดิม ไม่เปลี่ยน)

## หมายเหตุสถาปัตยกรรม (จาก Day 7, อัปเดตหลัง migration)
- โค้ดเกมทั้งหมดอยู่ใน assembly `NuclearReMind` (asmdef) — Manager ใหม่ทุกตัววางใน `Assets/Scripts/Managers|Systems|UI|Data|Utils/...` ตามหน้าที่ เพื่อให้อยู่ assembly เดียวกัน
- ใช้ `EventManager.Instance` ส่ง event เสมอ ห้าม direct reference ข้าม Manager
- Manager ที่ subscribe event ต้อง +=/-= ใน `OnEnable`/`OnDisable` พร้อม null-guard `EventManager.Instance == null` ใน `OnDisable` (ดู `EventTestSubscriber.cs` เป็นแบบ)
- ข้อมูลที่ปรับได้ (stats/dilemma/codex) ต้องเป็น ScriptableObject
