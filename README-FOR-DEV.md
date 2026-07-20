# Nuclear Re:Mind — handoff for contract bug-fixing

Unity **6000.3.6f1** · URP + URP 2D Renderer (Light 2D) · **Legacy** Input System · Windows Standalone
Branch in this archive: **`cutover`** · snapshot taken **2026-07-20**

> **Read "Who owns what" below before you touch anything.** The owner is fixing bugs in parallel.
> Working the wrong file means one of us throws work away.

---

## Open it

1. Install Unity **6000.3.6f1** exactly (other 6.x versions re-serialize assets and produce noisy diffs).
2. Open the extracted folder as a project. First import takes ~5–15 min — `Library/` is intentionally not in this archive because Unity regenerates it.
3. Main scene: `Assets/Scenes/Gamescene.unity`. The app boots from `Assets/Scenes/MainMenu.unity`.

## Read first

| File | What it is |
|---|---|
| `CLAUDE.md` (repo root) | **9 rules that must not be broken.** Read before changing anything. |
| `docs/GDD.md` | Full system spec §1–§33 |
| `docs/CONFIG.md` | **Every gameplay number lives here.** Values marked 🔒 must not be changed without re-running the balance sim. |
| `lastplan.md` | Current status, open decisions, and traps already hit |

The rules that bite most often:

- **No `if (day == X)`** except `day == 30` (the deadline). Every trigger binds to *state* (core %, heat, hunger, radiation), never the calendar.
- **No hardcoded gameplay numbers** — read from `GameConfigSO`, which mirrors `docs/CONFIG.md`.
- **Cross-system mutation goes through `EventManager.Instance`.** Read-only queries to `MasteryRegistry` / `GameConfigSO` / `KnowledgeDB` may be direct.
- **Hope is never written directly** — every system submits a `HopeEntry` to `HopeLedger`.
- **Locked options must stay visible**, never hidden.
- Comments in English. Do not modify `IsoToWorld` / `WorldToIso` in `GridManager.cs` (the math is test-covered).

---

## Who owns what

**Yours — please work only in these areas:**

| Bug | Files |
|---|---|
| Core Tower % rises without spending deuterium | `Core/ReactorController.cs` · `Managers/CoreTowerManager.cs` · `UI/CoreTowerPanelUI.cs` |
| Deuterium never accrues | `Managers/ResourceManager.cs` (`ComputeV63ProductionDelta`) · `Systems/DeuteriumExtraction.cs` |
| Minor crises don't repeat / major crises never fire | `Cards/*` |
| Five pre-existing test failures | the four test files named below |
| Depth-sort boxes overlap | `Systems/BuildingVisualSpawner.cs` · `Systems/WorkerView.cs` |

**Owner's — do not touch, these are being changed right now:**

`Workers/WorkerManager.cs` · `UI/UIManagerHUD.cs` · `Research/UI/NoteCardPopup.cs` ·
`Hope/*` · `UI/AlertController.cs` · `UI/QuestPanelController.cs` · `Core/GameConfigSO.cs`

If a fix genuinely needs a file from the second list, say so before starting rather than after.

---

## Verify a change

Unity Editor must be **closed** for both commands (it locks the project).

```powershell
# compile — grep the log for "error CS", zero means clean
& "C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe" -batchmode -quit `
  -projectPath "<project>" -logFile compile.log

# EditMode tests
& "C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe" -batchmode -runTests `
  -projectPath "<project>" -testPlatform EditMode -testResults results.xml -logFile test.log
```

⚠️ **If compilation fails, `-runTests` silently reuses the previous assemblies** and `results.xml` looks unchanged. Always check the `error CS` count in `test.log` before trusting a test result.

---

## Known bugs — the actual work

### 1. Core Tower % rises even though deuterium is never spent

`ReactorController.Initialize` sets `Fuel = cfg.fuelNeed` once at boot, and **nothing in the codebase ever writes `Fuel` again** — nothing spends it, nothing tops it up from `ResourceManager.Current.deuterium`. So `fe = min(1, Fuel/fuelNeed)` is permanently 1 and `Core += baseGain * fe` fires every day regardless of what the player does.

The reactor's fuel gate and the deuterium resource are two systems that never learned about each other. Confirm the intended coupling against `docs/GDD.md` §26 before wiring them.

