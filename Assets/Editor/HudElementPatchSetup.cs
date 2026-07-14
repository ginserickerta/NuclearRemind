using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using NuclearReMind;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// เมนู patch เฉพาะจุดของ HUD — แก้ sprite/โครงเล็ก ๆ บน object เดิมในซีน "โดยไม่ regenerate ทั้ง HUD"
    /// (ต่างจาก Setup HUD Canvas ที่ลบ HUDCanvas ทิ้งแล้วสร้างใหม่ → ตำแหน่งที่ผู้ใช้จัดเองหาย)
    /// ทุกเมนู idempotent · reuse helper จาก HUDCanvasSetup (LoadIcon/EnsureIconImported/CreateTextRow/LoadFont)
    /// ไม่อยู่ใน RunAllSetups (ทางนั้นรัน Setup HUD Canvas เต็ม ๆ ครอบคลุมอยู่แล้ว) — เมนูนี้ไว้ patch เดี่ยว ๆ
    /// </summary>
    public static class HudElementPatchSetup
    {
        // ── แผงภารกิจสอนเล่น Day 1: เปลี่ยน sprite พื้นหลัง + ปุ่ม (ไม่แตะข้อความ/ตำแหน่ง) ──
        [MenuItem("NuclearReMind/HUD/Apply Tutorial Sprites")]
        public static void ApplyTutorialSprites()
        {
            var tut = Object.FindFirstObjectByType<TutorialManager>();
            if (tut == null)
            {
                EditorUtility.DisplayDialog("Apply Tutorial Sprites",
                    "ไม่พบ TutorialManager ในซีน — เปิด Gamescene.unity ก่อน", "OK");
                return;
            }

            HUDCanvasSetup.EnsureIconImported("TutorialBg");
            HUDCanvasSetup.EnsureIconImported("TutorialButton");
            var bg = HUDCanvasSetup.LoadIcon("TutorialBg");
            var btn = HUDCanvasSetup.LoadIcon("TutorialButton");

            int n = 0;
            // พื้นหลังแผง (Image บน tutorialPanel)
            if (tut.tutorialPanel != null && bg != null)
            {
                var img = tut.tutorialPanel.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = bg; img.type = Image.Type.Simple; img.color = Color.white;
                    EditorUtility.SetDirty(img); n++;
                }
            }
            // ปุ่ม (Image ของ startButton — คง ColorTint เดิม: base ขาว × normal ขาว = sprite จริง / disabled = หรี่)
            if (tut.startButton != null && tut.startButton.image != null && btn != null)
            {
                var bi = tut.startButton.image;
                bi.sprite = btn; bi.type = Image.Type.Simple; bi.color = Color.white;
                EditorUtility.SetDirty(bi); n++;
            }

            MarkDirty();
            Debug.Log($"[HudElementPatch] ใส่ sprite แผงสอนเล่นแล้ว ({n}/2) — ไม่ได้รื้อ HUD · อย่าลืม Save Scene (Ctrl+S)");
        }

        // ── แถบประชากร: แทนบรรทัดเดียว (W/E/M/F) ด้วย 5 แถว icon เรียงบนลงล่าง ──
        [MenuItem("NuclearReMind/HUD/Apply Population Icons")]
        public static void ApplyPopulationIcons()
        {
            var hud = Object.FindFirstObjectByType<UIManagerHUD>();
            if (hud == null)
            {
                EditorUtility.DisplayDialog("Apply Population Icons",
                    "ไม่พบ UIManagerHUD ในซีน — เปิด Gamescene.unity ก่อน", "OK");
                return;
            }

            // หา PopulationPanel: พ่อของ hopeText (แผงเดียวกัน) → legacy populationText → ชื่อ object
            Transform panel = null;
            if (hud.hopeText != null) panel = hud.hopeText.transform.parent;
            else if (hud.populationText != null) panel = hud.populationText.transform.parent;
            if (panel == null)
            {
                var go = GameObject.Find("PopulationPanel");
                if (go != null) panel = go.transform;
            }
            if (panel == null)
            {
                EditorUtility.DisplayDialog("Apply Population Icons",
                    "ไม่พบ PopulationPanel — รัน Setup HUD Canvas สร้าง HUD ก่อน 1 ครั้ง", "OK");
                return;
            }

            var font = HUDCanvasSetup.LoadFont();

            // ลบของเดิม (idempotent): บรรทัดเดียว + แถวเก่า (เผื่อกดซ้ำ)
            RemoveChild(panel, "PopulationText");
            foreach (var rn in new[] { "PopTotalRow", "PopWorkerRow", "PopEngineerRow", "PopMedicRow", "PopFarmerRow" })
                RemoveChild(panel, rn);
            hud.populationText = null; // เลิกใช้บรรทัดเดียว

            foreach (var f in new[] { "PopTotal", "PopWorker", "PopEngineer", "PopMedic", "PopFarmer" })
                HUDCanvasSetup.EnsureIconImported(f);

            hud.popTotalText    = HUDCanvasSetup.CreateTextRow("PopTotalRow",    panel, font, "10/10", HUDCanvasSetup.LoadIcon("PopTotal"));
            hud.popWorkerText   = HUDCanvasSetup.CreateTextRow("PopWorkerRow",   panel, font, "6",     HUDCanvasSetup.LoadIcon("PopWorker"));
            hud.popEngineerText = HUDCanvasSetup.CreateTextRow("PopEngineerRow", panel, font, "2",     HUDCanvasSetup.LoadIcon("PopEngineer"));
            hud.popMedicText    = HUDCanvasSetup.CreateTextRow("PopMedicRow",    panel, font, "0",     HUDCanvasSetup.LoadIcon("PopMedic"));
            hud.popFarmerText   = HUDCanvasSetup.CreateTextRow("PopFarmerRow",   panel, font, "2",     HUDCanvasSetup.LoadIcon("PopFarmer"));

            // จัดให้อยู่บนสุดตามลำดับ: รวม → worker → engineer → medic → farmer (ก่อน hope/knowledge)
            hud.popTotalText.transform.SetSiblingIndex(0);
            hud.popWorkerText.transform.SetSiblingIndex(1);
            hud.popEngineerText.transform.SetSiblingIndex(2);
            hud.popMedicText.transform.SetSiblingIndex(3);
            hud.popFarmerText.transform.SetSiblingIndex(4);

            // ขยายความสูง panel ให้พอ 5 แถว (เผื่อของเดิมเตี้ย)
            var prt = panel.GetComponent<RectTransform>();
            if (prt != null && prt.sizeDelta.y < 320f)
                prt.sizeDelta = new Vector2(prt.sizeDelta.x, 340f);

            EditorUtility.SetDirty(hud);
            MarkDirty();
            Debug.Log("[HudElementPatch] ใส่แถว icon ประชากร 5 แถวแล้ว — ไม่ได้รื้อ HUD · อย่าลืม Save Scene (Ctrl+S)");
        }

        // ── แปลงกรอบที่เผลอใส่เป็น SpriteRenderer (world) ให้เป็น UI Image (แสดงบน Canvas ได้จริง) ──
        //   SpriteRenderer ไม่ถูก Canvas วาด + Screen Space-Overlay ทับทีหลัง → หายตอน Play
        //   เมนูนี้หา SpriteRenderer "ทุกตัวที่อยู่ใต้ Canvas" แล้วสร้าง UI Image แทน ตำแหน่ง/ขนาดเท่าเดิม แล้วลบตัวเก่า
        [MenuItem("NuclearReMind/HUD/Convert Sprite Frames to UI Image")]
        public static void ConvertSpriteFramesToUIImages()
        {
            var all = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var targets = new System.Collections.Generic.List<SpriteRenderer>();
            foreach (var sr in all)
            {
                if (sr == null || sr.sprite == null) continue;
                if (sr.GetComponentInParent<Canvas>() == null) continue; // เฉพาะ sprite ที่อยู่ใต้ Canvas (= กรอบที่หลงมา)
                targets.Add(sr);
            }

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("Convert Sprite Frames",
                    "ไม่พบ SpriteRenderer ที่อยู่ใต้ Canvas — ไม่มีกรอบชนิด sprite ให้แปลง (อาจแปลงไปแล้ว/ไม่มี)", "OK");
                return;
            }

            int done = 0;
            foreach (var sr in targets)
            {
                var srGO = sr.gameObject;
                var parent = srGO.transform.parent;
                if (parent == null) continue; // กรอบต้องอยู่ใต้ panel/canvas

                Sprite spr = sr.sprite;
                Bounds b = sr.bounds;                 // กรอบสี่เหลี่ยมในโลก (ตำแหน่ง+ขนาดที่วาดจริง)
                int sibling = srGO.transform.GetSiblingIndex();
                string keepName = srGO.name.Trim();
                Color col = sr.color;

                var go = new GameObject(keepName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, "Convert Sprite Frame");
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(parent, false);

                var img = go.GetComponent<Image>();
                img.sprite = spr;
                img.type = (spr.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
                img.color = col;
                img.raycastTarget = false;            // กรอบตกแต่ง ไม่กินคลิก

                rt.localScale = Vector3.one;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                // ขนาด UI (local) = ขนาดโลก / lossyScale ของ parent · ตำแหน่ง = center โลก (RectTransform แปลงให้เอง)
                Vector3 pls = parent.lossyScale;
                rt.sizeDelta = new Vector2(
                    Mathf.Approximately(pls.x, 0f) ? b.size.x : b.size.x / Mathf.Abs(pls.x),
                    Mathf.Approximately(pls.y, 0f) ? b.size.y : b.size.y / Mathf.Abs(pls.y));
                rt.position = b.center;
                rt.SetSiblingIndex(sibling);          // คงลำดับวาดเดิม (ปรับหน้า/หลัง bar ได้ทีหลัง)

                Undo.DestroyObjectImmediate(srGO);
                done++;
            }

            MarkDirty();
            Debug.Log($"[HudElementPatch] แปลงกรอบ SpriteRenderer → UI Image แล้ว {done} อัน — โชว์ตอน Play ได้ · " +
                      "ปรับลำดับหน้า/หลัง bar ใน Hierarchy ได้ (ล่างสุด=บนสุด) · อย่าลืม Save Scene (Ctrl+S)");
        }

        private static void RemoveChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }

        private static void MarkDirty()
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
