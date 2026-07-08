# NUCLEAR Re:Mind — Story Implementation Guide
### สำหรับเปิดใน Claude Code / Cursor แล้วสั่งให้ Claude สร้างระบบเนื้อเรื่องได้เลย

> **โปรเจกต์:** NUCLEAR Re:Mind — Survival City-Builder, Unity 6, C#, มุมมอง isometric
> **ไฟล์นี้ครอบคลุม:** ระบบเล่าเรื่อง + เนื้อหาทั้งหมด **เริ่มที่ Phase 1** (ตัดพาร์ทเปิดเกม/Intro cinematic ออกแล้ว)
> **อ้างอิง:** GDD v4.0 §16 (มี `CrisisSO`, `CrisisChoice`, `QuizQuestionSO` อยู่แล้ว)

---

## 0. คำสั่งสำหรับ Claude — อ่านก่อนเริ่ม (READ FIRST)

Claude กำลังจะช่วย implement **ชั้นเนื้อเรื่อง (story layer)** ของเกม city-builder ที่มีระบบเมือง/เตา/ทรัพยากรอยู่แล้ว งานคือทำให้เนื้อเรื่องและความรู้นิวเคลียร์ไหลออกมาถูกจังหวะระหว่างเล่น

**สิ่งที่ต้องสร้าง (สรุป):**
1. **ScriptableObject ใหม่ 3 ตัว** — `InfoCardSO`, `RecordCardSO`, `MemorialSO`
   (ส่วน `CrisisSO`/`CrisisChoice`/`QuizQuestionSO` มีอยู่แล้วใน §16 — **ต่อยอด ไม่สร้างซ้ำ** ถ้าฟิลด์ไม่พอค่อยเพิ่ม)
2. **Runtime managers** — `StoryDirector` (คิวลำดับเหตุการณ์ตามวัน/เงื่อนไข), `CardUIController` (เด้งการ์ดกลางจอ), `RecordsPanelController` (แผงย้อนอ่านบันทึก)
3. **สร้าง SO asset** จากบล็อกข้อมูลใน §4 (ทุกบล็อก = 1 asset) แล้ว wire เข้ากับ `StoryDirector`

**กติกาเนื้อเรื่องที่ห้ามผิด (สำคัญที่สุด):**
- **ความรู้ต้องมาก่อนควิซเสมอ** — ทุกครั้งที่จะมีควิซ ต้องมี `InfoCard` เด้งอธิบายก่อน ควิซเป็นแค่ "ทบทวนสิ่งที่เพิ่งรู้" ไม่ใช่การเดา
  → **ลำดับบังคับ:** `InfoCard → EventCard → Choice(A/B/C) → Outcome → QuizCard` (ห้ามสลับ)
- **บันทึกทั้งหมดเป็นข้อความอ่าน เด้งเป็นการ์ดกลางจอ** — ไม่มีไฟล์เสียง ไม่มีการกดฟัง
- **ไม่มีการเข้าห้อง/คลิกของเล็กๆ ในอาคาร** — มุมมองมองทั้งเมืองจากบนลงมา ทุกอย่างเด้งมาหาผู้เล่นเอง
  → ปมเนื้อเรื่องส่งผ่านระบบ **"กู้คืนบันทึก" (Data Recovery)**: ห้องวิจัยถอดรหัสข้อมูลเก่าทีละส่วนตามหมุดหมาย พอสำเร็จ `RecordCard` เด้งกลางจอเอง แล้วเก็บเข้าแผง Records
- **ไม่บรรยายกิริยาท่าทางตัวละคร** · บทพูดสั้น · ภาษาห้วนแบบเกมบริหารจริง

**Player-facing string เป็นภาษาไทยทั้งหมด** (ตามที่เขียนในไฟล์นี้ ห้ามแปล) — field name / โค้ด เป็นอังกฤษ

**เมื่อพร้อม ให้ทำตามลำดับ:** อ่าน §1–§3 (สถาปัตยกรรม + schema + flow) → สร้างไฟล์ C# ตาม §5 → สร้าง SO asset จาก §4 → ต่อ `StoryDirector` เข้ากับ event bus ของเมือง (build อาคาร, สกัดเชื้อเพลิง, ค่า HEAT/Q/Hope เปลี่ยน, วันเปลี่ยน)

---

## 1. สถาปัตยกรรมภาพรวม (Architecture)

```
┌─────────────────────────────────────────────────────────┐
│  GAME SYSTEMS (มีอยู่แล้ว)                                 │
│  DayClock · ResourceManager · ReactorState(HEAT/Q/CORE%) │
│  BuildingManager · HopeManager · WorkerManager           │
└───────────────┬─────────────────────────────────────────┘
                │ events (OnDayChanged, OnBuildingBuilt,
                │         OnDeuteriumExtracted, OnStat…)
                ▼
┌─────────────────────────────────────────────────────────┐
│  StoryDirector  (ตัวคุมลำดับเนื้อเรื่อง)                    │
│  - ถือ list ของ StoryBeat (เรียงตาม trigger)              │
│  - ฟัง event จากเมือง → เช็ค trigger → ยิง beat            │
│  - บังคับลำดับ InfoCard→Event→Choice→Outcome→Quiz         │
└───┬───────────────┬───────────────┬─────────────────────┘
    ▼               ▼               ▼
CardUIController  RecordsPanel   CodexController
(เด้งการ์ดกลางจอ) (แผงย้อนอ่าน)   (ปลดล็อก Codex)
```

**Data layer (ScriptableObjects):**
| SO | สถานะ | หน้าที่ |
|----|-------|---------|
| `InfoCardSO` | **สร้างใหม่** | การ์ดข้อมูลความรู้ เด้งก่อนควิซ |
| `RecordCardSO` | **สร้างใหม่** | การ์ดบันทึก Elara (กู้คืน) เด้งกลางจอ |
| `MemorialSO` | **สร้างใหม่** | ข้อมูลรายชื่อทีม 6 คน (คลิกอาคารอนุสรณ์เปิด) |
| `CrisisSO` (+`CrisisChoice`,`CrisisOutcome`) | **มีแล้ว §16** | การ์ดวิกฤต + ตัวเลือก + ผล |
| `QuizQuestionSO` | **มีแล้ว §16** | ควิซ +8/+3 + คำอธิบาย + Codex |
| `StoryBeatSO` | **สร้างใหม่** | มัด (InfoCard? + Crisis? + Quiz[] + Record?) เข้าด้วยกัน + เงื่อนไข trigger |

---

## 2. Schema (สรุปฟิลด์ — โค้ดเต็มอยู่ใน §5)

