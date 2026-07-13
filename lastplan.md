# lastplan.md — ความคืบหน้างาน NSC2026 (backlog 5 งาน)

> อัปเดตล่าสุด: 2026-07-13 · เขียนไว้เพื่อทำต่อบนอีกเครื่อง
> โปรเจกต์: **Nuclear Re:Mind** · Unity 6 (6000.3.6f1) · URP 2D · Legacy Input · path `C:\Users\UsEr\NSC2026`

---

## 0) กติกาถาวร (ห้ามลืม)

- **หากสงสัยห้ามคิดเอง ถามผู้ใช้เสมอ**
- ยอมรับงาน = ต้อง **compile ผ่าน batch mode (error CS = 0)** — ต้อง **ปิด Unity Editor ก่อน** รัน
- ห้ามแก้ไฟล์ต้นฉบับใน `Downloads` (คัดลอกเข้าโปรเจกต์ก่อน)
- **ห้ามแตะ** `IsoToWorld`/`WorldToIso`/`IsoToWorldF` · **grid = 43×43** (CLAUDE.md เขียน 20×12 = ล้าสมัย · scene จริง 43×43)
- cross-manager ผ่าน **`EventManager.Instance` เท่านั้น** (query `.Instance` อ่านอย่างเดียวได้) · ห้าม hardcode ค่า gameplay (→ SO/SerializeField)
- ทุก setup = `[MenuItem]` **idempotent** (รันซ้ำไม่สร้างซ้ำ) + ใส่ใน `RunAllSetups.cs`
- enum ต่อท้ายเท่านั้น (ห้ามแทรกกลาง) · เพิ่ม field ใน SaveData/struct ต้องมี default (SO asset ไม่อยู่ใน SaveData = ปลอดภัย)

### คำสั่ง verify (ปิด Editor ก่อน)
```powershell
# compile
& "C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\UsEr\NSC2026" -logFile compile.log
# grep error CS ใน compile.log — ต้อง 0 อัน

# EditMode tests
& "C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe" -batchmode -runTests -projectPath "C:\Users\UsEr\NSC2026" -testPlatform EditMode -testResults results.xml -logFile test.log
```

---

## 1) สถานะรวม 5 งาน

| # | งาน | สถานะ |
|---|-----|-------|
| 1 | **Crisis UI** (กรอบสนิม A/B/C + ยืนยัน) | ✅ **CODE COMPLETE** (รอ user รัน setup + compile) |
| 2 | **Quiz Explanation UI** (หน้าอธิบายหลังตอบ) | ✅ **CODE COMPLETE** (รอ user รัน setup + compile) — เพิ่ม `QuizExplanationSetup.cs` + RunAllSetups แล้ว (2026-07-13) |
| 3 | **Decor spawner** (โปรยของนอกกริด) | 🔶 **CODE COMPLETE** — `DecorSpawner.cs`+`DecorSetup.cs`+RunAllSetups เสร็จ · รอ asset PNG ลง `Assets/Sprites/Decor/` (2026-07-13) |
| 4 | **iPad Touch** (input/gesture/responsive/WebGL save) | 🔶 **CODE COMPLETE (v1)** — touch กล้อง 2 นิ้ว (แพน+พินช์) + WebGL save flush · ไฮบริดคงเมาส์/คีย์บอร์ด · Canvas responsive อยู่แล้ว · รอเทสต์บน iPad จริง (2026-07-13) |
| 5 | **WebGL build + itch.io** | 🔶 **CODE READY** — `WebGLBuilder.cs` เสร็จ · build ต้อง**ปิด Unity**ก่อนรัน batch · push รอ itch credential (user/slug + `butler login`) (2026-07-13) |

### Decision ที่ผู้ใช้ยืนยันแล้ว
- **#2 badge คะแนน** = "Badge อย่างเดียว (ไม่แตะ balance)" → เพิ่ม `scoreDelta` โชว์ +N เขียว/−N แดง · Knowledge จริงคงเดิม (ถูก+8/ผิด+3)
- **#3 decor density** = "กลาง ~8%"

