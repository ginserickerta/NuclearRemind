# แผนพัฒนา NUCLEAR Re:Mind — ยึด V4 เป็นหลัก (Dev Plan)

> **Spec หลัก:** `Assets/Docs/Final Plan/FINAL NUCLEAR ReMind V4.md` (GDD v4 · รวมแก้ไข 1 ก.ค. 2026: วิกฤต 2·B + Q4) · ฉบับก่อนหน้าเก็บไว้ที่ `Assets/Docs/archive/`
> **เวอร์ชันก่อนหน้า:** `Nuclear_ReMind_GDD_v2_2 2.pdf` (root) + `Plan/CLAUDE_3.md` (GDD v2.1)
> **สำรวจโค้ดจริง:** 2026-06-30 · เอกสารนี้ = แผนแปลงโค้ดให้ตรง V4 แบบ bottom-up · **เมื่อ V4 ขัดกับโค้ด → V4 ชนะ**
> **สถานะล่าสุด (2 ก.ค. 2026):** ✅ **เฟส 0–8 code-complete + 5b (batch production)** — ครบทุกระบบ V4 · playtest/จูน/อาร์ต = งาน Unity (ดู `Plan/UNITY_SETUP_AND_PLAYTEST.md`)

**สัญลักษณ์:** ✅ ใช้ได้ตาม V4 · 🔧 มีแล้วต้องแก้ · ➕ สร้างใหม่ · ➖ ถอด/ลด · 🔸 หมายเหตุ/งานมือ Unity

---

## 1 · ภาพรวมการเปลี่ยน (v2.2 → V4)

V4 ไม่ใช่แค่อัปเวอร์ชัน แต่เป็นการ **ตัดสโคปให้ทำจริงทันเดดไลน์ + เติมระบบที่ได้คะแนน NSC (Quiz/Knowledge) + แปลงเป็นสเปกพร้อมเขียนโค้ด** ทิศทาง: เล็กลง ชัดขึ้น เขียนได้จริง

โค้ดปัจจุบัน (Block A/B/C/D + Phase 1b) ทำตาม v2.1/v2.2 จึงมี 3 กลุ่ม: ของที่ใช้ต่อได้, ของที่ต้องแก้ย้อน (เช่น Hope/Despair), ของใหม่ที่ยังไม่มี (Quiz, ประชากร 3 คลาส, building level, placement pause, meta-progress)

---

## 2 · แผนที่สถานะทุกระบบ (V4 ↔ โค้ดจริง)

| # | ระบบใน V4 | โค้ดจริงตอนนี้ | หมวด | สรุปงาน |
|---|-----------|----------------|------|---------|
| 1 | Grid/Camera/Input/Placement/Construction/Demolition | ครบ 100% (20×12) | ✅ | ใช้ต่อ · เปลี่ยนกริด → **43×43** |
| 2 | Day loop 90s + day counter + events | `GameManager` dayLength=90, OnDayStarted/Ended | ✅ | ฐานใช้ได้ |
| 3 | Morale = Hope เดี่ยว (เริ่ม 100) | Hope+Despair 2 ค่า (50/20) | 🔧➖ | ถอด Despair, Hope 50→100 |
| 4 | ทรัพยากร 6: Energy/Water/Food/Iron/Deuterium/Tritium | `ResourceData`=Food/Water/**RadiationProtection**/Energy/**Workers**/**ResearchPoints** | 🔧 | refactor enum |
| 5 | Knowledge เป็นทรัพยากร (0–100) + thresholds | มีแค่ `researchPoints` (จ่ายปลด Codex) | 🔧➕ | → Knowledge + 4 threshold (สะสมไม่จ่าย) |
| 6 | Reactor: 3 เฟส/4 โหมด/SCRAM/HEAT/meltdown | มีครบใน `CoreTowerManager`+`TowerData` | ✅ | โครงใช้ได้ |
| 7 | Reactor: fuel = Deuterium/Tritium | ใช้ **Energy proxy** | 🔧 | ต่อ fuel จริง |
| 8 | Reactor: cooling เต็มสูตร + knowBonus | `cooling=15+water/10` เท่านั้น | 🔧 | เพิ่ม engineers×4 + towerLv×10 + knowBonus |
| 9 | Crisis A/B/C (3 ทาง) + ผูกควิซ | มีแค่ **A/B**, ผูก Hope/Despair | 🔧 | เพิ่ม C, retarget Hope, ผูกควิซ |
| 10 | ★ Quiz 10 ข้อ (QuizManager/IQuizTrigger) | **ไม่มีเลย** | ➕ | สร้างใหม่ — หัวใจคะแนน NSC |
| 11 | ประชากร 3 คลาส + ฝึก + Shelter cap + growth | มีแค่ `total` (maxWorkers=100 คงที่) | ➕ | สร้างใหม่ทั้งระบบ |
| 12 | Building levels L1–L3 + upgrade | ไม่มีระบบ level | ➕ | สร้างใหม่ (เบากว่าเดิม: ลด L5→L3) |
| 13 | Time: Planning 30s + Live 60s + mode lock + batch | timer เดียว, ผลิตทุก 5 วิ | 🔧 | แยกเฟส + ล็อกโหมด + batch |
| 14 | ★ Placement Pause (pause-reason stack) | **ไม่มี** | ➕ | สร้าง TimeManager pause-reason |
| 15 | Endings 3 แบบ (Q-based) + Defeat Summary | `GameEndType` 4 ค่าเก่า, ไม่มี Q-map | 🔧➖ | map Q→ending + จอสรุป · ถอด Bad/epilogue |
| 16 | Decrees 2 ข้อ (จ่าย Hope) | ยังไม่ implement | ➕ | สร้างใหม่ (ตัด Despair/epilogue) |
| 17 | ★ MetaProgress (Knowledge+Codex ข้ามรอบ) | ไม่มี persistence ข้ามรอบ | ➕ | สร้างใหม่ (PlayerPrefs) |
| 18 | Codex 20 entry | มี 30 .asset + ระบบครบ | ✅ | ใช้ต่อ + ผูก persistence |
| 19 | Items/craft (Rad-Gear, PET/SPECT, ไอโซโทปการแพทย์, Co-60 ฯลฯ) | ไม่มีระบบไอเทม | ➕ | ทำแบบเบา ผูกผลทางเลือกวิกฤต |
| 20 | Tutorial Day 1 | skeleton 30% | 🔧 | เติมให้ครบ |

