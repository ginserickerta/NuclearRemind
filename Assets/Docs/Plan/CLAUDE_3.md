# CLAUDE.md — Nuclear Re:Mind · NSC #28
> Context anchor สำหรับ Claude Code · อ้างอิง GDD v2.1 + PROGRESS.md (2026-06-25)
> อัปเดตทุกครั้งที่มีการเปลี่ยนแปลงระบบหลัก

---

## 0 · PROJECT SNAPSHOT

| ข้อมูล | ค่า |
|---|---|
| Engine | Unity 6 · C# · Built-in Render Pipeline · Legacy Input |
| Deadline | **10 ก.ค. 2569** (build พร้อมส่ง 9 ก.ค.) |
| สถานะ (25 มิ.ย.) | ~35% รวม · Architecture 90% · Content 12% |
| ด่วนที่สุด | ongoing consumption, worker-per-building, Codex content, Dilemma trigger |
| ห้ามทำ | PowerGridManager ใหม่, Story arc, NarrativeManager, Exploration System |

---

## 1 · ARCHITECTURE (สิ่งที่มีอยู่แล้ว)

### Managers (Singleton)
```
GameManager.cs          — State machine + pause  [100%]
EventManager.cs         — MonoBehaviour Singleton [DefaultExecutionOrder(-100)]  [100%]
ResourceManager.cs      — tick ทุก 5วิ, production จาก BuildingData  [90%]
                          ⚠ ขาด: ongoing consumption, worker-per-building
PopulationManager.cs    — trust decay, worker strike  [85%]
                          ⚠ ขาด: population growth
CoreTowerManager.cs     — 3 phases + win condition  [70%]
                          ⚠ Overdrive stub (CoreTowerManager.cs:25-26)
SaveManager.cs          — JSON round trip  [90%]
BuildingRegistry.cs     — lookup by name, re-occupy cells หลัง load  [100%]
CodexManager.cs         — TryUnlock, AutoUnlockByEvent  [100%]
DilemmaManager.cs       — popup UI ✅ แต่มีแค่ 1 asset, ไม่มี trigger  [40%]
```

### Systems
```
GridManager.cs          — 20×12, IsoToWorld/WorldToIso  [100%]
CameraController.cs     — WASD + zoom  [100%]
InputManager.cs         — click → grid cell  [100%]
PlacementController.cs  — ghost, footprint, ValidateCell  [100%]
BuildingVisualSpawner.cs — spawn/destroy sprite, power dimming  [100%]
ConstructionController.cs — build queue, cancel, refund  [95%]
DemolitionController.cs  — ทุบ + คืน workers  [90%]
PowerGridManager.cs     — BFS coverage  [85%]  ⚠ OUT OF SCOPE อย่าแตะ
```

### UI
```
UIManagerHUD.cs         — resource bars  [60%]  ⚠ ขาด Day counter, Speed controls, Alert
TooltipController.cs    — 3 ชั้น (name/description/nuclearKnowledge)  [80%]
CodexUIController.cs    — list, detail view, RP counter  [90%]
BuildingSelectionUI.cs  — hotbar 1-8  [90%]
BuildingQueueUI.cs      — queue, cancel  [80%]
TutorialManager.cs      — skeleton เท่านั้น  [30%]
```

### ScriptableObjects ที่มี
```
ScriptableObjects/Buildings/    — 7 assets (placeholder sprites)
ScriptableObjects/CodexEntries/ — 5/5 Core entries ✅
                                  0/5 Agriculture ❌
                                  0/5 Medical ❌
                                  0/5 Environment ❌
```

---

## 2 · GAME RULES (GDD v2.1 — AUTHORITATIVE)

### 2.1 Resources (5 ชนิด)
| Resource | ผลิตจาก | บริโภคโดย |
|---|---|---|
| Energy ⚡ | Power Plant, CORE TOWER (ท้ายเกม) | ทุกระบบ, สร้าง/วิจัย/อัป |
| Water 💧 | Water Plant | คน, หล่อเย็น CORE, สกัด Deuterium |
| Food 🌿 | Food Plant, Agri Dome | คน, ฝึกคนชั้นสูง |
| Material ⬛ | Mine | สร้าง/ซ่อม, คราฟต์ไอเทม |
| Fusion Fuel ☢ | สกัด Deuterium (Water L3), Zone B (Tritium) | CORE TOWER |

