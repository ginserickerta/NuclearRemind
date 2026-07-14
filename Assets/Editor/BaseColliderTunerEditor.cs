using UnityEditor;
using UnityEngine;
using NuclearReMind;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Custom editor สำหรับ BaseColliderTuner — ลากกล่องฐาน (base collider) ของอาคารใน Scene view
    /// เขียนค่าลง BuildingData.baseColliderSize/Offset (พิกัด local สไปรต์ · สเกลปกติ) โดยตรง
    /// เมนู NuclearReMind → Tools → Base Collider Editor สร้าง/เลือกตัว tuner ให้
    /// </summary>
    [CustomEditor(typeof(BaseColliderTuner))]
    public class BaseColliderTunerEditor : UnityEditor.Editor
    {
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
                EditorGUILayout.HelpBox("assign BuildingData ในช่อง Data ก่อน แล้วลากกล่องเขียวใน Scene view", MessageType.Info);
                return;
            }
            EditorGUILayout.HelpBox("ลากขอบ/กึ่งกลาง 'กล่องเขียว' ใน Scene view เพื่อปรับ base collider\n" +
                                    "ค่าเขียนลง BuildingData อัตโนมัติ · กด Ctrl+S เซฟ asset · กด Play ดูผลจริง", MessageType.Info);
            EditorGUILayout.LabelField("override", t.data.overrideBaseCollider ? "ON (ใช้กล่องนี้)" : "OFF (auto)");
            EditorGUILayout.Vector2Field("size", t.data.baseColliderSize);
            EditorGUILayout.Vector2Field("offset", t.data.baseColliderOffset);
            if (GUILayout.Button("รีเซ็ต = อัตโนมัติ (fraction 0.3)"))
            {
                Undo.RecordObject(t.data, "Reset base collider");
                InitFromAuto(t.data);
                EditorUtility.SetDirty(t.data);
            }
            if (GUILayout.Button("ปิด override (กลับไปใช้ auto ทั้งหมด)"))
            {
                Undo.RecordObject(t.data, "Disable base collider override");
                t.data.overrideBaseCollider = false;
                EditorUtility.SetDirty(t.data);
            }
        }

        private void OnSceneGUI()
        {
            var t = (BaseColliderTuner)target;
            if (t.data == null || t.data.sprite == null) return;
            var data = t.data;

            // ครั้งแรก (ยังไม่ override / size ว่าง) → ตั้งค่าเริ่มจาก auto 0.3 ให้มีกล่องให้ลาก
            if (!data.overrideBaseCollider || data.baseColliderSize == Vector2.zero)
            {
                Undo.RecordObject(data, "Init base collider");
                InitFromAuto(data);
                EditorUtility.SetDirty(data);
            }

            Vector3 origin = t.transform.position;
            Vector2 size = data.baseColliderSize;
            Vector2 off  = data.baseColliderOffset;
            Vector3 c = origin + new Vector3(off.x, off.y, 0f);

            float left = c.x - size.x * 0.5f, right = c.x + size.x * 0.5f;
            float bottom = c.y - size.y * 0.5f, top = c.y + size.y * 0.5f;

            // กล่องเขียว
            Vector3[] verts =
            {
                new Vector3(left, bottom), new Vector3(right, bottom),
                new Vector3(right, top),   new Vector3(left, top)
            };
            Handles.DrawSolidRectangleWithOutline(verts, new Color(0.3f, 1f, 0.4f, 0.12f), new Color(0.3f, 1f, 0.4f, 1f));

            float hs = HandleUtility.GetHandleSize(c) * 0.12f;
            Handles.color = new Color(0.3f, 1f, 0.4f, 1f);

            EditorGUI.BeginChangeCheck();
            float nRight  = Handles.Slider(new Vector3(right, c.y), Vector3.right, hs, Handles.DotHandleCap, 0f).x;
            float nLeft   = Handles.Slider(new Vector3(left,  c.y), Vector3.right, hs, Handles.DotHandleCap, 0f).x;
            float nTop    = Handles.Slider(new Vector3(c.x, top),    Vector3.up,    hs, Handles.DotHandleCap, 0f).y;
            float nBottom = Handles.Slider(new Vector3(c.x, bottom), Vector3.up,    hs, Handles.DotHandleCap, 0f).y;
            Vector3 nC    = Handles.FreeMoveHandle(c, hs * 1.3f, Vector3.zero, Handles.CircleHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(data, "Edit base collider");
                Vector3 d = nC - c;                       // ลากกึ่งกลาง = ย้ายทั้งกล่อง
                nLeft += d.x; nRight += d.x; nBottom += d.y; nTop += d.y;

                float newW = Mathf.Max(0.02f, nRight - nLeft);
                float newH = Mathf.Max(0.02f, nTop - nBottom);
                Vector2 newCenter = new Vector2((nLeft + nRight) * 0.5f, (nBottom + nTop) * 0.5f);

                data.overrideBaseCollider = true;
                data.baseColliderSize   = new Vector2(newW, newH);
                data.baseColliderOffset = new Vector2(newCenter.x - origin.x, newCenter.y - origin.y);
                EditorUtility.SetDirty(data);
            }

            Handles.Label(new Vector3(left, top + hs * 2f), $"base collider — {data.buildingName}");
        }

        private static void InitFromAuto(BuildingData data)
        {
            var b = data.sprite.bounds;
            float h = b.size.y * 0.3f;
            data.overrideBaseCollider = true;
            data.baseColliderSize   = new Vector2(b.size.x, h);
            data.baseColliderOffset = new Vector2(b.center.x, b.min.y + h * 0.5f);
        }
    }
}