---

## 3 · สิ่งที่ต้อง "ถอด/ลด" ออก (regression ที่ต้องระวัง)

| ของเดิม (v2.1/v2.2) | V4 | งาน |
|---------------------|-----|------|
| **Despair** + จลาจล Despair>80 | ตัดทิ้ง เหลือ Hope | ลบ field/logic/HUD/consequence |
| **Building L4–L5** | เหลือ **L1–L3** | ใส่แค่ 3 ระดับ |
| **Bad Ending** (Q<0.5) แยก | ยุบเป็น Game Over (TimeoutLowQ) | ลด 4→3 ฉากจบ |
| **epilogue flags** เปลี่ยนตอนจบ | Decree ไม่เปลี่ยนบทจบ (แค่สถิติ) | ตัด map flag→epilogue |
| **PowerGridManager** (BFS coverage) | V4 ใช้แค่ upkeep ต่ออาคาร | นอกสเปก V4 — ปิด/ปล่อย dormant |
| `RadiationProtection`/`Workers`/`ResearchPoints` ใน ResourceData | ไม่ใช่ทรัพยากร V4 | ย้าย/แทนที่ |

---

## 4 · Roadmap (เรียงตาม dependency + impact)

| เฟส | ชื่อ | หมวดงาน | effort | สถานะ |
|-----|------|---------|--------|--------|
| **0** | Refactor ฐานข้อมูล (Resource/Hope/GameEndType) | 🔧➖ | ~2–3 วัน | ✅ **implement เสร็จ** (30 มิ.ย. · รอ compile Unity) |
| **1** | Quiz + Knowledge (หัวใจ NSC) | ➕ | ~2–3 วัน | ✅ **implement เสร็จ** (1 ก.ค. · รอ compile Unity) |
| **2** | Reactor ให้ตรง V4 | 🔧 | ~2 วัน | ✅ **implement เสร็จ** (1 ก.ค. · รอ compile Unity) |
| **3** | ประชากร 3 คลาส + Shelter + growth | ➕ | ~2–3 วัน | ✅ **implement เสร็จ** (1 ก.ค. · รอ compile Unity) |
| **4** | Crisis A/B/C + retarget Hope | 🔧 | ~1–2 วัน | ✅ **implement เสร็จ** (1 ก.ค. · รอ compile Unity) |
| **5** | Time Planning/Live + Placement Pause (+batch→5b) | 🔧➕ | ~2–3 วัน | ✅ **implement เสร็จ** (pause/time 1 ก.ค. · **5b batch+บริโภค 2 ก.ค.** — compile ผ่าน Roslyn) |
| **6** | Building levels L1–L3 + Coils | ➕ | ~2 วัน | ✅ **implement เสร็จ** (1 ก.ค. · รอ compile) |
| **7** | Endings 3-tier + Defeat Summary + Decrees + MetaProgress | 🔧➕ | ~2–3 วัน | ✅ **implement เสร็จ** (1 ก.ค. · รอ compile) |
| **8** | UI/HUD + Tutorial + grid 43×43 + items + playtest | 🔧 | ~2–3 วัน | ✅ **โค้ดเสร็จ** (HUD phase/กริด 43×43) · playtest/จูน/อาร์ต→Unity |