### 2.2 Population (3 คลาส)
- **คนงาน** — ผลิตทุกอย่าง เริ่ม 10 คน เพดานจาก Shelter
- **วิศวกร** — วิจัย + คุม CORE ฝึกจากคนงาน (ใช้อาหาร + Research Lab, Phase 2+)
- **แพทย์** — รักษา + ลดรังสี (ใช้อาหาร + Med Center, Phase 3+)

เพดาน: Shelter L1=10 / L2=20 / L3=40 / L4=80

### 2.3 Day Cycle
```
Day 1:    Tutorial (ไม่จับเวลา) — ซ่อม Shelter, สร้าง 3 โรงงาน, จัดคน
Day 2-30: Planning 30s (หยุด) → Running 60s (ผลิต/เหตุการณ์) → EndOfDay resolve
```

### 2.4 Building Tiers (สูตรสำหรับ ScriptableObject)
สเกล: ×3 → ×2.5 → ×2 → ×2

**Power Plant** L1:+60/1คน/2💧 | L2:+180/2คน/6💧 | L3:+450/3คน/14💧 | L4:+900/5คน/30💧 | L5:+1800/8คน/60💧
ค่าอัป: L2=150 | L3=360 | L4=720 | L5=1500 (วัสดุ)

**Water Plant** L1:+50/1คน | L2:+150/2คน | L3:+375/3คน | L4:+750/5คน | L5:+1500/8คน
เดินระบบ (⚡): L1=10 | L2=30 | L3=70 | L4=150 | L5=300
ค่าอัป: L2=150 | L3=360 | L4=720 | L5=1500

**Food Plant** L1:+40/1คน | L2:+120/2คน | L3:+300/3คน | L4:+600/5คน | L5:+1200/8คน
เดินระบบ: L1=8💧+5⚡ | L2=24💧+15⚡ | L3=56💧+35⚡ | L4=120💧+75⚡ | L5=240💧+150⚡
ค่าอัป: L2=150 | L3=360 | L4=720 | L5=1500

**Mine** L1:+30/2คน | L2:+90/3คน | L3:+225/4คน | L4:+450/6คน | L5:+900/9คน
เดินระบบ (⚡): L1=10 | L2=30 | L3=70 | L4=150 | L5=300
ค่าอัป: L2=120 | L3=300 | L4=600 | L5=1200
⚠ L5 → trigger CRISIS_OUTBREAK ได้

---

## 3 · CORE TOWER SYSTEM (GDD v2.1 §07★)

### 3.1 แนวคิดหลัก
- วัดด้วย **CORE%** (0→100%) แทน Q value โดยตรง
- CORE% = 100% เท่ากับ Q ≥ 1.0 → ชนะ
- ผู้เล่นกด **โหมดเร่งเครื่อง** ทุกเทิร์น ระหว่าง Phase 2–3

### 3.2 3 เฟสเตา
| เฟส | ช่วง CORE% | วัน | เชื้อเพลิง | เร่งเครื่อง |
|---|---|---|---|---|
| 1 · Cold Assembly | 30→50% | Day 11–17 | Energy + Material | ล็อก |
| 2 · Plasma Ramp | 50→80% | Day 18–24 | Deuterium | เปิด |
| 3 · Ignition | 80→100% | Day 25–30 | Tritium | เร่งเต็ม + พายุ |

CORE เริ่มที่ 30% เมื่อปลดล็อกครั้งแรก (Day 11)

### 3.3 โหมดเร่งเครื่อง (เฟส 2–3)
| โหมด | ตัวคูณ | +CORE%/เทิร์น | +HEAT/เทิร์น |
|---|---|---|---|
| Idle · พัก | ×0 | +0 | หล่อเย็นล้วน ลด HEAT |
| Normal · ปกติ | ×1 | +3 | +5 |
| Boost · เร่ง | ×2 | +6 | +20 |
| Overdrive · เร่งสุด | ×3 | +9 | +40 |

