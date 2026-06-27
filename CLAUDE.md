# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

**Nuclear Re:Mind** (นิวเคลียร์เปลี่ยนความคิดโลก) — an isometric survival city-builder for NSC 2026
(หมวดโปรแกรมส่งเสริมทักษะเพื่อการเรียนรู้). Unity 6 (`6000.3.6f1`), C#, URP + URP 2D Renderer
(Light 2D), Legacy Input System, Windows Standalone target. The player (Dr. Auren Vasek) rebuilds
the abandoned city of Veltara and completes the CORE TOWER (a fusion reactor) before resources run
out, while a Learning Codex teaches real nuclear-science concepts (fission, half-life, radiation
shielding, nuclear medicine/agriculture applications).

**Read `Assets/Docs/ImproveCLAUDE.md` before writing any code.** It is the canonical, current
spec for folder structure, naming conventions, the full `EventManager` event list, ScriptableObject
schemas (`BuildingData`, `CodexEntry`, `DilemmaData`), the data contracts (`ResourceData`,
`PopulationData`, `TowerData`, `SaveData`), grid constants, and the "ห้ามทำเด็ดขาด" (never-do) rules
summarized below.

Other reference docs:
- `Assets/Docs/PHASE2_TODO.md` — current implementation status / day-by-day TODO list
- `Assets/Docs/ImprovePlan1.csv` — the 30-day development plan (day → tasks → deliverable)
- `Assets/Docs/NSC info.md` — full project proposal (storyline, learning objectives, team)

## Commands

The Unity Editor (6000.3.6f1) must be **closed** before running any `-batchmode` command — Unity
holds a project lock and batch invocations will fail/hang otherwise.

Compile check (forces full reimport, writes a log):
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\UsEr\NSC2026" -logFile compile.log
```
Then grep `compile.log` for `error CS` — zero matches = clean compile.

Run all EditMode tests:
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe" -batchmode -runTests -projectPath "C:\Users\UsEr\NSC2026" -testPlatform EditMode -testResults results.xml -logFile test.log
```

Run a single test (or a class) with `-testFilter`:
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe" -batchmode -runTests -projectPath "C:\Users\UsEr\NSC2026" -testPlatform EditMode -testFilter "NuclearReMind.Tests.GridManagerTests.IsoToWorld_KnownCell_ReturnsExpectedPosition" -testResults results.xml -logFile test.log
```

`results.xml` is NUnit XML; check `result="Failed"` attributes for failures.

## Architecture

### Assembly / namespace

All gameplay code lives under `Assets/Scripts/` in one assembly, `NuclearReMind.asmdef`
(assembly name `NuclearReMind`), and every class is in `namespace NuclearReMind`. EditMode tests
live in `Assets/Tests/EditMode/` under `NuclearReMind.Tests.EditMode.asmdef`, which references
`NuclearReMind` **by assembly name** — moving script paths within the assembly never requires
updating this reference.

### Folder layout (`Assets/Scripts/`)

```
Managers/   Singleton managers — GameManager, EventManager, ResourceManager (and future
            PopulationManager, CoreTowerManager, SaveManager)
Systems/    GridManager, CameraController, InputManager, PlacementController
UI/         UIManagerHUD and other HUD/Tooltip/Popup/Codex controllers
Data/       ScriptableObject + serializable data definitions — BuildingData, ResourceData,
            PopulationData, TowerData, SaveData, GameEndType, ResourceType
Utils/      Helpers and example subscribers (EventTestSubscriber)
Narrative/  Not created yet — added in Phase 3 when the first story/dilemma file lands
```
`Assets/ScriptableObjects/Buildings/` holds the `BuildingData` assets (one per building type).

### Singleton + EventManager (Observer pattern)

Every manager is a MonoBehaviour singleton (`public static X Instance`, set in `Awake()`,
`Destroy(gameObject)` on duplicates). **Systems never call another manager's methods directly** —
all cross-system communication goes through `EventManager.Instance`:

```csharp
// ✅ correct
EventManager.Instance.OnBuildingPlaced += HandleBuildingPlaced;
EventManager.Instance.RaiseBuildingPlaced(cell, data);

// ❌ wrong — never reference another manager directly
ResourceManager.Instance.AddFood(50);
```

`EventManager` carries `[DefaultExecutionOrder(-100)]` so its `Awake()` (which sets `Instance`)
runs before other components' `OnEnable()` subscribe calls in the same scene load. Subscribers
must `+=` in `OnEnable()` and `-=` in `OnDisable()`, guarding `OnDisable` with
`if (EventManager.Instance == null) return;` for teardown ordering (see
`Assets/Scripts/Utils/EventTestSubscriber.cs`). The full current event list is in
`Assets/Scripts/Managers/EventManager.cs`; the target list (including Phase-3 narrative/codex
events not yet added) is documented in `Assets/Docs/ImproveCLAUDE.md`.

### Isometric grid — `Assets/Scripts/Systems/GridManager.cs`

Fixed **20×12** grid. Conversion formulas (tested by `GridManagerTests.cs`, 8/8 must pass):

```csharp
// grid (col, row) -> world
x = (col - row) * (tileWidth / 2)
y = (col + row) * (tileHeight / 2)

// world -> grid (col, row)
col = worldPos.x / tileWidth + worldPos.y / tileHeight
row  = worldPos.y / tileHeight - worldPos.x / tileWidth
```

`GridManager` also owns `Cell` (per-tile state: `isOccupied`, `buildingType`, `radiationLevel`)
and the `BuildingType` enum. `PlacementController` uses `GridManager.Instance.GetCell/IsInBounds`
to validate and occupy a building's footprint (`BuildingData.size`) before raising
`OnBuildingPlaced`.

### BuildingData (`Assets/Scripts/Data/BuildingData.cs`)

ScriptableObject (`[CreateAssetMenu(... menuName = "NuclearReMind/Building")]`) holding identity
(`buildingName`, `description`, `nuclearKnowledge` — the 3-layer tooltip text), `sprite`, grid
`size`, `buildingType`, flat build costs (`materialCost`, `energyCost`, `workerRequired`),
per-tick production fields (`foodProduction`, `waterProduction`, `radiationProtectionBonus`,
`energyProduction`, `researchPointsPerTick`), and CORE TOWER linkage (`isCoreTowerPart`,
`towerPhaseRequired`). All gameplay values must come from these assets — never hardcode resource
numbers in manager code.

### Save data contracts (`Assets/Scripts/Data/`)

`ResourceData`, `PopulationData`, `TowerData` are `[Serializable]` structs; `SaveData` is the
top-level save object referencing all three plus placed-building lists and relationship values.
**Never add a field to `SaveData` (or the structs it contains) without giving it a default** —
old save files must still deserialize.

## Rules that must never be broken

1. Never modify `IsoToWorld()` / `WorldToIso()` in `GridManager.cs` — the math is tested and
   load-bearing.
2. Never change the grid size away from 20×12 — fixed design constraint.
3. Never add a `SaveData`/data-struct field without a default value.
4. Never hardcode resource/cost/production values — they belong in `BuildingData` (or other
   ScriptableObjects).
5. Never call another manager's members directly across systems — go through
   `EventManager.Instance`.
6. Never use `GameObject.Find()` or `FindObjectOfType()` inside `Update()` — cache references in
   `Awake()`.