The player-facing +/- deuterium buttons in `CoreTowerPanelUI` are also dead: `CoreTowerManager.HandleAllocationAdjust` opens with `if (V63Live) return;`, and `V63Live` is true whenever `ReactorController` exists, which is always.

### 2. Deuterium never accrues, so it can never be spent

`ResourceManager.ComputeProductionDelta` early-returns into `ComputeV63ProductionDelta` whenever `WorkerManager.Instance != null` — always, in a real session. That method computes food, water, iron and energy only; **there is no `delta.deuterium` term at all**. The only code that honours the extraction switch is the legacy per-building loop below it, which is unreachable.

The failing test `ResourceManagerTests.DailyProduction_UpgradedToL3_ProducesDeuterium` has a second, narrower cause: the test harness never creates a `DeuteriumExtraction` singleton, so the guard skips the deuterium block. Fixing the gameplay bug will not by itself make that test pass.

**Bugs 1 and 2 compound — fixing either alone changes nothing the player can see.**

### 3. Minor crises don't repeat; major crises never fire

`CardManager`'s bookkeeping looks correct on inspection: only `decree` is `onceOnly`, the other seven assets carry `cooldownDays` 3–6, all 8 assets load, and no calendar triggers exist. So the likely cause is that the **trigger conditions never become true**, not that the cards are blocked.

`CardManager.VerboseTrace` already exists and logs, per card per day, why nothing fired. Turn it on and read the log before changing any code — this one is easy to "fix" in the wrong place.

Two concrete things found while reading, either of which may be the real cause:

- `CardWorldState.Snapshot()` sets `medBayCapacity` from `GameConfigSO.medBayCapacity` (default 4) unconditionally, while `WorkerManager.HospitalBeds()` returns 0 until a Hospital is actually built. The `triage` trigger compares against a bed count that assumes beds exist.
- The doc comment on `CardWorldState` still claims the reactor/storm/Zone B systems "aren't built yet" and that non-firing is correct. That is stale — those systems went live in commits `9b231ce` / `b3e0b24`. Don't trust it.

### 4. Five EditMode tests fail on a clean checkout

Unrelated to each other and all predating this handoff:

- `ConstructionWorkerGateTests.EffectiveCap_NotUnderConstruction_FallsBackToWorkerRequired` — expected 1, got 0
- `ResourceManagerTests.DailyProduction_UpgradedToL3_ProducesDeuterium` — expected 8.0, got 0.0 (see bug 2)
- `RunStatsTests.SaveLoad_KeepsTheStatsThatNothingElseRemembers` — Zone B worker-days not surviving save
- `WorkerVisualSpawnerStabilityTests.ReducingCount_EvictsHighestSlotOnly_KeepsLowerSlotsUntouched`
- `WorkerVisualSpawnerStabilityTests.UnrelatedAssignmentElsewhere_DoesNotReshuffle_ExistingWorkers`

Baseline is **542 passed / 554 run**. Any additional failure is new.

> ⚠️ This baseline was measured before the 2026-07-20 changes below and has **not** been re-measured since — the Editor held the project lock. Re-run it yourself on first import and treat *that* number as your baseline.

### 5. `q_fusion` has a very narrow answerable window

`Mastery/QuizAvailability.cs` gates it on `coreProgress >= 95`, but the run ends at 100, so the quiz is only reachable if the run spends ~2 days in `[95, 100)`. Lowering the gate is a balance decision for the owner, not a straight fix.

### 6. Building/worker depth-sort boxes overlap

Colliders are derived from sprite bounds rather than the grid footprint, so neighbouring buildings' boxes overlap and workers sort inconsistently between them. Known and deliberately parked.

---

## Already fixed on 2026-07-20 — do not redo