**ปุ่ม SCRAM** (HEAT ≥ 90): ลด HEAT −40 แต่ CORE% −10, เสีย Water, มี cooldown

### 3.4 สูตรคำนวณ (ต่อเทิร์น)
```csharp
// CORE% progress
float fuelEff = Mathf.Min(1f, fuelInput / fuelNeeded);
float dCORE = 3f * modeMultiplier * fuelEff;

// Cooling capacity
float cooling = 15f
    + (waterUsed / 10f)
    + (coolEngineers * 4f)
    + (coolTowerLevel * 6f)
    + (usedSickLabor ? sickCount * 2f : 0f)    // Emergency Decree
    + (usedChildLabor ? 12f : 0f);              // Emergency Decree

// Heat delta
float dHEAT = modeHeat[mode] + stormHeat - cooling;
// stormHeat = +8 เฉพาะ Phase 3 (Day 25–30)
coreHeat += dHEAT;

// Thresholds
if (coreHeat >= 100) → MELTDOWN → GAME_OVER
if (coreHeat >= 80)  → RedZone: 15–30% chance micro-damage (heatCap--)
if (corePercent >= 100) → IGNITION_SUCCESS
```

### 3.5 Crisis Triggers จาก CORE
```
HEAT > 80% หรือ corePct ≥ 50 (Q>0.3) → trigger CRISIS_PLASMA (Day ~17)
```

---

## 4 · HOPE / DESPAIR SYSTEM

```
ค่าเริ่มต้น: Hope = 50, Despair = 20
คำนวณทุกสิ้นวัน:

hopeDelta = (foodOK ? +2 : 0)
          + (waterOK ? +2 : 0)
          + (qUp ? +3 : 0)
          - (sickCount * 5)
          - (deadCount * 10)
          - (usedSickLabor ? 3 : 0)   // Phase 3 เท่านั้น
          - (usedChildLabor ? 2 : 0)  // Phase 3 เท่านั้น

Thresholds:
  Hope > 80  → โบนัสผลิต
  Despair > 80 → ประท้วง โรงงานหยุด
  Hope ≤ 0   → GAME_OVER ทันที
```

**⚠ Hope และ Despair เป็น independent values ไม่ใช่ inverse กัน**
(hope ≠ 100 − despair)

---

## 5 · CRISIS EVENTS (3 ตัว)

### Crisis 1 · Plasma Instability (~Day 17)
- **Trigger**: HEAT > 80% หรือ CORE% ≥ 50
- **สอน**: โทคาแมก, สนามแม่เหล็กคู่ (Toroidal + Poloidal)
- **เวลา**: 3 วันก่อนเตาระเบิด
- A · Overdrive Toroidal: −300 Energy ทันที, ย้ายคน 3 คน 1 วัน → ปลอดภัยทันที
- B · Manual Poloidal Repair: วิศวกร 4 คน 2 วัน, −150 Material, เสี่ยงป่วย 50%
- C · Emergency Coolant: −50% Water คลัง, CORE% −20% → วิกฤตน้ำตามมา

### Crisis 2 · Malignant Outbreak (~Day 20)
- **Trigger**: zoneA_workers > threshold
- **สอน**: เวชศาสตร์นิวเคลียร์ (PET/SPECT, Radionuclide Therapy)
- คน 15 คนป่วย มีเวลา 3 วัน
- A · PET/SPECT Scan: −200 Energy, วิศวกร 2 คน → รักษา 10 คน (5 คนต้องใช้ B)
- B · Radionuclide Therapy: −200 Material, −20 Tritium → รักษา 15 คนใน 1 วัน
- C · Gamma Sterilize & Quarantine: ประหยัด แต่กักตัว 4 วัน เสี่ยงตาย 3 คน

### Crisis 3 · Spoilage / Food Crisis (~Day 24)
- **Trigger**: food > 500 หรือ !hasAgriDome
- **สอน**: การฉายรังสีถนอมอาหาร (Co-60), ปรับปรุงพันธุ์พืช (กข6/กข15)
- อาหารเน่าเร็ว ×3 มีเวลา 3 วัน
- A · Mutant Seed Breeding: −250 Material, วิจัย 3 คน → ผลผลิต ↑ ถาวร
- B · Cobalt-60 Sterilization: −300 Energy, คน 4 คน → เน่า 0% ทันที
- C · Ration Cutting: ประหยัด แต่ประสิทธิภาพ −50%, Hope ดิ่ง

