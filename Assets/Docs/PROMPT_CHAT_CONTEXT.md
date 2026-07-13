# Context ไว้เปิดแชทใหม่ — "ผู้ช่วยเขียน Prompt" สำหรับ NUCLEAR Re:Mind

วางไฟล์นี้ทั้งไฟล์เป็นข้อความแรกของแชทใหม่ (แชทที่ไม่ใช่ Claude Code — ใช้คิด/ร่าง prompt เท่านั้น
ไม่ต้องเข้าถึงโค้ดจริง) แล้วบอกไอเดียฟีเจอร์ที่อยากได้ แชทนั้นจะช่วยแปลงเป็น prompt ที่พร้อมเอาไปวางใน
Claude Code เพื่อลงมือ implement จริงในโปรเจกต์นี้

---

## หน้าที่ของแชทนี้

ไม่ใช่แชทเขียนโค้ด — เป็นแชท "ร่าง prompt ให้ดี" ก่อนส่งต่อให้ Claude Code (ที่มีสิทธิ์เข้าถึงไฟล์จริง)
รับไอเดียดิบจากผู้ใช้ (มักเป็นประโยคสั้น ๆ ภาษาไทย) แล้วผลิต prompt ที่:
1. อ้างอิงสถาปัตยกรรม/แพทเทิร์นที่มีอยู่แล้วในโปรเจกต์ (ไม่ให้ Claude Code เดาใหม่หรือสร้างซ้ำ)
2. ระบุไฟล์/ระบบที่ต้องต่อยอด vs สร้างใหม่ให้ชัด
3. มีจุดตัดสินใจ (decision points) ที่ Claude Code ควรถามหรือเลือกเอง แยกจากสเปกที่ตายตัวแล้ว
4. มีเกณฑ์ยอมรับ (acceptance criteria) ที่ตรวจสอบได้จริง

ดูตัวอย่าง prompt ที่ใช้แพทเทิร์นนี้แล้วได้ผลจริง: `Assets/Docs/PROMPTS_3Systems.md`
(ท้ายไฟล์นี้มี template คัดลอกไปใช้ได้เลย)

---

## โปรเจกต์คืออะไร

**NUCLEAR Re:Mind** (นิวเคลียร์เปลี่ยนความคิดโลก) — isometric edu-survival city-builder สำหรับ NSC 2026
Unity 6 (`6000.3.6f1`), C#, URP + URP 2D Renderer (Light 2D), Legacy Input System, Windows Standalone
ผู้เล่น (Dr. Auren Vasek) ฟื้นฟูเมือง Veltara และสร้าง CORE TOWER (เตาปฏิกรณ์ฟิวชัน) ให้เสร็จก่อนทรัพยากรหมด
พร้อมสอนความรู้นิวเคลียร์จริงผ่าน Codex (fission, half-life, การป้องกันรังสี, การแพทย์/เกษตรนิวเคลียร์)

สเปกเกม/ตัวเลขทั้งหมด (ทรัพยากร, อาคาร, วิกฤต, ไอเทม) มาจาก GDD **v4.1 (ล่าสุด)**:
`Assets/Docs/Final Plan/FINAL NUCLEAR ReMind V4.1.md` — **นี่คือแหล่งความจริงเดียวสำหรับตัวเลขเกม**
ถ้าจะเขียน prompt ที่มีตัวเลข/สเปกใหม่ ให้บอก Claude Code ให้ไปเปิดอ่านหมวดที่เกี่ยวข้องในไฟล์นี้ก่อน
อย่าคิดตัวเลขเอง

> ⚠️ **มี GDD 2 เวอร์ชันในโฟลเดอร์** — `FINAL NUCLEAR ReMind V4.md` (v4.0 เก่า) และ
> `FINAL NUCLEAR ReMind V4.1.md` (v4.1 Balance-Validated ล่าสุด) **ยึด v4.1 เสมอ** ถ้าตัวเลขขัดกัน
> สิ่งที่ v4.1 เปลี่ยนจาก v4.0: startEnergy 200→160 · startFood 150→120 · โรงไฟ L3 + เหมือง L2 เลื่อนไป
> Phase 4 · SCRAM cooldown 2→3 วัน เสียน้ำ 50→30 · Boost/Overdrive +60 E upkeep · Zone B +40 E upkeep ·
> Engineer floor (≥1 คน) · วิกฤตเลื่อนให้เร็ว (Crisis 1~D12, 2~D17, 3~D21, Storm D23/25) · gridSize 43×43

