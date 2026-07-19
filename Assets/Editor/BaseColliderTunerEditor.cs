using UnityEditor;
using UnityEngine;
using NuclearReMind;
using Box = NuclearReMind.BaseColliderTuner.TunerBox;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Custom editor สำหรับ BaseColliderTuner — ลากกล่อง depth-sort ของอาคารใน Scene view
    /// เขียนค่าลง BuildingData โดยตรง (พิกัด local สไปรต์ · สเกลปกติ)
    /// เมนู NuclearReMind → Tools → Base Collider Editor สร้าง/เลือกตัว tuner ให้
    ///
    /// แก้ได้ 3 กล่อง สลับด้วยปุ่มบนสุด · กล่องที่ไม่ได้แก้วาดเป็นเส้นจาง ให้เห็นว่าซ้อนกันยังไง
    /// </summary>
    [CustomEditor(typeof(BaseColliderTuner))]
    public class BaseColliderTunerEditor : UnityEditor.Editor
    {
        private static readonly Color CBuilding = new Color(0.30f, 1.00f, 0.40f); // เขียว — อาคารทับอาคาร
        private static readonly Color CWorkerBase = new Color(0.35f, 0.70f, 1.00f); // ฟ้า — พื้นที่ "ยืนหน้า"
        private static readonly Color CWorkerTop = new Color(1.00f, 0.60f, 0.25f); // ส้ม — พื้นที่ "ยืนหลัง"

        [MenuItem("NuclearReMind/Tools/Base Collider Editor")]
        public static void Open()
        {
            var go = new GameObject("BaseColliderTuner (ลบทิ้งได้เมื่อเสร็จ)",
                                    typeof(SpriteRenderer), typeof(BaseColliderTuner));
            go.transform.position = Vector3.zero;
            var tuner = go.GetComponent<BaseColliderTuner>();
            if (Selection.activeObject is BuildingData bd) tuner.data = bd;   // เลือก asset อยู่ → auto assign
            Undo.RegisterCreatedObjectUndo(go, "Base Collider Editor");
            Selection.activeGameObject = go;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var t = (BaseColliderTuner)target;
            EditorGUILayout.Space();
            if (t.data == null)
            {
                EditorGUILayout.HelpBox("assign BuildingData ในช่อง Data ก่อน แล้วลากกล่องใน Scene view", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("กล่องที่กำลังแก้", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawModeButton(t, Box.BuildingBase, "อาคาร (เขียว)");
                DrawModeButton(t, Box.WorkerBase, "worker ฐาน (ฟ้า)");
                DrawModeButton(t, Box.WorkerTop, "worker บน (ส้ม)");
            }

            EditorGUILayout.HelpBox(Explain(t.box), MessageType.Info);

            Get(t.data, t.box, out var size, out var offset, out bool over);
            EditorGUILayout.LabelField("override", over ? "ON (ใช้กล่องนี้)" : "OFF (auto)");
            EditorGUILayout.Vector2Field("size", size);
            EditorGUILayout.Vector2Field("offset", offset);

            if (GUILayout.Button("รีเซ็ตกล่องนี้ = อัตโนมัติ"))
            {
                Undo.RecordObject(t.data, "Reset collider");
                InitFromAuto(t.data, t.box);
                EditorUtility.SetDirty(t.data);
            }
            if (GUILayout.Button("ปิด override ของชุดนี้ (กลับไปใช้ auto)"))
            {
                Undo.RecordObject(t.data, "Disable collider override");
                if (t.box == Box.BuildingBase) t.data.overrideBaseCollider = false;
                else t.data.overrideWorkerCollider = false;
                EditorUtility.SetDirty(t.data);
            }
        }

        private static void DrawModeButton(BaseColliderTuner t, Box mode, string label)
        {
            bool active = t.box == mode;
            var prev = GUI.backgroundColor;
            if (active) GUI.backgroundColor = ColorOf(mode);
            if (GUILayout.Button(label) && !active)
            {
                Undo.RecordObject(t, "Switch tuner box");
                t.box = mode;
                EditorUtility.SetDirty(t);
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = prev;
        }

        private static string Explain(Box b)
        {
            switch (b)
            {
                case Box.BuildingBase:
                    return "กล่องเดิม — ตัดสิน 'อาคารไหนวาดทับอาคารไหน' เท่านั้น จูนไว้แล้ว ไม่เกี่ยวกับ worker";
                case Box.WorkerBase:
                    return "แถบพื้นของ worker — เท้าต่ำกว่า 'กึ่งกลาง' แถบนี้ = ยืนหน้าอาคาร → วาดทับอาคาร\n" +
                           "★ เตี้ยไว้ให้แนบพื้น กล่องยิ่งสูง กึ่งกลางยิ่งลอย คนที่ยืนหลังจะถูกยกมาข้างหน้าจนดูลอย";
                case Box.WorkerTop:
                    return "โซน 'ยืนตรงนี้ = อยู่หลังอาคาร' — เช็คจากจุดที่เท้าเหยียบ ถ้าอยู่ในกล่องนี้จะไม่ถูกยกขึ้นมาเลย\n" +
                           "ใช้คุมความกว้าง เช่น ตึกที่ยอดยื่นคร่อมออกไป คนที่ยืนใต้ชายคาต้องอยู่หลัง";
                default: return "";
            }
        }

        private static Color ColorOf(Box b)
            => b == Box.BuildingBase ? CBuilding : b == Box.WorkerBase ? CWorkerBase : CWorkerTop;

        // ── อ่าน/เขียนค่าตามกล่องที่เลือก (ที่เดียว — ไม่ให้ mapping หลุดกันระหว่าง inspector กับ scene) ──
        private static void Get(BuildingData d, Box b, out Vector2 size, out Vector2 offset, out bool over)
        {
            switch (b)
            {
                case Box.BuildingBase:
                    size = d.baseColliderSize; offset = d.baseColliderOffset; over = d.overrideBaseCollider; break;
                case Box.WorkerBase:
                    size = d.workerBaseColliderSize; offset = d.workerBaseColliderOffset; over = d.overrideWorkerCollider; break;
                default:
                    size = d.workerTopColliderSize; offset = d.workerTopColliderOffset; over = d.overrideWorkerCollider; break;
            }
        }

        private static void Set(BuildingData d, Box b, Vector2 size, Vector2 offset)
        {
            switch (b)
            {
                case Box.BuildingBase:
                    d.overrideBaseCollider = true;
                    d.baseColliderSize = size; d.baseColliderOffset = offset; break;
                case Box.WorkerBase:
                    d.overrideWorkerCollider = true;
                    d.workerBaseColliderSize = size; d.workerBaseColliderOffset = offset; break;
                default:
                    d.overrideWorkerCollider = true;
                    d.workerTopColliderSize = size; d.workerTopColliderOffset = offset; break;
            }
        }

        private void OnSceneGUI()
        {
            var t = (BaseColliderTuner)target;
            if (t.data == null || t.data.sprite == null) return;
            var data = t.data;
            Vector3 origin = t.transform.position;

            // ครั้งแรกของกล่องที่เลือก (ยังไม่ override / size ว่าง) → ตั้งจาก auto ให้มีอะไรให้ลาก
            Get(data, t.box, out var size, out var offset, out bool over);
            if (!over || size == Vector2.zero)
            {
                Undo.RecordObject(data, "Init collider");
                InitFromAuto(data, t.box);
                EditorUtility.SetDirty(data);
                Get(data, t.box, out size, out offset, out over);
            }

            // กล่องที่ไม่ได้แก้ — เส้นจาง ๆ ให้เห็นว่าซ้อนกันยังไง (worker base เตี้ยแค่ไหนเทียบกับฐานอาคาร)
            foreach (Box other in new[] { Box.BuildingBase, Box.WorkerBase, Box.WorkerTop })
            {
                if (other == t.box) continue;
                Get(data, other, out var os, out var oo, out bool oOver);
                if (!oOver || os == Vector2.zero) continue;
                DrawBox(origin, os, oo, ColorOf(other), 0.03f, 0.35f);
            }

            DrawBox(origin, size, offset, ColorOf(t.box), 0.12f, 1f);

            Vector3 c = origin + new Vector3(offset.x, offset.y, 0f);
            float left = c.x - size.x * 0.5f, right = c.x + size.x * 0.5f;
            float bottom = c.y - size.y * 0.5f, top = c.y + size.y * 0.5f;

            float hs = HandleUtility.GetHandleSize(c) * 0.12f;
            Handles.color = ColorOf(t.box);

            EditorGUI.BeginChangeCheck();
            float nRight  = Handles.Slider(new Vector3(right, c.y), Vector3.right, hs, Handles.DotHandleCap, 0f).x;
            float nLeft   = Handles.Slider(new Vector3(left,  c.y), Vector3.right, hs, Handles.DotHandleCap, 0f).x;
            float nTop    = Handles.Slider(new Vector3(c.x, top),    Vector3.up,    hs, Handles.DotHandleCap, 0f).y;
            float nBottom = Handles.Slider(new Vector3(c.x, bottom), Vector3.up,    hs, Handles.DotHandleCap, 0f).y;
            Vector3 nC    = Handles.FreeMoveHandle(c, hs * 1.3f, Vector3.zero, Handles.CircleHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(data, "Edit collider");
                Vector3 d = nC - c;                       // ลากกึ่งกลาง = ย้ายทั้งกล่อง
                nLeft += d.x; nRight += d.x; nBottom += d.y; nTop += d.y;

                float newW = Mathf.Max(0.02f, nRight - nLeft);
                float newH = Mathf.Max(0.02f, nTop - nBottom);
                Vector2 newCenter = new Vector2((nLeft + nRight) * 0.5f, (nBottom + nTop) * 0.5f);

                Set(data, t.box, new Vector2(newW, newH),
                    new Vector2(newCenter.x - origin.x, newCenter.y - origin.y));
                EditorUtility.SetDirty(data);
            }

            Handles.Label(new Vector3(left, top + hs * 2f), $"{t.box} — {data.buildingName}");
        }

        private static void DrawBox(Vector3 origin, Vector2 size, Vector2 offset, Color c, float fill, float line)
        {
            Vector3 ctr = origin + new Vector3(offset.x, offset.y, 0f);
            float l = ctr.x - size.x * 0.5f, r = ctr.x + size.x * 0.5f;
            float b = ctr.y - size.y * 0.5f, tp = ctr.y + size.y * 0.5f;
            Vector3[] verts = { new Vector3(l, b), new Vector3(r, b), new Vector3(r, tp), new Vector3(l, tp) };
            Handles.DrawSolidRectangleWithOutline(verts,
                new Color(c.r, c.g, c.b, fill), new Color(c.r, c.g, c.b, line));
        }

        private static void InitFromAuto(BuildingData data, Box box)
        {
            var b = data.sprite.bounds;
            switch (box)
            {
                case Box.BuildingBase:
                {
                    float h = b.size.y * 0.3f;
                    data.overrideBaseCollider = true;
                    data.baseColliderSize = new Vector2(b.size.x, h);
                    data.baseColliderOffset = new Vector2(b.center.x, b.min.y + h * 0.5f);
                    break;
                }
                case Box.WorkerBase:
                {
                    float h = b.size.y * BuildingDepthSort.DefaultWorkerBaseFraction;
                    data.overrideWorkerCollider = true;
                    data.workerBaseColliderSize = new Vector2(b.size.x, h);
                    data.workerBaseColliderOffset = new Vector2(b.center.x, b.min.y + h * 0.5f);
                    break;
                }
                default:
                {
                    // ต่อจากขอบบนของ worker base ขึ้นไปจนสุดสไปรต์ (ถ้ายังไม่มี base ให้ใช้สัดส่วนเริ่มต้น)
                    float baseH = data.workerBaseColliderSize != Vector2.zero
                        ? data.workerBaseColliderOffset.y + data.workerBaseColliderSize.y * 0.5f
                        : b.min.y + b.size.y * BuildingDepthSort.DefaultWorkerBaseFraction;
                    float h = Mathf.Max(0.02f, b.max.y - baseH);
                    data.overrideWorkerCollider = true;
                    data.workerTopColliderSize = new Vector2(b.size.x, h);
                    data.workerTopColliderOffset = new Vector2(b.center.x, baseH + h * 0.5f);
                    break;
                }
            }
        }
    }
}
