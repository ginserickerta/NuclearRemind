# CLAUDE.md — Nuclear Re:Mind

> อ่านไฟล์นี้ก่อนทุกครั้ง ก่อนเขียนโค้ดใดๆ

---

## โปรเจกต์คืออะไร

**Nuclear Re:Mind** — Isometric Survival City Builder สำหรับ NSC 2026  
Engine: 6000.3.6f1 (Unity 6) | ภาษา: C# | Platform: Windows PC Standalone  
ผู้เล่นรับบท Dr. Auren Vasek สร้าง CORE TOWER ใน Veltara ให้เสร็จก่อนทรัพยากรหมด

---

## Folder Structure

```
Assets/
├── Scripts/
│   ├── Managers/          ← Singleton managers ทั้งหมด
│   ├── Systems/           ← Grid, Placement, Exploration
│   ├── UI/                ← HUD, Tooltip, Popup, Codex
│   ├── Data/              ← ScriptableObject definitions
│   ├── Narrative/         ← Story, Dilemma, Relationship
│   └── Utils/             ← Extension methods, helpers
├── ScriptableObjects/
│   ├── Buildings/
│   ├── CodexEntries/
│   └── Dilemmas/
├── Prefabs/
├── Sprites/
│   ├── Buildings/
│   ├── UI/
│   └── Characters/
├── Tilemaps/
├── Scenes/
│   ├── MainMenu.unity
│   └── GameScene.unity
└── Audio/
    ├── BGM/
    └── SFX/
```

---

## Naming Conventions