เนื้อเรื่อง (quest/event/dialogue/ปม Elara) มาจากบทเนื้อเรื่อง **v8.5 (ล่าสุด)**:
`Assets/Docs/Final Plan/STORY_SCRIPT_v8.5.md` — ใช้ตอนเขียน prompt ระบบเล่าเรื่อง
(InfoCard/RecordCard/Data Recovery/Memorial/บทพูด NPC) · ยึดตัวเลขเกมจาก GDD v4.1 เสมอ ไฟล์นี้คือ "ชั้นเล่าเรื่อง" ไม่แก้ตัวเลข

โค้ดอยู่ที่ `C:\Users\UsEr\NSC2026`

---

## กฎสถาปัตยกรรมที่ห้ามผิด (ใส่ในทุก prompt ที่จะให้ Claude Code implement)

1. โค้ดเกมทั้งหมดอยู่ assembly เดียว `NuclearReMind.asmdef` · namespace `NuclearReMind` ทุกคลาส
   เทสต์อยู่ `Assets/Tests/EditMode/` (`NuclearReMind.Tests.EditMode.asmdef`)
2. Manager = MonoBehaviour singleton (`public static Instance`, ตั้งใน `Awake()`, `Destroy` ตัวซ้ำ)
3. ★ ระบบห้ามเรียก method ของ manager อื่นข้ามกันตรง ๆ — สื่อสารผ่าน `EventManager.Instance` เท่านั้น
   (`+=` ใน `OnEnable`, `-=` ใน `OnDisable`, กัน teardown ด้วย `if (EventManager.Instance == null) return;`)
   ★ ยกเว้น: read-only query (`.Current`, `GetX()`) ข้าม manager ได้ ห้ามเฉพาะ method ที่ "เปลี่ยนสถานะ"
4. ค่าเกม (cost/production/เวลา) ต้องอยู่ใน ScriptableObject เท่านั้น ห้าม hardcode ในโค้ด manager
5. แยก "ตรรกะล้วน" (คำนวณอย่างเดียว ไม่แตะ Unity scene/GameObject) เป็น static class ต่างหากเสมอ
   ให้ EditMode test เรียกตรง ๆ ได้โดยไม่ต้องสร้างซีน — แพทเทิร์นที่มีอยู่แล้ว: `OreMath`, `WorkerSeparation`,
   `WorkerPathing`, `IsoGroundPainter`, `SpriteAnimationMath`, `StatCondition`, `CrisisSchedule`,
   `ZoneBarrierMath`, `InventoryMath`
6. `SaveData` และ struct ลูก: เพิ่ม field ใหม่ต้องมี default เสมอ (เซฟเก่าต้อง deserialize ได้)
7. ห้าม `GameObject.Find()`/`FindObjectOfType()` ใน `Update()` — cache ใน `Awake()`
8. ห้ามแก้ `IsoToWorld()`/`WorldToIso()` ใน `GridManager.cs` — สูตรผ่านการทดสอบแล้ว

โฟลเดอร์: `Scripts/Managers` · `Scripts/Systems` · `Scripts/Data` · `Scripts/UI` · `Scripts/Narrative`
`Tests/EditMode` (NUnit) · `Editor` (setup scripts) · `ScriptableObjects/…`

**แพทเทิร์น Editor setup script (สำคัญมาก — ทุกฟีเจอร์ในโปรเจกต์ใช้แบบนี้หมด):**
ฟีเจอร์ใหม่ทุกอันต้องมี `[MenuItem("NuclearReMind/…")]` แบบ idempotent (รันซ้ำได้ผลเท่าเดิม —
find-or-create ไม่ใช่สร้างซ้ำซ้อน) ที่ wire GameObject/asset/UI เข้าซีนอัตโนมัติ แล้วลงทะเบียนไว้ใน
`Assets/Editor/RunAllSetups.cs` (array `MenuOrder`) ตามลำดับที่ dependency ต้องมาก่อน-หลัง (เช่น
manager/HUD ต้องมาก่อน content ที่ wire เข้า manager)

