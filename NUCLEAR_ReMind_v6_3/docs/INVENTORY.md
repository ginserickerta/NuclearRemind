# INVENTORY — ระบบคลัง

**สำหรับ:** `InventoryManager` + Inventory panel · **Sprint 1** (UI) / **Sprint 4** (crafting)
**หลักการ:** Stack-based Grid · 6 แท็บ · ของคราฟต์เก็บ stock จริง

---

## 1. โครงระบบ

```
Stack-based Grid — ช่องละ 1 ไอเทม แสดง 00/999
ทรัพยากร/เชื้อเพลิง/อาหาร → stack 999  (ของเทกอง)
ของคราฟต์ (การแพทย์)      → stack 5    (ของมีค่า)
```

### ทำไมต้องมีคลัง

HUD แสดงแค่ตัวเลขรวม 6 อย่าง — คลังคือที่ที่ผู้เล่น:
1. **เห็นของคราฟต์ที่มี** (Rad Suit เหลือกี่ชุด) → ตัดสินใจส่งคนเข้า Zone B
2. **คราฟต์ล่วงหน้า** → เก็บ stock ไว้ก่อนวิกฤตมา
3. **เห็นอัตราเปลี่ยนแปลง** (+/− ต่อวัน) → วางแผนได้

---

## 2. แท็บ 6 อัน

| # | แท็บ | ไอเทม | stack |
|---|---|---|---|
| 1 | **ทั้งหมด** | ทุกอย่าง (ค่า default) | — |
| 2 | **ทรัพยากร** | Power · Water · Iron · labMat | 999 |
| 3 | **เชื้อเพลิง** | Deuterium (fuel) · Tritium | 999 |
| 4 | **อาหาร** | Food | 999 |
| 5 | **การแพทย์** | ★ Rad Suit | **5** |
| 6 | **เกษตร** | Co-60 Chamber (สถานะ) · Mutation Lab (สถานะ) | — |

