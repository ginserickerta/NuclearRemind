# Phase 1 Summary — รากฐาน (Day 1–7)

> สรุปสิ่งที่ทำเสร็จแล้วในช่วง Day 1–7 ตามแผน `nuclear_remind_30day_plan.csv`
> Commit: `ad7023b feat: Phase 1 foundation (Day 1-7) - grid, camera, input, placement, events, tests`

## ภาพรวม

Phase 1 มีเป้าหมายวางรากฐานของเกม: ระบบ grid แบบ isometric, กล้อง, input, การวางสิ่งก่อสร้าง,
ระบบ event กลาง, และชุดทดสอบอัตโนมัติ — ทุกระบบหลักเสร็จสมบูรณ์และ commit แล้ว

## รายระบบที่ทำเสร็จ

### 1. GameManager (`Script/Core/GameManager.cs`)
- Singleton pattern (`Instance`)
- ห่อใน `namespace NuclearReMind`
- จัดการ game state (`SetState()`) และส่ง event `OnGameStateChanged` ผ่าน `EventManager`

### 2. GridManager (`Script/Grid/GridManager.cs`)
- Grid แบบ isometric ขนาด 12×9
- `IsoToWorld(col, row)` และ `WorldToIso(worldPos)` แปลงพิกัด
- แต่ละ `Cell` เก็บ `isOccupied`, `buildingType`, `radiationLevel`
- `InitializeGrid()` เปิดเป็น public, ปรับ `OnDrawGizmos()` ให้ตรวจสอบ occupied cell อย่างปลอดภัย

### 3. CameraController (`Script/Core/CameraController.cs`)
- เลื่อนกล้องด้วย WASD
- Zoom in/out ด้วย scroll wheel

### 4. InputManager (`Script/Core/InputManager.cs`)
- Singleton, แปลง mouse click → grid coordinate
- เพิ่ม null-guard สำหรับ `mainCamera`

### 5. Tilemap Setup (`Scenes/Gamescene.unity`)
- Tilemap layers: Ground / Buildings / FogOfWar
- ตั้ง Sorting Layers ใน `ProjectSettings/TagManager.asset`
- ⚠️ ยังรอ sprite จริงสำหรับวาด map Veltara

### 6. PlacementController & BuildingData (`Script/Buildings/`)
- ระบบวาง ghost + ตรวจ footprint ก่อนวาง
- รีแฟกเตอร์เป็น `OccupyFootprint()` แยกออกมา
- `BuildingData` เป็น ScriptableObject (ย้ายมาที่ `Script/Buildings/` เพื่อแก้ circular dependency, คง GUID เดิม)

### 7. EventManager (`Script/Core/EventManager.cs`)
- Static class แบบ Observer pattern
- Event หลัก: `OnBuildingPlaced`, `OnPlacementCancelled`, `OnGameStateChanged`
- `ResetEvents()` ผ่าน `[RuntimeInitializeOnLoadMethod]` ป้องกัน subscriber ค้างหลัง domain reload
- มี `EventTestSubscriber.cs` เป็นตัวอย่างการ subscribe/unsubscribe

### 8. Assembly Definitions
- `Script/NuclearReMind.asmdef` — assembly หลักของโค้ดเกม
- `Tests/EditMode/NuclearReMind.Tests.EditMode.asmdef` — สำหรับ EditMode tests

### 9. EditMode Unit Tests (`Tests/EditMode/GridManagerTests.cs`)
- 8 เทสครอบคลุม:
  - `IsoToWorld` / `WorldToIso` (รวม round-trip ทั้ง 108 cell)
  - `IsInBounds` (true/false case)
  - `GetCell` (in-bounds / out-of-bounds)

### 10. Documentation
- แก้ `CLAUDE.md` และ `NSC info.md` ให้ตรงกับ Unity 6000.3.6f1 + URP
- เพิ่ม `nuclear_remind_30day_plan.csv` เข้า repo
- เขียน `PHASE2_TODO.md` เป็นแผนต่อ Day 8–14

## ค้างจาก Phase 1 (งานที่ต้องทำใน Editor)
- [ ] Import sprite tiles/buildings แล้ววาด map Veltara 12×9 บน Tilemap "Ground"
- [ ] สร้าง `BuildingData` asset จริง (Create > NuclearReMind > Building Data) + ใส่ icon
- [ ] รัน EditMode Test Runner ให้ผ่านครบ 8 เทส (สีเขียว)
- [ ] ปรับ `tileWidth` / `tileHeight` / `originOffset` ให้ตรงกับ sprite จริง

## ต่อไป
Phase 2 (Day 8–14): ResourceManager, PopulationManager, CoreTowerManager, UIManagerHUD,
TooltipController, MoralDilemmaManager — รายละเอียดดูที่ `Assets/Docs/PHASE2_TODO.md`