### InfoCardSO
```
title        : string   // หัวข้อ เช่น "เชื้อเพลิงที่ซ่อนอยู่ในน้ำ"
category     : enum      // Energy / Reactor / Medical / Food / Ethics / Fusion
bodyTH       : string    // เนื้อหา 3-4 บรรทัด (ไทย)
buttonLabel  : string    // เช่น "เข้าใจแล้ว" / "รับทราบ"
```

### RecordCardSO
```
recordId     : string    // "elara_01" ...
authorLabel  : string    // "ระบบกู้คืนข้อมูลจากเครือข่ายเก่า · ผู้บันทึก: Dr. Elara Vane"
bodyTH       : string    // เนื้อความบันทึก (ไทย)
buttonLabel  : string    // "รับทราบ · เก็บเข้าแผง Records"
archiveTitle : string    // ชื่อที่โชว์ในแผง Records เช่น "บันทึก #01 — เชื้อเพลิงในน้ำ"
```

### StoryBeatSO  (ตัวมัดทุกอย่าง + trigger)
```
beatId       : string
triggerType  : enum      // OnDay / OnBuildingBuilt / OnDeuteriumExtracted /
                         //   OnReactorStart / OnStatThreshold / OnStormApproach
triggerParam : string    // เช่น "6" (day), "ResearchLab", "HEAT>80||Q>0.3"
infoCard     : InfoCardSO      // optional (null ได้)
crisis       : CrisisSO        // optional
quiz         : QuizQuestionSO[]// optional (ยิงหลัง Outcome)
record       : RecordCardSO    // optional
noteTH       : string          // คอมเมนต์สำหรับทีม (ไม่โชว์ผู้เล่น)
```

### CrisisSO / CrisisChoice / CrisisOutcome (คาดว่ามีแล้ว — ต่อยอดถ้าจำเป็น)
```
CrisisSO      : title, bodyTH, timeLimitDays, choices[3]
CrisisChoice  : id(A/B/C), labelTH, CrisisOutcome
CrisisOutcome : effects(StatDelta[]), afterTextTH  // afterText = log/voice/npc หลังเลือก
```

### QuizQuestionSO (คาดว่ามีแล้ว)
```
questionTH, options[3](text,isCorrect), explanationTH,
scoreCorrect(=8), scoreWrong(=3), codexUnlockId
```

---

## 3. Card Flow (ลำดับ runtime — pseudocode)

```csharp
// เมื่อ StoryDirector ตัดสินใจยิง beat หนึ่ง:
IEnumerator PlayBeat(StoryBeatSO b) {
    if (b.record   != null) yield return CardUI.ShowRecord(b.record);   // การ์ดบันทึกกลางจอ → archive
    if (b.infoCard != null) yield return CardUI.ShowInfo(b.infoCard);   // ★ ความรู้ก่อนควิซ
    if (b.crisis   != null) {
        var choice   = yield return CardUI.ShowCrisis(b.crisis);        // รอผู้เล่นเลือก A/B/C
        ApplyEffects(choice.outcome.effects);                          // ผลในเกม
        yield return CardUI.ShowAfterText(choice.outcome.afterTextTH);  // บทหลังเลือก
    }
    foreach (var q in b.quiz)                                           // ★ ควิซหลัง = ทบทวน
        yield return CardUI.ShowQuiz(q);                                // ให้คะแนน + ปลด Codex
}
```

**กฎเหล็ก:** `infoCard` (หรือ record ที่ให้ความรู้) ต้องเล่น **ก่อน** `quiz` เสมอ ถ้า beat ไหนมี quiz แต่ไม่มี infoCard และผู้เล่นยังไม่เคยรู้เรื่องนั้น = บั๊กดีไซน์ ให้เตือน

---

## 4. เนื้อหาเนื้อเรื่อง — Phase 1 เป็นต้นไป (แปลงเป็น SO asset ได้ทันที)

> แต่ละบล็อก `###` = 1 StoryBeat asset · สร้างตามลำดับ · ค่า effect เป็นตัวเลขตั้งต้น ปรับ balance ได้

---

### PHASE 1 · เอาตัวรอด (Day 1–5) — ช่วงสอนระบบ ไม่มีวิกฤต

#### BEAT: tutorial_day1
```yaml
triggerType: OnDay
triggerParam: "1"
type: tutorial            # ใช้ระบบ tutorial/quest ที่มีอยู่ ไม่ใช่ card popup วิกฤต
npcLines:
  - "Kova: วิศวกรใหม่สินะ ที่นี่เหลือแค่นี้แหละ เริ่มจากไฟ น้ำ อาหาร"
tutorialSteps:
  - "วางอาคาร: คลิกเมนูสร้าง เลือกโรงไฟ/โรงน้ำ/โรงอาหาร วางบนกริด"
  - "จัดคนงาน: คลิกคน ลากไปโรงงาน"
  - "ฝึกคน: เพิ่มประชากรผ่าน Shelter"
onFirstPowerPlantBuilt:
  systemLog: "[ระบบ] เครื่องกำเนิดไฟฟ้าเริ่มทำงาน · พลังงาน +60/วัน"
  npcLine: "Kova: ดี ที่เหลือคิดเองเป็นแล้ว"
  innerVoice: "เมืองนี้ยังไม่ตายซะทีเดียว"
firstQuest:
  title: "ทำให้เมืองมีไฟ"
  body: "ไม่มีพลังงาน — สร้างหรืออัปเกรดเครื่องกำเนิดไฟฟ้า"
  innerVoice: "ที่นี่มืดสนิท... เริ่มจากไฟก่อน"
note: "จุดเดียวที่ไกด์ หลังจากนี้ปล่อยผู้เล่นเรียนรู้เอง"
```

#### MEMORIAL (setup ตั้งแต่ต้นเกม — เป็น building คลิกได้ ไม่ใช่ beat)
```yaml
asset: MemorialSO (memorial_veltara)
buildingType: Memorial     # มีในฐานตั้งแต่เริ่ม คลิกที่ตัวอาคารเปิดแผงรายชื่อ (interaction ปกติ)
headerTH: "เพื่อจดจำทีมสร้างหอคอย — Veltara Core Project"
names:
  - "ELARA VANE — Lead Reactor Physicist — 2151–2157"
  - "(อีก 5 ชื่อ · ตำแหน่ง · ปี — ทีมเติมได้)"
innerVoiceOnFirstOpen: "คนพวกนี้เคยอยู่ที่นี่... ก่อนผมมาถึง"
note: "ไม่ไกด์ ไม่ชี้ · Elara คือ 1 ใน 6 ชื่อ ปมเชื่อมกับบันทึกภายหลัง เกมไม่บอกตรงๆ"
```

---

### PHASE 2 · ฟื้นฟู (Day 6–10)

