using UnityEditor;
using UnityEngine;
using NuclearReMind;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: เครื่องมือ Editor จูนเงาอาคารด้วยการลาก handle ใน Scene view แล้วบันทึกค่าลง asset
    /// Custom editor สำหรับ ShadowTuner — ลาก "เงา" ของอาคารใน Scene view แล้วค่าเขียนลง BuildingData
    /// (shadowOffset / shadowSquash / shadowLeanDegrees / shadowAlpha + overrideShadow = true)
    ///
    /// handle ที่ลากได้:
    ///   • วงกลม (ปลายเงา) = ลากย้ายตำแหน่ง + ยืด/หด + เอียง พร้อมกันในทีเดียว
    ///   • จุดสี่เหลี่ยม (ฐานเงา) = เลื่อนจุดเริ่มเงา (offset) อย่างเดียว
    /// เมนู NuclearReMind → Tools → Shadow Editor สร้าง/เลือกตัว tuner ให้
    /// </summary>
    [CustomEditor(typeof(ShadowTuner))]
    public class ShadowTunerEditor : UnityEditor.Editor
    {
        private static readonly Color CShadow = new Color(0.35f, 0.75f, 1f, 1f);

        // เมนูนี้: สร้าง GameObject ShadowTuner ชั่วคราวในซีนสำหรับลากจูนเงาอาคาร
        [MenuItem("NuclearReMind/Tools/Shadow Editor")]
        public static void Open()
        {
            var go = new GameObject("ShadowTuner (ลบทิ้งได้เมื่อเสร็จ)",
                                    typeof(SpriteRenderer), typeof(ShadowTuner));
            go.transform.position = Vector3.zero;
            var tuner = go.GetComponent<ShadowTuner>();
            if (Selection.activeObject is BuildingData bd) tuner.data = bd;   // เลือก asset อยู่ → auto assign
            Undo.RegisterCreatedObjectUndo(go, "Shadow Editor");
            Selection.activeGameObject = go;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var t = (ShadowTuner)target;
            EditorGUILayout.Space();

            if (t.data == null)
            {
                EditorGUILayout.HelpBox("ยังไม่ได้เลือกอาคาร — ลาก asset จาก Assets/ScriptableObjects/Buildings/\n" +
                                        "มาใส่ช่อง 'Data' ด้านบน (เช่น PowerPlant.asset) แล้วอาคารจะโผล่ใน Scene view",
                                        MessageType.Warning);
                if (GUILayout.Button("ใช้อาคารที่เลือกอยู่ใน Project window"))
                {
                    if (Selection.activeObject is BuildingData sel)
                    {
                        Undo.RecordObject(t, "Assign building");
                        t.data = sel;
                        EditorUtility.SetDirty(t);
                    }
                    else Debug.LogWarning("[ShadowTuner] เลือกไฟล์ BuildingData (.asset) ใน Project window ก่อน");
                }
                return;
            }

            if (t.PreviewSprite == null)
            {
                EditorGUILayout.HelpBox($"'{t.data.buildingName}' ไม่มีสไปรต์ (ช่อง sprite / levelSprites ว่าง) — " +
                                        "ปรับเงาไม่ได้เพราะเงาใช้รูปสไปรต์ของอาคารเอง", MessageType.Warning);
                return;
            }

            var data = t.data;
            EditorGUILayout.HelpBox("ลาก 'วงกลมปลายเงา' = ย้าย/ยืด/เอียง · 'จุดฐาน' = เลื่อน offset\n" +
                                    "ค่าเขียนลง BuildingData อัตโนมัติ · กด Ctrl+S เซฟ asset · กด Play ดูผลจริง",
                                    MessageType.Info);

            EditorGUILayout.LabelField("override", data.overrideShadow ? "ON (ใช้ค่าเงานี้)" : "OFF (ใช้ค่ากลาง)");
            EditorGUILayout.Vector2Field("offset", data.shadowOffset);
            EditorGUILayout.LabelField("squash (ความยาว)", data.shadowSquash.ToString("0.###"));
            EditorGUILayout.LabelField("lean (องศา)", data.shadowLeanDegrees.ToString("0.#"));
            EditorGUILayout.LabelField("alpha", data.shadowAlpha.ToString("0.##"));

            EditorGUILayout.Space();
            if (GUILayout.Button("รีเซ็ตเป็นค่ามาตรฐาน (offset 0,−0.02 · squash 0.42 · lean 0)"))
            {
                Undo.RecordObject(data, "Reset shadow");
                data.overrideShadow = true;
                data.shadowOffset = new Vector2(0f, -0.02f);
                data.shadowSquash = 0.42f;
                data.shadowLeanDegrees = 0f;
                data.shadowAlpha = 0.30f;
                EditorUtility.SetDirty(data);
            }
            if (GUILayout.Button("ปิด override (กลับไปใช้ค่ากลางของ BuildingVisualSpawner)"))
            {
                Undo.RecordObject(data, "Disable shadow override");
                data.overrideShadow = false;
                EditorUtility.SetDirty(data);
            }
        }

        private void OnSceneGUI()
        {
            var t = (ShadowTuner)target;
            var sprite = t.PreviewSprite;
            if (t.data == null || sprite == null) return;
            var data = t.data;

            Vector3 origin = t.transform.position;
            float spriteH = Mathf.Max(0.001f, sprite.bounds.size.y);

            // ฐานเงา = origin + offset · ปลายเงา = ฐาน + เวกเตอร์ยาว (squash × ความสูงสไปรต์) หมุนตาม lean
            Vector3 baseP = origin + new Vector3(data.shadowOffset.x, data.shadowOffset.y, 0f);
            float len = data.shadowSquash * spriteH;
            Vector3 dir = Quaternion.Euler(0f, 0f, data.shadowLeanDegrees) * Vector3.down;
            Vector3 tipP = baseP + dir * len;

            Handles.color = CShadow;
            Handles.DrawAAPolyLine(4f, baseP, tipP);
            Handles.DrawWireDisc(baseP, Vector3.forward, HandleUtility.GetHandleSize(baseP) * 0.06f);

            float hs = HandleUtility.GetHandleSize(tipP) * 0.12f;

            EditorGUI.BeginChangeCheck();
            Vector3 newTip  = Handles.FreeMoveHandle(tipP,  hs * 1.4f, Vector3.zero, Handles.CircleHandleCap);
            Vector3 newBase = Handles.FreeMoveHandle(baseP, hs,        Vector3.zero, Handles.RectangleHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(data, "Edit shadow");
                data.overrideShadow = true;

                // ลากฐาน → ย้าย offset (ปลายเงาเลื่อนตามไปเอง เพราะคิดจากฐาน)
                if (newBase != baseP)
                {
                    Vector3 d = newBase - baseP;
                    data.shadowOffset += new Vector2(d.x, d.y);
                    baseP = newBase;
                }

                // ลากปลาย → ความยาว (squash) + มุมเอียง (lean) จากเวกเตอร์ ฐาน→ปลาย
                if (newTip != tipP)
                {
                    Vector3 v = newTip - baseP;
                    float newLen = v.magnitude;
                    data.shadowSquash = Mathf.Clamp(newLen / spriteH, 0.05f, 1.2f);

                    // มุมเทียบแกน "ลง" (0° = เงาทอดตรงลงล่างเหมือนเดิม)
                    float ang = Vector2.SignedAngle(Vector2.down, new Vector2(v.x, v.y));
                    data.shadowLeanDegrees = Mathf.Clamp(ang, -70f, 70f);
                }

                EditorUtility.SetDirty(data);
            }

            Handles.Label(tipP + Vector3.up * hs * 2f,
                          $"เงา — {data.buildingName}  (ยาว {data.shadowSquash:0.##} · เอียง {data.shadowLeanDegrees:0}°)");
        }
    }
}