| ประเภท | รูปแบบ | ตัวอย่าง |
|--------|--------|---------|
| Class | PascalCase | `ResourceManager` |
| Method | PascalCase | `GetCurrentFood()` |
| Private field | _camelCase | `_currentFood` |
| Public property | PascalCase | `CurrentFood` |
| Event (C# Action) | On + PascalCase | `OnFoodChanged` |
| ScriptableObject asset | PascalCase + type | `FoodFarm_BuildingData` |
| Codex entry asset | Branch_Index_Name | `Agri_01_FoodIrradiation` |
| Coroutine | PascalCase + Coroutine | `SpawnEventCoroutine()` |

---

## Architecture Rules

### Singleton Pattern
ใช้กับ Manager ทุกตัว — ห้ามเรียก Manager โดยตรงจาก MonoBehaviour อื่น  
ให้ subscribe ผ่าน EventManager เท่านั้น

```csharp
// ✅ ถูก
EventManager.Instance.OnBuildingPlaced += HandleBuildingPlaced;

// ❌ ผิด — อย่าเรียกตรง
ResourceManager.Instance.AddFood(50);
```

### EventManager — Events ทั้งหมดในระบบ

```csharp
// Building
public event Action<BuildingData> OnBuildingPlaced;
public event Action<Vector2Int> OnBuildingRemoved;

// Resources
public event Action<ResourceData> OnResourceChanged;
public event Action<ResourceType> OnResourceCritical;   // < 20%
public event Action<ResourceType> OnResourceDepleted;   // = 0

// Population
public event Action<float> OnTrustChanged;              // 0-100
public event Action OnWorkerStrike;
public event Action OnRiotStarted;

// CORE TOWER
public event Action<int> OnTowerPhaseComplete;          // phase 1,2,3
public event Action OnTowerComplete;

// Game State
public event Action<GameEndType> OnGameOver;            // Win/ResourceDepleted/TrustCollapsed
public event Action<DilemmaData> OnDilemmaTriggered;
public event Action<string> OnCodexEntryUnlocked;       // entry ID
public event Action<NarrativeEvent> OnNarrativeEvent;
```

### Data Model — ทุก struct ต้อง Serializable ตั้งแต่แรก

```csharp
[System.Serializable]
public struct ResourceData {
    public float food;
    public float water;
    public float radiationProtection;
    public float energy;
    public int workers;
}

[System.Serializable]
public struct PopulationData {
    public int total;
    public float trust;       // 0-100
    public bool isOnStrike;
}

[System.Serializable]
public struct TowerData {
    public int currentPhase;  // 0,1,2,3
    public float phaseProgress; // 0-1
}

[System.Serializable]
public class SaveData {
    public ResourceData resources;
    public PopulationData population;
    public TowerData tower;
    public List<Vector2Int> placedBuildings;
    public List<string> buildingTypes;
    public List<string> unlockedCodexEntries;
    public float gameTime;
    public int aethonRelationship;  // -100 to 100
    public int keranRelationship;   // -100 to 100
}
```

**ห้ามเพิ่ม field ใหม่ใน SaveData โดยไม่เพิ่ม default value — จะทำให้ save เก่าพัง**

---

## ScriptableObject Definitions

### BuildingData

```csharp
[CreateAssetMenu(fileName = "NewBuilding", menuName = "NuclearReMind/Building")]
public class BuildingData : ScriptableObject {
    public string buildingName;
    public string description;        // ชั้น 2 tooltip: การทำงานในเกม
    public string nuclearKnowledge;   // ชั้น 3 tooltip: ความรู้จริง
    public Sprite sprite;
    public Vector2Int size;           // grid cells

    [Header("Cost")]
    public int materialCost;
    public int energyCost;
    public int workerRequired;

    [Header("Production per tick")]
    public float foodProduction;
    public float waterProduction;
    public float radiationProtectionBonus;
    public float energyProduction;
    public int researchPointsPerTick;

    [Header("Tower")]
    public bool isCoreTowerPart;
    public int towerPhaseRequired;    // 0 = ทุก phase
}
```

### CodexEntry

```csharp
[CreateAssetMenu(fileName = "NewCodexEntry", menuName = "NuclearReMind/CodexEntry")]
public class CodexEntry : ScriptableObject {
    public string entryId;            // เช่น "Agri_01"
    public string title;
    public string branch;             // "Agriculture" | "Medical" | "Environment" | "Core"
    [TextArea(5, 20)]
    public string content;            // ภาษาไทย เขียนให้มัธยมอ่านได้
    public Sprite illustration;
    public int researchPointCost;     // 0 = unlock อัตโนมัติตาม story
    public string unlockedByEvent;    // event ID ที่ trigger unlock
}
```

### DilemmaData

```csharp
[CreateAssetMenu(fileName = "NewDilemma", menuName = "NuclearReMind/Dilemma")]
public class DilemmaData : ScriptableObject {
    public string dilemmaId;
    [TextArea(3, 10)]
    public string scenarioText;
    public string choiceAText;
    public string choiceBText;

    [Header("Choice A Consequences")]
    public float choiceA_FoodChange;
    public float choiceA_TrustChange;
    public int choiceA_AethonRelationChange;
    public int choiceA_KeranRelationChange;

    [Header("Choice B Consequences")]
    public float choiceB_FoodChange;
    public float choiceB_TrustChange;
    public int choiceB_AethonRelationChange;
    public int choiceB_KeranRelationChange;

    public string triggerCondition;   // "phase_1_complete" | "trust_below_40" | etc.
}
```

---

## Grid System

- Grid size: **20 × 12** cells (ห้ามเปลี่ยน — เป็น design constraint)
- Isometric angle: 45°
- Cell size: 64px × 32px (isometric diamond)
- Origin: bottom-left = (0,0)

```csharp
// Conversion formulas — ห้ามแก้
public Vector3 IsoToWorld(int x, int y) {
    return new Vector3((x - y) * cellWidth / 2f, (x + y) * cellHeight / 2f, 0);
}
public Vector2Int WorldToIso(Vector3 worldPos) {
    float x = (worldPos.x / (cellWidth / 2f) + worldPos.y / (cellHeight / 2f)) / 2f;
    float y = (worldPos.y / (cellHeight / 2f) - worldPos.x / (cellWidth / 2f)) / 2f;
    return new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(y));
}
```

---

## Resource System

### Tick Rate
- Resource tick: ทุก **5 วินาที** (game time)
- Population demand check: ทุก **10 วินาที**
- Crisis check: ทุก **tick** — ถ้า resource ใดถึง 0 → fire `OnResourceDepleted`

### Starting Values (วันที่ 1 ของเกม)
```
food: 100 / 500 max
water: 80 / 400 max  
radiationProtection: 50 / 200 max
energy: 30 / 300 max
workers: 20 / 100 max
trust: 70 / 100
population: 50
```

### CORE TOWER Requirements
| Phase | Materials | Energy | Workers | Time (ticks) |
|-------|-----------|--------|---------|--------------|
| 1 (30%→65%) | 200 | 150 | 15 | 30 |
| 2 (65%→90%) | 350 | 300 | 25 | 50 |
| 3 (90%→100%) | 500 | 500 | 40 | 80 |

---

## Code Style

```csharp
// ✅ ถูก — ใช้ properties ไม่ใช่ public fields
public float CurrentFood { get; private set; }

// ❌ ผิด
public float currentFood;

// ✅ ถูก — null check ก่อนเรียก event
OnFoodChanged?.Invoke(CurrentFood);

// ✅ ถูก — ใช้ [Header] จัด Inspector
[Header("Runtime Values")]
[SerializeField] private float _currentFood;

// ❌ ผิด — อย่าใช้ FindObjectOfType ใน Update
void Update() {
    var rm = FindObjectOfType<ResourceManager>(); // ❌ แพง
}
```

---

## สิ่งที่ Claude Code ห้ามทำเด็ดขาด

1. **ห้ามแก้ IsoToWorld() / WorldToIso()** — formula นี้ผ่านการทดสอบแล้ว แก้แล้วทั้งระบบพัง
2. **ห้ามเพิ่ม field ใน SaveData โดยไม่ระบุ default** — save เก่าจะ deserialize ไม่ได้
3. **ห้าม hardcode ค่า resource** — ค่าทั้งหมดต้องมาจาก BuildingData ScriptableObject
4. **ห้ามเรียก Manager โดยตรงข้าม system** — ใช้ EventManager เท่านั้น
5. **ห้ามใช้ GameObject.Find() หรือ FindObjectOfType() ใน Update()**
6. **ห้ามแก้ Grid size จาก 20×12** — เป็น design constraint ของเกม

---

## วิธีให้ Claude Code ทำงานได้ดีที่สุด

เวลาขอให้เขียนโค้ด ให้ระบุ:
- **ชื่อ class/method** ที่ต้องการ
- **input/output** ที่คาดหวัง
- **event ที่ต้อง subscribe/fire**
- **error ที่เจอ** (copy เต็มๆ รวม stack trace)

ตัวอย่างที่ดี:
```
เขียน ExplorationManager.cs
- method: SendTeam(Zone zone, int teamSize, float radiationGear)
- คำนวณ successRate = (radiationGear / zone.radiationLevel) * teamSize / 3
- ถ้า success: fire OnExplorationSuccess(List<ResourceDrop>)
- ถ้า fail: fire OnTeamMemberLost(int casualties)
- subscribe OnExplorationComplete ใน ResourceManager เพื่อเพิ่ม resource
```