---

## 6 · EMERGENCY DECREES (§11★ — Phase 3 เท่านั้น)

ปรากฏ Day 25–30 เป็น UI เล็กมุมจอ กดได้ เลือกใช้หรือไม่ก็ได้

| กฎ | ได้ทันที | จ่ายทันที | จ่ายต่อเนื่อง | ความเสี่ยง |
|---|---|---|---|---|
| 1 · เกณฑ์แรงงานผู้ป่วย | +cooling ~2/คน | Hope −8, Despair +6 | Hope −3/วัน | ตาย 20%/วัน → Hope −10/คน |
| 2 · ใช้แรงงานเด็ก | +cooling ~12 รวม | Hope −15, Despair +10 | Hope −2/วัน | ติด flag → epilogue เปลี่ยน |

**Flags**: `bool usedSickLabor`, `bool usedChildLabor`
→ map เข้า ending epilogue เท่านั้น ไม่เปลี่ยน win condition หลัก

**จุดสอน**: ALARA principle (กลุ่มเปราะบางต้องได้รับการปกป้อง)

---

## 7 · ENDINGS (4 แบบ)

| เงื่อนไข | Ending |
|---|---|
| Hope ≤ 0 ก่อน Day 30 | 💀 Game Over |
| Day 30 + CORE% < 50 (Q < 0.5) | ⚠ Bad Ending |
| Day 30 + CORE% 50–99 (Q 0.5–0.99) | 🌥 Normal Ending |
| CORE% ≥ 100 + ผู้รอดครบ | ☀ True Ending |
| ↳ + usedSickLabor | ☀ True Ending + epilogue "จ่ายด้วยชีวิตผู้อ่อนแอ" |
| ↳ + usedChildLabor | ☀ True Ending (ขมขื่น) + ตราบาปในสถิติ |

---

## 8 · PHASE GATING (Unlock Timeline)

| Phase | วัน | ปลดล็อก |
|---|---|---|
| 1 | Day 1–5 | Power Plant, Water Plant, Food Plant, Mine, Shelter |
| 2 | Day 6–10 | Research Lab, Train Engineer, Deuterium extraction (Water L3 required) |
| 3 | Day 11–20 | Zone A, Med Center, CORE TOWER (Cold Assembly เริ่ม) |
| 4 | Day 21–30 | Zone B, Tritium, Plasma Shield (Zone B required) |

---

## 9 · CODEX SYSTEM

### โครงสร้าง
```
CodexEntry SO fields:
  - id: string
  - title: string
  - branch: enum { Core, Agriculture, Medical, Environment }
  - content: string (เนื้อหาวิทย์จริง)
  - unlockEvent: string (EventManager event name)
  - prerequisiteId: string (optional)
```

### สถานะ
| Branch | เสร็จ | เป้าหมาย |
|---|---|---|
| Core | 5/5 ✅ | ฟิชชัน, ลูกโซ่, ครึ่งชีวิต, สารหล่อเย็น, รังสี |
| Agriculture | 0/5 ❌ | โคบอลต์-60, ฉายรังสีถนอมอาหาร, พันธุ์พืช (กข6/กข10/กข15) |
| Medical | 0/5 ❌ | PET/SPECT, Radionuclide Therapy, ปริมาณรังสี, เซลล์มะเร็ง |
| Environment | 0/5 ❌ | ALARA, รังสีพื้นหลัง, เขตรังสี, ผลระยะยาว |

---

## 10 · INITIAL STATE VALUES

```csharp
// GameState เริ่มต้น (หลังซ่อม Shelter)
energy    = 400
water     = 100
food      = 100
material  = 50
fuel      = 0
population = 10      // workers
hope       = 50
despair    = 20
corePercent = 0f     // เตาพัง ยังไม่เปิด
coreHeat    = 0f
day         = 1
dayLength   = 90f    // วินาที (Day 1 ไม่จับเวลา)
```