---

## 2) STEP 0 — reuse map (ผลสำรวจ 4 ระบบ · อ้างอิงตอนทำต่อ)

### Crisis (#1)
- **ไม่มี `CrisisSO`** — คลาสจริง = `DilemmaData` (`Assets/Scripts/Data/DilemmaData.cs`) · **รองรับ 3 ตัวเลือก A/B/C อยู่แล้ว** (`choiceA/B/C_Text` + ผลแยกครบ · C ซ่อนได้ถ้าเว้นว่าง)
- UI เดิม = `DilemmaPopupController` (`Assets/Scripts/UI/DilemmaPopupController.cs`) สร้างโดยเมนู **"Setup Tooltip and Dilemma UI"** (`Assets/Editor/TooltipDilemmaSetup.cs`)
- flow: `OnDilemmaTriggered` → controller โชว์ → กด → `RaiseDilemmaResolved(dilemma, choiceIndex)` · **managers 2 ตัวฟัง** (`DilemmaManager` + `CrisisEffectManager`) — ห้ามเปลี่ยน exit point นี้
- สกินโลหะ: `Assets/Resources/CodexUI/frame_metal.png` (9-slice border Vector4(46,44,57,44)) · `Assets/Resources/StoryUI/plate_inset.png` · `btn_metal.png`
- popup canvas convention: HUD/popup=0, Story/RecordCard=60, Pause=100

