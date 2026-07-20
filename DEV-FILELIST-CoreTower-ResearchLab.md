# File list — Core Tower & Research Lab

Verified against commit `25c6a25` on branch `cutover`, **2026-07-20**.
Every path below was confirmed to exist at this commit. Line counts and auto-spawn status were read
from the files themselves, not from memory.

Context: the WebGL build is reported to break **specifically in these two systems** while the rest of
the game runs. The two are not independent — see "Where they meet" at the bottom before splitting the
work.

---

## 🗼 Core Tower

### Live — where the reported bugs are

| File | Lines | Auto-spawns | Role |
|---|---:|:---:|---|
| `Assets/Scripts/Core/ReactorController.cs` | 235 | yes | Owns `Core`, `Heat`, `Fuel`. Spawns the Storm/ZoneB/Sensor cluster too. **`Fuel` is set once in `Initialize` and never written again anywhere** — that is the "core % rises for free" bug. |
| `Assets/Scripts/Managers/CoreTowerManager.cs` | 781 | no | Facade over the v4.1 tower sim. Most of it is switched off: handlers open with `if (V63Live) return;`, and `V63Live` is true whenever `ReactorController` exists, i.e. always. |
| `Assets/Scripts/UI/CoreTowerPanelUI.cs` | 673 | yes | The panel. Built in C# at runtime, not authored in the scene. The +/- deuterium buttons call a handler that early-returns, so they do nothing. Line 312 reads `ResearchManager.CoreUnlockDone` **inverted** — a missing manager counts as "researched". |
| `Assets/Scripts/Systems/DeuteriumExtraction.cs` | 178 | yes | Gating logic for extraction. Reads correct against `docs/CONFIG.md`; nothing consumes its output. |
| `Assets/Scripts/Managers/ResourceManager.cs` | 494 | no | `ComputeProductionDelta` early-returns into `ComputeV63ProductionDelta`, which has **no deuterium term at all**. The legacy loop below it that honours the extraction switch is unreachable. |
| `Assets/Scripts/Data/TowerData.cs` | 20 | no | Plain data struct. |

### Live — supporting

```
Assets/Scripts/Core/GameConfigSO.cs        every gameplay number (no GameConfig.asset exists —
                                           the C# field defaults are what actually runs)
Assets/Scripts/Core/PhaseManager.cs
Assets/Scripts/Core/EndingSystem.cs
Assets/Scripts/Core/RunStats.cs
Assets/Scripts/Core/Achievements.cs
Assets/Scripts/Storm/StormSystem.cs        spawned as one cluster with ReactorController
Assets/Scripts/Storm/SensorArray.cs
```

### Dead — do not spend time here

```
Assets/Scripts/UI/CoreTowerUI.cs           119 lines, no auto-spawn, not referenced by any scene
```

### Editor tools and tests

```
Assets/Editor/CoreTowerAnimationSetup.cs
Assets/Editor/CoreTowerPanelBaker.cs
Assets/Editor/CoreTowerSpriteSetup.cs
Assets/Editor/CoreTowerUISetup.cs
Assets/Tests/EditMode/ReactorTests.cs
Assets/Tests/EditMode/CoreTowerManagerTests.cs
Assets/Tests/EditMode/DeuteriumExtractionTests.cs
Assets/Tests/EditMode/StormTests.cs
```

---

## 🔬 Research Lab — two systems run side by side

This is the part most likely to waste your time if nobody says it out loud.

### System A — v4.1, still running

| File | Lines | Auto-spawns | Role |
|---|---:|:---:|---|
| `Assets/Scripts/Managers/ResearchManager.cs` | 271 | yes | Three projects: `seeds`, `isotope`, `core_tower`. **Owns `CoreUnlockDone`, the flag that gates the Core Tower.** Not retired — it auto-spawns and runs every session. |

### System B — v6.3 notes and leads

