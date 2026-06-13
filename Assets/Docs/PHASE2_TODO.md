# Phase 2 — Core Systems (Day 8–14)

> เขียนเมื่อจบ Day 7 (Review & Buffer — Phase 1)
> อ้างอิงแผนเต็ม: `nuclear_remind_30day_plan.csv`

## สถานะเมื่อจบ Phase 1 (Day 1–7)

| ระบบ | ไฟล์ | สถานะ |
|---|---|---|
| GameManager (state + pause) | `Script/Core/GameManager.cs` | ✅ |
| GridManager (12×9 iso + Gizmos) | `Script/Grid/GridManager.cs` | ✅ |
| Camera (WASD + zoom) | `Script/Core/CameraController.cs` | ✅ |
| Input (click → grid) | `Script/Core/InputManager.cs` | ✅ |
| Tilemap (Ground/Buildings/FogOfWar + sorting layers) | `Scenes/Gamescene.unity` | ⚠️ รอ sprite จริง |
| Placement (ghost + footprint) | `Script/Buildings/PlacementController.cs` | ✅ |
| BuildingData (SO) | `Script/Buildings/BuildingData.cs` | ✅ |
| EventManager (Observer) | `Script/Core/EventManager.cs` | ✅ |
| EditMode Tests (GridManager) | `Tests/EditMode/GridManagerTests.cs` | ✅ |
| Assembly Definitions | `Script/NuclearReMind.asmdef` | ✅ |

## ค้างจาก Phase 1 (ทำเองใน Editor)
- [ ] Import sprite tiles/buildings → วาด map Veltara 12×9 บน Tilemap "Ground"
- [ ] สร้าง `BuildingData` asset จริง (Create > NuclearReMind > Building Data) + ใส่ icon
- [ ] รัน EditMode tests ใน Test Runner ให้เขียวครบ
- [ ] ปรับ tileWidth/tileHeight/originOffset ให้ตรง sprite จริง

## Phase 2 Tasks

### Day 8 — ResourceManager
- Singleton, ติดตาม Food / Water / RadiationProtection / Energy / Workers
- `ResourceData` struct + `Tick()` ทุก game turn
- subscribe `EventManager.OnBuildingPlaced` → หักต้นทุนตาม `BuildingData.cost`

### Day 9 — PopulationManager & Trust
- Trust (0–100), Morale, Health
- Trust decay เมื่อทรัพยากรขาด; Trust < 20 → worker strike event

### Day 10 — CoreTowerManager (Phased Construction) ✅ Milestone
- 3 phase (Foundation / Reactor Core / Fusion Activation) + progress bar + win condition

### Day 11 — UIManagerHUD
- resource bar, CORE TOWER progress, trust meter; subscribe events อัปเดต real-time

### Day 12 — TooltipController (3 ชั้น)
- `BuildingKnowledge` SO; ชั้น 1 ชื่อ+cost / ชั้น 2 การทำงาน / ชั้น 3 ความรู้จริง

### Day 13 — MoralDilemmaManager
- `DilemmaData` SO + Popup UI + consequence ต่อ Trust/Resources

### Day 14 — Review & Integration Test ✅ Milestone
- ทดสอบ flow รวม, commit + tag `v0.1-prototype`

## หมายเหตุสถาปัตยกรรม (จาก Day 7)
- โค้ดเกมทั้งหมดอยู่ใน assembly `NuclearReMind` (asmdef) — Manager ใหม่ทุกตัววางใน `Assets/Script/...` เพื่อให้อยู่ assembly เดียวกัน
- ใช้ `EventManager` ส่ง event เสมอ ห้าม direct reference ข้าม Manager
- Manager ที่ subscribe event ต้อง +=/-= ใน `OnEnable`/`OnDisable` (ดู `EventTestSubscriber.cs` เป็นแบบ)
- ข้อมูลที่ปรับได้ (stats/dilemma/codex) ต้องเป็น ScriptableObject