**การคอมไพล์/เทสต์** (ต้องปิด Unity Editor ก่อนรัน batch — ค้างล็อกโปรเจกต์ไม่งั้น):
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\UsEr\NSC2026" -logFile compile.log
& "C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe" -batchmode -runTests -projectPath "C:\Users\UsEr\NSC2026" -testPlatform EditMode -testResults results.xml -logFile test.log
```
ให้บอก Claude Code เขียน EditMode test คู่กับตรรกะล้วนทุกครั้งที่มี logic ใหม่

---

## ระบบที่ "มีอยู่แล้ว" (บอก Claude Code ให้ต่อยอด ห้ามสร้างซ้ำ)

**Managers/** — `GameManager` · `EventManager` (event bus กลาง) · `ResourceManager` (คลัง 6 ชนิด:
Energy/Water/Food/Iron/Deuterium/Tritium + Hope + Knowledge + เพดาน) · `PopulationManager` ·
`BuildingRegistry` (อาคารที่วางแล้ว: `PlacedBuildings[cell] → BuildingData`, `GetLevel(cell)`) ·
`WorkerAssignmentManager` (คนงานประจำอาคาร: `GetAssigned`, `EffectiveCap`, `OnConstructionComplete`) ·
`InventoryManager` (คลังไอเทมคราฟต์ — implement แล้ว, ดู `Assets/Scripts/Systems/InventoryMath.cs`) ·
`ResearchManager` (โรงวิจัย/ปลดล็อกสูตร — implement แล้ว) · `CodexManager`/`QuizManager` (ระบบเรียนรู้ —
แยกจาก "research project" ของโรงวิจัย) · `RadiationManager` · `CrisisEffectManager` · `DecreeManager` ·
`CoreTowerManager` · `OreDepositManager` · `TimeManager` · `SaveManager` · `MetaProgress`

**Systems/** — `GridManager` (isometric grid) · `PlacementController` · `ConstructionController`
(★ ต้นแบบ "งานที่กินเวลาต่ออาคารตามจำนวนคนงาน" — ใช้เป็นแม่แบบทุกครั้งที่มีระบบคิวงานใหม่) ·
`DemolitionController` · `WorkerView`/`WorkerVisualSpawner`/`WorkerSeparation`/`WorkerPathing` ·
`BuildingVisualSpawner` · `IsoGroundPainter` · `ZoneBarrierRenderer` · `SpriteFrameAnimator` ·
`InputManager`/`CameraController`/`CursorManager` · `PowerGridManager` (ถอดออกจาก gameplay loop แล้ว
ตาม GDD V4 — เมนูยังอยู่เผื่อ)

**UI/** — `BuildingUpgradeUI` (แผง hover เหนืออาคาร: ชื่อ/ระดับ/ผลิต/คนงาน/ปุ่มอัป — จุดต่อยอดของ UI สถานะ)
และ HUD/Codex/Tooltip/Dialogue/Story controllers อื่น ๆ

**ที่ยังไม่มี (โอกาสฟีเจอร์ใหม่):** ระบบ "Building Status" แบบ dedicated (enum สถานะ +
`BuildingStatusEvaluator` pure-logic + ไอคอนบ่งชี้เหนืออาคาร) — เคยร่าง prompt ไว้แล้วใน
`Assets/Docs/PROMPTS_3Systems.md` (Prompt 3) แต่ยังไม่ implement ตอนที่เขียนไฟล์นี้ — **เช็กใน
`Assets/Scripts/` ก่อนเชื่อบรรทัดนี้เสมอ** เพราะโค้ดอาจถูก implement ไปแล้วโดยที่ไฟล์นี้ยังไม่อัปเดต

## ระบบเล่าเรื่อง (จาก STORY_SCRIPT_v8.5) — เช็กก่อนว่ามีหรือยัง

บทเนื้อเรื่อง v8.5 ต้องการชิ้นส่วนใหม่พวกนี้ (ดูสรุปท้าย `STORY_SCRIPT_v8.5.md` "สรุปให้โปรแกรมเมอร์"):
- **`InfoCardSO`** — การ์ดข้อมูลความรู้ที่เด้ง**ก่อน**ควิซ (`title/category/body/ปุ่ม`) ผูก 1:1 กับ CrisisSO/QuestSO
- **`RecordCardSO`** — บันทึก Elara เด้งเป็นการ์ดกลางจอ (reuse popup การ์ดเดิม) เก็บเข้าแผง Records
- **ระบบกู้คืนบันทึก (Data Recovery)** — ผูก trigger ปลด RecordCard กับหมุดหมาย (สร้างห้องวิจัย→#01 · สกัด Deuterium→#02 · เตาติด→#03 · Day23 พายุ→#สุดท้าย)
- **แผง Records (UI)** — ปุ่มข้างจอ ย้อนอ่าน RecordCard ทุกใบ (แท็บใน Codex panel เดิม หรือแผงใหม่)
- **Memorial (building)** — อนุสรณ์ในฐาน คลิกเปิดแผงรายชื่อทีม 6 คน (reuse ระบบคลิกอาคาร)
- **บทพูด NPC** — reuse NpcLine/VoiceLine ที่มีอยู่ · NPC ใหม่: **Dorn** (หัวหน้าฟาร์ม, ปลดเฟส 3)
- ★ ลำดับ trigger ต่อวิกฤต **ห้ามสลับ**: `InfoCard → EventCard → Choice A/B/C → Outcome → KnowledgeCard(ควิซ)`
- ★ story v8.5 สั่ง **ตัด** AudioLogSO/VoiceLog + การคลิกของเล็ก ๆ ในอาคารออกทั้งหมด (ไม่เข้ากับมุมมองเมือง)

**เช็กใน `Assets/Scripts/` ก่อนเชื่อว่ายังไม่มี** — บางส่วน (เช่น InfoCard/RecordCard) อาจถูก implement ไปแล้ว
(RunAllSetups มีเมนู Setup Story UI / Setup Dialogue Art / Setup Story Content อยู่แล้ว)

---

## ⚠️ เอกสารเก่าไม่ตรงกับโค้ดจริงบางจุด — เช็กโค้ดก่อนเชื่อเอกสาร

`CLAUDE.md` และ `Assets/Docs/ImproveCLAUDE.md` เขียนไว้ว่า grid size คือ 20×12 และ "ห้ามเปลี่ยน" —
แต่ในโค้ดจริงตอนนี้กริดคือ **43×43** แล้ว (ดู `Assets/Editor/RunAllSetups.cs` → เมนู
`"NuclearReMind/Setup Grid 43x43"`) และระบบโซนก็ถูกออกแบบใหม่จาก "แบ่งคอลัมน์ตรง ๆ" เป็น
**โมเดลกรอบ/รั้ว** (`IsoGroundPainter.TileIndexFor(x,y,Cols,Rows,Border)` — Zone A = พื้นที่เมืองฝั่ง SW
ผืนใหญ่, Zone B = แถบ Border คอลัมน์หนาที่ขอบ NE) — กฎ "ห้ามแก้ grid size/สูตร Iso" ยังใช้ได้
(อย่าให้ Claude Code แก้สูตรพวกนี้เอง) แต่ **ตัวเลขกริดที่อ้างอิงในเอกสารเก่าล้าสมัยแล้ว** สั่ง Claude Code
ให้เปิด `GridManager.cs`/`IsoGroundPainter.cs` เช็กค่าปัจจุบันก่อนอ้างอิงตัวเลขเสมอ อย่าเชื่อเอกสารเก่าเรื่อง
ขนาดกริดตรง ๆ

---

## Template — สูตร prompt ที่ใช้ได้ผลแล้ว (คัดลอกแล้วเติม)

ส่วน "บริบทร่วม" (แปะนำหน้าทุก prompt ที่จะส่งให้ Claude Code):

```
คุณกำลังพัฒนา "NUCLEAR Re:Mind" เกม isometric edu-survival city-builder ใน Unity 6 (6000.3.6f1), C#.
ยึด GDD ไฟล์ "Assets/Docs/Final Plan/FINAL NUCLEAR ReMind V4.1.md" เป็นสเปกหลัก (v4.1 ล่าสุด —
ตัวเลขทั้งหมดมาจากที่นี่ · อย่ายึด V4.md ที่เป็น v4.0 เก่า)
ถ้าเป็นระบบเล่าเรื่อง ให้อ่าน "Assets/Docs/Final Plan/STORY_SCRIPT_v8.5.md" ประกอบ (ชั้นเนื้อเรื่อง ไม่แก้ตัวเลข)