รวม **~17–23 วันงานโปรแกรมเมอร์** · **✅ 9/9 เฟส code-complete (0-8) + 5b** · เหลือ: งาน Unity (tests/playtest/จูน/อาร์ต — ดู `UNITY_SETUP_AND_PLAYTEST.md`)

**Critical path:** เฟส 0 → (1 ขนาน) → 2 → 3 → 4 — ทุกอย่างพึ่ง refactor เฟส 0
**คะแนน NSC:** ดันเฟส 1 (Quiz) ขึ้นต้น ๆ เพราะเนื้อหาพร้อมใน V4 §12

---

## 5 · เฟส 0 — Refactor ฐานข้อมูล (task ละเอียด)

**เป้าหมาย:** ปรับ data model 3 จุดให้ตรง V4 โดย compile ผ่าน + ไม่พังของเดิม

**กฎการตัดสินใจ (Decisions Log):**
- **D1** Hope อยู่ที่ `PopulationManager` ต่อ (ไม่ย้ายไป ResourceManager) — ลด churn
- **D2** ✅ *ยืนยันแล้ว:* ใช้โมเดล V4 เต็มในเฟส 0 — Knowledge **สะสมอย่างเดียวไม่จ่าย**, Codex ปลดด้วย **event** เท่านั้น
- **D3** `radiationProtection` ถอดจากทรัพยากร (V4 เป็นไอเทม Rad-Gear)
- **D4** `workers` ถอดจาก `ResourceData` → ใช้ `PopulationManager.Current.total`

### 🅰 Work-stream A — Resource Model

**T0.A1 · `Data/ResourceData.cs`** — struct + enum ใหม่
```csharp
public struct ResourceData {
    public float energy, water, food, iron, deuterium, tritium;
    public float knowledge;   // 0..100 (แทน researchPoints)
}
public enum ResourceType { Energy, Water, Food, Iron, Deuterium, Tritium, Knowledge }
```
- ลบ `radiationProtection`, `workers`, และ enum `RadiationProtection/Workers/ResearchPoints`

**T0.A2 · `Managers/ResourceManager.cs`** — 6 จุด
1. Max caps (14–19): ลบ maxRadiationProtection/maxResearchPoints/maxWorkers · เพิ่ม maxIron/Deuterium/Tritium=9999, maxKnowledge=100 · maxFood=500, maxEnergy=9999, maxWater=9999
2. ค่าเริ่ม `Current` (30–37): energy 200, water 150, food 150, iron 100, deuterium 0, tritium 0, knowledge 0 · ลบ workers
3. `HandleResourceDelta` switch (104–124): ลบ case rad/Workers/RP · เพิ่ม Iron/Deuterium/Tritium/Knowledge
4. `Tick()` worker source (145–146): `(float)c.workers` → `PopulationManager.Instance.Current.total`
5. `Tick()` production (164–168): ลบ `radiationProtection +=` และ `researchPoints +=` · เพิ่ม hook ผลิต iron
6. clamp + `CheckThresholds` (171–208): ลบ rad/workers/RP · เพิ่ม iron/fuel

**T0.A3 · `Data/BuildingData.cs`**
- `radiationProtectionBonus` (41), `researchPointsPerTick` (43): **ลบทิ้ง** (D2: อาคารไม่ให้ Knowledge)
- เพิ่ม `ironCost`, `ironProduction` (+ เผื่อเฟส 2: `deuteriumProduction`, `tritiumProduction`)
- `materialCost` (34) → ใช้ `ironCost` แทน

**T0.A4 · `Managers/CodexManager.cs`** — ถอดกลไกจ่ายแต้ม (D2)
- ลบ `currentRP` check (62) + ลบ `RaiseResourceDelta(ResearchPoints,-cost)` (71) → เหลือ `AutoUnlockByEvent` อย่างเดียว
- เพิ่ม `AddKnowledge(+2)` ตอนปลด Codex (V4 §9) + `UnlockById()` ให้ QuizManager เรียก (เฟส 1)

**T0.A5 · ไฟล์อ้าง field เก่า (compile fix)**
| ไฟล์:บรรทัด | แก้ |
|---|---|
| `UI/CodexUIController.cs:90` | `researchPoints`→`knowledge`, แสดง Knowledge อ่านอย่างเดียว |
| `UI/AlertController.cs:87–89` | ลบ label rad/Workers/RP · เพิ่ม Iron/Deuterium/Tritium/Knowledge |
| `UI/BuildingSelectionUI.cs:252,258` | `_resources.workers` → `PopulationManager.Instance.Current.total` |
| `UI/UIManagerHUD.cs:26,63,107,162,164` | ลบ radiationProtectionBar/workersBar/_max* (ทำพร้อม T0.B5) |

