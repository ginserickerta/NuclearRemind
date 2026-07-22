// ★ EDITOR-ONLY debug tool — the whole file compiles out of real builds (never ships to WebGL).
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NuclearReMind
{
    /// <summary>
    /// แผงทดสอบ "การ์ดบันทึก" โดยเฉพาะ — เด้งการ์ดใบไหนก็ได้ทันทีโดยไม่ต้องเล่นไปถึงหมุดกู้บันทึกจริง
    /// ทดสอบได้ทั้ง 2 ที่ที่การ์ดโผล่:
    ///   • ป๊อปอัพตอนกู้ได้  → ปุ่ม "เด้งป๊อปอัพ" (RaiseStoryRecordShown → RecordCardUI)
    ///   • แผงย้อนอ่าน       → ปุ่ม "ดูย้อนอ่าน" (เปิด RecordsPanel + ShowRecord)
    ///
    /// โหลด RecordCardSO ทุกใบจาก Resources/Records (แหล่งเดียวกับ DataRecovery) + เช็คว่ามีสกินรูป
    /// สำเร็จรูป (Resources/StoryUI/RecordCards/&lt;recordId&gt;) ไหม → โชว์ ✓/✗ ใช้เป็น wiring-check ไปในตัว
    ///
    /// Auto-spawn ตอน Play (editor เท่านั้น) · กด F9 เปิด/ปิดแผง · หน้าต่างลากย้ายได้
    /// วาดเฉพาะตอนอยู่ในเกม (EventManager พร้อม) → ไม่โผล่ค้างในเมนูหลัก
    /// </summary>
    public class RecordCardTester : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.F8;

        private static RecordCardTester _instance;

        private readonly List<RecordCardSO> _records = new List<RecordCardSO>();
        private bool _visible = false; // ★ 2026-07-22: hidden until F8 — no more auto-popup at game start
        private Rect _window = new Rect(16, 16, 340, 400);
        private Vector2 _scroll;

        // spawn ครั้งแรก + ทุกครั้งที่โหลดฉากใหม่ (ป๊อปอัพ/แผงถูกสร้างใหม่ต่อฉาก → tester ต้องตามไปด้วย)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            EnsureInstance();
            SceneManager.sceneLoaded += (_, __) => EnsureInstance();
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("[RecordCardTester]");
            _instance = go.AddComponent<RecordCardTester>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            ReloadRecords();
        }

        private void ReloadRecords()
        {
            _records.Clear();
            var loaded = Resources.LoadAll<RecordCardSO>("Records");
            if (loaded != null) _records.AddRange(loaded);
            // เรียงตาม recordId ให้ลำดับคงที่ (record_01, record_02, record_03, record_final)
            _records.Sort((a, b) => string.CompareOrdinal(
                a != null ? a.recordId : "", b != null ? b.recordId : ""));
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey)) _visible = !_visible;
        }

        private void OnGUI()
        {
            // วาดเฉพาะตอนอยู่ในเกมจริง (มี EventManager) — กันโผล่ค้างในเมนูหลัก
            if (!_visible || EventManager.Instance == null) return;
            _window = GUILayout.Window(GetInstanceID(), _window, DrawWindow, "🧪 ทดสอบการ์ดบันทึก (F8 ซ่อน)");
        }

        private void DrawWindow(int id)
        {
            if (_records.Count == 0)
            {
                GUILayout.Label("ไม่พบ RecordCardSO ใน Resources/Records");
                if (GUILayout.Button("โหลดใหม่")) ReloadRecords();
                GUI.DragWindow();
                return;
            }

            GUILayout.Label($"พบบันทึก {_records.Count} ใบ · ✓ = มีสกินรูปสำเร็จรูป");
            _scroll = GUILayout.BeginScrollView(_scroll);

            foreach (var rec in _records)
            {
                if (rec == null) continue;

                bool hasSprite = Resources.Load<Sprite>("StoryUI/RecordCards/" + rec.recordId) != null;
                string title = string.IsNullOrEmpty(rec.archiveTitle) ? rec.recordId : rec.archiveTitle;

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{(hasSprite ? "✓" : "✗")}  {title}");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("เด้งป๊อปอัพ")) PopCard(rec);
                if (GUILayout.Button("ดูย้อนอ่าน")) ShowInPanel(rec);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();

            GUILayout.Space(4);
            if (GUILayout.Button("เปิด/ปิด แผง Records")) TogglePanel();
            if (GUILayout.Button("โหลดรายการใหม่")) ReloadRecords();

            GUI.DragWindow(); // ลากหัวหน้าต่างย้ายได้
        }

        // เด้งป๊อปอัพการ์ด (เหมือนตอน DataRecovery กู้บันทึกได้จริง)
        private static void PopCard(RecordCardSO rec)
        {
            if (EventManager.Instance != null) EventManager.Instance.RaiseStoryRecordShown(rec);
        }

        // เปิดแผงย้อนอ่านแล้วโชว์รายละเอียดใบนี้ (พรีวิวรูปในแผงโดยไม่ต้องกู้/archive จริง)
        private static void ShowInPanel(RecordCardSO rec)
        {
            var rc = RecordsPanelController.Instance;
            if (rc == null) return;
            if (!rc.IsOpen) rc.Toggle();
            rc.ShowRecord(rec);
        }

        private static void TogglePanel()
        {
            RecordsPanelController.Instance?.Toggle();
        }
    }
}
#endif
