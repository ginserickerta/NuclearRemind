# Nuclear Re:Mind — NSC 2026 Project Proposal

## ข้อมูลโครงการ
- **ชื่อ:** นิวเคลียร์เปลี่ยนความคิดโลก / Nuclear Re:Mind
- **การแข่งขัน:** NSC 2026 (ครั้งที่ 28) หมวดโปรแกรมส่งเสริมทักษะเพื่อการเรียนรู้
- **ทีม:** นายธนาคิม โจว (หัวหน้า), น.ส.นาราภัทร ชุ่มปลั่ง, นายกรินภูสิษฐ์ คงทองสิทธิโชค
- **ที่ปรึกษา:** น.ส.ภานุชนารถ เลิศศรีเพ็ชร
- **โรงเรียน:** ศึกษานารีวิทยา สพม.กท.1

---

## 1. สาระสำคัญ
Simulation-Based Learning Game บน PC เปลี่ยนภาพลบของพลังงานนิวเคลียร์ -> การเรียนรู้ผ่านประสบการณ์จริง
- กลุ่มเป้าหมาย: ม.ปลาย – มหาวิทยาลัย
- Keywords: Simulation-Based Learning / Systems Thinking / Nuclear Safety / Sustainable Energy / Learning by Doing

---

## 2. ปัญหาที่แก้
1. Nuclear Phobia + ภาพจำลบจากภัยพิบัติ/อาวุธ
2. การเรียนนิวเคลียร์เน้นสูตรคำนวณ → นามธรรม ไม่เห็นภาพรวม
3. ขาดพื้นที่ฝึก Systems Thinking เชื่อมโยงพลังงาน-เศรษฐกิจ-สังคม-สิ่งแวดล้อม

---

## 3. วัตถุประสงค์
1. พัฒนาเกมสอนหลักการนิวเคลียร์ + กระบวนการผลิตพลังงาน + มาตรฐานความปลอดภัย
2. บูรณาการ Simulation + Learning Codex -> ปรับกระบวนการเรียนรู้ผ่านประสบการณ์ตรง
3. พัฒนา Systems Thinking + Critical Thinking ผ่าน Energy Dashboard
4. ฝึก Ethical Decision Making + ประเมินผลกระทบเทคโนโลยีต่อสิ่งแวดล้อม/สังคม
5. แก้ปัญหาเนื้อหาซับซ้อน/นามธรรมในห้องเรียน -> เรียนรู้เชิงโครงสร้าง real-time

---

## 4. เป้าหมายและขอบเขต

### 4.1 เป้าหมาย
| ประเภท | รายละเอียด |
|---|---|
| Output | เกม PC ครบ 4 Phase + Learning Codex พร้อมใช้งาน |
| Outcome | ผู้เล่นมีคะแนน Systems Thinking + Ethical Decision Making เพิ่มอย่างมีนัยสำคัญ |
| Impact | สื่อการเรียนรู้ต้นแบบสำหรับรายวิชาวิทยาศาสตร์/พลังงาน/สิ่งแวดล้อม |

### 4.2 แพลตฟอร์ม + Development
- Platform: Windows PC
- Engine: Unity (6000.3.6f1 / Unity 6), Language: C#
- Database: ระบบ Codex ภายในเกม

### 4.3 ขอบเขตเนื้อหา
- **นิวเคลียร์:** Fission, Chain Reaction, Half-life, Coolant System
- **ความยั่งยืน/จริยธรรม:** Meltdown prevention, Risk communication, Resource management

### 4.4 ระบบในเกม
| ระบบ | รายละเอียด |
|---|---|
| Dashboard | Real-time: MW output, Core Temp, Coolant Level, Safety Index |
| เนื้อเรื่อง | 4 สถานการณ์หลัก, ตัวเลือกการตัดสินใจ, จบ 3 รูปแบบ |
| Learning Codex | สารานุกรมในเกม unlock ตามความคืบหน้า |
| Quiz | แบบทดสอบท้ายแต่ละ Phase |

---