---

## 11 · CRITICAL TODO (เรียงตาม impact)

### 🔴 ต้องทำก่อนทุกอย่าง (เกมเล่นไม่สมบูรณ์)
1. **ongoing consumption** ใน ResourceManager — อาคารต้องกิน resource ทุก tick ไม่ใช่ผลิตฝั่งเดียว
2. **worker-per-building** — คนงานประจำอาคาร ไม่ใช่ global pool
3. **Day counter + Speed controls** บน HUD
4. **Alert/Notification popup** (food ต่ำ, ไฟฟ้าไม่พอ, วิกฤต)

### 🔴 NSC Scoring — เนื้อหาวิทย์ (หัวใจคะแนน)
5. **Codex Agriculture** 5 entries + unlock trigger
6. **Codex Medical** 5 entries + unlock trigger
7. **Codex Environment** 5 entries + unlock trigger
8. **Dilemma assets** อีก 2 ตัว + trigger จริงใน game event (ตอนนี้มี 1 asset, 0 trigger)
9. **End Game Summary + Knowledge Quiz**

### 🟡 ปิด stub ที่ค้าง
10. `CoreTowerManager.cs:25` — Overdrive energy consumption rate
11. `CoreTowerManager.cs:26` — Overdrive durability decay rate
12. **Population growth** logic
13. **CORE TOWER phase system** (v2.1 เปลี่ยนจาก S0–S4 เป็น 3 เฟส + CORE%)

### 🟢 ทำทีหลังได้
14. Tutorial guided steps
15. Ending scenes (4 แบบ + epilogue flags)
16. SFX (ไซเรน 1 + เสียงสร้าง 1)
17. Emergency Decrees UI
18. Art sprite จาก Naraphat (รอ handoff)

---

## 12 · NAMING CONVENTIONS

```
Managers:   *Manager.cs           (GameManager, ResourceManager)
Systems:    *Controller.cs        (PlacementController)
Data:       *Data.cs / *SO.cs     (BuildingData, CodexEntry)
UI:         *UI.cs / *HUD.cs      (UIManagerHUD, CodexUIController)
Events:     EventManager.Publish("EVENT_NAME")
```

### EventManager Event Names (ที่ใช้อยู่)
```
"ON_DAY_START"         "ON_DAY_END"
"ON_RESOURCE_CHANGED"  "ON_BUILDING_PLACED"
"ON_CRISIS_TRIGGERED"  "ON_CORE_PHASE_CHANGED"
"ON_CODEX_UNLOCKED"    "ON_GAME_OVER"
"ON_VICTORY"
```

---

## 13 · SCOPE RULES (ห้ามทำ)

- ❌ PowerGridManager ใหม่หรือ feature เพิ่ม (out of scope)
- ❌ Story arc Dr. Auren / NarrativeManager / Aethon / Keran
- ❌ Exploration System (6 zones)
- ❌ Save/Load UI (ไม่จำเป็น เล่นรวดเดียว ~45 นาที)
- ❌ Worker assignment แบบ spatial (global pool พอ)
- ❌ L4–L5 balance ละเอียด (ใส่ค่าหยาบ ๆ ไว้ก่อน)
- ❌ Animation / Particle effect ก่อน Day 14 ของ dev

---

## 14 · CONFLICT RESOLUTION

เมื่อ GDD v2.1 กับ codebase ขัดกัน → **GDD v2.1 ชนะเสมอ**
เมื่อพบ conflict → flag ให้ Rut confirm ก่อนแก้

ตัวอย่าง conflict ที่รู้อยู่แล้ว:
- `CoreTowerManager.cs` ยังใช้ระบบ S0–S4 แต่ GDD v2.1 เปลี่ยนเป็น 3 เฟส (Cold/Ramp/Ignition) + CORE%
- `PopulationManager.cs` มี trust/hope แต่ GDD v2.1 ระบุชัดว่า Hope/Despair เป็น independent ไม่ใช่ inverse

---

*CLAUDE.md version: 2.1-sync · อิงจาก GDD v2.1 + PROGRESS.md 2026-06-25*
