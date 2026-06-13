# Nuclear Re:Mind — CLAUDE.md
# NSC 2026 (ครั้งที่ 28) | หมวดโปรแกรมส่งเสริมทักษะเพื่อการเรียนรู้

---

## 🎯 ภาพรวมโปรเจกต์

**ชื่อ:** นิวเคลียร์เปลี่ยนความคิดโลก / Nuclear Re:Mind
**ประเภท:** Simulation-Based Learning Game บน Windows PC
**เป้าหมาย:** เปลี่ยนภาพลบของพลังงานนิวเคลียร์ผ่านการเรียนรู้โดยตรง (Learning by Doing)
**กลุ่มเป้าหมาย:** นักเรียน ม.ปลาย – นักศึกษามหาวิทยาลัย

**ข้อมูลโครงการเต็ม:** `Assets/Docs/NSC_info.md`
**แผนพัฒนา 30 วัน:** `Assets/Docs/nuclear_remind_30day_plan.csv`

---

## 👥 ทีม

| บทบาท | ชื่อ |
|---|---|
| หัวหน้าทีม / Lead Dev | นายธนาคิม โจว |
| Developer / Art | น.ส.นาราภัทร ชุ่มปลั่ง |
| Developer | นายกรินภูสิษฐ์ คงทองสิทธิโชค |
| ที่ปรึกษา | น.ส.ภานุชนารถ เลิศศรีเพ็ชร |
| โรงเรียน | ศึกษานารีวิทยา สพม.กท.1 |

> โปรเจกต์นี้เป็น Upgrade & Expansion จาก Nuclear Re:Mind prototype (รางวัล Mythsmasher 2569)
> ยกระดับกราฟิกเป็น Isometric, เพิ่ม Simulation Grid, เขียน Unity logic ที่ซับซ้อนขึ้น

---

## 🛠️ Tech Stack

| เครื่องมือ | เวอร์ชัน | หน้าที่ |
|---|---|---|
| Unity | 6000.3.6f1 (Unity 6) | Game Engine หลัก |
| C# | .NET Standard 2.1 | ภาษาโปรแกรม |
| Visual Studio | 2022 | IDE |
| Git / GitHub | latest | Version Control |
| iPad | — | วาด / ออกแบบ Assets |

**Platform output:** Windows Standalone (.exe)
**Rendering:** Universal Render Pipeline (URP) + URP 2D Renderer (Light 2D)
**Input System:** Legacy Input System (Input.GetMouseButton / Input.GetKey)

---

## 📁 โครงสร้างโฟลเดอร์

```
NuclearReMind/
├── CLAUDE.md                          ← ไฟล์นี้
├── Assets/
│   ├── Docs/
│   │   ├── NSC_info.md                ← spec โปรเจกต์เต็ม
│   │   └── nuclear_remind_30day_plan.csv
│   ├── Scripts/
│   │   ├── Core/                      ← GameManager, EventManager
│   │   ├── Grid/                      ← GridManager, Cell
│   │   ├── Building/                  ← PlacementController, BuildingData
│   │   ├── Resource/                  ← ResourceManager, ResourceData
│   │   ├── Population/                ← PopulationManager
│   │   ├── Tower/                     ← CoreTowerManager
│   │   ├── UI/                        ← UIManagerHUD, TooltipController
│   │   ├── Codex/                     ← CodexManager, CodexEntry
│   │   ├── Dilemma/                   ← MoralDilemmaManager, DilemmaData
│   │   ├── Exploration/               ← ExplorationManager
│   │   ├── Narrative/                 ← NarrativeManager
│   │   └── Save/                      ← SaveManager
│   ├── ScriptableObjects/
│   ├── Scenes/
│   │   ├── MainMenu.unity
│   │   └── GameScene.unity
│   ├── Sprites/
│   ├── Audio/
│   ├── Prefabs/
│   └── UI/
├── Packages/
└── ProjectSettings/
```

---

## 🎮 ระบบในเกม (Game Systems)

