using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// แผง Inventory (GDD §13) — เปิด/ปิดด้วยปุ่ม HUD หรือ hotkey I
    /// แถวต่อไอเทม: ไอคอน/ชื่อ×จำนวน · ต้นทุน/ความคืบหน้า · ปุ่มคราฟต์ (disabled เมื่อ gate ไม่ผ่าน
    /// แนว BuildingUpgradeUI) · ปุ่มใช้ (ซ่อนสำหรับ passive เช่น Rad-Gear/PET ที่ถือไว้มีผล)
    /// สั่งงานผ่าน event เท่านั้น: RaiseCraftItemRequested / RaiseUseItemRequested
    /// อ่านสถานะแบบ read-only query จาก InventoryManager/ResourceManager (แพทเทิร์นเดิม)
    /// แถวสร้างจาก rowTemplate ที่ InventorySetup เตรียมไว้ (แนว entryButtonPrefab ของ CodexUIController)
    /// </summary>
    public class InventoryPanelController : MonoBehaviour, GameUIStack.IPanel
    {
        public static InventoryPanelController Instance { get; private set; }

        [Header("Panel (wire โดยเมนู Setup Inventory)")]
        public GameObject panel;
        public Transform rowsParent;      // content ที่มี VerticalLayoutGroup
        public GameObject rowTemplate;    // แถวต้นแบบ (inactive) — ลูก: Icon/Name/Info/CraftBtn/UseBtn
        public Button toggleButton;       // ปุ่ม "🎒 ไอเทม (I)" บน HUD
        public Button closeButton;

        [Header("Behaviour")]
        public KeyCode toggleKey = KeyCode.I;

        [Header("Colors (อ่านออกบนพื้นสว่าง — แนว BuildingUpgradeUI)")]
        public Color affordColor = new Color(0.72f, 0.5f, 0.1f);
        public Color cantAffordColor = new Color(0.8f, 0.22f, 0.22f);
        public Color okColor = new Color(0.2f, 0.55f, 0.33f);

        // แถวที่ instantiate แล้ว ต่อไอเทม (สร้างครั้งเดียวตอนเปิดครั้งแรก แล้ว refresh ข้อมูล)
        private class Row
        {
            public ItemSO item;
            public Text icon;
            public Text name;
            public Text info;
            public Button craftBtn;
            public Text craftLabel;
            public Button useBtn;
        }
        private readonly List<Row> _rows = new List<Row>();

        // itemId → progress ล่าสุดจาก OnCraftProgressChanged (โชว์ "กำลังผลิต x/y")
        private readonly Dictionary<string, (int progress, int total)> _crafting =
            new Dictionary<string, (int, int)>();

        private static readonly StringBuilder _sb = new StringBuilder(64);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnInventoryChanged     += HandleInventoryChanged;
            EventManager.Instance.OnResourceChanged      += HandleResourceChanged;
            EventManager.Instance.OnCraftProgressChanged += HandleCraftProgress;
            EventManager.Instance.OnItemCrafted          += HandleItemCrafted;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnInventoryChanged     -= HandleInventoryChanged;
            EventManager.Instance.OnResourceChanged      -= HandleResourceChanged;
            EventManager.Instance.OnCraftProgressChanged -= HandleCraftProgress;
            EventManager.Instance.OnItemCrafted          -= HandleItemCrafted;
        }

        private void Start()
        {
            if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (panel != null) panel.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                Toggle();
        }

        public void Toggle()
        {
            if (panel == null) return;
            bool nowOpen = !panel.activeSelf;
            if (nowOpen) UIPopIn.Ensure(panel);
            panel.SetActive(nowOpen);
            if (nowOpen)
            {
                GameUIStack.Push(this); // ขึ้นบนสุด + ลงทะเบียน (บล็อก Pause / Esc=ปิด)
                BuildRowsIfNeeded();
                RefreshAll();
            }
            else GameUIStack.Pop(this);
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
            GameUIStack.Pop(this);
        }

        // ── GameUIStack (แผงปิดได้: Esc=ปิดเหมือน ✕ · กติกากลางใน PauseMenuController) ──
        bool GameUIStack.IPanel.ClosableByEscape => true;
        void GameUIStack.IPanel.BringToFront() => GameUIStack.RaiseToTop(panel);
        void GameUIStack.IPanel.CloseFromStack() => Close();

        // ───────────────────────── rows ─────────────────────────

        private void BuildRowsIfNeeded()
        {
            if (_rows.Count > 0 || rowTemplate == null || rowsParent == null) return;
            var inv = InventoryManager.Instance;
            if (inv == null || inv.allItems == null) return;

            foreach (var item in inv.allItems)
            {
                if (item == null) continue;
                var go = Instantiate(rowTemplate, rowsParent);
                go.name = $"Row_{item.id}";
                go.SetActive(true);

                var row = new Row
                {
                    item = item,
                    icon = FindText(go, "Icon"),
                    name = FindText(go, "Name"),
                    info = FindText(go, "Info"),
                    craftBtn = FindButton(go, "CraftBtn"),
                    useBtn = FindButton(go, "UseBtn"),
                };
                if (row.craftBtn != null)
                {
                    row.craftLabel = row.craftBtn.GetComponentInChildren<Text>();
                    string id = item.id; // capture ต่อแถว
                    row.craftBtn.onClick.AddListener(() => EventManager.Instance.RaiseCraftItemRequested(id));
                }
                if (row.useBtn != null)
                {
                    string id = item.id;
                    row.useBtn.onClick.AddListener(() => EventManager.Instance.RaiseUseItemRequested(id));
                }
                _rows.Add(row);
            }
        }

        private static Text FindText(GameObject root, string childName)
        {
            var t = root.transform.Find(childName);
            return t != null ? t.GetComponent<Text>() : null;
        }

        private static Button FindButton(GameObject root, string childName)
        {
            var t = root.transform.Find(childName);
            return t != null ? t.GetComponent<Button>() : null;
        }

        // ───────────────────────── refresh ─────────────────────────

        private void RefreshAll()
        {
            var inv = InventoryManager.Instance;
            if (inv == null) return;

            foreach (var row in _rows)
                RefreshRow(inv, row);
        }

        private void RefreshRow(InventoryManager inv, Row row)
        {
            var item = row.item;
            int count = inv.GetCount(item.id);
            bool crafting = _crafting.ContainsKey(item.id) || inv.GetQueuedCount(item.id) > 0;

            if (row.icon != null) row.icon.text = CategoryEmoji(item.category);
            if (row.name != null) row.name.text = $"{item.displayName}  ×{count}";

            if (row.info != null)
            {
                if (crafting && _crafting.TryGetValue(item.id, out var p))
                {
                    row.info.text = $"⏳ กำลังผลิต {p.progress}/{p.total}";
                    row.info.color = affordColor;
                }
                else if (crafting)
                {
                    row.info.text = "⏳ อยู่ในคิวผลิต";
                    row.info.color = affordColor;
                }
                else
                {
                    bool afford = inv.CanAffordCraft(item);
                    row.info.text = CostString(item);
                    row.info.color = afford ? okColor : cantAffordColor;
                }
            }

            if (row.craftBtn != null)
            {
                bool can = inv.CanCraft(item);
                row.craftBtn.interactable = can;
                if (row.craftLabel != null)
                    row.craftLabel.text = CraftBlockLabel(inv, item, can);
            }

            if (row.useBtn != null)
            {
                // passive (Rad-Gear/PET) ถือไว้มีผล — ไม่มีปุ่มใช้
                row.useBtn.gameObject.SetActive(!item.isPassive);
                row.useBtn.interactable = !item.isPassive && count > 0;
            }
        }

        // ป้ายปุ่มคราฟต์บอกเหตุผลสั้น ๆ เมื่อกดไม่ได้ (แนว upgradeButtonLabel ของ BuildingUpgradeUI)
        private static string CraftBlockLabel(InventoryManager inv, ItemSO item, bool can)
        {
            if (can) return "⚒ ผลิต";
            if (!inv.IsResearchUnlocked(item)) return "ต้องวิจัยก่อน";
            if (!inv.HasStaffedBuilding(item.craftedAt)) return "ขาดอาคาร/คน";
            if (!inv.CanAffordCraft(item)) return "ของไม่พอ";
            return "เต็มเพดาน";
        }

        private static string CostString(ItemSO item)
        {
            if (item.craftCost == null || item.craftCost.Length == 0) return "ฟรี";
            _sb.Clear();
            _sb.Append("ต้นทุน: ");
            for (int i = 0; i < item.craftCost.Length; i++)
            {
                if (i > 0) _sb.Append("  ");
                _sb.Append(ResourceEmoji(item.craftCost[i].type))
                   .Append(Mathf.RoundToInt(item.craftCost[i].amount));
            }
            if (item.craftTicks > 0) _sb.Append("  ⏱").Append(item.craftTicks);
            return _sb.ToString();
        }

        private static string ResourceEmoji(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Energy:    return "⚡";
                case ResourceType.Water:     return "💧";
                case ResourceType.Food:      return "🌿";
                case ResourceType.Iron:      return "⛏";
                case ResourceType.Deuterium: return "D";
                case ResourceType.Tritium:   return "⚛";
                default:                     return "📖";
            }
        }

        private static string CategoryEmoji(ItemCategory c)
        {
            switch (c)
            {
                case ItemCategory.Equipment: return "🛡";
                case ItemCategory.Medical:   return "⚕";
                case ItemCategory.Agri:      return "🌾";
                case ItemCategory.Emergency: return "🧯";
                default:                     return "⚛";
            }
        }

        // ───────────────────────── event handlers ─────────────────────────

        private void HandleInventoryChanged()
        {
            if (panel != null && panel.activeSelf) RefreshAll();
        }

        private void HandleResourceChanged(ResourceData _)
        {
            if (panel != null && panel.activeSelf) RefreshAll(); // สีต้นทุน/ปุ่ม afford อัปเดตสด
        }

        private void HandleCraftProgress(string itemId, int progress, int total)
        {
            _crafting[itemId] = (progress, total);
            if (panel != null && panel.activeSelf) RefreshAll();
        }

        private void HandleItemCrafted(ItemSO item)
        {
            if (item != null) _crafting.Remove(item.id);
            EventManager.Instance.RaiseNotice($"⚒ ผลิต {item?.displayName} สำเร็จ");
            if (panel != null && panel.activeSelf) RefreshAll();
        }
    }
}