### Quiz (#2)
- `QuizManager.SubmitAnswer(int)` (`Assets/Scripts/Managers/QuizManager.cs`): ถูก→+rewardKnowledge(8), ผิด→+3 (const `WrongAnswerKnowledge`, **บวกเสมอ ไม่มีติดลบ**) · จบด้วย `RaiseQuizAnswered(quizId, correct)` แล้ว `ShowNext()`
- **hook** = subscribe `EventManager.OnQuizAnswered (Action<string,bool>)` → `QuizManager.Instance.GetById(quizId)`
- `QuizQuestionSO` fields: id, category, topicTitle, question, options[], correctIndex, rewardKnowledge, explainText, speaker, codexUnlockId (+ ที่เพิ่มใหม่ ดู #2 ด้านล่าง)
- `CodexEntry`: entryId, title, titleEn, category, content, **illustration(Sprite)**, iconName, ...
- Quiz UI เดิม = `QuizPopupController` (`Assets/Scripts/UI/QuizPopupController.cs`) สร้างใน `HUDCanvasSetup.SetupQuizPopup` · footer "ปลดล็อก Codex :" มีอยู่แล้ว (book icon `Assets/Resources/CodexUI/header_book.png`)
- SaveData: Codex unlock persisted · **quiz answered ไม่ persist** · เพิ่ม field ใน QuizQuestionSO = ไม่กระทบ save

### Decor (#3)
- **grid 43×43** · `OreDepositManager.apronMargin = 35` (scene) · `zoneBorderThickness = 7`
- iso→world: **เรียก** `GridManager.Instance.IsoToWorldF(float col, float row)` (มี float overload) · `tileWidth=1, tileHeight=0.5`
- sorting: `GridManager.SortOrder(col,row)` = `RoundToInt((col+row)*16)` · `SortTier { Unit=0, Ore=1, Building=2, CoreTower=3 }`, `TierStep=4` · `SortOrder(col,row,tier)` = SortOrder + tier*4
- sorting layers (TagManager): Default, Ground, **Buildings**, Units, FogOfWar, WorldUI · building+worker อยู่ layer **"Buildings"** แยกด้วย order · **แนะนำ decor: layer "Buildings" + `SortTier.Unit`** (occlude ถูกตาม iso depth)
- **off-grid range**: painted square x,y ∈ **[-35, 77]** · on-grid [0,43) · **เก็บเฉพาะ** `x<0 || x>=43 || y<0 || y>=43`
- deterministic hash: `IsoGroundPainter.Hash(int col,int row)` (public, non-negative) · **แยก digit**: `h % 100 < density%` = ประตูวางหรือไม่ · `(h/100) % N` = เลือกชิ้นไหน
- footprint กันชน (ถ้าต้องเช็ค): `BuildingRegistry.Instance.PlacedBuildings` (Dictionary<Vector2Int,BuildingData>) · `OreDepositManager.BuildBlockedCells()` เป็น pattern (แต่ decor นอกกริด = ไม่ทับ footprint อยู่แล้ว)
- import PNG ตาม `Assets/Editor/BuildingArtSetup.cs` `ConfigureImporter`: `Sprite/Single`, **pivot bottom-center จาก alpha bounds**, `PPU = artW/targetWidth`, `FilterMode.Point`, `Uncompressed`, `alphaIsTransparency`, `mipmap off` · เก็บที่ **`Assets/Sprites/Decor/`** (world art ไม่ใช่ Resources)
- lighting: ให้ decor ใช้วัสดุเดียวกับ building/worker (รับแสงเท่ากัน) — default
- **แหล่ง asset**: `C:\Users\UsEr\Downloads\drive-download-20260712T204202Z-2-001` — 8 PNG:
  `IMG_1721(801×1600) 1722(1600×1600) 1723(1200×1600) 1724(1160×1600) 1725(1600×1236) 1726(900×1600) 1728(1067×1600) 1729(1200×1600)` — pixel-art tall, พื้นหลังโปร่ง

### Touch (#4)
- ทุกอย่าง Legacy Input · **จุดแก้คุ้มสุด** = `InputManager.GetMouseGridPosition()` (`Assets/Scripts/Systems/InputManager.cs:66`) ให้รู้จัก touch (placement/demolish/panel ใช้ร่วม)
- กล้อง (`Assets/Scripts/Systems/CameraController.cs`): WASD/arrow pan (line 89), right-drag pan (112-122), scroll zoom (134) · clamp ที่ 137/164-182/187-218 (**ห้ามแตะ clamp**)
- right-click ต้องมีปุ่มจอ: cancel placement (`PlacementController.cs:88`), exit demolish (`DemolitionController.cs:72`)
- **คีย์ไม่มีปุ่มเลย (ต้องเพิ่ม)**: **Esc→pause menu** (`PauseMenuController.cs:42`), F5 save/F9 load (`InputManager.cs:34-37`), P power overlay
- hover UI แตก touch: CursorManager (ปิดตอน touch), BuildingUpgradeUI nameplate, PlacementController ghost, DemolitionController highlight · **BuildingUpgradeUI เลือกอาคารเป็น tap อยู่แล้ว** (ดี)
- `EventSystem.IsPointerOverGameObject()` 5 จุด (no-arg) ต้องแก้เป็น fingerId-aware: BuildingUpgradeUI:123, CoreTowerPanelUI:109, LabPanelUI:108, MemorialPanelController:52, CursorManager:57
- Canvas ทุกตัว = ScaleWithScreenSize ref **1536×864** match=0 (width) · **ไม่มี safe-area** → iPad 4:3 แถวบน/ล่างล้นได้
- Save: `SaveManager` = `File.WriteAllText(Application.persistentDataPath/savegame.json)` → **WebGL ต้อง flush FS.syncfs** (หรือ fallback PlayerPrefs) · `MetaProgress` (PlayerPrefs) รอด WebGL อยู่แล้ว

---

## 3) งาน #1 Crisis — ✅ CODE COMPLETE

**สิ่งที่ทำ** (ต่อยอดของเดิม ไม่สร้าง controller ใหม่ ตามกฎ STEP 0):

1. `Assets/Scripts/Data/DilemmaData.cs` — เพิ่ม field `public string title;` (หลัง `dilemmaId`) · data-driven หัวเรื่อง (SO ไม่อยู่ใน SaveData = ปลอดภัย)
2. `Assets/Scripts/UI/DilemmaPopupController.cs` — **เขียนใหม่**: เพิ่ม `titleText` + `confirmButton` · เปลี่ยนจากกดปุ่ม commit ทันที เป็น **select-then-Confirm** (`SelectA/B/C()` ไฮไลต์ + Outline glow, `Confirm()` = `Resolve(index)`) · confirm `interactable=false` จนกว่าจะเลือก · exit เดิม `RaiseDilemmaResolved` คงไว้ · title ว่าง→fallback "เหตุการณ์วิกฤต"
3. `Assets/Editor/TooltipDilemmaSetup.cs` — re-skin `SetupDilemmaPopup` เป็น **กรอบโลหะ 2 คอลัมน์**: ซ้าย=หัวเรื่อง(ขอบเหลือง Outline)+เส้นคั่น+กล่องคำอธิบาย · ขวา=ปุ่ม A/B/C เรียงตั้ง(ชิปตัวอักษร) · ล่าง=ปุ่มยืนยันเต็มกว้าง · **idempotent** (ลบ TooltipPanel/DilemmaPopupPanel/DilemmaPopupController เก่าก่อนสร้าง) · helper ใหม่ self-contained (LoadUISkin, MetalPlate, PlaceTL, FillParentText, MakeChoice, MakeMetalButton) · ผูก persistent listener SelectA/B/C + Confirm
   - หมายเหตุ: `CreateButton` เดิมเหลือ dead code (ไม่ใช้แล้ว แต่ compile ผ่าน)

**สำคัญ**: scene `Gamescene.unity` มี persistent listener baked ชี้ `ChooseA/B/C` (บรรทัด 4488/171108/174940) — เป็น **string method name (reflection) ไม่ใช่ compile-time ref** → ลบ ChooseA/B/C **ไม่ทำให้ compile error** · setup ใหม่จะลบ popup เก่า+สร้างใหม่พร้อม Select/Confirm ตอนรันเมนู

**verify #1**: ปิด Editor → รันเมนู **"NuclearReMind/Setup Tooltip and Dilemma UI"** → compile · Show ด้วย DilemmaData 2 ใบต่างกัน (title/scenario/A/B/C เปลี่ยนครบ) · เลือก A/B/C แล้วยืนยัน ผลตรง · ยืนยันไม่ได้ถ้ายังไม่เลือก

---

## 4) งาน #2 Quiz Explanation — 🔶 IN PROGRESS

### ✅ เสร็จแล้ว (core)
1. `Assets/Scripts/Data/QuizQuestionSO.cs` — เพิ่ม 2 field:
   - `public string explanationTitle;` (ว่าง = ใช้ topicTitle)
   - `public int scoreDelta = 8;` (badge display-only: ถูก +N เขียว/ผิด −N แดง · ไม่แตะ Knowledge จริง)
2. `Assets/Scripts/Managers/TimeManager.cs` — enum `PauseReason` ต่อท้าย `QuizExplanation`
3. `Assets/Scripts/Managers/EventManager.cs` — เพิ่ม event `OnCodexUnlockRequested (Action<string>)` + `RaiseCodexUnlockRequested(string entryId)` (section Codex)
4. `Assets/Scripts/Managers/CodexManager.cs` — subscribe `OnCodexUnlockRequested` ใน OnEnable/OnDisable + `HandleCodexUnlockRequested(entryId) => UnlockById(entryId)`
5. `Assets/Scripts/Managers/QuizManager.cs` — `SubmitAnswer`: เปลี่ยน codex unlock จาก **direct call (ทั้งถูก/ผิด)** เป็น **`RaiseCodexUnlockRequested` เฉพาะ `correct`** (แก้ทั้ง doc comment + code) → ตอบผิดไม่ปลด, ผ่าน event
   - **เช็คแล้ว: QuizManagerTests ไม่แตก** — `CodexLinkedQuiz_UnlocksCodexOnAnswer` ตอบถูก + CodexManager subscribe ทัน (NewComponent เรียก OnEnable) → ยัง unlock + knowledge=10 ✓
6. `Assets/Scripts/UI/QuizExplanationPopupController.cs` — **สร้างใหม่เสร็จ**: subscribe `OnQuizAnswered` → `GetById` → `Show(quiz, wasCorrect)` · pause `QuizExplanation` ตอนโชว์/resume ตอนปิด · reward bar โชว์เฉพาะ `wasCorrect && codexUnlockId` (ตอบผิด=ซ่อน) · ปลด Codex ทำที่ QuizManager (popup แค่แสดง)
   - **public fields ที่ setup ต้อง wire**: `popupPanel`, `titleText`, `scoreBadgeText`, `bodyText`, `rewardBar`, `rewardIcon`(Image), `rewardText`, `closeButton` · closeButton ผูก onClick ที่ `Start()` เอง (ไม่ต้อง persistent listener)

### ⬜ เหลือทำ (2 ไฟล์)

**A) `Assets/Editor/QuizExplanationSetup.cs` (สร้างใหม่)**
- `[MenuItem("NuclearReMind/Setup Quiz Explanation UI")]` · idempotent (ลบ "QuizExplanationCanvas" เก่าก่อน) · เปิด Gamescene → build → MarkSceneDirty + SaveScene
- **canvas แยกเอง** (ไม่ใช่ลูก HUDCanvas): GameObject "QuizExplanationCanvas" root · `Canvas` ScreenSpaceOverlay **sortingOrder 70** (เหนือ quiz popup=0 ใต้ pause=100) · `CanvasScaler` ScaleWithScreenSize ref (1536,864) match 0 · `GraphicRaycaster` · ensure `EventSystem` มี
- ใช้สกินโลหะ (self-contained helper แบบเดียวกับ TooltipDilemmaSetup): `frame_metal.png`, `plate_inset.png`, `btn_metal.png` · fallback สีถ้าไม่มี
- **layout (mockup)** — dialog กลางจอ `DW=820 DH=560 P=28`:
  - overlay เต็มจอ (Image ดำ ~0.72) → dialog (frame_metal 9-slice)
  - **หัวกลางบน** `titleText`: x=P y=P w=DW-2P h=48, MiddleCenter, gold bold, best-fit 20-34
  - **มุมขวาบน** `scoreBadgeText`: x=DW-P-140 y=26 w=140 h=44, right-align bold ~30 (สร้างทีหลัง = render บน title)
  - เส้นคั่น: y=84 x=P w=DW-2P h=3 (gold α0.55)
  - **body plate** (plate_inset): x=P y=94 w=DW-2P h=286 · `bodyText` fill+padding18, best-fit 14-22, upper-left, word-wrap
  - **reward bar** (plate_inset): x=P y=392 w=DW-2P h=76 · `rewardIcon` ซ้าย (12,8 size 60×60) · `rewardText` เต็มขวา (offset left 86) สีฟ้า (0.59,0.80,1)
  - **ปุ่มปิด** "ปิด" (btn_metal): x=290 y=480 w=240 h=52 (MakeMetalButton) → wire `closeButton`
  - `rewardBar` = GameObject ของ plate (SetActive คุมโดย controller)
