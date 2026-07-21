# AUDIO — แผนเสียงทั้งเกม (ยังไม่ implement — รอเจ้าของเลือกเสียงก่อน)

> ★ 2026-07-22 สถานะ: **ค้างไว้ก่อน** — เจ้าของโปรเจกต์จะฟัง/คัดเสียงเอง แล้วโยนไฟล์ที่เลือกไว้โฟลเดอร์เดียว
> จากนั้นค่อยทำ AudioManager + ต่อสายเข้าระบบ (คลิก/hover ผูกอัตโนมัติทุกปุ่ม) + ทำ `CREDITS.md` บันทึกไลเซนส์
>
> เงื่อนไขลิขสิทธิ์ (สำคัญกับ NSC): ใช้เฉพาะ **CC0** (Kenney/OpenGameArt) หรือ **Pixabay Content License**
> (ฟรี เชิงพาณิชย์ได้ ไม่ต้องเครดิต) — ห้ามเสียงจาก YouTube rip / เกมอื่น / แหล่งไม่ระบุไลเซนส์

---

## แหล่งที่คัดแล้ว

### BGM (Pixabay — ฟังหน้าเว็บได้เลย)

| ที่ใช้ | อารมณ์ | ลิงก์ |
|---|---|---|
| Lobby/เมนู | มืดแต่มีหวัง | <https://pixabay.com/music/corporate-hopeful-ambient-background-180353/> · ค้น "sci-fi ambient" |
| ในเกม (วันปกติ) | ambient เบา วนลูปไม่เลี่ยน | <https://pixabay.com/music/ambient-sci-fi-ambient-music-183269/> · ค้น "sci-fi game" |
| พายุ/วิกฤต | กดดัน ต่ำ ทึบ | <https://pixabay.com/music/suspense-space-ambient-sci-fi-121842/> · <https://opengameart.org/content/dark-ambience-loop> |

หลักการเลือก BGM: 1 วันในเกม = ~1-2 นาที ผู้เล่นฟังวนเป็นชั่วโมง → เลือกเพลง**ไม่มีเมโลดี้เด่นซ้ำ**

### SFX (Kenney — CC0 โหลดทั้งแพ็ค สไตล์กลมกลืนกันทั้งเกม)

| แพ็ค | จำนวน | ใช้กับ |
|---|---|---|
| UI Audio <https://kenney.nl/assets/ui-audio> | 50 | คลิก · hover · switch |
| Interface Sounds <https://kenney.nl/assets/interface-sounds> | 100 | confirm · error · เปิด/ปิด panel · ควิซถูก/ผิด |
| Impact Sounds <https://kenney.nl/assets/impact-sounds> | 130 | วางสิ่งก่อสร้าง · โลหะกระทบ |
| Sci-Fi Sounds <https://kenney.nl/assets/sci-fi-sounds> | 70 | เตา CORE TOWER hum · อัพเลเวล · วิจัยเสร็จ · BOOST/Overdrive |

### เสียงเฉพาะธีมนิวเคลียร์ (Pixabay)

- **Geiger counter** (ติ๊กๆ วัดรังสี — พายุ/Zone B): <https://pixabay.com/sound-effects/search/geiger-counter/>
- **ไซเรน meltdown/SCRAM**: <https://pixabay.com/sound-effects/search/nuclear%20siren%20alarm/> · ค้น "sci-fi alarm"
  (มีตัวชื่อ "Nuclear emergency alarm (meltdown)" ตรงธีม)

---

## Checklist เสียงที่เกมต้องมี

- [ ] BGM เมนู/Lobby
- [ ] BGM ในเกม (วันปกติ)
- [ ] BGM/ambience พายุ-วิกฤต
- [ ] คลิก UI
- [ ] hover UI
- [ ] เปิด/ปิด panel
- [ ] แจ้งเตือนทั่วไป (Notice)
- [ ] แจ้งเตือนอันตราย (heat สูง · อาหารหมด · Hope ต่ำ)
- [ ] วางสิ่งก่อสร้าง
- [ ] อัพเลเวล / วิจัยเสร็จ
- [ ] ตอบควิซถูก
- [ ] ตอบควิซผิด
- [ ] เปิด Crisis Card
- [ ] จบวัน/ขึ้นวันใหม่
- [ ] SCRAM / ไซเรน
- [ ] Geiger (พายุ / Zone B)
- [ ] ชนะ (CORE 100)
- [ ] แพ้ (meltdown / Hope 0)

---

## ขั้นตอนตอนกลับมาทำ

1. เจ้าของโหลดเสียงที่เลือกไว้โฟลเดอร์เดียว (เช่น `Downloads\game-sounds\`) ตั้งชื่อพอเดาได้
2. ย้ายเข้า `Assets/Resources/Audio/` (BGM แยก `Audio/BGM/`)
3. สร้าง `AudioManager` (MonoBehaviour singleton ตามแบบแผนโปรเจกต์):
   - BGM crossfade ตาม state (เมนู → เกม → พายุ) — ผูก state ไม่ผูกวัน (กฎ #1)
   - SFX ผ่าน `EventManager` events ที่มีอยู่แล้ว (OnBuildingPlaced, OnQuizAnswered, OnCrisisCardShown, OnNotice, OnDayStarted, ...)
   - คลิก/hover: hook อัตโนมัติทุก `Button` ตอน spawn (ไม่ไล่ผูกทีละปุ่ม)
   - volume slider แยก BGM/SFX ใน pause menu · เซฟลง PlayerPrefs
4. ทำ `CREDITS.md` — ชื่อไฟล์ · ที่มา (URL) · ไลเซนส์ ทุกเสียง (กันคำถามกรรมการ)
