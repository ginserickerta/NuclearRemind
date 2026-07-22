using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Serialized handle to EVERY element of the Research Lab panel prefab
    /// (Resources/ResearchUI/ResearchLabPanel.prefab, baked by NuclearReMind → Setup Research Lab
    /// Prefabs). The prefab exists so the owner can tune position/size/color/font of any piece in
    /// the Editor without touching code; ResearchLabPanelUI instantiates it and wires behaviour
    /// through these references, so children can be moved or restyled freely — only deleting an
    /// element disables the matching feature (every consumer null-guards).
    /// </summary>
    public class ResearchLabPanelRefs : MonoBehaviour
    {
        [Header("Shell")]
        public Button backdropButton;      // full-screen dim — click closes the panel
        public GameObject ring;            // fallback-look shadow ring (hidden when the metal skin is on)
        public GameObject card;            // the metal panel_frame card
        public GameObject interior;        // dark fill just inside the metal border
        public float edge = 28f;           // content inset that clears the metal border

        [Header("Header")]
        public GameObject head;
        public GameObject headLine;
        public Text flask;                 // ⚗ icon
        public Text title;                 // "ห้องวิจัย"
        public Text subtitle;              // "ฐานความรู้ · เมือง Veltara"
        public Image statusPillBg;
        public Text statusPillText;
        public Button closeButton;

        [Header("Repair block (shown while the lab is ruined)")]
        public GameObject repairBlock;
        public Text repairHead;
        public Text repairLine;
        public Button repairButton;
        public Button repairCrewMinus;
        public Button repairCrewPlus;
        public Text repairCrewDisplay;
        public Text repairCrewHint;
        public GameObject repairBarWrap;
        public Image repairBarFill;

        [Header("Main block")]
        public GameObject mainBlock;
        public GameObject[] statCards;     // 4 slate stat cards, left→right
        public Text[] statLabels;
        public Text[] statValues;          // [0]=engineers [1]=slots [2]=idle labor [3]=records

        [Header("Left column — engineer assignment sidebar")]
        public GameObject leftCol;
        public Text assignHead;
        public Button crewMinus;
        public Button crewPlus;
        public GameObject crewDisplayBox;
        public Text crewDisplay;
        public Text assignHint;
        public GameObject laborWarn;
        public Text laborWarnText;
        public GameObject ratioWarn;
        public Text ratioWarnText;

        [Header("Right column — projects")]
        public Text projectsHead;
        public Button tabAll;
        public Button tabNotes;
        public Button tabRecords;
        public ScrollRect scroll;
        public RectTransform listContent;  // rows are instantiated here
        public Text masteryHead;
        public RectTransform masteryChips; // chips are instantiated here
    }
}