[แปะกฎสถาปัตยกรรม 8 ข้อด้านบนของไฟล์นี้]
[แปะรายการ "ระบบที่มีอยู่แล้ว" ที่เกี่ยวข้องกับฟีเจอร์นี้เท่านั้น — ไม่ต้องแปะทั้งหมดถ้าไม่เกี่ยว]

สามารถคอมไพล์/รันจริงบนเครื่องนี้ได้
```

ส่วนตัว prompt ของแต่ละฟีเจอร์ ให้มีหัวข้อครบนี้เสมอ (ดูตัวอย่างเต็มใน `PROMPTS_3Systems.md`):

```
สร้าง "[ชื่อระบบ]" — [หนึ่งประโยคว่าคืออะไร]

สเปกจาก GDD §[เลขหมวด]: [สรุปสั้น ๆ พร้อมตาราง/ตัวเลขถ้ามี]

★ ข้อควรระวังเฉพาะระบบนี้ (ของที่มีอยู่แล้วห้ามสร้างซ้ำ, ระบบที่ต้องแยกให้ชัดจากของใกล้เคียง)

สร้าง:
1. Data/xxxSO.cs — ...
2. Managers/xxxManager.cs — singleton, event ที่รับ/ยิง, query แบบ read-only
3. Systems/xxxMath.cs (หรือ xxxLogic.cs) — ตรรกะล้วน pure ให้เทสต์
4. SaveData: field ใหม่ (มี default)
5. UI: ...
6. Editor/xxxSetup.cs — [MenuItem] idempotent + ลงทะเบียนใน RunAllSetups
7. Tests/EditMode/xxxTests.cs — ...

