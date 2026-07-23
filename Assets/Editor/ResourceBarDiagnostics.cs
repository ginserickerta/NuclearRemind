using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using NuclearReMind;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: เครื่องมือ Editor วินิจฉัยหลอดทรัพยากรบน HUD ว่าทำไมไม่ขยับ พร้อมซ่อม fillRect ที่หลุดให้
    /// ไล่เช็กหลอดทรัพยากรทุกอัน (foodBar…tritiumBar ของ UIManagerHUD) ว่าทำไม fill ไม่ขยับ
    ///   • fillRect ของ Slider หลุด (None) → ซ่อมอัตโนมัติ (หา child "Fill Area/Fill")
    ///   • Fill Image หาย/ถูกปิด/alpha 0
    ///   • ช่อง fillImage ใน HUD ชี้ผิดตัว (ไม่ตรงกับ Fill จริง)
    ///   • มี sibling วาดทับ Fill (กรอบ/พื้นหลังที่ตรงกลางทึบ)
    /// รายงานลง Console + สรุปใน dialog · ซ่อมเฉพาะ fillRect ที่หลุด (ไม่แตะงานกรอบที่ทำมือ)
    /// </summary>
    public static class ResourceBarDiagnostics
    {
        // เมนูนี้: ตรวจหลอดทรัพยากรทุกหลอดแล้วรายงานลง Console + ซ่อม fillRect ที่หลุดอัตโนมัติ
        [MenuItem("NuclearReMind/HUD/Diagnose Resource Bars")]
        public static void Diagnose()
        {
            var hud = Object.FindFirstObjectByType<UIManagerHUD>();
            if (hud == null)
            {
                EditorUtility.DisplayDialog("Diagnose Resource Bars",
                    "ไม่พบ UIManagerHUD ในซีน — เปิด Gamescene.unity ก่อน", "OK");
                return;
            }

            var bars = new (string name, UIManagerHUD.ResourceBarUI ui)[]
            {
                ("FoodBar", hud.foodBar),
                ("WaterBar", hud.waterBar),
                ("IronBar", hud.ironBar),
                ("EnergyBar", hud.energyBar),
                ("DeuteriumBar", hud.deuteriumBar),
                ("TritiumBar", hud.tritiumBar),
            };

            var sb = new StringBuilder();
            int problems = 0, fixedCount = 0;

            foreach (var (name, ui) in bars)
            {
                sb.Append("• ").Append(name).Append(": ");

                if (ui == null) { sb.AppendLine("❌ ResourceBarUI = null (ยังไม่ wire ใน HUD)"); problems++; continue; }
                if (ui.bar == null) { sb.AppendLine("❌ ช่อง bar (Slider) ว่าง — ลาก Slider มาใส่ใน UIManagerHUD"); problems++; continue; }

                var slider = ui.bar;
                var issues = new StringBuilder();

                // 1) fillRect หลุด → ซ่อม
                if (slider.fillRect == null)
                {
                    var fill = slider.transform.Find("Fill Area/Fill") as RectTransform;
                    if (fill == null)
                    {
                        // เผื่อ Fill ถูกย้าย/เปลี่ยนชื่อ — หา Image ตัวแรกที่ไม่ใช่ตัว root
                        foreach (var img in slider.GetComponentsInChildren<Image>(true))
                            if (img.gameObject != slider.gameObject) { fill = img.rectTransform; break; }
                    }
                    if (fill != null)
                    {
                        Undo.RecordObject(slider, "Fix Slider fillRect");
                        slider.fillRect = fill;
                        EditorUtility.SetDirty(slider);
                        issues.Append("🔧 ซ่อม Fill Rect (หลุด None → ").Append(fill.name).Append(") · ");
                        fixedCount++;
                    }
                    else { issues.Append("❌ Fill Rect = None และหา child Fill ไม่เจอ · "); }
                }

                // 2) Fill Image ที่ Slider ใช้จริง
                Image fillImg = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
                if (slider.fillRect != null && fillImg == null)
                    issues.Append("❌ Fill (").Append(slider.fillRect.name).Append(") ไม่มี component Image · ");
                if (fillImg != null)
                {
                    if (!fillImg.enabled) issues.Append("❌ Fill Image ถูกปิด (enabled=false) · ");
                    if (fillImg.color.a <= 0.02f) issues.Append("❌ Fill Image alpha≈0 (โปร่งใส) · ");
                }

                // 3) ช่อง fillImage ใน HUD ชี้ตรงกับ Fill จริงไหม (SetBar ตั้งสีผ่านช่องนี้)
                if (ui.fillImage == null)
                    issues.Append("⚠ ช่อง fillImage ใน HUD ว่าง (สีหลอดจะไม่อัปเดต) · ");
                else if (fillImg != null && ui.fillImage != fillImg)
                    issues.Append("⚠ ช่อง fillImage ชี้ผิดตัว (ไม่ใช่ Fill จริง → สีไปลงผิด Image) · ");

                // 4) sibling วาดทับ Fill — child ของ root ที่อยู่หลัง "Fill Area" และ Image ตรงกลางทึบ
                int fillAreaIdx = -1;
                var fa = slider.transform.Find("Fill Area");
                if (fa != null) fillAreaIdx = fa.GetSiblingIndex();
                if (fillAreaIdx >= 0)
                {
                    for (int i = fillAreaIdx + 1; i < slider.transform.childCount; i++)
                    {
                        var c = slider.transform.GetChild(i);
                        var cimg = c.GetComponent<Image>();
                        if (cimg != null && cimg.enabled && cimg.color.a > 0.9f && cimg.sprite == null)
                            issues.Append("⚠ '").Append(c.name).Append("' วาดทับ Fill (ทึบ ไม่มี sprite) · ");
                    }
                }

                if (issues.Length == 0)
                {
                    float mv = slider.maxValue, v = slider.value;
                    sb.Append("✅ โครงถูก (fillRect ต่อ, Image เห็น) · value=").Append(v.ToString("0.#"))
                      .Append("/").Append(mv.ToString("0.#"));
                    sb.AppendLine(mv <= 0f ? "  ⚠ maxValue=0 → หลอดคำนวณไม่ได้" : "");
                }
                else { sb.Append(issues.ToString()).AppendLine(); problems++; }
            }

            if (fixedCount > 0)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            string header = $"ตรวจหลอด: มีปัญหา {problems} · ซ่อม fillRect ให้ {fixedCount} อัน\n" +
                            (fixedCount > 0 ? "กด Ctrl+S เพื่อเซฟการซ่อม\n" : "") + "\n";
            Debug.Log("[ResourceBarDiagnostics]\n" + header + sb.ToString());
            EditorUtility.DisplayDialog("Diagnose Resource Bars", header + sb.ToString(), "OK");
        }
    }
}