### 🅱 Work-stream B — ถอด Despair เหลือ Hope

**T0.B1 · `Data/PopulationData.cs`**
```csharp
public struct PopulationData { public int total; public float hope; } // Hope เดี่ยว เริ่ม 100
```
- ลบ `despair`, `isOnStrike`

**T0.B2 · `Managers/PopulationManager.cs`**
- field tuning (16–21): ลบ despairGain/despairRecovery/riotThreshold
- `Current` init (23–29): `total=10` (เดิม 50!), `hope=100f` · ลบ despair/isOnStrike
- `HandleResourceChanged` (74–81): ลบบรรทัด rad/workers
- `HandleDayEnded` (84–103): ตัด despair เหลือ Hope (V4 §9: อาหารขาด −10/วัน, Medic+ครบของ +2/วัน)
- `HandleMoraleDelta` (106–112): signature → `float hopeDelta`
- `ApplyAndBroadcast` (115–139): ลบ clamp despair + strike/riot block (128–132) · `RaiseMoraleChanged(hope)`
- game-over (134–138): `MoraleCollapsed` → `HopeZero` (T0.C1)

**T0.B3 · `Managers/EventManager.cs`**
- 29: `OnMoraleChanged`→`Action<float>` · 33: `OnMoraleDelta`→`Action<float>`
- 32: ลบ `OnRiotStarted` (+ Raise 121) · 118,122: อัปเดต Raise signature

**T0.B4 · `Narrative/DilemmaManager.cs` + `Data/DilemmaData.cs`**
- DilemmaData: `choiceA/B_DespairChange` (24,33) → ลบ
- DilemmaManager (105–116): `HandleMoraleChanged` param เดียว · ลบ trigger `despair_above_`
- (153,166–167): ตัด despairChange · `RaiseMoraleDelta(hopeChange)`
- 🔸 retarget consequence เต็ม = เฟส 4

**T0.B5 · `UI/UIManagerHUD.cs`**
- ลบ despairBar/Text (48–49), riotWarning (56), OnRiotStarted (82,94), HandleRiotStarted (237–240)
- morale handler (201–213): ลบ block despair/strike, รับ hope param เดียว
- ลบ radiationProtectionBar/workersBar
- 🔸 งาน Unity มือ: ลบ orphan GameObject DespairBar/RiotWarning/TrustBar + รัน `Setup HUD Canvas`

### 🅲 Work-stream C — GameEndType

**T0.C1 · `Data/GameEndType.cs`**
```csharp
public enum GameEndType { Win, HopeZero, Meltdown, TimeoutLowQ }
```
- ลบ ResourceDepleted/MoraleCollapsed/TowerDestroyed · 🔸 แยก True/Normal + Defeat Summary = เฟส 7

**T0.C2 · จุด raise + แสดงผล**
| ไฟล์:บรรทัด | แก้ |
|---|---|
| `CoreTowerManager.cs:169` | `TowerDestroyed`→`Meltdown` |
| `PopulationManager.cs:137` | `MoraleCollapsed`→`HopeZero` |
| `UI/UIManagerHUD.cs:254` | switch message: ลบ ResourceDepleted · เพิ่ม HopeZero/Meltdown/TimeoutLowQ |
| `GameManager.cs:60–64` | ✅ ไม่ต้องแก้ (เช็กแค่ `==Win`) |

### 🧪 T0.T · Tests + SaveData
- `SaveData.cs`: struct เปลี่ยนอัตโนมัติ · เซฟเก่า incompatible (ไม่มีเซฟจริง — ปลอดภัย)
- แก้: `PopulationManagerTests` (ตัด despair/riot, hope 50→100, total 50→10), `ResourceManagerTests` (ค่าเริ่ม + workers/RP/rad→iron/fuel/knowledge), `DilemmaManagerTests`, `IntegrationFlow`

### ลำดับลงมือเฟส 0
```
1. T0.A1 (ResourceData) → 2. T0.C1 (GameEndType) → 3. T0.B1 (PopulationData)   [เปลี่ยน "สัญญา" ก่อน]
4. T0.A2,A3,A4 + T0.B2,B3,B4 + T0.C2   [ไล่แก้ผู้ใช้งานให้ compile]
5. T0.A5 + T0.B5   [UI fix]
6. T0.T (เทสต์เขียว) → รัน Setup HUD Canvas ใน Unity
```
**DoD:** compile สะอาด · tests เขียว · เล่น Day 1→จบ ด้วย Energy/Water/Food/Iron + Hope เดี่ยว · ไม่มี reference despair/workers/RP/rad หลงเหลือ

