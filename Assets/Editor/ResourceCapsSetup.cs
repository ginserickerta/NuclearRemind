using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: สคริปต์ Editor ตั้งเพดานคลังทรัพยากรใน ResourceManager + อัปเกรดแผงทรัพยากรบน HUD
    /// ตั้งเพดานคลังตาม GDD V4 §4 ลง ResourceManager ใน scene:
    ///   Energy/Water/Iron/Deuterium/Tritium = 9999 ("แทบไม่จำกัด") · Food = 500 (เกิน → วิกฤตเน่า) · Knowledge = 100
    ///
    /// ต้องแก้ที่ scene component — ค่าเก่า (E300/W400/Fe1000/D500/T500) serialize ค้างอยู่
    /// แก้ default ในโค้ดอย่างเดียวไม่มีผล · critical* เป็น field ใหม่ → ใช้ default อัตโนมัติ
    ///
    /// + ล้าง tritiumProduction ของ Laboratory.asset (ค่าเก่า 4 ค้างบนดิสก์ —
    ///   Tritium ต้องมาจากการขุดแหล่งแร่โซน B เท่านั้น ดู OreDepositSetup)
    ///
    /// + migrate ResourcePanel ใน scene เดิม (ไม่ rebuild HUD ทั้งอัน — ลูก ๆ ของ canvas จะพัง):
    ///   ทุกแถวเป็นหลอด (ซ่อมหลอดที่เคยถูกถอด) + เพิ่มแถว Deuterium/Tritium + rewire UIManagerHUD
    ///   หลอดตันที่ display* ของ UIManagerHUD (2000/500) — ตัวเลขวิ่งต่อได้ถึง cap 9999
    ///
    /// รัน: เมนู NuclearReMind/Apply Resource Caps (V4 §4) — หรือรวมใน Run All Setups
    /// </summary>
    public static class ResourceCapsSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const string LabPath = "Assets/ScriptableObjects/Buildings/Laboratory.asset";

        // เมนูนี้: เขียนเพดานคลัง (9999/500/100) ลงซีน + เพิ่มแถว Deuterium/Tritium ในแผงทรัพยากร
        [MenuItem("NuclearReMind/Apply Resource Caps (V4 §4)")]
        public static void SetupAll()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1) เพดานคลัง GDD §4 — ทับค่า scene override เดิม
            var rm = Object.FindFirstObjectByType<ResourceManager>();
            if (rm != null)
            {
                rm.maxEnergy = 9999f;
                rm.maxWater = 9999f;
                rm.maxFood = 500f;
                rm.maxIron = 9999f;
                rm.maxDeuterium = 9999f;
                rm.maxTritium = 9999f;
                rm.maxKnowledge = 100f;
                EditorUtility.SetDirty(rm);
                Debug.Log("[ResourceCapsSetup] เพดานคลัง (V4 §4): E/W/Fe/D/T = 9999 · Food 500 · Knowledge 100 " +
                          "(critical alert แยกเป็นค่าสัมบูรณ์: E<60 W<80 F<100 Fe<200)");
            }
            else
                Debug.LogWarning("[ResourceCapsSetup] ไม่พบ ResourceManager ในซีน — เปิด Gamescene แล้วรันใหม่");

            // 2) ปลดสะพาน Tritium ของ Laboratory (asset บนดิสก์ค้างค่า 4 อยู่)
            var lab = AssetDatabase.LoadAssetAtPath<BuildingData>(LabPath);
            if (lab != null && lab.tritiumProduction != 0f)
            {
                lab.tritiumProduction = 0f;
                EditorUtility.SetDirty(lab);
                Debug.Log("[ResourceCapsSetup] Laboratory.tritiumProduction → 0 (Tritium ขุดจากแหล่งแร่โซน B เท่านั้น)");
            }

            // 3) migrate ResourcePanel: ตัวเลขล้วน + แถวเชื้อเพลิง
            UpdateResourcePanel();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ResourceCapsSetup] ✅ เสร็จ — เซฟซีน + assets แล้ว");
        }

        // ─────────────────────────────────────────
        //  ResourcePanel migration (scene HUD เดิม — ไม่ rebuild ทั้ง canvas)
        // ─────────────────────────────────────────

        private static void UpdateResourcePanel()
        {
            var hud = Object.FindFirstObjectByType<UIManagerHUD>();
            var panelGO = GameObject.Find("ResourcePanel");
            if (hud == null || panelGO == null)
            {
                Debug.LogWarning("[ResourceCapsSetup] ไม่พบ UIManagerHUD/ResourcePanel ในซีน — ข้าม migrate panel " +
                                 "(HUD ที่สร้างใหม่ด้วย Setup HUD Canvas จะได้ดีไซน์ใหม่อยู่แล้ว)");
                return;
            }

            var anyText = panelGO.GetComponentInChildren<Text>();
            var font = anyText != null ? anyText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var panel = panelGO.transform;

            // ทุกแถวเป็นหลอด — ซ่อมหลอดที่เคยถูกถอด (ดีไซน์ตัวเลขล้วนรอบก่อน) + สร้างแถวเชื้อเพลิงใหม่
            // สีหลอดตรงกับ HUDCanvasSetup ทุกแถว
            hud.waterBar = EnsureBarRow(panel, font, "WaterBar", "W", new Color(0.2f, 0.6f, 1f));
            hud.ironBar = EnsureBarRow(panel, font, "IronBar", "⛏", new Color(0.6f, 0.55f, 0.5f));
            hud.energyBar = EnsureBarRow(panel, font, "EnergyBar", "⚡", new Color(1f, 0.8f, 0.2f));
            hud.deuteriumBar = EnsureBarRow(panel, font, "DeuteriumBar", "D", new Color(0.35f, 0.7f, 0.95f));
            hud.tritiumBar = EnsureBarRow(panel, font, "TritiumBar", "⚛", new Color(0.85f, 0.45f, 0.2f));
            // FoodBar ไม่แตะ — หลอด /500 เดิมถูกอยู่แล้ว

            // จัด icon ทุกแถว (รวม Food/Water ที่เป็น sprite) ให้กล่องเท่ากัน → กึ่งกลางตรงกันทั้งคอลัมน์
            foreach (var rowName in new[] { "FoodBar", "WaterBar", "IronBar", "EnergyBar", "DeuteriumBar", "TritiumBar" })
            {
                var rowTf = panel.Find(rowName);
                if (rowTf != null) NormalizeIcon(rowTf.Find(rowName + "Icon"));
            }

            // ขยาย panel รองรับ 6 แถว (VerticalLayoutGroup จัดตำแหน่งเอง)
            var rect = panelGO.GetComponent<RectTransform>();
            if (rect != null) rect.sizeDelta = new Vector2(260, 200);

            EditorUtility.SetDirty(hud);
            Debug.Log("[ResourceCapsSetup] ResourcePanel: หลอดครบ 6 แถว (ตันที่ 2000 · อาหาร 500 · เชื้อเพลิง 500) " +
                      "+ แถว Deuterium/Tritium");
        }

        /// <summary>
        /// แถวหลอด (icon ซ้าย + Slider กลาง + ตัวเลขชิดขวา) — mirror CreateResourceBar ของ HUDCanvasSetup
        /// มีอยู่แล้ว → ซ่อมส่วนที่ขาด (หลอดโดนถอด/ความกว้าง text เพี้ยน) แล้ว rewire
        /// </summary>
        private static UIManagerHUD.ResourceBarUI EnsureBarRow(Transform panel, Font font, string rowName, string icon, Color fillColor)
        {
            var rowTf = panel.Find(rowName);
            GameObject row;
            if (rowTf == null)
            {
                row = new GameObject(rowName, typeof(RectTransform));
                row.transform.SetParent(panel, false);
                row.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 26);
                MakeRowText(row.transform, font, rowName + "Icon", icon, 18,
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(2, 0), new Vector2(24, 24), TextAnchor.MiddleCenter);
            }
            else
                row = rowTf.gameObject;

            // slider — สร้างใหม่ถ้าไม่มี (โดนถอดจากดีไซน์รอบก่อน หรือแถวเพิ่งสร้าง)
            var sliderTf = row.transform.Find(rowName + "Slider");
            Slider slider;
            if (sliderTf != null)
            {
                slider = sliderTf.GetComponent<Slider>();
                slider.gameObject.SetActive(true); // เผื่อเคยถูกซ่อน
            }
            else
            {
                var sliderGO = new GameObject(rowName + "Slider", typeof(RectTransform));
                sliderGO.transform.SetParent(row.transform, false);
                var sRect = sliderGO.GetComponent<RectTransform>();
                sRect.anchorMin = new Vector2(0f, 0.5f);
                sRect.anchorMax = new Vector2(0f, 0.5f);
                sRect.pivot = new Vector2(0f, 0.5f);
                sRect.anchoredPosition = new Vector2(28, 0);
                sRect.sizeDelta = new Vector2(150, 20);
                slider = BuildSlider(sliderGO, fillColor);
            }
            var fillImage = slider.transform.Find("Fill Area/Fill").GetComponent<Image>();

            // value text — สร้างถ้าไม่มี + บีบความกว้างกลับเป็น 80 (ดีไซน์รอบก่อนขยายเป็น 140 จะทับหลอด)
            var valTf = row.transform.Find(rowName + "ValueText");
            Text valueText;
            if (valTf != null)
            {
                valueText = valTf.GetComponent<Text>();
                valTf.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 24);
            }
            else
                valueText = MakeRowText(row.transform, font, rowName + "ValueText", "0", 16,
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0, 0), new Vector2(80, 24), TextAnchor.MiddleRight);

            // ResourcePanel พื้นโปร่งใส (CreatePanel ไม่ใส่ background) — ตัวเลขขาวอ่านชัดบนพื้นเกม
            // เท่ากับแถว Food ที่ HUDCanvasSetup สร้างไว้ (CreateText = ขาว) → ทุกแถวสีเดียวกัน
            valueText.color = Color.white;

            return new UIManagerHUD.ResourceBarUI { bar = slider, valueText = valueText, fillImage = fillImage };
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

        /// <summary>
        /// บังคับกล่อง icon ให้เท่ากันทุกแถว (sprite หรือ text ก็ตาม) — anchor ซ้ายกึ่งกลางแนวตั้ง
        /// กล่อง 24×24 ที่ x=2 · sprite = preserveAspect กึ่งกลางกล่อง · text = MiddleCenter
        /// ตรงกับ HUDCanvasSetup.CreateResourceBar → คอลัมน์ icon เรียงตรงกัน
        /// </summary>
        private static void NormalizeIcon(Transform iconTf)
        {
            if (iconTf == null) return;
            var rect = iconTf.GetComponent<RectTransform>();
            if (rect == null) return;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(2, 0);
            rect.sizeDelta = new Vector2(24, 24);

            var img = iconTf.GetComponent<Image>();
            if (img != null) img.preserveAspect = true;
            var txt = iconTf.GetComponent<Text>();
            if (txt != null) txt.alignment = TextAnchor.MiddleCenter;
        }

        private static Text MakeRowText(Transform parent, Font font, string name, string content, int fontSize,
            Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var txt = go.AddComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.color = new Color(0.13f, 0.15f, 0.19f); // เข้ม — อ่านบนพื้น panel สว่าง (ตรง textNormalColor)
            return txt;
        }
    }
}
