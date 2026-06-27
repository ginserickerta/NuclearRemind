using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Hotbar ด้านล่างจอ: แสดงปุ่มสำหรับ building แต่ละประเภท (สูงสุด 8 ช่อง)
    /// คลิกปุ่ม → raise OnBuildingSelectRequested → PlacementController.BeginPlacement
    /// ปุ่มหรี่ลง (dimmed) เมื่อ resource ไม่พอ, highlight เมื่อถูกเลือก
    /// แป้น 1-7 ยังใช้ได้เหมือนเดิม และ sync highlight กับ UI ด้วย
    /// </summary>
    public class BuildingSelectionUI : MonoBehaviour
    {
        public static BuildingSelectionUI Instance { get; private set; }

        [Header("Building list (ลำดับตรงกับ hotbar 1-8)")]
        public BuildingData[] buildings;

        [Header("Runtime — wire โดย setup script")]
        public Transform buttonContainer;

        // state
        private Button[] _buttons;
        private Image[]  _buttonImages;
        private BuildingData _selected;
        private ResourceData _resources;
        private bool _isDemolishing;

        // demolish button refs (สร้างแยกจาก building slots)
        private Image _demolishImage;

        // สีสถานะ
        private static readonly Color ColNormal      = new Color(0.15f, 0.15f, 0.22f, 1f);
        private static readonly Color ColSelected    = new Color(0.25f, 0.55f, 0.85f, 1f);
        private static readonly Color ColCantAfford  = new Color(0.35f, 0.15f, 0.15f, 1f);
        private static readonly Color ColDemolish    = new Color(0.35f, 0.10f, 0.10f, 1f);
        private static readonly Color ColDemolishOn  = new Color(0.85f, 0.20f, 0.20f, 1f);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnBuildingSelected    += HandleBuildingSelected;
            EventManager.Instance.OnResourceChanged     += HandleResourceChanged;
            EventManager.Instance.OnDemolishModeToggled += HandleDemolishModeToggled;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingSelected    -= HandleBuildingSelected;
            EventManager.Instance.OnResourceChanged     -= HandleResourceChanged;
            EventManager.Instance.OnDemolishModeToggled -= HandleDemolishModeToggled;
        }

        private void Start()
        {
            BuildButtons();
        }

        // ─────────────────────────────────────────
        //  Build UI
        // ─────────────────────────────────────────

        private void BuildButtons()
        {
            if (buttonContainer == null || buildings == null) return;

            // ล้างปุ่มเก่า (ถ้า setup รันซ้ำ)
            foreach (Transform child in buttonContainer) Destroy(child.gameObject);

            _buttons      = new Button[buildings.Length];
            _buttonImages = new Image[buildings.Length];

            for (int i = 0; i < buildings.Length; i++)
            {
                var data = buildings[i];
                if (data == null) continue;

                var slot = CreateSlot(i, data);
                _buttons[i]      = slot.GetComponent<Button>();
                _buttonImages[i] = slot.GetComponent<Image>();
            }

            BuildDemolishButton();
        }

        private static Font LoadKanitFont()
        {
            var f = Resources.Load<Font>("Fonts/Kanit-Regular");
            return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void BuildDemolishButton()
        {
            var font = LoadKanitFont();

            var slot = new GameObject("Slot_Demolish", typeof(RectTransform));
            slot.transform.SetParent(buttonContainer, false);
            slot.GetComponent<RectTransform>().sizeDelta = new Vector2(90f, 110f);

            _demolishImage = slot.AddComponent<Image>();
            _demolishImage.color = ColDemolish;

            var btn = slot.AddComponent<Button>();
            btn.targetGraphic = _demolishImage;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.6f, 0.2f, 0.2f, 1f);
            colors.pressedColor     = new Color(0.9f, 0.1f, 0.1f, 1f);
            btn.colors = colors;

            // ไอคอนค้อน
            MakeText("Icon",    slot.transform, font, "🔨", 30,
                new Vector2(0, 20), new Vector2(0, 48), TextAnchor.MiddleCenter)
                .GetComponent<RectTransform>().anchorMin = new Vector2(0, 0.3f);
            MakeText("Label",   slot.transform, font, "ทุบอาคาร", 11,
                new Vector2(0, 24), new Vector2(0, 20), TextAnchor.LowerCenter)
                .GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
            var subLbl = MakeText("SubLabel", slot.transform, font, "คลิกขวายกเลิก", 9,
                new Vector2(0, 6), new Vector2(0, 16), TextAnchor.LowerCenter);
            subLbl.color = new Color(0.8f, 0.6f, 0.6f);
            subLbl.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
            subLbl.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);

            btn.onClick.AddListener(ToggleDemolish);
        }

        private GameObject CreateSlot(int index, BuildingData data)
        {
            var font = LoadKanitFont();

            // Slot container
            var slot = new GameObject($"Slot_{index + 1}", typeof(RectTransform));
            slot.transform.SetParent(buttonContainer, false);
            slot.GetComponent<RectTransform>().sizeDelta = new Vector2(90f, 110f);

            var bg = slot.AddComponent<Image>();
            bg.color = ColNormal;

            var btn = slot.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.35f, 0.45f, 0.65f, 1f);
            colors.pressedColor     = new Color(0.1f, 0.3f, 0.6f, 1f);
            btn.colors = colors;

            // Hotkey label (มุมบนซ้าย)
            var keyLbl = MakeText("KeyLabel", slot.transform, font, $"{index + 1}", 13,
                new Vector2(4, -4), new Vector2(20, 18), TextAnchor.UpperLeft);
            keyLbl.color = new Color(0.7f, 0.7f, 0.7f);

            // Building icon
            if (data.sprite != null)
            {
                var iconGO = new GameObject("Icon", typeof(RectTransform));
                iconGO.transform.SetParent(slot.transform, false);
                var iconRect = iconGO.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.1f, 0.35f);
                iconRect.anchorMax = new Vector2(0.9f, 0.90f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                var iconImg = iconGO.AddComponent<Image>();
                iconImg.sprite = data.sprite;
                iconImg.preserveAspect = true;
            }

            // Building name
            var nameLbl = MakeText("NameLabel", slot.transform, font, data.buildingName, 12,
                new Vector2(0, 24), new Vector2(0, 20), TextAnchor.LowerCenter);
            nameLbl.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
            nameLbl.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);
            nameLbl.color = Color.white;
            nameLbl.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Cost label
            string costStr = BuildCostString(data);
            var costLbl = MakeText("CostLabel", slot.transform, font, costStr, 11,
                new Vector2(0, 6), new Vector2(0, 18), TextAnchor.LowerCenter);
            costLbl.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
            costLbl.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);
            costLbl.color = new Color(1f, 0.85f, 0.3f);

            // Click listener
            var captured = data;
            btn.onClick.AddListener(() => EventManager.Instance.RaiseBuildingSelectRequested(captured));

            return slot;
        }

        // ─────────────────────────────────────────
        //  Event handlers
        // ─────────────────────────────────────────

        private void HandleBuildingSelected(BuildingData data)
        {
            _selected = data;
            // การเลือกอาคารจะยกเลิก demolish mode อัตโนมัติ
            if (data != null && _isDemolishing)
                EventManager.Instance.RaiseDemolishModeToggled(false);
            RefreshButtonColors();
        }

        private void HandleResourceChanged(ResourceData res)
        {
            _resources = res;
            RefreshButtonColors();
        }

        private void HandleDemolishModeToggled(bool active)
        {
            _isDemolishing = active;
            if (_demolishImage != null)
                _demolishImage.color = active ? ColDemolishOn : ColDemolish;
        }

        private void ToggleDemolish()
        {
            // PlacementController.HandleDemolishModeToggled ดูแล cancel placement แล้ว
            EventManager.Instance.RaiseDemolishModeToggled(!_isDemolishing);
        }

        private void RefreshButtonColors()
        {
            if (_buttons == null) return;

            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] == null || buildings[i] == null) continue;

                bool isSelected = buildings[i] == _selected && !_isDemolishing;
                bool canAfford  = CanAfford(buildings[i]);

                _buttonImages[i].color = isSelected  ? ColSelected
                                       : !canAfford  ? ColCantAfford
                                                     : ColNormal;
            }

            if (_demolishImage != null)
                _demolishImage.color = _isDemolishing ? ColDemolishOn : ColDemolish;
        }

        // ─────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────

        private bool CanAfford(BuildingData data) =>
            _resources.energy  >= data.energyCost &&
            _resources.workers >= data.workerRequired;

        private static string BuildCostString(BuildingData data)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (data.energyCost    > 0) parts.Add($"⚡{data.energyCost}");
            if (data.workerRequired > 0) parts.Add($"👷{data.workerRequired}");
            return parts.Count > 0 ? string.Join(" ", parts) : "ฟรี";
        }

        private static Text MakeText(string name, Transform parent, Font font,
            string content, int size, Vector2 offset, Vector2 sizeDelta, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = offset;
            rect.sizeDelta        = sizeDelta;
            var txt = go.AddComponent<Text>();
            txt.font = font; txt.fontSize = size;
            txt.alignment = anchor; txt.text = content;
            txt.color = Color.white;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            return txt;
        }
    }
}
