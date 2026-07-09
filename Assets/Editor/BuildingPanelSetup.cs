using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// migrate แผง hover อาคาร (BuildingUpgradePanel) ใน scene เดิม (ไม่ rebuild HUD ทั้งอัน — GDD §6):
    ///   เพิ่มแถบความคืบหน้าก่อสร้าง BU_ConstructBar (หลอดสีเขียว) ตำแหน่งเดียวกับปุ่มอัปเกรด (y=-26)
    ///   → BuildingUpgradeUI โชว์แถบนี้แทนปุ่มตอนอาคารกำลังสร้าง (บาร์ลอย world-space เดิมถูกถอดแล้ว)
    ///
    /// รันครั้งเดียวบน scene ที่ HUD สร้างไว้ก่อนมี field นี้ · idempotent (มี BU_ConstructBar แล้ว → แค่ rewire)
    /// scene ที่สร้างใหม่ด้วย Setup HUD Canvas จะมีแถบนี้อยู่แล้ว
    ///
    /// รัน: เมนู NuclearReMind/Update Building Hover Panel — หรือรวมใน Run All Setups
    /// </summary>
    public static class BuildingPanelSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";

        [MenuItem("NuclearReMind/Update Building Hover Panel")]
        public static void SetupAll()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var buUI = Object.FindFirstObjectByType<BuildingUpgradeUI>();
            var panelGO = GameObject.Find("BuildingUpgradePanel");
            if (buUI == null || panelGO == null)
            {
                Debug.LogWarning("[BuildingPanelSetup] ไม่พบ BuildingUpgradeUI/BuildingUpgradePanel ในซีน — " +
                                 "รัน NuclearReMind/Setup HUD Canvas ก่อน");
                return;
            }

            var panel = panelGO.transform;
            var barTf = panel.Find("BU_ConstructBar");
            Slider bar;
            if (barTf != null)
            {
                bar = barTf.GetComponent<Slider>();
            }
            else
            {
                // ตำแหน่ง/ขนาดตรงกับ HUDCanvasSetup: y=-26 (ที่เดียวกับปุ่มอัปเกรด) ขนาด 190×18
                var barGO = new GameObject("BU_ConstructBar", typeof(RectTransform));
                barGO.transform.SetParent(panel, false);
                var rect = barGO.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0, -26);
                rect.sizeDelta = new Vector2(190, 18);
                bar = BuildSlider(barGO, new Color(0.3f, 0.75f, 0.35f));
            }

            buUI.constructionBar = bar;
            EditorUtility.SetDirty(buUI);
            bar.gameObject.SetActive(false); // BuildingUpgradeUI เปิดเองตอนอาคารกำลังสร้าง

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[BuildingPanelSetup] ✅ BU_ConstructBar พร้อม + wire buUI.constructionBar แล้ว (เซฟซีน)");
        }

        // โครง Slider แบบเดียวกับ HUDCanvasSetup.BuildSlider (private ที่ต้นทาง — คัดลอกมา local)
        private static Slider BuildSlider(GameObject go, Color fillColor)
        {
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            var fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGO.transform.SetParent(go.transform, false);
            var fillAreaRect = fillAreaGO.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(2, 2);
            fillAreaRect.offsetMax = new Vector2(-2, -2);

            var fillGO = new GameObject("Fill", typeof(RectTransform));
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fillRect = fillGO.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fillGO.AddComponent<Image>();
            fillImage.color = fillColor;

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.targetGraphic = fillImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.interactable = false;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            return slider;
        }
    }
}