---

## 6 · เฟส 1 — Quiz + Knowledge System

**Dependency:** เฟส 0 (มี `knowledge` + `ResourceType.Knowledge`)

### 🅰 Quiz Data + Content
**T1.A1 · `Data/QuizCategory.cs` + `Data/IQuizTrigger.cs`**
```csharp
public enum QuizCategory { Reactor, Agriculture, Medical, Ethics }
public interface IQuizTrigger { QuizQuestionSO[] GetLinkedQuizzes(); }
```
**T1.A2 · `Data/QuizQuestionSO.cs`** (V4 §16): `id, category, question, options[3], correctIndex, rewardKnowledge=8, explainText, speaker, codexUnlockId`
**T1.A3 · `Editor/QuizSetup.cs`** — เมนู `Setup Quiz System` generate 10 .asset จาก **V4 §12** (เนื้อหาพร้อมก๊อป) · สี §17: Q1,2,3,8,9=Reactor · Q6,7=Agri · Q4=Medical · Q5,10=Ethics

### 🅱 QuizManager
**T1.B1 · `Managers/QuizManager.cs`** (singleton)
- `HashSet<string> _answered` กันถามซ้ำ · `Queue<QuizQuestionSO> _pending`
- `ShowQuizPopup` → pause (เฟส 1 ใช้ `GameManager.SetState(Paused)`; เฟส 5 เปลี่ยนเป็น `TimeManager.Pause(QuizPopup)`)
- `SubmitAnswer`: ถูก +8 / ผิด +3 ผ่าน `RaiseResourceDelta(Knowledge,n)` · ปลด Codex ด้วย codexUnlockId · แสดง explain · queue ต่อ/resume
- `EnqueueQuizzes(IQuizTrigger)`, `TriggerByIds(params string[])`
**T1.B2 · `Managers/EventManager.cs`** — เพิ่ม `OnKnowledgeChanged(float)`, `OnQuizShown`, `OnQuizAnswered(id,correct)`

### 🅲 Quiz Popup UI
**T1.C1 · `UI/QuizPopupController.cs`** (เลียน `DilemmaPopupController`)
- คำถาม + 3 ปุ่ม · ยืนยัน enable เมื่อเลือกแล้ว (ตอบบังคับ ไม่มีข้าม)
- ตอบเสร็จ → ไฮไลต์ถูก(เขียว)/ผิด(แดง) + explainText 1 ย่อหน้า + ปุ่มปิด · สีตาม category · speaker VESTA/Dr. Auren Vasek
**T1.C2 · Setup** — สร้าง `QuizPanel` prefab + auto-wire (🔸 งาน Unity มือ)

### 🅳 Knowledge Integration
**T1.D1 · `Managers/ResourceManager.cs`** — `KnowledgeTier Tier`, `float KnowBonus => (knowledge>=80)?0.10f:0f`, raise OnKnowledgeChanged
**T1.D2 · `Managers/CoreTowerManager.cs`** — `fuelEfficiency = min(1,fuel/need) + KnowBonus` (🔸 fuel จริง = เฟส 2; เฟส 1 ใส่ +KnowBonus ก่อน)
**T1.D3 · `UI/UIManagerHUD.cs`** — knowledgeText/Bar (0–100) + ป้าย tier · subscribe OnKnowledgeChanged

### 🅴 Trigger Wiring
**T1.E1 · `Data/DilemmaData.cs`+`Narrative/DilemmaManager.cs`** — เพิ่ม `string[] linkedQuizIds` (implement IQuizTrigger) · ใน ApplyChoice → `EnqueueQuizzes(this)` · Plasma→Q2,Q3 · Outbreak→Q4,Q5 · Food→Q6,Q7
**T1.E2 · Trigger ที่เหลือ (stub รอเฟสที่เกี่ยว):** Q1 (Deuterium·เฟส6), Q5 (Zone A·เฟส3), Q8,Q9 (ignition·เฟส2), Q10 (decree·เฟส7) — ใส่ฮุค `TriggerByIds` ไว้รอ
**T1.E3 · `Managers/CodexManager.cs`** — `UnlockById(codexUnlockId)` (เชื่อม §12 "🔓 ปลดล็อก Codex")

### 🧪 T1.T
`QuizManagerTests` (ใหม่): ถูก+8/ผิด+3, กันซ้ำ, queue, ปลด Codex · `ResourceManagerTests`: clamp 0–100, KnowBonus ที่ 80 · `DilemmaManagerTests`: ApplyChoice → quiz เข้า queue