## 5. เนื้อเรื่อง (Storyboard)
- **ปี 2157:** การใช้เชื้อเพลิงฟอสซิลเกินขีดจำกัด -> วิกฤต
- Aethon (ตะวันตก) + Keran (ตะวันออก) พัฒนา CORE TOWER (Fusion Reactor) แต่โลภเดินเครื่องเกิน -> ระเบิดทั่วโลก
- **ผู้เล่น:** Dr. Auren Vasek, Nuclear Engineer, อายุ 35, รอดคนเดียวจาก 6 คน (อยู่นอกเขตตอนเกิดเหตุ แต่รายงานเตือนล่วงหน้าที่ส่งตามขั้นตอนปกติแทนการแจ้งตรง -> ทีมเสียชีวิต)
- **ภารกิจ:** เมืองร้าง Veltara, ก่อสร้าง CORE TOWER ค้างอยู่ที่ 30% -> สร้างให้เสร็จก่อนทรัพยากรหมด
- **3 ระบบหลัก:** ทรัพยากร (วัสดุ/พลังงาน/อาหาร/น้ำ/อุปกรณ์กันรังสี) | ประชากร (การผลิตอาหาร/การรักษา/Morale) | การก่อสร้าง (วางแผน/พัฒนาเทคโนโลยี/ความปลอดภัย)
- **จบ:** สร้างสำเร็จ = โลกได้รับพลังงาน | ทรัพยากรหมด = ล้มเหลว | เริ่มใหม่ = วางแผนยุทธศาสตร์ใหม่

---

## 6. เทคนิคและระบบ

### 6.1 Tech Stack
| เครื่องมือ | ประเภท | หน้าที่ |
|---|---|---|
| Unity 6000.3.6f1 (Unity 6) | Game Engine | ฉาก/ฟิสิกส์/กราฟิก Isometric |
| Visual Studio 2022 | IDE | เขียน/แก้ C# |
| Git/GitHub | Version Control | จัดการโค้ด ทำงานร่วมกัน |
| iPad | Hardware | วาด/ออกแบบ Assets |

### 6.2 ระบบหลักในเกม
1. **Isometric Grid (12×9):** วางสิ่งก่อสร้าง -> ฝึก Systems Thinking เชิงพื้นที่
2. **Resource Management:** ติดตาม Food/Water/Radiation Protection/Energy/Worker แบบ real-time
3. **Phased Construction (3 ระยะ):** แต่ละระยะมี Req.ทรัพยากร + ความรู้ Fusion Reactor
4. **Population & Trust System:** Trust ต่ำ -> หยุดงาน/วุ่นวาย -> สะท้อนการยอมรับโครงการพลังงานจริง
5. **Moral Dilemma System:** เลือกระหว่างเร่งสร้าง vs. ดูแลความปลอดภัย
6. **Exploration System:** ส่งทีมหาทรัพยากรนอกเมือง, แต่ละพื้นที่มีระดับรังสีต่างกัน -> เรียนรู้การป้องกันรังสี

### 6.3 Nuclear Knowledge Tech Tree
| สาขา | เนื้อหา |
|---|---|
| Agriculture | การปรับปรุงพันธุ์พืช, การฉายรังสีอาหาร, ตรวจคุณภาพผลผลิต |
| Medical | PET Scan, Scintigraphy, การรักษามะเร็งด้วยรังสี |
| Environment | ตรวจการปนเปื้อน, จัดการกากนิวเคลียร์, เปรียบเทียบผลกระทบ Nuclear vs Fossil |
- Req. ปลดล็อก: Research Points (จากการสร้างห้องปฏิบัติการ)

### 6.4 UI / Screen Design
- Unity Canvas system รองรับหลายขนาดหน้าจอ
- แบ่งพื้นที่: แผนที่ Isometric | แถบทรัพยากร/สถานะ | เมนูคำสั่ง
- **Info Tooltip (3 ชั้น):** (1) ชื่อ+ค่าใช้จ่าย (2) การทำงานในเกม (3) ความรู้จริง เช่น Reactor Core Tooltip -> Plasma Confinement

### 6.5 Software Architecture