#### BEAT: recover_record_01
```yaml
triggerType: OnBuildingBuilt
triggerParam: "ResearchLab"   # สร้าง+จัดคนเข้าห้องวิจัย → เริ่มกู้คืนข้อมูล
record:
  recordId: elara_01
  authorLabel: "ระบบกู้คืนข้อมูลจากเครือข่ายเก่า · ผู้บันทึก: Dr. Elara Vane"
  bodyTH: |
    ถ้ามีใครได้อ่านข้อความนี้ แปลว่าห้องวิจัยยังไม่พังหมด
    เชื้อเพลิงตัวแรกอยู่ในที่ที่นายคาดไม่ถึง — มันอยู่ในน้ำ
    ยังมีบันทึกอีกหลายส่วนที่กู้ไม่สำเร็จ ระบบจะถอดรหัสต่อเมื่อเมืองเดินหน้า
  buttonLabel: "รับทราบ · เก็บเข้าแผง Records"
  archiveTitle: "บันทึก #01 — เชื้อเพลิงในน้ำ"
innerVoiceAfter: "Elara... ชื่อนี้อยู่บนอนุสรณ์ในฐาน"
note: "การ์ดเด้งกลางจอเอง ไม่ต้องเข้าห้อง · เก็บเข้าแผง Records ให้ย้อนอ่าน"
```

---

### PHASE 2→3 · จุดเตา + สกัด Deuterium (Day 11) — ★ จุดสำคัญ: ความรู้มาก่อนควิซ

#### BEAT: deuterium_ignition   (Info Card → Record → Quiz)
```yaml
triggerType: OnDeuteriumExtracted   # ผู้เล่นสั่งสกัด Deuterium จากโรงน้ำครั้งแรก
npcLinePre: "Kova: เตานี่ไม่เหมือนโรงไฟบ้าๆ นะ จ้อง CORE% กับ HEAT ให้ดี"

# STEP 1 — ★ ให้ความรู้ก่อน (เด้งตอนกดสั่งสกัด)
infoCard:
  title: "เชื้อเพลิงที่ซ่อนอยู่ในน้ำ"
  category: Energy
  bodyTH: |
    น้ำทะเลทั่วไปมีไอโซโทปของไฮโดรเจนชนิดหนึ่งชื่อ ดิวเทอเรียม (²H) ปนอยู่แล้ว
    เราไม่ต้องผลิตใหม่ แค่แยกมันออกจากน้ำ ก็ได้เชื้อเพลิงป้อนเตาฟิวชันได้เลย
    → กำลังสกัดดิวเทอเรียมจากคลังน้ำของเมือง
  buttonLabel: "เข้าใจแล้ว"

# STEP 2 — กู้คืนบันทึก (เด้งกลางจอ)
record:
  recordId: elara_02
  authorLabel: "ระบบถอดรหัสข้อมูลเพิ่มเติมสำเร็จ · ผู้บันทึก: Dr. Elara Vane"
  bodyTH: |
    เชื้อเพลิงตัวแรกอยู่ในน้ำมาตลอด เราแค่ไม่เคยมองมัน
    ดิวเทอเรียมมีอยู่ในน้ำทุกหยด รอแค่เครื่องแยก
  buttonLabel: "รับทราบ · เก็บเข้าแผง Records"
  archiveTitle: "บันทึก #02 — ดิวเทอเรียมในน้ำ"
innerVoiceAfter: "อยู่ในน้ำมาตลอด... ก็อย่างที่รายงานเขียนไว้"

# STEP 3 — ★ ควิซทบทวน (ผู้เล่นรู้คำตอบแล้วจาก InfoCard)
quiz:
  - id: fuel_from_water
    questionTH: "เมื่อกี้เราสกัดเชื้อเพลิงตัวแรกจาก \"น้ำ\" ได้ เพราะอะไร?"
    options:
      - { text: "เพราะน้ำเป็นสารกัมมันตรังสีที่ปลอดภัยที่สุด", correct: false }
      - { text: "เพราะน้ำมีดิวเทอเรียมอยู่แล้ว ไม่ต้องสร้างใหม่ จึงมีเชื้อเพลิงป้อนเตาได้ต่อเนื่อง", correct: true }
      - { text: "เพราะน้ำช่วยดับไฟในเตาไม่ให้ร้อน", correct: false }
    explanationTH: |
      ดิวเทอเรียม (²H) เป็นไอโซโทปของไฮโดรเจนที่ปนอยู่ในน้ำทั่วไปอยู่แล้ว
      เราจึงแยกออกมาใช้เป็นเชื้อเพลิงฟิวชันได้เลย โดยไม่ต้องผลิตขึ้นใหม่
    codexUnlockId: "Deuterium"
```

#### BEAT: recover_record_03
```yaml
triggerType: OnReactorStart    # เตาเริ่มเดินเครื่อง
record:
  recordId: elara_03
  authorLabel: "ระบบถอดรหัสข้อมูลเพิ่มเติมสำเร็จ · ผู้บันทึก: Dr. Elara Vane"
  bodyTH: |
    ความร้อนในเตาไม่ใช่ศัตรู มันบอกว่าเราใกล้ความจริง
    แต่ถ้าคุมมันไม่อยู่... นายจะเข้าใจว่าทำไมทีมเราถึงไม่เหลือใคร
  buttonLabel: "รับทราบ · เก็บเข้าแผง Records"
  archiveTitle: "บันทึก #03 — ความร้อนไม่ใช่ศัตรู"
```

---

### PHASE 3 · นิวเคลียร์ (Day 11–20) — วิกฤต 3 ใบ