### ลำดับ + DoD
```
T1.A1,A2 → T1.B1,B2 → T1.D1,D2,D3 → T1.C1,C2 → T1.A3 (เนื้อหา) → T1.E1,E3 (+E2 stub) → T1.T
```
**DoD:** วิกฤต → เลือก A/B → เด้งควิซ 2 ข้อ ตอบบังคับ → Knowledge +8/+3 → ปลด Codex → เห็น tier บน HUD · knowBonus มีผลเมื่อ Knowledge≥80 · tests เขียว

---

## 7 · เฟส 2 — Reactor ให้ตรง V4 (§8)

**Dependency:** เฟส 0 (deuterium/tritium) + เฟส 1 (KnowBonus) · **stub:** วิศวกร (เฟส3), Coils (เฟส6), mode-lock (เฟส5)

### 🅰 เชื้อเพลิงจริง
**T2.A1 · `CoreTowerManager.cs`** — ลบ `fuelNeededPerTurn=50f` (32) → `FuelNeed = {0,10,20,30}` (ต่อโหมด) · เชื้อเพลิงตามเฟส: P1=ไม่กิน(ประกอบ), P2=Deuterium, P3=Tritium
**T2.A2 · `CoreTowerManager.cs:130–135`** — รื้อบล็อก fuel: อ่าน/หัก deuterium|tritium ตามเฟส
```csharp
ResourceType fuelType = (t.currentPhase >= 3) ? ResourceType.Tritium : ResourceType.Deuterium;
float need = FuelNeed[mode];
float fuelEff = (P1) ? 1f : Mathf.Min(1f, stock/Mathf.Max(1f,need)) + ResourceManager.Instance.KnowBonus;
fuelEff = Mathf.Min(fuelEff, 1.10f);
```
อัปเดต comment หัวไฟล์ (9–11) ลบ "proxy=Energy"
> 🔸 **supply chain:** Deuterium=Water L3 (เฟส6), Tritium=Zone B (เฟส3) → ก่อนนั้นคลัง=0 ใส่ **dev-grant ชั่วคราว** playtest

### 🅱 Cooling เต็มสูตร
**T2.B1 · `CoreTowerManager.cs:137–142`** — `cooling = 15 + waterUsed/10 + coolEng×4 + min(towerLv,3)×10`
**T2.B2** — เพิ่ม field: `coolingTowerLevel=0` (Toroidal·เฟส6), `hasPoloidalCoils=false` (เฟส6) · `PopulationManager.AssignedCoolingEngineers` (stub=0, เฟส3)

### 🅲 HEAT + micro-damage + พายุ
**T2.C1** — const `heatWarnZone=80f`, `heatMeltdown=100f` · ลบ heatCap-degradation · meltdown check (164): `>= heatMeltdown`
**T2.C2 · (153–159)** — micro-damage ใหม่: `if HEAT∈[80,100) && !hasPoloidalCoils → corePercent -= 2`
**T2.C3 · (35,149)** — `stormHeat 8→12` · พายุผูก `day>=25` (ส่ง day เข้า AdvanceTurn: แก้ 113→118,121)
**T2.C4 · `Data/TowerData.cs`** — deprecate `heatCap`

### 🅳 Q + ignition quiz
**T2.D1** — `public float Q => Current.corePercent/100f;`
**T2.D2 · UpdatePhaseTransitions (187–198)** — `completed==2` (เข้า Ignition) → `QuizManager.TriggerByIds("Q8","Q9")`
**T2.D3 · `ForceIdle(int days)`** — บังคับเตาเดินโหมด Idle N วัน (CORE% ไม่ขึ้น) → รองรับวิกฤต 2·B (GDD ล่าสุด, ใช้จริงเฟส 4)

### 🅴 (ออปชัน) **T2.E1 · `Data/GameConfigSO.cs`** — รวมค่าคงที่เตา (V4 §18) · เร็ว: คงเป็น field ใน manager ก่อน ยกไปเฟส 8

### 🧪 T2.T (`CoreTowerManagerTests` 12 ตัว — ปรับ)
fuel (Deut พอ/ครึ่ง, Tritium P3) · cooling (eng/towerLv) · knowBonus (≥80) · micro-damage (มี/ไม่มี Poloidal) · storm (day≥25) · meltdown (`Meltdown`) · SCRAM คงเดิม

### ลำดับ + DoD
```
T2.C3 (ส่ง day) → T2.A1,A2 → T2.B1,B2 → T2.C1,C2,C4 → T2.D1,D2 → T2.T (+dev-grant) → (ออปชัน T2.E1)
```
**DoD:** P2 กิน Deuterium / P3 กิน Tritium · cooling ครบ 4 เทอม · knowBonus มีผล · HEAT 80–99 ไม่มี Poloidal → CORE%−2 · day≥25 พายุ+12 · meltdown@100→`Meltdown` · เข้า Ignition เด้ง Q8/Q9

---

## 8 · เฟส 3 — ประชากร 3 คลาส (โครง)

