using System.Text;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// ตั้งค่าเงาให้ BuildingData ทุกหลังพร้อมกัน — แก้อาการ "อาคารลอย"
    ///
    /// ปัญหาของค่าเดิม: ความยาวเงา = squash × **ความสูงสไปรต์** → อาคารยิ่งสูง เงายิ่งทอดยาวลงไกล
    /// จนหลุดออกจากฐาน อ่านเป็นเงาของวัตถุที่ลอยอยู่ (หรือเงาสะท้อนในน้ำ) ไม่ใช่เงาที่ทาบพื้น
    ///
    /// สูตรใหม่: ล็อก "ความยาวเงาจริงในโลก" ให้สัมพันธ์กับ **ความกว้างฐาน** ของอาคารแทน
    ///     shadowLen = Spread × spriteWidth        (เงาแผ่ออกราว 1/3 ของความกว้างอาคาร)
    ///     squash    = shadowLen / spriteHeight    (แปลงกลับเป็นสัดส่วนที่ระบบเงาใช้)
    /// ผลคือ หอสูง (CORE TOWER) ได้เงาสั้นเตี้ยติดฐาน · โรงเรือนเตี้ยกว้างได้เงาแผ่กว้างตามตัว
    /// ทุกหลังจึงดู "นั่งอยู่บนพื้น" เท่ากันหมด
    ///
    /// + ทุบเงาขึ้นเล็กน้อย (TuckUp) ให้หัวเงาซ้อนใต้ฐานอาคาร ปิดรอยต่อที่ทำให้เห็นเป็นช่องว่าง
    /// + เอียงองศาเดียวกันทั้งเมือง (Lean) → อ่านเป็นแสงดวงเดียวส่องทั้งฉาก
    ///
    /// idempotent · รันซ้ำได้ · ปรับรายหลังต่อได้ที่ NuclearReMind → Tools → Shadow Editor
    /// รัน: NuclearReMind/Setup Building Shadows (grounded)
    /// </summary>
    public static class BuildingShadowSetup
    {
        private const string BuildingFolder = "Assets/ScriptableObjects/Buildings";

        // ── ปรับรสนิยมตรงนี้ แล้วรันเมนูใหม่ ──
        private const float Spread   = 0.30f;  // ความยาวเงา เทียบความกว้างสไปรต์ (0.30 = 30%)
        private const float TuckUp   = 0.020f; // ดันเงาขึ้น เทียบความสูงสไปรต์ — ปิดรอยต่อฐาน
        private const float Lean     = -15f;   // องศาเอียง (ลบ = เงาเทไปทางขวา) — ใช้ค่าเดียวทั้งเมือง
        private const float Alpha    = 0.32f;  // ความเข้มเงาอาคารทั่วไป

        // กองแร่/ซากบนพื้น: แบนอยู่แล้ว เงาต้องจางและสั้นกว่า ไม่งั้นดูเป็นหลุมดำ
        private const float OreSpread = 0.16f;
        private const float OreAlpha  = 0.20f;

        private const float MinSquash = 0.08f;
        private const float MaxSquash = 0.50f;

        [MenuItem("NuclearReMind/Setup Building Shadows (grounded)")]
        public static void Apply()
        {
            var guids = AssetDatabase.FindAssets("t:BuildingData", new[] { BuildingFolder });
            var log = new StringBuilder("[BuildingShadowSetup] ตั้งเงาใหม่ (grounded):\n");
            int done = 0, skipped = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
                if (data == null) continue;

                var sprite = data.SpriteForLevel(1) ?? data.sprite;
                if (sprite == null)
                {
                    log.AppendLine($"  – {data.name}: ข้าม (ไม่มีสไปรต์)");
                    skipped++;
                    continue;
                }

                float w = sprite.bounds.size.x;
                float h = sprite.bounds.size.y;
                if (w <= 0.001f || h <= 0.001f)
                {
                    log.AppendLine($"  – {data.name}: ข้าม (ขนาดสไปรต์เป็นศูนย์)");
                    skipped++;
                    continue;
                }

                bool ore = data.isOreNode;
                float spread = ore ? OreSpread : Spread;

                Undo.RecordObject(data, "Setup building shadow");
                data.overrideShadow    = true;
                data.shadowSquash      = Mathf.Clamp(spread * w / h, MinSquash, MaxSquash);
                data.shadowOffset      = new Vector2(0f, TuckUp * h);
                data.shadowLeanDegrees = ore ? 0f : Lean;   // กองแร่ไม่เอียง (ไม่มีตัวตั้งให้เอียง)
                data.shadowAlpha       = ore ? OreAlpha : Alpha;
                EditorUtility.SetDirty(data);

                log.AppendLine($"  ✔ {data.name,-16} sprite {w:0.##}×{h:0.##}  " +
                               $"→ ยาว {data.shadowSquash:0.###} (โลก {spread * w:0.##}) · " +
                               $"ยก {data.shadowOffset.y:0.###} · เอียง {data.shadowLeanDegrees:0}° · α {data.shadowAlpha:0.##}");
                done++;
            }

            AssetDatabase.SaveAssets();
            log.AppendLine($"รวม {done} หลัง (ข้าม {skipped}) — กด Play ดูผล · ปรับรายหลังต่อที่ Tools → Shadow Editor");
            Debug.Log(log.ToString());
        }

        [MenuItem("NuclearReMind/Setup Building Shadows (grounded)", true)]
        private static bool Validate() => AssetDatabase.IsValidFolder(BuildingFolder);
    }
}