- ทำ controller: `AddComponent<QuizExplanationPopupController>()` (บน canvas หรือ child) → wire fields ทั้งหมด → `overlay.SetActive(false)` → `EditorUtility.SetDirty`

**B) `Assets/Editor/RunAllSetups.cs`** — เพิ่ม `"NuclearReMind/Setup Quiz Explanation UI"` ใน `MenuOrder` (วางหลัง `"NuclearReMind/Setup Quiz System"` หรือหลัง `"Setup Story Content"`)

**verify #2**: compile · ตอบถูก→เขียว +N + ปลด Codex จริง (footer โชว์) · ตอบผิด→แดง −N + ไม่ปลด (footer ซ่อน) · explanation ตาม SO · ข้อความไม่ล้น · setup รันซ้ำไม่ซ้อน

---

## 5) งาน #3 Decor — ⬜ NOT STARTED (สเปคครบ · density=8%)

**คัดลอก asset ก่อน**: 8 PNG จาก `C:\Users\UsEr\Downloads\drive-download-20260712T204202Z-2-001` → `Assets/Sprites/Decor/` (อย่าแก้ต้นฉบับ)

**A) `Assets/Editor/DecorSetup.cs` (ใหม่)** `[MenuItem("NuclearReMind/Setup Decor")]`
- import 8 PNG ตาม `BuildingArtSetup.ConfigureImporter`: Sprite/Single, **pivot bottom-center จาก alpha bounds**, PPU=artW/targetWidth (เทียบ scale กับ tile/อาคาร), FilterMode.Point, Uncompressed, alphaIsTransparency, mipmap off
- สร้าง/หา GameObject "DecorSpawner" ในซีน + ใส่ component `DecorSpawner` + assign `Sprite[] decorSprites` (โหลด 8 ตัว) · idempotent (ไม่ซ้อน)
- ใส่ใน `RunAllSetups.cs` **หลัง** "Fill Grids" (IsoGroundPainter/พื้น)