**V4 §5:** Worker/Engineer/Medic · ฝึกจาก Worker · Shelter cap L1–L4 (10/20/40/80) · growth +1/วัน (Food พอ & Hope≥50)
**Dependency:** เฟส 0 (Hope/total) · ปลดล็อก `AssignedCoolingEngineers` ที่ stub ไว้เฟส 2

### งานหลัก (file-level — ละเอียดเพิ่มตอนลงมือ)
- **`Data/PopulationData.cs`** — ขยาย: `int workers, engineers, medics;` (`total` = ผลรวม) · `int shelterCap;`
- **`Managers/PopulationManager.cs`**:
  - `TrainEngineer()` — Worker→Engineer (Food 30+Energy 50, 1 วัน, ต้องมี Research Lab) · `TrainMedic()` (Food 40+Energy 60, ต้องมี Hospital)
  - `AssignedCoolingEngineers` property จริง (แทน stub เฟส 2)
  - growth: ใน HandleDayEnded → `if (food เหลือ && hope>=50 && total<shelterCap) workers++`
  - shelterCap จาก Shelter building level (เชื่อมเฟส 6) — เฟส 3 ใช้ const map L1–L4
- **`Data/BuildingData.cs`** — เพิ่ม `int shelterCapacity;` (Shelter) · flag `unlocksEngineerTraining/MedicTraining` (Research Lab/Hospital)
- **`Managers/ResourceManager.cs`** — เลิกใช้ `maxWorkers=100` คงที่ → ดึง cap จาก PopulationManager.shelterCap · workerScale ใช้ `workers` (เฉพาะคลาส Worker) ไม่ใช่ total
- **`UI/UIManagerHUD.cs`** — populationText แตกเป็น 👷Worker / 🔧Engineer / ⚕Medic + cap
- **`UI`** ใหม่ — ปุ่มฝึกคลาส (Research Lab/Hospital panel) + assign engineer หล่อเย็น
- **Editor** — Shelter/Research Lab/Hospital assets (ถ้ายังไม่มี) ใส่ field cap/unlock
- **Tests** — `PopulationManagerTests`: ฝึกคลาส, growth ตามเงื่อนไข, cap ตาม Shelter

**DoD:** ฝึก Engineer/Medic ได้ · ประชากรโต +1/วัน ตามเงื่อนไข · เพดานตาม Shelter L1–L4 · วิศวกรหล่อเย็นเข้าสูตรเตาจริง (ปลด stub เฟส 2)

---

## 9 · เฟส 4–8 (โครงย่อ — แตก task ละเอียดเมื่อถึงคิว)

### เฟส 4 · Crisis A/B/C + retarget Hope (~1–2 วัน)
- `DilemmaData`: เพิ่ม `choiceCText` + consequence C · `DilemmaPopupController`: ปุ่ม C (17–18,52–53)
- เปลี่ยน consequence → Hope (ตัด despair จากเฟส 0 แล้ว) · เติมเนื้อหา A/B/C 3 วิกฤตจาก **§10 ฉบับล่าสุด**
- ผูกควิซ (ทำในเฟส 1 T1.E1 แล้ว — เติม C path)
- **★ วิกฤต 2·B (แก้ใน GDD ล่าสุด):** เลิกใช้ Tritium 20 → **บังคับเตาเดิน Idle 1 วัน** (ใช้ฟลักซ์นิวตรอนจากเตาผลิตไอโซโทปการแพทย์จริง Mo-99/Tc-99m/Lu-177/I-131) ⇒ วันนั้น CORE% ไม่ขึ้น = ต้นทุนจริงแทนการกินเชื้อเพลิง
  - ต้องมี **consequence ชนิดใหม่ที่ "สั่งโหมดเตา"** (force Idle N วัน) — ไม่ใช่แค่ resource delta → เพิ่ม `ReactorController.ForceIdle(days)` (ผูกกับเตาเฟส 2) แล้วให้ `CrisisChoice.effects` เรียกได้
  - Q4 เขียนใหม่เป็น **2 ขั้น** (วินิจฉัย PET/SPECT ถ่ายภาพหาเป้า → รักษา targeted therapy ยิงเป้า) — ดึงเนื้อหาจาก §12 ฉบับล่าสุด

### เฟส 5 · Time Planning/Live + Placement Pause + batch (~2–3 วัน)
- `Managers/TimeManager.cs` ใหม่ — pause-reason stack (`HashSet<PauseReason>`), Planning 30s/Live 60s, `LockMode()` ตอนเข้า Live (V4 §16)
- ผลิตแบบ **batch ตอน EndDay** (เลิก tick 5 วิใน ResourceManager) — ApplyDailyProduction/Consumption
- `PlacementController` — เข้า placement → `TimeManager.Pause(Placement)`, จบ/ยกเลิก → Resume (V4 §15)
- เปลี่ยน QuizManager/CrisisPopup จาก SetState(Paused) → `TimeManager.Pause(QuizPopup/CrisisPopup)`