### 6.6 Learning Objectives
| ด้าน | ผลลัพธ์ที่คาดหวัง |
|---|---|
| ความรู้ | อธิบายหลักการ Fission, เปรียบเทียบ Nuclear vs Fossil ผลกระทบสิ่งแวดล้อม |
| ความเข้าใจ | อธิบายผลกระทบรังสีต่อสิ่งมีชีวิต, การใช้นิวเคลียร์ใน Agriculture/Medical |
| การประยุกต์ | วิเคราะห์/ตัดสินใจจัดสรรทรัพยากรจำกัดภายใต้ความกดดัน |
| ทัศนคติ | ตระหนักความสำคัญ Energy Policy และผลกระทบต่อวงกว้าง |

---

## 7. Input/Output

| ประเภท | รูปแบบ | คำอธิบาย |
|---|---|---|
| Input | คลิกซ้าย | เลือก/ยืนยันวางสิ่งก่อสร้าง/กดปุ่ม |
| Input | คลิกขวา | ยกเลิกคำสั่ง |
| Input | เลื่อนเมาส์ | แสดง Tooltip |
| Input | Space | Pause/Resume |
| Input | Esc | เมนูหยุดเกม |
| Output | แผนที่ Isometric | เมือง Veltara + สิ่งก่อสร้าง + สถานะ |
| Output | HUD | ทรัพยากร/ความคืบหน้า CORE TOWER/ประชากร real-time |
| Output | Popup | เหตุการณ์/สถานการณ์ตัดสินใจ |
| Output | หน้าสรุป | ผลลัพธ์ + Knowledge Summary ท้ายรอบ |

---

## 8. System Requirements

| ส่วนประกอบ | Minimum | Recommend |
|---|---|---|
| OS | Windows 10 (64-bit) | Windows 10/11 (64-bit) |
| CPU | Intel Core i3 | Intel Core i5 / AMD Ryzen 5+ |
| RAM | 4 GB | 8 GB+ |
| GPU | DirectX 11 | Discrete GPU (NVIDIA/AMD) |
| Storage | 500 MB | 1 GB+ |
| Resolution | 1280×720 | 1920×1080+ |
| Output Format | Windows Standalone (.exe) | Windows Standalone (.exe) |

---

## 9. ประวัติผู้พัฒนา (รางวัลที่เกี่ยวข้อง)

### นายธนาคิม โจว
- รองชนะเลิศ อันดับ 2 ระดับประเทศ (สื่อ/เกม) — Mythsmasher 2569, สมาคมนิวเคลียร์แห่งประเทศไทย (ผลงาน: Nuclear Re:Mind prototype)
- เหรียญทอง — Science News Communicator 2025, มจธ. (ม.ปลาย)
- เหรียญเงิน — Arduino Education Day Thailand 2025 + ฟิสิกส์สัประยุทธ์

### น.ส.นาราภัทร ชุ่มปลั่ง
- รองชนะเลิศ อันดับ 2 ระดับประเทศ (สื่อ/เกม) — Mythsmasher 2569
- ชนะเลิศ อันดับ 1 + Special Award — IS Competition ม.5, ศึกษานารีวิทยา 2569
- เหรียญทอง — Science News Communicator 2025, มจธ.
- เหรียญทองแดง — TESET 2569 (TAC + LITU มธ.)
- เหรียญเงิน — Arduino Education Day Thailand 2025 + ฟิสิกส์สัประยุทธ์

### นายกรินภูสิษฐ์ คงทองสิทธิโชค
- รองชนะเลิศ อันดับ 2 ระดับประเทศ (สื่อ/เกม) — Mythsmasher 2569
- เหรียญทอง ประเภทสิ่งประดิษฐ์และนวัตกรรม — SWU Research Day 2025, มศว

> **หมายเหตุทีม:** NSC 2026 นี้เป็นการ Upgrade & Expansion จาก Nuclear Re:Mind prototype (Mythsmasher) โดยยกระดับกราฟิกเป็น Isometric, เพิ่ม Simulation Grid, และเขียนตรรกะ Unity Engine ที่ซับซ้อนขึ้น