**B) `Assets/Scripts/Systems/DecorSpawner.cs` (ใหม่)** — code-built sprite แบบ worker/building
- serialized: `Sprite[] decorSprites`, `[Range(0,30)] int densityPercent = 8`, `int apronMargin`(อ่านจาก OreDepositManager ถ้ามี ไม่งั้น 35), `float jitter = 0.25f`, ระยะกันซ้อนชิ้นใหญ่
- วน tile นอกกริดใน `[-apronMargin, 43+apronMargin)` เก็บเฉพาะ `x<0||x>=43||y<0||y>=43`
- deterministic: `int h = IsoGroundPainter.Hash(x,y);` · วางเมื่อ `h % 100 < densityPercent` · เลือกชิ้น `(h/100) % decorSprites.Length` · jitter ตำแหน่งจาก hash (ไม่ใช่ Random รายเฟรม)
- world pos: `GridManager.Instance.IsoToWorldF(x + jx, y + jy)` · SpriteRenderer sortingLayerName="Buildings", sortingOrder=`GridManager.SortOrder(x, y, GridManager.SortTier.Unit)`
- ชิ้นใหญ่ (ซากรถ) เว้นระยะกันซ้อน (เช็ค cell รอบข้างจาก hash หรือ min spacing) · วัสดุเดียวกับ worker/building (รับแสงเท่ากัน)
- spawn ตอน Start / หรือ hook วันใหม่ (ให้ deterministic วางเดิมทุกครั้ง)

