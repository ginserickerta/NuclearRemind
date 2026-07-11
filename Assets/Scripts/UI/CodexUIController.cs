using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// หน้าจอ Codex (Codex_Spec v8 — 11 entry ปลดจากควิซ · ถาวรข้ามรอบผ่าน MetaProgress §16):
    ///   • ส่วนหัว: ชื่อ + ความคืบหน้า "ปลดแล้ว x/11" (นับรวมเสมอ ไม่นับต่อหมวด) + ย้ำบันทึกถาวร
    ///   • แถบกรอง 5 ปุ่ม: ทั้งหมด / เตา-ฟิวชัน / แพทย์-รังสี / เกษตร / จริยธรรม (สี 4 หมวดตาม §17)
    ///   • ลิสต์ซ้าย: ไอคอน + ชื่อ — entry ล็อกโชว์ "? ? ?" ไม่เผยชื่อ (ล็อกไว้ ไม่ซ่อน — แรงจูงใจเก็บ)
    ///   • รายละเอียดขวา: pill หมวด + แหล่งปลด + หัวข้อไทย/EN + เนื้อหา + ท้าย "+2 Knowledge"
    ///     entry ล็อกกดได้ → โชว์ hint "ยังไม่ปลดล็อก — ปลดได้จาก [แหล่ง]" (สเปกแนะนำแบบนี้)
    /// ไม่มีผู้บรรยาย (VESTA ถูกตัดตาม v8) · ไม่มีการซื้อด้วย RP — ปลดจากควิซเท่านั้น
    /// UI สร้าง edit-time โดย CodexSetup — คลาสนี้แค่เติมข้อมูล/กรอง
    /// </summary>
    public class CodexUIController : MonoBehaviour
    {
        public static CodexUIController Instance { get; private set; }

        [Header("Panel")]
        public GameObject codexPanel;
        public Text progressText;          // "ปลดแล้ว x/11"
        public Button toggleButton;        // ปุ่ม Codex บน HUD (เปิด/ปิดแผง)
        public Button closeButton;         // ปุ่ม ✕ มุมแผง
        public KeyCode toggleKey = KeyCode.C; // คีย์ลัดเปิด/ปิด Codex

        [Header("Tabs (ทั้งหมด/เตา/แพทย์/เกษตร/จริยธรรม)")]
        public Button tabAll;
        public Button tabReactor;
        public Button tabMedical;
        public Button tabAgriculture;
        public Button tabEthics;

        [Header("List (ซ้าย)")]
        public Transform entryListParent;
        public GameObject entryButtonPrefab;

        [Header("Detail (ขวา)")]
        public Image detailPill;           // badge หมวด (สีตามหมวด)
        public Text detailPillLabel;
        public Text detailUnlockFrom;      // "ปลดจาก ควิซ #8"
        public Text detailTitle;
        public Text detailTitleEn;
        public Text detailContent;
        public Text detailFooter;          // "อ่านครั้งแรก +2 Knowledge"
        public Image detailIllustration;

        private bool _filterActive;                 // false = ทั้งหมด
        private QuizCategory _filter = QuizCategory.Reactor;
        private readonly List<GameObject> _spawnedButtons = new List<GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnCodexEntryUnlocked += HandleEntryUnlocked;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnCodexEntryUnlocked -= HandleEntryUnlocked;
        }

        private void Start()
        {
            WireTabs();
            if (codexPanel != null) codexPanel.SetActive(false);
        }

        private void Update()
        {
            if (codexPanel == null) return;
            if (Input.GetKeyDown(toggleKey)) Toggle();
            else if (codexPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Toggle();
        }

        private void WireTabs()
        {
            // ปุ่มเปิด/ปิดแผง — wire ตอน runtime (lambda listener จาก editor ไม่ถูก serialize → กดไม่ติด)
            if (toggleButton != null) { toggleButton.onClick.RemoveAllListeners(); toggleButton.onClick.AddListener(Toggle); }
            if (closeButton != null) { closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(Toggle); }

            if (tabAll != null) { tabAll.onClick.RemoveAllListeners(); tabAll.onClick.AddListener(() => SetFilter(null)); }
            if (tabReactor != null) { tabReactor.onClick.RemoveAllListeners(); tabReactor.onClick.AddListener(() => SetFilter(QuizCategory.Reactor)); }
            if (tabMedical != null) { tabMedical.onClick.RemoveAllListeners(); tabMedical.onClick.AddListener(() => SetFilter(QuizCategory.Medical)); }
            if (tabAgriculture != null) { tabAgriculture.onClick.RemoveAllListeners(); tabAgriculture.onClick.AddListener(() => SetFilter(QuizCategory.Agriculture)); }
            if (tabEthics != null) { tabEthics.onClick.RemoveAllListeners(); tabEthics.onClick.AddListener(() => SetFilter(QuizCategory.Ethics)); }
        }

        private void HandleEntryUnlocked(CodexEntry _)
        {
            if (codexPanel != null && codexPanel.activeSelf) RefreshAll();
        }

        public void Toggle()
        {
            if (codexPanel == null) return;
            bool show = !codexPanel.activeSelf;
            codexPanel.SetActive(show);
            if (show) RefreshAll();
        }

        private void SetFilter(QuizCategory? category)
        {
            _filterActive = category.HasValue;
            if (category.HasValue) _filter = category.Value;
            RefreshAll();
        }

        private void RefreshAll()
        {
            RefreshProgress();
            RefreshTabColors();
            RefreshList();
        }

        // "ปลดแล้ว x/11" — นับจาก entry ที่ปลดทั้งหมด (ตอนกรองหมวดตัวเลขยังเป็น x/รวม เท่าเดิม — สเปก §6.1)
        private void RefreshProgress()
        {
            var mgr = CodexManager.Instance;
            if (progressText == null || mgr == null || mgr.allCodexEntries == null) return;

            int total = 0, unlocked = 0;
            foreach (var e in mgr.allCodexEntries)
            {
                if (e == null) continue;
                total++;
                if (mgr.IsUnlocked(e.entryId)) unlocked++;
            }
            progressText.text = $"ปลดแล้ว {unlocked} / {total}";
        }

        private void RefreshTabColors()
        {
            SetTabColor(tabAll, !_filterActive, new Color(0.55f, 0.60f, 0.66f));
            SetTabColor(tabReactor, _filterActive && _filter == QuizCategory.Reactor, CategoryColor(QuizCategory.Reactor));
            SetTabColor(tabMedical, _filterActive && _filter == QuizCategory.Medical, CategoryColor(QuizCategory.Medical));
            SetTabColor(tabAgriculture, _filterActive && _filter == QuizCategory.Agriculture, CategoryColor(QuizCategory.Agriculture));
            SetTabColor(tabEthics, _filterActive && _filter == QuizCategory.Ethics, CategoryColor(QuizCategory.Ethics));
        }

        private static void SetTabColor(Button tab, bool active, Color accent)
        {
            if (tab == null) return;
            var img = tab.GetComponent<Image>();
            if (img != null)
                img.color = active ? accent : new Color(0.13f, 0.14f, 0.18f, 1f);
        }

        private void RefreshList()
        {
            if (entryListParent == null || entryButtonPrefab == null) return;
            var mgr = CodexManager.Instance;
            if (mgr == null || mgr.allCodexEntries == null) return;

            foreach (var go in _spawnedButtons)
                if (go != null) Destroy(go);
            _spawnedButtons.Clear();

            foreach (var entry in mgr.allCodexEntries) // เรียงตามลำดับใน array (ลำดับสเปก 1–11)
            {
                if (entry == null) continue;
                if (_filterActive && entry.category != _filter) continue;

                bool unlocked = mgr.IsUnlocked(entry.entryId);
                var go = Instantiate(entryButtonPrefab, entryListParent);
                go.SetActive(true);
                _spawnedButtons.Add(go);

                var label = go.GetComponentInChildren<Text>();
                if (label != null)
                {
                    // ล็อก = "? ? ?" ไม่เผยชื่อ (สเปก §6.2) · ปลดแล้ว = ไอคอน + ชื่อ
                    label.text = unlocked ? $"{IconGlyph(entry.iconName)}  {entry.title}" : "?  ? ? ?";
                    label.color = unlocked ? Color.white : new Color(0.5f, 0.53f, 0.58f);
                }

                var btn = go.GetComponent<Button>();
                if (btn != null)
                {
                    var e = entry;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => { if (CodexManager.Instance.IsUnlocked(e.entryId)) ShowEntry(e); else ShowLockedHint(e); });
                }
            }
        }

        /// <summary>แสดงรายละเอียด entry ที่ปลดแล้ว (สเปก §6.2)</summary>
        public void ShowEntry(CodexEntry entry)
        {
            if (entry == null) return;
            var col = CategoryColor(entry.category);

            if (detailPill != null) detailPill.color = col;
            if (detailPillLabel != null) detailPillLabel.text = CategoryLabel(entry.category);
            if (detailUnlockFrom != null) detailUnlockFrom.text = string.IsNullOrEmpty(entry.unlockedFrom) ? "" : $"ปลดจาก {entry.unlockedFrom}";
            if (detailTitle != null) { detailTitle.text = $"{IconGlyph(entry.iconName)}  {entry.title}"; detailTitle.color = Color.white; }
            if (detailTitleEn != null) detailTitleEn.text = entry.titleEn;
            if (detailContent != null) detailContent.text = entry.content;
            if (detailFooter != null) detailFooter.text = "อ่านครั้งแรก +2 Knowledge · บันทึกถาวรข้ามรอบเล่น";

            if (detailIllustration != null)
            {
                detailIllustration.sprite = entry.illustration;
                detailIllustration.gameObject.SetActive(entry.illustration != null);
            }
        }

        // entry ล็อก: กดแล้วโชว์ hint ว่าปลดได้จากไหน (สเปกแนะนำแบบ hint — ไม่ใช่กดไม่ได้)
        private void ShowLockedHint(CodexEntry entry)
        {
            if (entry == null) return;
            var col = CategoryColor(entry.category);

            if (detailPill != null) detailPill.color = col;
            if (detailPillLabel != null) detailPillLabel.text = CategoryLabel(entry.category);
            if (detailUnlockFrom != null) detailUnlockFrom.text = "";
            if (detailTitle != null) { detailTitle.text = "?  ? ? ?"; detailTitle.color = new Color(0.6f, 0.63f, 0.68f); }
            if (detailTitleEn != null) detailTitleEn.text = "";
            if (detailContent != null)
                detailContent.text = $"ยังไม่ปลดล็อก — ปลดได้จาก {(string.IsNullOrEmpty(entry.unlockedFrom) ? "การเล่นต่อไป" : entry.unlockedFrom)}";
            if (detailFooter != null) detailFooter.text = "";
            if (detailIllustration != null) detailIllustration.gameObject.SetActive(false);
        }

        // ── หมวด → สี/ป้าย (§17: เตา=น้ำเงิน แพทย์=แดง เกษตร=เขียว จริยธรรม=เทา) ──
        public static Color CategoryColor(QuizCategory c) => c switch
        {
            QuizCategory.Reactor => new Color(0.30f, 0.55f, 0.90f),
            QuizCategory.Medical => new Color(0.85f, 0.32f, 0.30f),
            QuizCategory.Agriculture => new Color(0.36f, 0.70f, 0.36f),
            QuizCategory.Ethics => new Color(0.58f, 0.58f, 0.62f),
            _ => Color.gray,
        };

        public static string CategoryLabel(QuizCategory c) => c switch
        {
            QuizCategory.Reactor => "เตา/ฟิวชัน",
            QuizCategory.Medical => "แพทย์/รังสี",
            QuizCategory.Agriculture => "เกษตร",
            QuizCategory.Ethics => "จริยธรรม",
            _ => "",
        };

        // iconName (สเปก §6.3 สไตล์ Tabler) → สัญลักษณ์ BMP ที่ legacy Text วาดได้จริง
        // (emoji นอก BMP เช่น 💧🔒 วาดไม่ได้ — บทเรียนใน CodexSetup) · ทีมส่ง sprite มาค่อยสลับ
        private static string IconGlyph(string iconName) => iconName switch
        {
            "droplet" => "◆",
            "flame" => "▲",
            "magnet" => "Ω",
            "atom" => "⊙",
            "atom-2" => "⊕",
            "stethoscope" => "✚",
            "shield" => "▣",
            "seeding" => "✿",
            "meat" => "♨",
            "leaf" => "♣",
            _ => "●",
        };
    }
}
