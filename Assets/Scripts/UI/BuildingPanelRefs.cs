using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// ตัวถือ reference ของชิ้นส่วนแผง Building UI — อยู่บน prefab ที่ bake ออกมา (ไม่มี logic)
    /// BuildingUpgradeUI (component logic ในซีน) จะ instantiate prefab นี้แล้วอ่าน ref กลับผ่าน BindFromRefs
    /// แยก holder ออกจาก logic เพื่อไม่ให้มี BuildingUpgradeUI 2 ตัว (ซีน + prefab) ชน singleton
    /// </summary>
    public class BuildingPanelRefs : MonoBehaviour
    {
        [System.Serializable]
        public class CardRef
        {
            public GameObject root;
            public Outline frame;
            public Text lv;
            public Image sprite;
            public Text output;
            public Text status;
        }

        public GameObject root, backdrop;
        public Image iconImg, spriteImg;
        public Text nameTxt, headLvTxt, descTxt, hintTxt;
        public Text prodLabelTxt, prodValTxt, workerValTxt, bigLvTxt, maxLvTxt, extractTxt;
        public GameObject extractRow;
        public Button workerMinus, workerPlus;
        public Text upTitleTxt;
        public Button closeBtn;
        public GameObject reqRow, lvBox;
        public Text reqTitleTxt, reqEnergyTxt, reqIronTxt, reqWorkerTxt, reqTimeTxt, warnTxt;
        public Button upgradeBtn;
        public Text upgradeBtnTxt;
        public CardRef[] cards;
    }
}
