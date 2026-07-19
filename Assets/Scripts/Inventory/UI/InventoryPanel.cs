using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// v6.3 inventory panel (docs/INVENTORY.md ข้อ 6) — 6 tabs over a slot grid.
    /// Every number is read live from InventoryManager.GetSlots (view layer, no state here).
    ///
    /// Each slot row shows: icon · count/cap · delta/day (+green / −red).
    /// ★ Tritium negative delta blinks red — the single most important teaching cue in the game
    ///   (player must SEE −6.0/วัน to go hunt for q_tritium_breeding).
    ///
    /// Wire in scene: 6 tab Buttons call SetTab(int), a slot row template (Text, disabled),
    /// and a container with a vertical/grid layout group.
    /// </summary>
    public class InventoryPanel : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform slotContainer;
        [SerializeField] private Text slotTemplate;        // disabled template row
        [SerializeField] private Text tabLabel;            // shows active tab name

        [Header("Colors")]
        [SerializeField] private Color gainColor = new Color(0.45f, 0.85f, 0.45f);
        [SerializeField] private Color lossColor = new Color(0.95f, 0.45f, 0.40f);
        [SerializeField] private Color lockedColor = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] private float blinkHz = 2f;       // tritium warning blink

        private static readonly string[] TabNames =
            { "ทั้งหมด", "ทรัพยากร", "เชื้อเพลิง", "อาหาร", "การแพทย์", "เกษตร" };

        private InventoryTab _tab = InventoryTab.All;
        private readonly List<Text> _rows = new List<Text>();
        private readonly List<int> _blinkRows = new List<int>(); // row indices that blink (tritium < 0)

        private void OnEnable()
        {
            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnResourceChanged += HandleResourceChanged;
                EventManager.Instance.OnInventoryChanged += Refresh;
            }
            if (WorkerManager.Instance != null)
                WorkerManager.Instance.OnWorkersChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnResourceChanged -= HandleResourceChanged;
                EventManager.Instance.OnInventoryChanged -= Refresh;
            }
            if (WorkerManager.Instance != null)
                WorkerManager.Instance.OnWorkersChanged -= Refresh;
        }

        private void HandleResourceChanged(ResourceData _) => Refresh();

        private void Update()
        {
            // tritium warning blink — red pulses while its delta is negative
            if (_blinkRows.Count == 0) return;
            float a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * blinkHz));
            var c = lossColor; c.a = a;
            for (int i = 0; i < _blinkRows.Count; i++)
                if (_blinkRows[i] < _rows.Count)
                    _rows[_blinkRows[i]].color = c;
        }

        /// <summary>HUD inventory button hooks this.</summary>
        public void TogglePanel()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(!panelRoot.activeSelf);
            if (panelRoot.activeSelf) Refresh();
        }

        /// <summary>Tab buttons pass their index (0=ทั้งหมด … 5=เกษตร).</summary>
        public void SetTab(int tabIndex)
        {
            _tab = (InventoryTab)Mathf.Clamp(tabIndex, 0, TabNames.Length - 1);
            if (tabLabel != null) tabLabel.text = TabNames[(int)_tab];
            Refresh();
        }

        public void Refresh()
        {
            if (InventoryManager.Instance == null || slotContainer == null || slotTemplate == null) return;

            var slots = InventoryManager.Instance.GetSlots(_tab);
            _blinkRows.Clear();

            while (_rows.Count < slots.Count)
                _rows.Add(Instantiate(slotTemplate, slotContainer));
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].gameObject.SetActive(i < slots.Count);

            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                _rows[i].text = FormatSlot(s);

                if (!string.IsNullOrEmpty(s.lockedHint))
                    _rows[i].color = lockedColor;
                else if (s.itemId == "tritium" && s.deltaPerDay < 0f)
                {
                    _rows[i].color = lossColor;
                    _blinkRows.Add(i); // ★ แดงกระพริบ — จุดสอนของเกมทั้งเกม
                }
                else
                    _rows[i].color = s.deltaPerDay >= 0f ? gainColor : lossColor;
            }
        }

        /// <summary>"⚡ Power  240/400  +45/วัน" · locked → "🔒 Rad Suit — ต้องวิจัย ...". Static for tests.</summary>
        public static string FormatSlot(InventorySlotView s)
        {
            if (!string.IsNullOrEmpty(s.lockedHint))
                return $"[ล็อก] {s.displayName} — {s.lockedHint}";

            string cap = s.cap >= 999f ? $"{s.count:0}" : $"{s.count:0}/{s.cap:0}";
            string delta = s.deltaPerDay >= 0f ? $"+{s.deltaPerDay:0.#}" : $"−{Mathf.Abs(s.deltaPerDay):0.#}";
            string craft = s.isCraftable ? (s.craftEnabled ? "  [คราฟต์]" : "  [คราฟต์ ✕]") : "";
            return $"{s.icon} {s.displayName}  {cap}  {delta}/วัน{craft}";
        }
    }
}