#### BEAT: crisis_plasma_stability   (~Day 17) — Info Card → Event → Choice → Quiz×2
```yaml
triggerType: OnStatThreshold
triggerParam: "HEAT>80||Q>0.3"

infoCard:                       # ★ ให้ความรู้ก่อนตัดสินใจ
  title: "พลาสมาถูกขังด้วยอะไร"
  category: Reactor
  bodyTH: |
    ในเตาฟิวชัน เชื้อเพลิงร้อนเป็นพลาสมาหลายล้านองศา ร้อนเกินกว่าผนังใดจะทนได้
    เราจึงใช้สนามแม่เหล็ก ขังพลาสมาไว้กลางเตา ไม่ให้แตะผนัง
    ถ้าสนามอ่อนลง พลาสมาจะหลุดชนผนัง แล้วเตาจะหลอมละลาย
  buttonLabel: "รับทราบ"

crisis:
  title: "เสถียรภาพพลาสมา"
  bodyTH: |
    สนามแม่เหล็กเริ่มเอาไม่อยู่
    พลาสมาในเตาร้อนหลายล้านองศา ถูกกักด้วยสนามแม่เหล็ก
    สนามเริ่มไม่นิ่ง ถ้าพลาสมาหลุดชนผนัง เตาจะหลอมละลาย
  timeLimitDays: 3
  choices:
    - id: A
      labelTH: "เร่งสนามแม่เหล็กเติมกำลัง"
      effects: { energy: -300, workersReassigned: 3, reassignDays: 1 }
      afterTextTH: |
        [ระบบ] สนามแม่เหล็กเสถียร · HEAT กลับสู่ระดับปลอดภัย
        [ระบบ] พลังงานสำรองหมด · ไฟทั้งเมืองดับชั่วคราว
        Kova: รอดแล้ว แต่คืนนี้มืดทั้งเมือง หวังว่าคุ้มนะ
        ▸ ความคิด: แลกไฟทั้งเมืองกับเตาหนึ่งคืน... คุ้มไหม
    - id: B
      labelTH: "ซ่อมขดลวดด้วยมือ"
      effects: { workers: 4, days: 2, iron: -150, radSickRisk: 0.5, hope: -1 }
      afterTextTH: |
        [ระบบ] ขดลวดซ่อมเสร็จใน 2 วัน · เตากลับมาเสถียร
        [ระบบ] วิศวกร 2 คนได้รับรังสีเกินขนาด
        Kova: ซ่อมได้ แต่คนของเราไม่ใช่อะไหล่
        ▸ ความคิด: สองคน... ที่ผมส่งลงไปเอง
    - id: C
      labelTH: "ฉีดสารหล่อเย็นฉุกเฉิน"
      effects: { cleanWaterPct: -0.5, q: -0.2, deferredCrisis: "water" }
      afterTextTH: |
        [ระบบ] HEAT ลดฮวบทันที · เตาปลอดภัยชั่วคราว
        [ระบบ] คลังน้ำลดลง 50%
        ▸ ความคิด: น้ำหายไปครึ่งคลัง... เดี๋ยวได้เจอปัญหาใหม่แน่

quiz:
  - id: plasma_why_meltdown
    questionTH: "จาก Info Card เมื่อกี้ อะไรคือสาเหตุที่ทำให้เตาหลอมละลายเมื่อพลาสมาไม่เสถียร?"
    options:
      - { text: "พลาสมาที่ร้อนหลายล้านองศาหลุดไปชนผนังเตา ถ่ายเทความร้อนเข้าตัวอาคาร", correct: true }
      - { text: "พลาสมาเย็นเกินไปจนเตาหยุดทำงาน", correct: false }
      - { text: "มีน้ำมากเกินไปจนเตาจม", correct: false }
    explanationTH: |
      ในโทคาแมก พลาสมาร้อนหลายล้านองศาถูกกักด้วยสนามแม่เหล็ก ถ้าสนามไม่นิ่ง
      พลาสมาหลุดไปชนผนัง จะถ่ายเทความร้อนเข้าตัวอาคารจนหลอมละลาย
    codexUnlockId: "Plasma Confinement"
  - id: magnetic_dual
    questionTH: "เมื่อ HEAT ของเตาเริ่มไต่สูง ควรทำอะไรเพื่อกันไม่ให้ถึง Meltdown?"
    options:
      - { text: "ป้อนอาหารเพิ่มให้คนงาน", correct: false }
      - { text: "ปิดโรงน้ำเพื่อประหยัดพลังงาน", correct: false }
      - { text: "เสริมสนามแม่เหล็ก/หล่อเย็น (Toroidal–Poloidal) เพื่อยกเพดานและรีดความร้อนออก", correct: true }
    explanationTH: |
      สนามแม่เหล็กคู่ทำงานร่วมกัน — Toroidal บีบพลาสมาให้เป็นวง
      ส่วน Poloidal กันไม่ให้พลาสมาชนผนัง เมื่อเสริมให้แข็งแรงจะกักพลาสมาไว้กลางเตาและรีดความร้อนที่รั่วออก
    codexUnlockId: "Magnetic Confinement"
```

#### BEAT: crisis_radiation_disease   (~Day 20) — Info Card → Event → Choice → Quiz×2
```yaml
triggerType: OnStatThreshold
triggerParam: "ZoneA_workers>threshold"

infoCard:                       # ★ ให้ความรู้ก่อน
  title: "รังสีกับร่างกายคน"
  category: Medical
  bodyTH: |
    รังสีปริมาณสูงทำลายเซลล์ในร่างกาย ทำให้เกิดเนื้อร้ายได้
    การรักษาสมัยใหม่ใช้เวชศาสตร์นิวเคลียร์ — ฉีดสารเภสัชรังสีเข้าไป "ถ่ายภาพ" หาตำแหน่งก่อน
    แล้วค่อยส่งรังสีไป "ทำลาย" เฉพาะจุด แม่นยำ ไม่กระทบเนื้อดี
    หลักสำคัญ: ALARA — ให้คนรับรังสีน้อยที่สุดเท่าที่ทำได้ และปกป้องกลุ่มเปราะบาง (ผู้ป่วย/เด็ก) ก่อน
  buttonLabel: "รับทราบ"

crisis:
  title: "โรคจากรังสี"
  bodyTH: |
    คนงานล้มป่วยพร้อมกัน
    คนงานที่ขุดแร่ 15 คนเกิดเนื้อร้าย เนื้อเยื่อโตผิดปกติ
    ฟาร์มกับโรงน้ำชะงักเพราะคนล้ม
  timeLimitDays: 3
  choices:
    - id: A
      labelTH: "สแกนคัดกรอง (PET / SPECT)"
      effects: { energy: -200, highSkillWorkers: 2 }
      afterTextTH: |
        [ระบบ] สแกนพบตำแหน่งเนื้อร้าย · รักษาเฉพาะจุด 10 คนหาย
        Mira: เห็นก่อนถึงรักษาถูกจุด แต่อีกห้าคนหนักเกินไปแล้ว
      note: "10 หาย · อีก 5 หนัก ต้องไปต่อ B"
    - id: B
      labelTH: "บำบัดด้วยสารเภสัชรังสี"
      effects: { labMaterial: -200, reactorIdleDays: 1 }
      afterTextTH: |
        [ระบบ] เตาเปลี่ยนโหมดผลิตไอโซโทปการแพทย์ 1 วัน
        [ระบบ] คนงานทั้ง 15 คนฟื้น กลับมาทำงาน
        Mira: รังสีที่คนกลัวกันนี่ วันนี้มันช่วยชีวิตคนสิบห้าคน
    - id: C
      labelTH: "กักตัว รอให้หายเอง"
      effects: { quarantineDays: 4, deaths: 3, hope: -3, deferredCrisis: "food" }
      afterTextTH: |
        [ระบบ] ไม่มีการรักษา · 4 วันผ่านไป เสียชีวิต 3 คน
        [ระบบ] แรงงานฟาร์มขาด · อาหารเริ่มหมด (วิกฤตซ้อน)
        ▸ ความคิด: ผมเลือกไม่รักษาพวกเขา...

quiz:
  - id: nuclear_medicine
    questionTH: "จาก Info Card เมื่อกี้ เวชศาสตร์นิวเคลียร์จัดการเซลล์เนื้อร้ายเป็น 2 ขั้นตอน ข้อใดถูก?"
    options:
      - { text: "PET/SPECT ยิงรังสีทำลายเนื้อร้ายทันทีตั้งแต่ตอนสแกน", correct: false }
      - { text: "PET/SPECT ใช้สารรังสี \"ถ่ายภาพ\" หาตำแหน่งก่อน จากนั้นยาเฉพาะจุดจึงส่งรังสีไป \"ทำลาย\" เป้าแม่นยำ", correct: true }
      - { text: "รังสีฆ่าทุกเซลล์เท่ากันหมด ไม่ว่าจะดีหรือร้าย", correct: false }
    explanationTH: |
      เวชศาสตร์นิวเคลียร์ทำงาน 2 ขั้น — (1) วินิจฉัย: PET/SPECT ฉีดสารเภสัชรังสีถ่ายภาพหาตำแหน่งเซลล์ผิดปกติ
      (2) รักษา: ยาเฉพาะจุด (targeted therapy) ส่งรังสีไปทำลายเฉพาะเป้า กระทบเนื้อดีน้อย
    codexUnlockId: "Nuclear Medicine"
  - id: alara_principle
    questionTH: "ตามหลัก ALARA ที่เพิ่งอ่าน เมื่อต้องส่งคนเข้าพื้นที่เสี่ยงรังสี ควรจัดการอย่างไร?"
    options:
      - { text: "จำกัดเวลา/ปริมาณรังสีต่อคนให้น้อยที่สุด และเลี่ยงส่งกลุ่มเปราะบาง (ผู้ป่วย/เด็ก)", correct: true }
      - { text: "ส่งใครก็ได้เข้าไปนานเท่าไรก็ได้ ถ้ามีชุดกันรังสี", correct: false }
      - { text: "ส่งผู้ป่วยเข้าไปก่อน เพราะป่วยอยู่แล้ว", correct: false }
    explanationTH: |
      ALARA (As Low As Reasonably Achievable) คือ "ให้คนรับรังสีน้อยที่สุดเท่าที่ทำได้"
      และต้องปกป้องกลุ่มที่ไวต่อรังสีเป็นพิเศษ (ผู้ป่วย/เด็ก) ก่อนเสมอ
    codexUnlockId: "ALARA"
optionalNpc: "ถ้าเคยส่งคนเข้า Zone A มากไป → Mira: รังสีไม่เลือกว่าใครแข็งแรง มันทำร้ายเด็กกับคนป่วยง่ายที่สุด"
```