### 1. Isometric Grid (12×9)
- `GridManager.cs` จัดการ grid ขนาด 12 คอลัมน์ × 9 แถว
- method หลัก: `IsoToWorld(int col, int row)` และ `WorldToIso(Vector3 worldPos)`
- แต่ละ cell เก็บ: `bool isOccupied`, `BuildingType buildingType`, `int radiationLevel`

### 2. Resource Management
ติดตามค่าต่อไปนี้แบบ real-time ผ่าน `Tick()` ทุก game turn:
- `Food` — อาหารเลี้ยงประชากร
- `Water` — น้ำสำหรับระบบ coolant + ประชากร
- `RadiationProtection` — อุปกรณ์กันรังสีสำหรับทีม exploration
- `Energy` — พลังงานขับเคลื่อนสิ่งก่อสร้าง
- `Workers` — จำนวนแรงงานที่ใช้งานได้

### 3. Population & Trust System
- `PopulationManager.cs` ดูแล Trust (0–100), Morale, Health
- Trust ลดเมื่อ: ทรัพยากรขาด, เกิด crisis, ตัดสินใจผิดพลาด
- Trust ต่ำกว่า 20 → worker strike event → ชะลอการก่อสร้าง

### 4. CORE TOWER — Phased Construction (3 ระยะ)
```
Phase 1 (0–30%)  → Foundation: ใช้ Materials + Energy
Phase 2 (30–70%) → Reactor Core: ใช้ Materials + RadiationProtection + Workers
Phase 3 (70–100%)→ Fusion Activation: ใช้ Energy + Workers + Trust > 50
```
- เชื่อมกับ win condition: สร้างสำเร็จ = โลกได้รับพลังงาน

### 5. Moral Dilemma System
- `MoralDilemmaManager.cs` trigger popup เมื่อถึง event
- แต่ละ dilemma มี 2 ตัวเลือก: เร่งสร้าง vs. ดูแลความปลอดภัย
- ผลกระทบต่อ Trust, Resources, และ Codex unlock

### 6. Exploration System
- `ExplorationManager.cs` จัดการ 6 zones นอกเมือง Veltara
- แต่ละ zone มี `radiationLevel` และ `resourceReward` ต่างกัน
- ต้องใช้ RadiationProtection เพียงพอก่อนส่งทีม

### 7. Learning Codex (Tech Tree)
สาขาความรู้ 3 ด้านที่ unlock ด้วย Research Points:
- **Agriculture:** การปรับปรุงพันธุ์พืช, การฉายรังสีอาหาร, ตรวจคุณภาพผลผลิต
- **Medical:** PET Scan, Scintigraphy, การรักษามะเร็งด้วยรังสี
- **Environment:** ตรวจการปนเปื้อน, จัดการกากนิวเคลียร์, Nuclear vs Fossil

### 8. Info Tooltip (3 ชั้น)
เมื่อ hover อาคาร:
1. ชั้น 1: ชื่อ + ค่าใช้จ่ายทรัพยากร
2. ชั้น 2: การทำงานในเกม
3. ชั้น 3: ความรู้วิทยาศาสตร์จริง เช่น "Plasma Confinement คืออะไร"

---

## 🎬 เนื้อเรื่อง (Narrative Context)

**ปี 2157:** ฟอสซิลหมด → วิกฤตพลังงานโลก
**Aethon** (ตะวันตก) + **Keran** (ตะวันออก) สร้าง CORE TOWER (Fusion Reactor)
แต่โลภ → เดินเครื่องเกิน → ระเบิดทั่วโลก

**ผู้เล่น:** Dr. Auren Vasek, Nuclear Engineer, อายุ 35
- รอดคนเดียวจากทีม 6 คน (อยู่นอกเขตตอนเกิดเหตุ)
- รายงานเตือนที่ส่งตามขั้นตอนปกติ แทนการแจ้งตรง → ทีมเสียชีวิต
- แบกความผิดพลาดนั้นไว้