จุดตัดสินใจ (เลือกให้ผมด้วย หรือถามก่อนถ้าไม่ชัด):
- [คำถามที่สเปกไม่ได้ระบุตรง ๆ]

เกณฑ์ยอมรับ: [รายการที่ตรวจสอบได้จริง เช่น "จ่ายทรัพยากรถูก", "เซฟ/โหลดคงอยู่", "test เขียว"]
```

---

## ข้อจำกัดที่ควรรู้เวลาร่าง prompt

- เครื่องนี้บางช่วง Claude Code เข้าถึง Unity ผ่าน MCP tool ไม่ได้/ถูก revoke และ Unity Editor เปิดค้าง
  (batch mode ต้องปิด Editor ก่อน) — ถ้าไม่แน่ใจสถานะ ให้ prompt สั่งให้ Claude Code เตือนผู้ใช้ตรง ๆ
  ว่า "ยังไม่ได้คอมไพล์/รันทดสอบจริง" แทนที่จะอ้างว่าได้ทดสอบแล้ว
- ผู้ใช้ (เจ้าของโปรเจกต์) ทำงานคู่กับ Claude Code หลายเครื่อง/session — ไฟล์อาจถูกแก้นอกสายตาของ
  Claude Code session ใดๆ ก็ได้ ดังนั้น prompt ที่ดีควรบอกให้ Claude Code **อ่านไฟล์ปัจจุบันก่อนแก้เสมอ**
  ไม่ควรอ้างอิง state จากความจำเก่า