#### BEAT: crisis_food_spoilage   (~Day 24) — Info Card → Event → Choice → Quiz (A→#6, B→#7)
```yaml
triggerType: OnStatThreshold
triggerParam: "foodStored>500||noAgriDome"

infoCard:                       # ★ ให้ความรู้ก่อน (ครอบทั้ง 2 ทางเลือกความรู้)
  title: "รังสีช่วยเรื่องอาหารได้ 2 ทาง"
  category: Food
  bodyTH: |
    ทางที่ 1 — ถนอมอาหาร: ฉายรังสีแกมมา (เช่น จากโคบอลต์-60) ทะลุผ่านอาหาร ฆ่าจุลินทรีย์และเชื้อรา
    อาหารเก็บได้นานขึ้น โดยอาหารไม่กลายเป็นสารรังสี (อาหารฉายรังสี ≠ อาหารมีรังสี)
    ทางที่ 2 — ปรับปรุงพันธุ์: ฉายรังสีกระตุ้นให้พืชกลายพันธุ์ นักวิจัยคัดเฉพาะสายพันธุ์ที่ทนทาน/ผลผลิตสูงไว้ใช้ถาวร (เช่น ข้าว กข6 ของไทย)
  buttonLabel: "รับทราบ"

crisis:
  title: "วิกฤตอาหาร"
  bodyTH: |
    เสบียงเน่าเพราะรังสี
    คลังอาหารเน่าเร็วกว่าปกติสามเท่า เพราะรังสีปนเปื้อน
    ถ้าคนอดตาย หอคอยก็ไม่มีความหมาย
  timeLimitDays: 3
  choices:
    - id: A
      labelTH: "เพาะเมล็ดกลายพันธุ์"
      effects: { iron: -250, researchers: 3, yieldPct: +1.0 }
      afterTextTH: |
        [ระบบ] ปลดล็อกแปลงพืชสายพันธุ์ทนรังสี · ผลผลิต +100%
        ▸ ความคิด: พืชโตในดินที่เป็นพิษได้... ใครจะเชื่อ
      quizRef: mutation_breeding    # ★ A → Quiz #6
    - id: B
      labelTH: "ฉายรังสีถนอมด้วยโคบอลต์-60"
      effects: { energy: -300, workers: 4, spoilRate: 0 }
      afterTextTH: |
        [ระบบ] เสบียงผ่านห้องฉายรังสีแกมมา · หยุดเน่าทันที
        Kova: ฉายรังสีอาหาร ไม่ได้แปลว่าอาหารมีรังสีนะ คนละเรื่อง
      quizRef: food_irradiation     # ★ B → Quiz #7
    - id: C
      labelTH: "ลดปันส่วนอาหาร"
      effects: { workerEfficiencyPct: -0.5, hope: -3, riotRisk: true }
      afterTextTH: |
        [ระบบ] ทุกคนได้อาหารครึ่งเดียว · แรงงานอ่อนแรง งานช้าลง 50%
        ▸ ความคิด: พวกเขาหิว... แต่เราไม่มีทางเลือกอื่นแล้วเหรอ
      quizRef: null                 # C ไม่ให้ความรู้ใหม่ ไม่มีควิซ

quizPool:                          # ยิงเฉพาะตัวที่ตรงกับ choice ที่เลือก
  - id: mutation_breeding
    questionTH: "จาก Info Card เมื่อกี้ การฉายรังสีใส่เมล็ดพันธุ์ช่วยแก้วิกฤตอาหารระยะยาวได้อย่างไร?"
    options:
      - { text: "ทำให้พืชเรืองแสงจึงปลูกตอนกลางคืนได้", correct: false }
      - { text: "ทำให้พืชกลายเป็นกัมมันตรังสี กินแล้วแข็งแรง", correct: false }
      - { text: "กระตุ้นการกลายพันธุ์เชิงบวก คัดสายพันธุ์ที่ทนทาน/ผลผลิตสูงไว้ใช้ถาวร", correct: true }
    explanationTH: |
      การฉายรังสีกระตุ้นให้เกิดการกลายพันธุ์ นักวิจัยคัดเลือกเฉพาะสายพันธุ์ที่ทนทานและให้ผลผลิตสูงไว้ใช้ถาวร
      เช่น ข้าว กข6 ของไทย เป็นการแก้ปัญหาที่ต้นเหตุ
    codexUnlockId: "Mutation Breeding"
  - id: food_irradiation
    questionTH: "จาก Info Card เมื่อกี้ การฉายรังสีแกมมาจากโคบอลต์-60 ช่วยถนอมอาหารด้วยกลไกใด?"
    options:
      - { text: "รังสีเคลือบผิวอาหารด้วยโลหะกันบูด", correct: false }
      - { text: "รังสีทำลายจุลินทรีย์/เชื้อราในอาหาร ชะลอการเน่า โดยอาหารไม่กลายเป็นสารรังสี", correct: true }
      - { text: "รังสีทำให้อาหารแช่แข็งตลอดเวลา", correct: false }
    explanationTH: |
      รังสีแกมมาทะลุผ่านอาหารและฆ่าจุลินทรีย์/เชื้อรา ทำให้เก็บได้นานขึ้น
      โดยอาหารไม่กลายเป็นสารกัมมันตรังสี (อาหารฉายรังสี ≠ อาหารมีรังสี)
    codexUnlockId: "Food Irradiation"
```