| File | Lines | Auto-spawns | Role |
|---|---:|:---:|---|
| `Assets/Scripts/Research/ResearchLab.cs` | 347 | yes | Queue, progress, completion. Raises `OnResearchNoteCompleted`. |
| `Assets/Scripts/Research/KnowledgeDB.cs` | 155 | no | Registry of notes and leads. Loads via `Resources.LoadAll<ResearchNoteSO>("ResearchNotes")`. |
| `Assets/Scripts/Research/ResearchNoteSO.cs` | 49 | no | Note asset definition. |
| `Assets/Scripts/Research/ResearchLeadSO.cs` | 21 | no | Lead asset definition. |
| `Assets/Scripts/Research/SoftTriggerWatcher.cs` | 118 | no | Unlocks leads from game state. |

### UI

| File | Lines | Auto-spawns | Status |
|---|---:|:---:|---|
| `Assets/Scripts/Research/UI/ResearchLabPanelUI.cs` | 963 | yes | Main panel. Built in C# at runtime. |
| `Assets/Scripts/UI/LabPanelUI.cs` | 478 | yes | The lab building's own panel. Loads sprites from `Resources/LabUI/`. |
| `Assets/Scripts/Research/UI/NoteCardPopup.cs` | 100 | **no** | 🔴 Correct implementation of the spec, but **in no scene and with no auto-spawn hook** — nothing listens for `OnResearchNoteCompleted` to draw anything. This is why finishing research shows nothing. **The owner is fixing this file — do not touch it.** |
| `Assets/Scripts/Research/UI/ResearchQueuePanel.cs` | 377 | yes | Dormant by design — its `AutoSpawn` is a documented no-op stub. |

### Editor tools and tests

```
Assets/Editor/ResearchNotesSetup.cs
Assets/Editor/LabUISetup.cs
Assets/Tests/EditMode/ResearchSystemTests.cs
Assets/Tests/EditMode/ResearchManagerTests.cs
Assets/Tests/EditMode/CodexLabUnlockTests.cs
Assets/Tests/EditMode/KnowledgeSummaryTests.cs
```

---

## Where the two systems meet

```
ResearchManager.CoreUnlockDone
        ├──> CoreTowerManager.cs:217
        └──> CoreTowerPanelUI.cs:312     (inverted: no manager == "researched")
```

`ResearchManager` is the **only shared dependency** between the two systems reported broken on WebGL.
If a single thing is taking out both, this is the first place to look. Please do not split Core Tower
and Research Lab across two people.

`Assets/Scripts/Managers/TimeManager.cs` (44 lines) is also worth reading before debugging either
one. The day clock runs only when a pause-reason set is empty, and 14 call sites can add to it —
including `CoreTowerPanelUI`, `LabPanelUI` and `ResearchLabPanelUI`, which all take
`PauseReason.LabPopup`. A panel that fails to draw on WebGL holds its pause forever, and the symptom
is "the game does not advance". This exact failure has already happened once on desktop, via an
inactive `RecordCardCanvas`.

---

## Already ruled out for the WebGL fault — please do not re-check

- The WebGL build itself succeeds, with no compile or link errors.
- `Assets/Plugins/WebGL/NuclearSave.jslib` exists, and `SaveManager` already guards its WebGL path
  with `#if UNITY_WEBGL` plus a `NuclearSyncFs()` IndexedDB flush.
- No `BinaryFormatter`, no threads, no `async`/`Task` anywhere in `Assets/Scripts`.
- Sprite paths are case-correct: every `Resources.Load` string in `LabPanelUI` and `CoreTowerPanelUI`
  matches the real filename exactly. (Worth stating because case-sensitivity is the classic
  works-on-Windows-dies-on-WebGL bug, and it is not this.)

## Still open

- `ProjectSettings.asset` has `webGLExceptionSupport: 1` — explicitly-thrown exceptions only. A
  NullReferenceException that is merely noisy on Windows becomes undefined behaviour on WebGL, which
  matches "glitched, no clean crash". Raising this to full turns a silent freeze into a readable
  error and is the recommended first move.
- Nobody has read the browser console yet. An old WebGL build is still on disk under `Build/WebGL`
  and `Builds/WebGL`, so F12 → Console takes about 30 seconds and needs no rebuild. **Please do this
  before changing any code** — everything above is inference from static reading, and the console is
  direct evidence.
