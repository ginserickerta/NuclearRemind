# STORY_PLAYTEST.md — เฟส 6: ตรวจรับระบบเนื้อเรื่อง (Story Guide)

> ใช้ไล่เช็คหลังรัน `NuclearReMind → Run All Setups (in order)` + Ctrl+S
> ระบบเนื้อเรื่อง 5 เฟสอยู่ใน commit: `6ea363d` (data) → `7f75acc` (director) → `f80e999` (UI) → `08e968e` (content) → `3aa02bc` (deferred/decree)

---

## 1. รันเทสต์อัตโนมัติ (~200 เคส)

**ทางที่ 1 — Unity เปิดอยู่:** `Window → General → Test Runner → แท็บ EditMode → Run All`

**ทางที่ 2 — ปิด Unity ก่อน แล้วรันใน PowerShell:**
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe" -batchmode -runTests -projectPath "C:\Users\UsEr\NSC2026" -testPlatform EditMode -testResults results.xml -logFile test.log
```
เสร็จแล้วเปิด `results.xml` หา `result="Failed"` — ไม่มี = ผ่านหมด

เทสต์ฝั่งเนื้อเรื่อง 37 เคส:
| ไฟล์ | เคส | ตรวจอะไร |
|---|---|---|
| StoryDataTests | 7 | schema การ์ด/ควิซรายทางเลือก |
| StoryDirectorTests | 9 | trigger 7 แบบ + ลำดับบังคับ + save/load |
| StoryUITests | 8 | การ์ดหยุดเวลา/กดปิด + แผง Records + อนุสรณ์ |
| StoryContentTests | 7 | asset จริงบนดิสก์ — กฎเหล็ก "ความรู้มาก่อนควิซ" ฯลฯ |
| StoryDeferredCrisisTests | 6 | วิกฤตซ้อน 2 วัน + เงื่อนไข decree |

⚠️ StoryContentTests จะขึ้น **Ignore** (สีเหลือง) ถ้ายังไม่เคยรัน `Setup Story Content` — ไม่ใช่บั๊ก

---

## 2. เช็คซีนหลังรัน Run All Setups

- Hierarchy มี: `StoryCanvas` · `StoryDirector` · `CardUIController` · `RecordsPanelController` · `MemorialPanelController` · `PrePlacedMemorial`
- Inspector ของ `StoryDirector`: **beats = 12** เรียงตามไทม์ไลน์
- Inspector ของ `DilemmaManager`: **dilemmaPool = 0** (วิกฤตทุกใบยิงผ่าน beat แล้ว — ถ้าไม่ว่างแปลว่ารัน Crisis Setup เวอร์ชันเก่า)
- Project: `Assets/ScriptableObjects/Story/` มี 23 asset (Beat×12 · Record×4 · Info×6 · Memorial×1) · `Dilemmas/` มี 6

---

## 3. ไล่เล่น Day 1→30 — สิ่งที่ต้องเห็นตามลำดับ

| # | ทำให้เกิด | ต้องเห็น |
|---|---|---|
| 1 | สร้าง**ห้องปฏิบัติการ**จนเสร็จ | การ์ด **บันทึก #01 — เชื้อเพลิงในน้ำ** เด้งกลางจอ เวลาหยุด (HUD ขึ้น "หยุด") · กดปิดแล้วเวลาเดินต่อ |
| 2 | กดปุ่ม **"บันทึก"** (ซ้ายล่าง เหนือปุ่มคีย์ลัด) | แผง Records เปิด มีบันทึก #01 กดย้อนอ่านได้ |
| 3 | **คลิกตึกอนุสรณ์** (2×2 ข้างขวาหอคอย) | แผงรายชื่อ 6 คน — มี **ELARA VANE** · toast "▸ ความคิด: คนพวกนี้เคยอยู่ที่นี่..." เด้ง**ครั้งแรกครั้งเดียว** |
| 4 | อัปโรงน้ำถึง L3 → ได้ **Deuterium ครั้งแรก** | Kova พูด → บันทึก #02 → การ์ดความรู้ "เชื้อเพลิงที่ซ่อนอยู่ในน้ำ" → **ควิซ Q1** (ลำดับต้องเป๊ะ: บันทึก → ความรู้ → ควิซ) |
| 5 | เตาปลดล็อก (Day 11) | บันทึก #03 — ความร้อนไม่ใช่ศัตรู |
| 6 | จบวันที่ **HEAT > 80 หรือ Q > 0.3** (~Day 17) | การ์ดความรู้พลาสมา → วิกฤต**เสถียรภาพพลาสมา** → เลือก → การ์ดบทสรุปของทางที่เลือก → **Q2, Q3** |
| 6b | (ถ้าเลือก **C** ฉีดสารหล่อเย็น) | **2 วันต่อมา** วิกฤตซ้อน "น้ำไม่พอ" เด้ง |
| 7 | จบวัน 20 | การ์ดความรู้การแพทย์ → วิกฤต**โรคจากรังสี** → **Q4, Q5** |
| 7b | (ถ้าเลือก **C** กักตัว) | ตาย 3 คน · **2 วันต่อมา** วิกฤตซ้อน "แรงงานฟาร์มขาด" |
| 8 | จบวันที่**อาหารเกิน 500** (~Day 24) | การ์ดความรู้อาหาร → วิกฤต**อาหาร** → **A ได้ Q6 · B ได้ Q7 · C ไม่มีควิซ** ← จุดตรวจสำคัญ |
| 9 | เริ่มวัน 20–23 | **ลางพายุวันละ 1 บรรทัด** (เซนเซอร์เพี้ยน → Kova → ท้องฟ้า → สภาพอากาศ) ไม่รวบทีเดียว |
| 10 | เริ่มวัน 23 | การ์ด **บันทึก #สุดท้าย — รายงานเตือนพายุ** + ความคิด "รายงานที่ผมเอาไปส่ง..." |
| 11 | เริ่มวัน 25 | Kova ตะโกน + แจ้งพายุ → การ์ดความรู้**ฟิวชัน** → **Q8, Q9** · จากนี้ heat +12/วัน |
| 12 | จบวันช่วงพายุที่ **HEAT ≥ 70** | การ์ดทบทวน **ALARA** → **ประกาศฉุกเฉิน**: A ไม่มีควิซ · B (−8) / C (−15) → **Q10** |
| 13 | CORE ถึง 100% | **True Ending** "ภารกิจสำเร็จ — แสงแรกของโลกใหม่" (บทเต็ม + สถิติ + ข้อความให้กำลังใจ) |
| 13b | ทางแพ้ | Hope 0 / HEAT ≥ 100 / หมดเวลา → GAME OVER ตามแบบ · Q 0.5–0.99 → Normal Ending |
| 14 | **F5 เซฟ** กลางเรื่อง → **F9 โหลด** | beat ที่เล่นแล้ว**ไม่เด้งซ้ำ** · แผง Records ยังครบ · วิกฤตซ้อนที่ค้างยัง**มาตามนัด** |

---

## 4. Checklist §6 ของ Story Guide — สถานะ

| ข้อ | บังคับที่ | สถานะ |
|---|---|---|
| ทุก beat ที่มีควิซ มี infoCard/record นำก่อน | StoryDirector state machine + เทสต์ IronRule | ✅ |
| บันทึกเด้งกลางจอ + เก็บเข้าแผง Records | CardUIController + RecordsPanelController | ✅ |
| ไม่มีคลิก interactable เล็กๆ เพื่ออ่านบันทึก | บันทึกเด้งเองทุกใบ (Data Recovery) | ✅ |
| Memorial เป็น building คลิกเปิดแผงรายชื่อ | MemorialPanelController.TryOpenAtCell | ✅ |
| ลำดับ InfoCard→Event→Choice→Outcome→Quiz | StoryDirector.Advance (+ DilemmaManager ข้าม enqueue ให้) | ✅ |
| ควิซ +8 ถูก / +3 ผิด + explanation + ปลด Codex | QuizManager (ระบบเดิม) | ✅ |
| Ending ตามลำดับ · ทุกทางชนะ → True Ending เดียว | GameManager + บทจบตาม guide ใน UIManagerHUD | ✅ |
| String ผู้เล่นเป็นไทยตามไฟล์ ไม่แปล | CrisisSetup/StorySetup — verbatim | ✅ |

## 5. จุดที่ทีมปรับได้ (จดไว้ให้)

- ตัวเลข effect ทุกวิกฤต = ค่าตั้งต้นจาก guide ("ปรับ balance ได้") — แก้ที่ `CrisisSetup.cs`/`StorySetup.cs` แล้วรัน setup ซ้ำ
- ชื่อ 5 คนบนอนุสรณ์ (นอกจาก ELARA VANE) แต่งไว้ให้ก่อน — แก้ได้ที่ `Story/Memorial_veltara.asset`
- วิกฤตซ้อน 2 ใบ (น้ำ/อาหาร) guide ให้แค่คีย์ — เนื้อหาแต่งเพิ่มตามโทน แก้ได้ที่ `StorySetup.CreateAftermathCrises`
- effect ที่เกมยังไม่มีระบบรองรับถูกข้ามไว้ (จดใน comment): workersReassigned, radSickRisk, yieldPct, spoilRate, coolingWorkers +1 ของ decree