**verify #3**: compile · decor โผล่เฉพาะนอกกริด ไม่ทับ 43×43/อาคาร · วางเดิมทุกครั้ง · sorting ถูก (ไม่จมพื้น/ไม่บังผิด) · density ปรับ Inspector ได้ · setup รันซ้ำไม่ซ้อน

---

## 6) งาน #4 iPad Touch — ⬜ NOT STARTED (ต้องถาม decision ก่อนเริ่ม)

**ถามผู้ใช้ก่อน**: (1) ปุ่ม help/cancel วางมุมไหน · (2) เก็บคีย์บอร์ด PC ไว้ด้วยใช่ไหม (แนะนำ: ใช่ เพิ่ม touch ควบคู่)

**ทำ 4 ส่วน (compile ผ่านทุกส่วน · แบ่ง commit ได้):**
- **A** Touch input + กล้อง: world-pick จาก touch (`InputManager.GetMouseGridPosition` + 2 จุด ScreenToWorldPoint ใน CameraController/MemorialPanel) · 1 นิ้วลาก=pan (แทน right-drag), pinch=zoom (แทน scroll), **คง clamp เดิม** · ปุ่ม "ยกเลิก/ย้อน" บนจอ (แทน right-click) · กัน tap ทะลุ UI (`IsPointerOverGameObject(fingerId)` 5 จุด)
- **B** hover+คีย์ → tap-to-select + ปุ่มจอ: BuildingUpgradeUI tap เลือกอาคาร (เป็นอยู่แล้วบางส่วน) · ปุ่มจอแทน Q/E(+/-), B(พับ), F1(help), Space(pause), Esc(pause menu), F5/F9(save/load) · **คงคีย์บอร์ดเดิมไว้ด้วย**
- **C** Responsive + safe-area: Canvas ScaleWithScreenSize (มีแล้ว ref 1536×864) · เพิ่ม `Screen.safeArea` handling · ปุ่มสำคัญไม่ชิดขอบ · ปุ่มแตะ ≥~44pt · เทส 4:3 (iPad) + 16:9
- **D** WebGL save: `SaveManager` เพิ่ม FS.syncfs หลังเขียน (jslib) หรือ fallback PlayerPrefs · **คง SaveData/format เดิม** · Player Settings WebGL (template/compression/exceptions)