> ❌ **ตัดแท็บ "ฉุกเฉิน"** — น้ำหล่อเย็นฉุกเฉินเป็น crisis action (การ์ด #1 ตัวเลือก C)
> ไม่ใช่ไอเทมที่ถือได้จริง
>
> ❌ **โล่พลาสมาไม่อยู่หมวดไหน** — เป็น win token (flag `core >= 100`) ไม่ใช่ของในคลัง

---

## 3. ไอเทมทั้งหมด (ตรงกับ GDD §13)

### แท็บ 2 · ทรัพยากร

| ไอเทม | cap | ที่มา | หมายเหตุ |
|---|---|---|---|
| **Power** | 400 | `powerWorkers × 45 − draw` | 🔒 floor 0 (บั๊ก #17) |
| **Water** | 200 | `waterWorkers × 30 − pop × 1.0` | 🔒 floor 0 (บั๊ก #18) · **ใช้ร่วมกับหล่อเย็น** |
| **Iron** | 999 | `mineWorkers × 14` | เริ่ม 200 |
| **labMat** | 999 | `+5/วัน` | ใช้คราฟต์ Rad Suit |

### แท็บ 3 · เชื้อเพลิง

| ไอเทม | cap | ที่มา | ใช้ทำอะไร |
|---|---|---|---|
| **Deuterium** (fuel) | 999 | Extractor `+6/วัน` | ดัน CORE 30→80% |
| **Tritium** | 999 | ★ Zone B `3.0` / **`8.0`** ถ้าตอบควิซ | ★ **บังคับ** ตั้งแต่ CORE 80% |

> ★ **Tritium ต้องโชว์อัตราสุทธิ** — ผลิต 3.0 − Boost กิน 9.0 = **−6.0/วัน**
> นี่คือจุดที่ผู้เล่นต้องเห็นด้วยตาว่า "ขาดทุน" → ไปหาความรู้

### แท็บ 4 · อาหาร

| ไอเทม | cap | ที่มา | หมายเหตุ |
|---|---|---|---|
| **Food** | 999 | `farmWorkers × 9 − aliveWorkers × 1` | มี spoil rate |

> **spoil** = `0.05 + (avgRad/100) × 0.15` · Co-60 → `×0.3` · Granary → `×0.6`
> ต้องโชว์ **อัตราเน่า** ในแท็บนี้ ไม่งั้นผู้เล่นไม่รู้ว่าทำไมอาหารหาย

### แท็บ 5 · การแพทย์ ★

| ไอเทม | stack | คราฟต์จาก | ผล |
|---|---|---|---|
| **Rad Suit** | **5** | `labMat 30/ชุด` · ปลดจาก `nuclear_medicine` | `rad ×0.4` · Zone B อยู่ได้ 3.5 → 8 วัน |

> ★ **เป้า 5 ชุด** = ตรงกับ `suit_cost = 30 · เป้า 5 ชุด` ใน CONFIG
> labMat +5/วัน → คราฟต์ครบ 5 ชุด ใช้ **30 วัน** ถ้าไม่มี stock เริ่ม
> เริ่มเกมมี labMat 100 → คราฟต์ได้ทันที 3 ชุด

### แท็บ 6 · เกษตร

| รายการ | ประเภท | สถานะ |
|---|---|---|
| **Co-60 Chamber** | อาคาร | สร้างแล้ว/ยัง → `spoil ×0.3` |
| **Mutation Lab** | อาคาร | สร้างแล้ว/ยัง → `farmYield +15%` (ต้องตอบ `q_mutation`) |

> ⚠ **แท็บนี้ไม่มีไอเทมถือได้** — แสดงสถานะอาคารเกษตร + อัตราเน่า/ผลผลิตปัจจุบัน
> **ถ้าทีมคิดว่าเปลืองแท็บ → ยุบรวมเข้าแท็บ "อาหาร" ได้**

---

## 4. ❌ ที่ตัดออกจาก spec เดิม

| รายการ | เหตุผล |
|---|---|
| **เมล็ดพันธุ์ฉายรังสี** | v5.2 ทำเป็น Mutation Lab bonus ถาวร (`farmYield +15%`) ไม่ใช่ไอเทม |
| **PET/SPECT** | ผูกวิกฤต 2·A ที่ถูกลบ → ความรู้ย้ายเข้า `q_nuclear_medicine` |
| **ไอโซโทปการแพทย์** | ผูกวิกฤต 2·B ที่ถูกลบ → ความรู้ย้ายเข้า note `nuclear_medicine` |
| **Rad-Gear** | เปลี่ยนชื่อ → **Rad Suit** |
| **แท็บฉุกเฉิน** | น้ำหล่อเย็นฉุกเฉิน = crisis action ไม่ใช่ไอเทม |

---

## 5. Data Structure

```csharp
public enum ItemCategory { All, Resource, Fuel, Food, Medical, Agriculture }

[CreateAssetMenu(menuName = "NRM/Item")]
public class ItemSO : ScriptableObject {
    public string itemId;           // power / water / iron / labmat / fuel / tritium / food / radsuit
    public string displayName;
    public ItemCategory category;
    public int stackSize;           // 999 หรือ 5
    public string iconName;
    public bool isCraftable;        // ★ Rad Suit = true
    public int craftCostLabMat;     // 30 (Rad Suit)
    public string unlockedByNote;   // nuclear_medicine
}

public class InventorySlot {
    public ItemSO item;
    public int count;
    public float deltaPerDay;       // ★ +/− ต่อวัน แสดงใน UI
}

public class InventoryManager : MonoBehaviour {
    // ★ ไม่เก็บ state ซ้ำ — อ่านจาก ResourceManager
    // Inventory = "หน้าต่างมอง" ไม่ใช่ source of truth
    public InventorySlot[] GetSlots(ItemCategory filter);
    public bool CanCraft(ItemSO item);
    public void Craft(ItemSO item);
}
```

> 🔴 **สำคัญ:** Inventory **ห้ามเก็บตัวเลขซ้ำ** กับ `ResourceManager`
> ถ้าเก็บ 2 ที่ → sync ไม่ตรง → บั๊กหาไม่เจอ
> Inventory เป็นแค่ **view layer** อ่านจาก `ResourceManager` + `ItemDB`

---

## 6. หน้าจอ

```
┌──────────────────────────────────────────────────┐
│  คลัง                                        [X] │
├──────────────────────────────────────────────────┤
│ [ทั้งหมด] [ทรัพยากร] [เชื้อเพลิง] [อาหาร] [แพทย์] [เกษตร] │
├──────────────────────────────────────────────────┤
│  ┌────┐ ┌────┐ ┌────┐ ┌────┐                    │
│  │ ⚡ │ │ 💧 │ │ ⛏ │ │ 🧪 │                    │
│  │ 240│ │ 90 │ │ 200│ │ 100│                    │
│  │+45 │ │−14 │ │ +14│ │ +5 │  ← delta/วัน       │
│  └────┘ └────┘ └────┘ └────┘                    │
│  Power   Water   Iron   labMat                   │
│                                                   │
│  ┌────┐                                          │
│  │ 🥼 │  Rad Suit                                │
│  │ 3/5│  [คราฟต์ −30 labMat]                     │
│  └────┘                                          │
└──────────────────────────────────────────────────┘
```

### ต้องแสดง

- **จำนวน / cap** — เช่น `240/400` (Power) · `3/5` (Rad Suit)
- ★ **delta/วัน** — `+45` เขียว · `−14` แดง → ผู้เล่นเห็นแนวโน้ม
- **ปุ่มคราฟต์** — เฉพาะของที่ `isCraftable` · เทาถ้า labMat ไม่พอ หรือยังไม่วิจัย
- **ล็อก 🔒** — ของที่ยังไม่ปลดวิจัย โชว์ไอคอนเทา + "ต้องวิจัย [note]"
  (หลักการเดียวกับตัวเลือกล็อกในการ์ด)

### ⚠ Tritium ต้องเด่น

```
┌────┐
│ ⚛  │  Tritium
│  12│  ★ −6.0/วัน  ← แดงกระพริบ
└────┘     ผลิต 3.0 · Boost กิน 9.0
```

นี่คือ **จุดสอนของเกมทั้งเกม** — ผู้เล่นต้องเห็นตัวเลขติดลบด้วยตาตัวเอง
แล้วถึงจะไปหาว่า "ทำไงให้ผลิตมากขึ้น" → เจอ `q_tritium_breeding` → ชนะ

---

## 7. ระบบคราฟต์ (Rad Suit)

```csharp
public bool CanCraft(ItemSO item) {
    if (!MasteryRegistry.HasNote(item.unlockedByNote)) return false;  // ยังไม่วิจัย
    if (resources.labMat < item.craftCostLabMat)       return false;  // ของไม่พอ
    if (GetCount(item) >= item.stackSize)              return false;  // เต็ม stack
    return true;
}

public void Craft(ItemSO item) {
    if (!CanCraft(item)) return;
    resources.labMat -= item.craftCostLabMat;
    AddItem(item, 1);
    // ★ ไม่ใช้เวลา ไม่ใช้คน — คราฟต์ทันที
}
```

> ★ **แบบ B (ตามที่ทีมเคาะ):** คราฟต์ล่วงหน้าเก็บ stock ได้จริง
> **ไม่ใช่แบบ A** (หักค่าตอนวิกฤต)
>
> **ผลกระทบ 3 จุด:**
> 1. **UI คราฟต์** — ต้องมีปุ่มคราฟต์ในคลัง
> 2. **Logic วิกฤต** — การ์ด #6 (Zone B) ต้องเช็ค **stock** ไม่ใช่หัก cost
> 3. **สมดุล** — ⚠ ต้อง playtest ใหม่ (ดูข้อ 9)

### การ์ดที่เช็ค stock

| การ์ด | เช็คอะไร | ผล |
|---|---|---|
| **#6 Zone B หมดคน** | `suitsMade >= zoneBWorkers` | มีชุดครบ → คนอยู่ได้ 8 วัน · ไม่ครบ → 3.5 วัน |

### Bark ที่เกี่ยวข้อง

```
M07  zoneB.isOpen && suitsMade < zoneBWorkers
     "คนใน Zone B ยังไม่มีชุดกันรังสีครบ"
```

---

## 8. Hope ที่เกี่ยวข้อง

| sourceKey | เงื่อนไข | ค่า |
|---|---|---|
| `alara.compliant` | ★ Zone B ชุดครบ | **+2** |

> คราฟต์ชุดครบ = ทำตามหลัก ALARA = Hope ขึ้น
> เชื่อมความรู้ (`q_alara`) เข้ากับกลไกจริง

---

## 9. ⚠ ที่ยังต้องเช็ค

| # | ปัญหา | สถานะ |
|---|---|---|
| 1 | **ระบบคราฟต์แบบ B ยังไม่ผ่าน sim** — sim รันโดยสมมติว่ามีชุดพอเสมอ | ⏳ ต้องรัน sim ใหม่ |
| 2 | **labMat +5/วัน พอมั้ย** — คราฟต์ 5 ชุด = 150 labMat = 30 วัน (เริ่มมี 100 → ได้ 3 ชุดทันที) | ⏳ ต้อง playtest |
| 3 | **แท็บเกษตรอาจเปลือง** — มีแค่สถานะอาคาร 2 อัน | 💡 พิจารณายุบเข้าแท็บอาหาร |

---

## 10. งานที่ต้องทำ

- [ ] สร้าง `ItemSO` 8 ตัว (power, water, iron, labmat, fuel, tritium, food, radsuit)
- [ ] `InventoryManager` — **view layer** อ่านจาก `ResourceManager` ห้ามเก็บ state ซ้ำ
- [ ] คำนวณ `deltaPerDay` จากสูตร Economy (`CONFIG.md`)
- [ ] Inventory panel — grid + 6 แท็บ + ปุ่มคราฟต์
- [ ] `RadSuitManager` — ต่อกับการ์ด #6 + Bark M07 + Hope `alara.compliant`
- [ ] ★ Tritium delta แดงกระพริบเมื่อติดลบ

**ประมาณ 1–1.5 วันโปรแกรมเมอร์**
