using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: ที่รวม reference ของชิ้นส่วนใน prefab "แถว/ชิป" ของหน้าห้องวิจัย (ไม่มี logic)
    /// component เดียวใช้ร่วมทุกชนิดแถว — prefab ไหนไม่ใช้ field ไหนก็ปล่อยว่าง
    /// โค้ดเติมข้อความ/สี/การมองเห็นให้ตอนรัน ส่วนเจ้าของโปรเจกต์แต่งหน้าตาใน Editor ได้อิสระ
    ///
    /// Serialized handle to every element of a Research Lab row/chip prefab
    /// (Resources/ResearchUI/Row_Note · Row_RecordActive · Row_RecordDone · Row_Simple ·
    /// Chip_Mastery — baked by NuclearReMind → Setup Research Lab Prefabs).
    ///
    /// One component covers all row kinds; a prefab simply leaves the fields it has no use for
    /// empty. ResearchLabPanelUI fills text/colors/visibility at runtime through these references,
    /// so the owner can restyle any child in the Editor. badgeTemplate stays INACTIVE in the
    /// prefab — it is cloned once per badge with live text/colors.
    /// </summary>
    public class ResearchLabRowRefs : MonoBehaviour
    {
        public Image background;          // slate plate (or rounded fallback)
        public LayoutElement layout;      // height is content-driven, set by code each rebuild
        public Text title;                // note title / record head / simple-row text
        public Text meta;                 // cost line (note) / status hint (record active)
        public Text hint;                 // lead hint line (note rows only)
        public Text blockedNote;          // "ขาด prerequisite" (note rows only)
        public GameObject badgeTemplate;  // inactive pill, cloned per badge
        public Button actionButton;       // "เริ่มวิจัย" (note) / "ถอดรหัส" (record active)
        public GameObject barTrack;       // progress bar track
        public Image barFill;             // progress bar fill (anchors driven by code)
    }
}