---

## 7) งาน #5 WebGL build + itch.io — 🔒 BLOCKED

- ทำ **หลัง #4 เสร็จ** (ไม่งั้นเล่น iPad ไม่ได้จริง)
- **A** `Assets/Editor/WebGLBuilder.cs`: `[MenuItem]` + static `Build()` (-executeMethod batchmode) → `Build/WebGL/` · Player Settings: Brotli + decompression fallback ON, color space, WebGL template, จอพอดี iPad, exceptions demo-friendly · รัน batchmode 0 error
- **B** upload itch ด้วย `butler` — **ต้องหยุดถามผู้ใช้ก่อน**: itch username, game slug, API key/login (อย่าเดา อย่า hardcode) → `butler push Build/WebGL <user>/<game>:html5` · ตั้งหน้า itch: play in browser + mobile-friendly ON

---

## 8) ลำดับทำต่อ (แนะนำ)

1. ~~**จบ #2**: เขียน `QuizExplanationSetup.cs` + เพิ่มใน `RunAllSetups.cs`~~ ✅ เสร็จ 2026-07-13 → **รอ compile ในเอดิเตอร์ + รันเมนู "Setup Quiz Explanation UI"**
2. ~~**#3 Decor**: DecorSpawner + DecorSetup + RunAllSetups~~ ✅ โค้ดเสร็จ 2026-07-13 → **คัดลอก 8 PNG ลง `Assets/Sprites/Decor/` แล้วรันเมนู "Setup Decor"** (ไม่มี asset = spawner เปล่า ไม่พัง)
3. ~~**#4 Touch**~~ ✅ v1 เสร็จ 2026-07-13 (default: ไฮบริด · 2 นิ้ว=กล้อง · 1 นิ้ว=แตะวาง) → **เทสต์บน iPad จริง** ปรับ `pinchZoomSpeed`/`enableTouch` ใน CameraController Inspector ได้ · (ยังไม่ทำ: ปุ่มลอย cancel/help, safe-area — ทำเพิ่มถ้าเทสต์แล้วขาด)
4. **#5**: ~~WebGLBuilder~~ ✅ โค้ดเสร็จ 2026-07-13 → **ปิด Unity → รัน "Build WebGL" (หรือ batch -executeMethod ...WebGLBuilder.Build)** → `butler login` → `butler push Build/WebGL <user>/<game>:html5` (ต้องรอ user/slug + login itch)

> ⚠ repo hygiene: `QuizExplanationPopupController.cs.meta` มาจาก pull แบบ untracked (source machine commit `.cs` แต่ไม่ commit `.meta`) — ต้อง `git add` .meta ทั้งของไฟล์นี้ + 3 ไฟล์ใหม่ (QuizExplanationSetup/DecorSetup/DecorSpawner) ให้สองเครื่อง GUID ตรงกัน
> เมื่อจบแต่ละงาน: **ปิด Unity → รัน compile batch → error CS ต้อง = 0** (หรือ focus เอดิเตอร์ที่เปิดอยู่ให้ recompile) แล้วให้ผู้ใช้รันเมนู setup ที่เกี่ยวข้อง (+ RunAllSetups) เพื่อ apply UI ในซีน