### เฟส 6 · Building levels L1–L3 + Coils (~2 วัน)
- `BuildingData` หรือ `BuildingLevelSO[]` — stats ต่อระดับ (V4 §18 ตาราง Production/Upkeep) · upgrade action + cost
- Water L3 → ปลดสกัด Deuterium (แหล่ง fuel เฟส 2) · Zone B → Tritium
- Toroidal Coils (coolingTowerLevel +1, cap 3) · Poloidal Coils (เปิด coolEng×4 + กัน micro-damage) → ปลด stub เฟส 2

### เฟส 7 · Endings + Defeat Summary + Decrees + MetaProgress (~2–3 วัน)
- `GameManager.CheckWinLose` — Q≥1.0→True, Q 0.5–0.99→Normal, else TimeoutLowQ (V4 §14,§16)
- `UI` จอ Defeat Summary (lossReason: HopeZero/Meltdown/TimeoutLowQ) + Ending Normal/True
- `Managers/DecreeManager.cs` + `DecreeSO` — 2 decree (จ่าย Hope เท่านั้น) + ผูก Q10 · ปลดล็อก cooling labor
- `MetaProgress` (static, PlayerPrefs) — KnowledgeBank + UnlockedCodex ข้ามรอบ · Restart รีเซ็ตทุกอย่างยกเว้น 2 ค่านี้ (V4 §9,§16)

### เฟส 8 · UI/Polish (~2–3 วัน)
- HUD ครบ (Hope/Knowledge/CORE%/HEAT/day/phase/mode/SCRAM/Build) · กริด 20×12→**43×43**
- Tutorial Day 1 (เติมจาก skeleton) · ไอเทมเบา (ผูกผลทางเลือกวิกฤต) · (ออปชัน) GameConfigSO รวมค่าจูน
- playtest Day 1→30 × 2–3 รอบ → จูน §18 (โดยเฉพาะความตึง Energy Phase 4 + จำนวนวัน Boost)

---

## 10 · ตารางพึ่งพาข้ามเฟส (ของที่ stub จนเฟสเกี่ยวข้องเสร็จ)

| เทอม/ฟีเจอร์ | นิยามที่เฟส | ใช้จริงที่เฟส | ระหว่างนั้น |
|---|---|---|---|
| `knowledge` / `KnowBonus` | 0 | 1 | field พร้อม, ค่า 0 |
| Deuterium supply | 6 (Water L3) | 2 | dev-grant ชั่วคราว |
| Tritium supply | 6 (Zone B/3) | 2 | dev-grant ชั่วคราว |
| `coolingEngineers×4` | 3 (Engineer) | 2 | stub property = 0 |
| `coolingTowerLevel×10` | 6 (Toroidal) | 2 | config = 0 |
| `hasPoloidalCoils` | 6 (Poloidal) | 2 | config = false |
| mode lock (Planning) | 5 (TimeManager) | 2 | ตั้งโหมดได้ตลอด |
| batch production | 5 | 0–4 | tick 5 วิ ไปก่อน |
| pause-reason (Quiz/Crisis) | 5 | 1 | `SetState(Paused)` ไปก่อน |
| Shelter cap จาก building level | 6 | 3 | const map L1–L4 |
| วิกฤต 2·B สั่งเตา Idle 1 วัน (GDD ล่าสุด) | 4 (Crisis effect) | 4 | พึ่ง `ReactorController.ForceIdle(days)` — expose ตอนเฟส 2 |

---

## 11 · เช็กพอยต์ก่อนเริ่ม
- 🔸 เคลียร์ commit งานเดิม (Block C/D ที่ค้างใน working tree ตาม PROGRESS) ก่อน refactor เฟส 0 กันชนกัน
- 🔸 ทุกเฟสจบด้วย: compile สะอาด + EditMode tests เขียว + รันเมนู Setup ที่เกี่ยวข้องใน Unity
- 🔸 เนื้อหาพร้อมก๊อปจาก V4: ควิซ (§12), Crisis A/B/C (§10), ตาราง tuning (§18)

*เอกสารนี้สรุปจากการสำรวจโค้ดจริง 2026-06-30 · อัปเดต 2026-07-01 ให้ตรง GDD ฉบับล่าสุด (`FINAL NUCLEAR ReMind V4.md` — วิกฤต 2·B ใช้เตา Idle ผลิตไอโซโทปการแพทย์ แทน Tritium 20 + Q4 แบบ 2 ขั้น · ฉบับก่อนอยู่ `Docs/archive/`)*