---

### PHASE 4 · จุดติดเตา (Day 21–30) — ไคลแม็กซ์

#### BEAT: foreshadow_storm   (Day 20–23) — ปูสัญญาณ ไม่เฉลย
```yaml
triggerType: OnDay
triggerParam: "20"
sequence:                # เด้งกระจายหลายวัน ไม่รวบ
  - systemLog: "[ระบบ] เซนเซอร์อ่านค่ารังสีพื้นหลังสูงผิดปกติ"
  - npcLine:   "Kova: เครื่องวัดเพี้ยนอีกแล้ว ครั้งที่สามวันนี้ ไม่เคยเพี้ยนโดยไม่มีเหตุ"
  - innerVoice:"ท้องฟ้ากลางคืนสีแปลกๆ... หรือผมคิดไปเอง"
  - systemLog: "[ระบบ] ตรวจพบความผิดปกติของสภาพอากาศเหนือ Veltara"
note: "พายุไม่เฉลย ค่อยๆ ปูให้ผู้เล่นเอะใจเอง"
```

#### BEAT: recover_record_final   (~Day 23) — เฉลยปมเงียบๆ
```yaml
triggerType: OnDay
triggerParam: "23"      # หรือผูกกับ stormApproach flag
record:
  recordId: elara_final
  authorLabel: "ถอดรหัสไฟล์รายงานฉบับเต็มสำเร็จ · ผู้บันทึก: Dr. Elara Vane"
  bodyTH: |
    ก่อนหอคอยระเบิด — เราตรวจพบบางอย่าง คลื่นรังสีที่จะตามมาหลังการระเบิด
    เราเขียนมันลงในรายงาน รายงานที่ควรจะถึงมือทุกคน
    ถ้ามันกำลังจะมา เตาต้องติดเต็มร้อยก่อนมันจะถึง นั่นคือทางเดียว
  buttonLabel: "รับทราบ · เก็บเข้าแผง Records"
  archiveTitle: "บันทึก #สุดท้าย — รายงานเตือนพายุ"
innerVoiceAfter: "รายงานที่ผมเอาไปส่ง... มันเขียนเรื่องนี้ไว้ตั้งแต่แรก"
note: "ปมเชื่อม: ผู้เล่นที่คลิกอนุสรณ์ (เห็นชื่อ Elara) + กู้บันทึกครบ จะเข้าใจเองว่ารายงานที่ Auren ส่ง = รายงานเตือนพายุนี้ เกมไม่ตอกย้ำ"
```

#### BEAT: storm_first_light   (Day 25) — Info Card → Event → Quiz×2 (ระหว่างเตาติด)
```yaml
triggerType: OnStormApproach
crisisAlert:
  title: "พายุรังสีเคลื่อนเข้า Veltara"
  bodyTH: |
    ท้องฟ้าเปลี่ยนเป็นสีม่วง เซนเซอร์ทั่วเมืองร้องเตือน
    อุณหภูมิแกนเตาจะเพิ่มต่อเนื่องตราบที่พายุยังอยู่
    ทางเดียวที่จะรอด: ดันเตาให้ถึง 100%
  npcLine: "Kova: มันมาจริงๆ นั่นแหละที่เครื่องฉันพยายามเตือน! เตาต้องติดเต็มร้อย ไม่งั้นละลาย!"

infoCard:                       # ★ ให้ความรู้ก่อนควิซชุดสุดท้าย
  title: "จุดที่เรากำลังจะไปให้ถึง: ฟิวชัน"
  category: Fusion
  bodyTH: |
    สิ่งที่เตากำลังทำคือฟิวชันนิวเคลียร์ — การหลอมรวมนิวเคลียสเบา (ไฮโดรเจน + ไฮโดรเจน)
    ให้เป็นธาตุที่หนักกว่า แล้วปลดปล่อยพลังงานมหาศาล
    ตรงข้ามกับฟิชชัน ที่เป็นการแตกนิวเคลียสหนัก (แบบโรงไฟฟ้านิวเคลียร์เก่า)
    ฟิวชันสะอาดกว่าเพราะเชื้อเพลิงมาจากน้ำ ไม่ปล่อย CO₂ ถ้าเสียสมดุลเตาจะดับเอง (ไม่ระเบิด) และไม่มีกากรังสีอายุยืนแบบฟิชชัน
  buttonLabel: "ดันเตาต่อ"

climaxMechanic:
  stormHeatPerDay: +12
  duration: 6            # Day 25–30
  goal: "CORE% 80→100 ขณะ HEAT ไม่ทะลุ 100"
  note: "Boost ดัน % เร็ว แต่เพิ่มความร้อน ต้องลงทุนหล่อเย็นก่อน (ดาบสองคม)"

quiz:
  - id: what_is_fusion
    questionTH: "จาก Info Card เมื่อกี้ \"ฟิวชันนิวเคลียร์\" ที่เพิ่งจุดติดในเตา แท้จริงคืออะไร?"
    options:
      - { text: "การหลอมรวมนิวเคลียสเบา (ไฮโดรเจน + ไฮโดรเจน) ให้เป็นธาตุที่หนักกว่า แล้วปลดปล่อยพลังงานมหาศาล", correct: true }
      - { text: "การแตกตัวของนิวเคลียสหนักออกเป็นชิ้นเล็ก เหมือนเครื่องปฏิกรณ์ฟิชชัน", correct: false }
      - { text: "การเผาไหม้ถ่านหินด้วยความร้อนสูง", correct: false }
    explanationTH: |
      ฟิวชันคือการหลอมรวมนิวเคลียสเบา (เช่น ไฮโดรเจน) ให้กลายเป็นธาตุที่หนักกว่า แล้วปลดปล่อยพลังงานมหาศาล
      "ตรงข้าม" กับฟิชชันที่เป็นการแตกตัวของนิวเคลียสหนัก
    codexUnlockId: "Nuclear Fusion"
  - id: why_fusion_clean
    questionTH: "จาก Info Card เมื่อกี้ ข้อใดอธิบายได้ถูกว่าทำไมพลังงานฟิวชันถึงสะอาดกว่าทางเลือกอื่น?"
    options:
      - { text: "เพราะมันไม่เกี่ยวข้องกับรังสีหรือนิวเคลียร์เลย บริสุทธิ์ 100%", correct: false }
      - { text: "เพราะมันเผาถ่านหินที่สะอาดเป็นพิเศษ", correct: false }
      - { text: "เชื้อเพลิงมาจากน้ำทะเล ไม่ปล่อย CO₂ ไม่มีปฏิกิริยาลูกโซ่ที่คุมไม่ได้ และไม่มีกากรังสีอายุยืนแบบฟิชชัน", correct: true }
    explanationTH: |
      ฟิวชันสะอาดกว่าเพราะเชื้อเพลิงหาได้จากน้ำ ไม่ปล่อย CO₂ ถ้าเสียสมดุลเตาจะดับเอง (ไม่ระเบิด)
      และไม่มีกากรังสีอายุยืนแบบฟิชชัน — แต่ "สะอาดกว่า" ไม่ได้แปลว่า "ไม่มีรังสีเลย" เพราะเชื้อเพลิง D-T ยังปล่อยนิวตรอน
    codexUnlockId: "Clean Energy"
```