| Commit | What |
|---|---|
| `355a458` | Population could never grow. The only growth formula sat in `PopulationManager.HandleDayEnded` behind `if (WorkerManager.Instance != null) return;` — always true in a live game — and nothing appended to `_workers` outside `Initialize()`. Implemented in `WorkerManager` per GDD §5. |
| `62c3e25` | Growth numbers loosened now that the formula actually runs: `growthFoodRatio` 5→3, `growthChance` 0.25→0.35. Rationale and date are in `docs/CONFIG.md`. |
| `cc2901a` | HUD population counter read the retired `PopulationData` record and showed a frozen `10/80`. Now reads `WorkerManager.AliveCount` / `ShelterCap`. |
| `c4875ae` | The engineer/medic/farmer HUD rows counted job classes that v5.2 deleted, so all three read 0 forever. Repurposed as exhausted / sick / hungry counts. |
| `5e6670a` | `RecordCardCanvas` had been saved inactive out of a play session — see the trap list below. |

---

## Still open, not yet diagnosed to a line

- Hope barely moves. The worker attrition pipeline is wired correctly and tested, but five documented Hope sources (`heat.critical`, `storm.active`, `core.stalled`, `alara.compliant`, `core.progress`) have **no call site anywhere**, and `food.surplus` pays +3 every day with no cap, which masks the small negatives.
- The resource alert spams. `ResourceManager.startIron` is 100 while `criticalIron` is 200, so iron is born "critical", and `AlertController` only debounces for the display duration — it re-fires every 5 s tick.
- Research completion shows nothing. `Research/UI/NoteCardPopup.cs` is a correct implementation of the spec but is in no scene and has no auto-spawn hook, so the `OnResearchNoteCompleted` event has no listener that draws anything. **Owner is fixing this one.**

## Blocked on a design decision — do not implement

Three requests conflict with the shipped spec. They need the owner's ruling first, and the docs need editing either way.

- **Data Recovery pops up on its own.** `docs/STORY.md` explicitly requires this and marks the player-initiated version with ❌. The request to make it manual reverses a documented decision.
- **Quiz after quest completion.** There is no quest-completion concept in the codebase; `QuestPanelController` is a non-interactive daily checklist. `docs/QUIZZES.md` also states quizzes should not pop at all.
- **Knowledge system.** `docs/GDD.md` §16 says Knowledge was cut and replaced by `MasteryRegistry`; `docs/CONFIG.md` re-added a start value on 2026-07-19. The two docs contradict each other, and the code sits in between — Knowledge is produced, displayed and persisted, but its only mechanical payoff is dead code.

---

## Traps in this codebase

- **Legacy uGUI `Text` cannot render characters above U+FFFF.** Emoji like 🔒 come out as a blank gap. Use text markers such as `[ล็อก]` instead. Most were swept already; do not reintroduce them.
- **`Assets/Scripts/Narrative/_archive/` is still compiled** by Unity, but is *not* listed in the generated `.csproj`. An out-of-band Roslyn check can pass on code Unity then rejects. Grep it before deleting or changing any public member — the no-op shims in `QuizManager` exist for exactly this reason.
- **Play mode can save state into the scene.** `RecordCardCanvas` was found flipped to inactive this way. An inactive canvas there means `SetActive(true)` on its child draws nothing, the dismiss buttons cannot be clicked, and the day clock used to hang on day 9. `git diff` the scene after any editor or batch run.
- **UI is built in C# at runtime**, not authored in the scene — panels like `CrisisCardPanelUI`, `CodexPanelUI`, `ResearchLabPanelUI` have no prefab to inspect. `QuizPopupController` is the exception and *is* scene-authored.
- **Fonts load through `UIFonts.Body` only.** Do not add new `Resources.Load<Font>` calls; the old per-panel copies pointed at a path that never existed.
- **There is no `GameConfig.asset`.** `GameConfigSO.Instance` falls back to `CreateInstance`, so the **C# field defaults are what the game actually runs on** — `docs/CONFIG.md` describes intent, the C# defaults are truth. Change both.
- **Two `startIron` values exist** — `ResourceManager.startIron` (100, actually used) and `GameConfigSO.startIron` (200, never read). Check which one you are looking at.

---

## Ground rules for handing work back

- One commit per self-contained change, with the reasoning in the message.
- Do not change values marked 🔒 in `docs/CONFIG.md` without asking.
- If something is not covered by the docs, ask rather than guess — that is the closing rule of `CLAUDE.md`, and it exists because guessing has broken balance before.
- This archive has no `.git` directory, so you cannot push. Send back **changed files only**, with a list of what you touched, rather than the whole folder — the owner is committing to the same tree in parallel and a folder overwrite would destroy their work.