**ภารกิจ:** เมืองร้าง Veltara — CORE TOWER ค้างอยู่ที่ 30%
→ สร้างให้เสร็จก่อนทรัพยากรหมด

**3 endings:**
- ✅ สร้างสำเร็จ = โลกได้รับพลังงาน
- ❌ ทรัพยากรหมด = ล้มเหลว
- 🔄 เริ่มใหม่ = วางแผนยุทธศาสตร์ใหม่

---

## 🗺️ 30-Day Development Milestones

| Phase | วัน | เนื้อหา | Milestone |
|---|---|---|---|
| Phase 1: รากฐาน | Day 1–7 | Setup, Grid, Camera, Tilemap, Placement, EventManager | Day 5, Day 7 |
| Phase 2: Core Systems | Day 8–14 | Resource, Population, CORE TOWER, HUD, Tooltip, Dilemma | Day 10, Day 14 |
| Phase 3: Content | Day 15–21 | Codex, Agriculture, Medical, Environment, Exploration, Story, Endings | Day 21 |
| Phase 4: Polish | Day 22–30 | Art, UI, Sound, Save, Playtesting ×2, Bug Fix, Final Build | Day 30 |

**ดู task รายวันละเอียด:** `Assets/Docs/nuclear_remind_30day_plan.csv`

---

## ⌨️ Controls (Input/Output Spec)

| Input | Action |
|---|---|
| คลิกซ้าย | เลือก / ยืนยันวางสิ่งก่อสร้าง / กดปุ่ม |
| คลิกขวา | ยกเลิกคำสั่ง |
| เลื่อนเมาส์ | แสดง Tooltip 3 ชั้น |
| Space | Pause / Resume |
| Esc | เมนูหยุดเกม |
| WASD | เลื่อนกล้อง |
| Scroll | Zoom in/out |

---

## 💻 System Requirements

| ส่วนประกอบ | Minimum |
|---|---|
| OS | Windows 10 (64-bit) |
| CPU | Intel Core i3 |
| RAM | 4 GB |
| GPU | DirectX 11 |
| Storage | 500 MB |
| Resolution | 1280×720 |

---

## 📐 Coding Conventions

- **ภาษา comments:** ภาษาไทยได้ เช่น `// ตรวจสอบว่า cell ว่างหรือไม่`
- **XML doc:** ทุก public method ต้องมี `/// <summary>`
- **ห้าม:** `FindObjectOfType<>()` ใน `Update()` — ใช้ cache ใน `Awake()` แทน
- **EventManager:** ส่ง event ผ่าน `EventManager` เสมอ ไม่ direct reference ข้าม Manager
- **ScriptableObject:** ข้อมูลที่ปรับได้ (building stats, codex entries) ต้องเป็น SO
- **Namespace:** `NuclearReMind` ทุก class
- **Singleton:** GameManager, ResourceManager, PopulationManager ใช้ pattern `Instance`

### ตัวอย่าง Singleton Pattern ที่ใช้ในโปรเจกต์นี้:
```csharp
namespace NuclearReMind
{
    /// <summary>
    /// จัดการสถานะหลักของเกม
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
```

---

## 🚦 วิธีใช้งานกับ Claude Code

เมื่อรัน `claude` ใน root ของโปรเจกต์ Claude จะอ่านไฟล์นี้อัตโนมัติ
ตัวอย่างคำสั่งที่ใช้บ่อย:

```
# ถามสถานะโปรเจกต์
> อ่าน 30day_plan.csv แล้วบอกว่าวันนี้ Day ไหน และต้องทำอะไร

# สร้าง script ใหม่
> สร้าง GridManager.cs ตาม spec ใน CLAUDE.md (Isometric 12×9, IsoToWorld, WorldToIso)

# debug
> ดู ResourceManager.cs แล้วหาว่าทำไม Food ไม่ลดตาม Tick()

# review
> ตรวจ PlacementController.cs ว่าใช้ EventManager ถูกต้องตาม convention ไหม
```