#### BEAT: decree_emergency   (Day 25–30) — Info Card → Event(Decree) → Quiz #10
```yaml
triggerType: OnStormActive
triggerParam: "coolingWorkerShortage"

infoCard:                       # ★ ทบทวน ALARA ในบริบทตัดสินใจ (ก่อนออกประกาศ)
  title: "ทบทวน: ALARA กับการเกณฑ์แรงงาน"
  category: Ethics
  bodyTH: |
    อย่าลืมหลัก ALARA — คนที่ไวต่อรังสีเป็นพิเศษคือกลุ่มเปราะบาง (ผู้ป่วย/เด็ก)
    การบังคับให้พวกเขาเข้าเขตเสี่ยงรังสี ขัดหลัก ALARA โดยตรง
    การตัดสินใจนี้เป็นของคุณ เกมไม่ตัดสินถูก-ผิดแทน แต่ผลของมันคือ Hope ของเมือง
  buttonLabel: "ตัดสินใจ"

crisis:                         # Decree = การ์ดจริยธรรม ทุกทางแลก Hope
  title: "ประกาศฉุกเฉิน"
  bodyTH: |
    แรงงานไม่พอรับมือพายุ
    ระบบหล่อเย็นต้องการคนเพิ่มด่วน ไม่งั้นเตาร้อนเกิน
    มีทางที่ได้ผล แต่ขัดกับสิ่งที่ควรทำ — จะออกประกาศไหม
  timeLimitDays: 0              # ตัดสินใจทันที
  choices:
    - id: A
      labelTH: "ไม่ออกประกาศ — หาทางอื่น"
      effects: { hope: 0 }
      afterTextTH: |
        [ระบบ] ไม่เกณฑ์กลุ่มเปราะบาง · เมืองต้องบีบทรัพยากรให้พอ
        ชาวเมือง: ขอบคุณที่ไม่ทิ้งพวกเรา แม้ในวันที่ยากที่สุด
      quizRef: null
    - id: B
      labelTH: "เกณฑ์ผู้ป่วยร่วมงาน"
      effects: { coolingWorkers: +1, hope: -8, hopePerDay: -3, patientDeathRisk: 0.2 }
      afterTextTH: |
        [ระบบ] ผู้ป่วยถูกเรียกออกมาทำงานในเขตเสี่ยงรังสี
        Mira: เราชนะพายุไปทำไม ถ้าไม่เหลือใครให้ช่วย
      quizRef: alara_price
    - id: C
      labelTH: "ดึงแรงงานเด็ก"
      effects: { coolingWorkers: +1, hope: -15 }
      afterTextTH: |
        [ระบบ] เด็กถูกส่งไปทำงานเบาในเขตหล่อเย็น
        ชาวเมือง: นี่คือสิ่งที่เราหนีมา ไม่ใช่สิ่งที่เราอยากสร้าง
      quizRef: alara_price
warning: "★ ดาบสองคม: ถ้าออกทั้ง B และ C, Hope อาจร่วงถึง 0 = แพ้ — บทเรียนคือ ALARA ในรูปการตัดสินใจ"

quizPool:
  - id: alara_price
    questionTH: "จาก Info Card เมื่อกี้ การเกณฑ์แรงงานฉุกเฉินให้คนเข้าทำงานในเขตเสี่ยงรังสีขณะวิกฤต ขัดกับหลักจริยธรรมอย่างไร?"
    options:
      - { text: "ไม่มีปัญหาอะไร เพราะคนกลุ่มนี้แค่ทำงานช้ากว่าคนทั่วไป", correct: false }
      - { text: "กลุ่มเปราะบางไวต่อรังสีเป็นพิเศษ การบังคับให้เข้าไปเสี่ยงจึงขัดหลัก ALARA โดยตรง", correct: true }
      - { text: "ไม่ขัดอะไรเลย ถ้าทุกคนในเมืองตกลงร่วมมือกัน", correct: false }
    explanationTH: |
      การส่งผู้ป่วย/เด็กเข้าเขตรังสีขัดหลัก ALARA เพราะคนกลุ่มนี้ไวต่อรังสีเป็นพิเศษ
      การตัดสินใจนี้เป็นของผู้เล่น เกมไม่ตัดสินถูก-ผิดแทน แต่ผลของมันคือ Hope และแรงงานในรอบนั้น
    codexUnlockId: "ALARA (ทบทวน)"
```

---

### ENDINGS (Day 30) — ตรวจเงื่อนไขแล้วโชว์การ์ดสรุป

```yaml
resolveOrder:
  - if: "Hope <= 0"        -> gameover_hope
  - if: "HEAT >= 100"      -> gameover_meltdown
  - if: "day > 30 && Q < 1.0" -> gameover_timeout
  - if: "Q >= 1.0 && survivors" -> true_ending
  - else (Q 0.5–0.99)      -> normal_ending
note: "ทุกเส้นทางที่ชนะไปจบที่ True Ending เดียว · Decree กระทบแค่สถิติ ไม่เปลี่ยนบทจบ"

true_ending:
  title: "ภารกิจสำเร็จ — แสงแรกของโลกใหม่"
  bodyTH: |
    โลพลาสมากางรับคลื่นรังสีไว้ทั้งหมด เสียงพายุเงียบลง
    ปฏิกิริยาฟิวชันถึงจุดเสถียร Q = 1.0 · Veltara ปลอดภัย
    ต้นไม้ต้นแรกผลิใบในดินที่เคยเป็นพิษ
  onEnd:
    - "ปลดล็อก True Ending · บันทึกสถิติการใช้ Decree"
    - "แสดง Knowledge Summary — ความรู้ที่สะสมทั้งหมด"
  npcLine: "Kova: นายทำได้ วิศวกร ที่พวกเราทั้งทีมทำไม่สำเร็จ"
  innerVoice: "Elara... ทุกคน ผมส่งรายงานไม่ทันในวันนั้น แต่คราวนี้ผมส่งมันถึงแล้ว"
  systemLog: "พลังงานสะอาด ไม่ได้เกิดจากความสมบูรณ์แบบ แต่เกิดจากคนที่กล้ารับผิดชอบมัน"

normal_ending:
  title: "ภารกิจสำเร็จบางส่วน — เมืองที่ยังต้องซ่อม"
  bodyTH: |
    เตาติด แต่ไม่ถึงจุดเสถียรเต็มที่ โลพลาสมากางได้ครึ่งเดียว
    พายุผ่านไป แต่เมืองบาดเจ็บ
  npcLine: "Kova: มันไม่เพอร์เฟกต์ แต่เราก็ยังอยู่"
  innerVoice: "ยังไม่จบ ยังมีงานให้ทำอีกมาก แต่เมืองนี้ยังมีพรุ่งนี้"

gameover_hope:      { title: "GAME OVER — ขวัญเมืองหมด",   bodyTH: "Hope = 0 · ประชาชนหมดศรัทธา เมืองล่มสลาย" }
gameover_meltdown:  { title: "GAME OVER — เตาหลอมละลาย",   bodyTH: "HEAT ≥ 100 · เร่งเครื่องเกินกำลังหล่อเย็น" }
gameover_timeout:   { title: "GAME OVER — หมดเวลา",        bodyTH: "หมดเวลา 30 วัน แต่ค่า Q ยังไม่ถึง 1.0" }

restart:
  title: "เริ่มใหม่ — ให้กำลังใจ ไม่ลงโทษ"
  rules:
    - "ทรัพยากรรีเซ็ตกลับค่าเริ่มต้น"
    - "คลังความรู้ (Knowledge + Codex ที่ปลดแล้ว) ยังอยู่ถาวร"
  message: "ความรู้ที่คุณได้ — ไม่มีวันหาย เริ่มใหม่แล้วไปให้ไกลกว่าเดิม"
```

---

## 5. ไฟล์ C# เริ่มต้นที่แนบมา (ในโฟลเดอร์ Scripts/Story)

Claude ควรอ่านไฟล์เหล่านี้เป็นจุดตั้งต้น แล้วเติมส่วนที่เหลือ:

| ไฟล์ | หน้าที่ | สถานะ |
|------|---------|-------|
| `Data/InfoCardSO.cs` | SO การ์ดข้อมูล | โครงพร้อม ปรับได้ |
| `Data/RecordCardSO.cs` | SO การ์ดบันทึก | โครงพร้อม |
| `Data/MemorialSO.cs` | SO รายชื่อทีม | โครงพร้อม |
| `Data/StoryBeatSO.cs` | SO มัดเหตุการณ์ + trigger | โครงพร้อม |
| `Data/StoryEnums.cs` | enum (TriggerType, CardCategory) | พร้อม |
| `Runtime/StoryDirector.cs` | คุมลำดับ ฟัง event ยิง beat | skeleton + TODO |
| `Runtime/CardUIController.cs` | เด้งการ์ดกลางจอ (coroutine) | skeleton + TODO |
| `Runtime/RecordsPanelController.cs` | แผงย้อนอ่านบันทึก | skeleton + TODO |

**สิ่งที่ Claude ต้องต่อเอง (TODO ในโค้ด):**
1. ผูก `StoryDirector` เข้ากับ event ของเมืองจริง (DayClock/ReactorState/BuildingManager) — จุดต่อทำ interface `IGameEvents` ไว้ให้แล้ว
2. ทำ prefab UI ของการ์ด (Info/Record/Crisis/Quiz) — โครง controller ระบุ field ที่ต้องมี
3. สร้าง `.asset` จากข้อมูล §4 (เขียน editor script import อัตโนมัติ หรือสร้างมือก็ได้)
4. ต่อ `QuizCard` กับระบบคะแนน + `CodexController.Unlock(codexUnlockId)` ที่มีอยู่
5. Balance ค่า effect ใน §4 กับ ResourceManager จริง

---

## 6. Checklist ให้ Claude ตรวจก่อนถือว่าเสร็จ

- [ ] ทุก beat ที่มี `quiz` มี `infoCard` (หรือ record ที่ให้ความรู้) มาก่อนในไทม์ไลน์
- [ ] การ์ดบันทึกทุกใบเด้งกลางจอ + ถูกเก็บเข้าแผง Records (กดย้อนอ่านได้)
- [ ] ไม่มีโค้ดที่ให้ผู้เล่นคลิก interactable เล็กๆ ในอาคารเพื่ออ่านบันทึก
- [ ] Memorial เป็น building คลิกเปิดแผงรายชื่อ (ไม่ใช่ป้ายเล็กให้ล่าคลิก)
- [ ] ลำดับ `InfoCard→Event→Choice→Outcome→Quiz` ถูกบังคับใน `StoryDirector`
- [ ] Quiz ให้ +8 ถูก / +3 ผิด + โชว์ explanation ทั้งตอบถูกและผิด + ปลด Codex
- [ ] Ending resolve ตามลำดับใน §4 · ทุกทางชนะ → True Ending เดียว
- [ ] Player-facing string เป็นภาษาไทยตามไฟล์นี้ (ไม่แปล)

---

*ไฟล์นี้อ้างอิงบทเนื้อเรื่อง NUCLEAR Re:Mind v8 · เนื้อหาเริ่มที่ Phase 1 (ตัด Intro/เปิดเกมออก) · ความรู้นิวเคลียร์อ้างอิง GDD v4.0 · เนื้อเรื่องอ้างอิง NSC Proposal*
