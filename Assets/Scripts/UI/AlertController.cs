using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// A5 — Alert/Notification popup: เด้งข้อความเตือนมุมจอเมื่อทรัพยากรวิกฤต/หมด หรือเกิดวิกฤต (dilemma)
    /// auto-dismiss ใน displayDuration วินาที (real-time) — กันสแปมด้วย active-key debounce
    /// (alert ชนิดเดียวกันจะไม่เด้งซ้ำระหว่างยังค้างจออยู่ ; tick ของ ResourceManager ยิง event ทุก 5 วิ)
    /// ฟังผ่าน EventManager เท่านั้น ไม่ reference manager อื่นตรง
    /// </summary>
    public class AlertController : MonoBehaviour
    {
        public static AlertController Instance { get; private set; }

        [Header("UI")]
        [Tooltip("parent ของ alert entries (มี VerticalLayoutGroup) — ผูกโดย HUDCanvasSetup")]
        public RectTransform alertContainer;
        public Font font;

        [Header("Behaviour")]
        public float displayDuration = 5f;

        // debounce: alert ที่กำลังแสดงอยู่ (key) — กันเด้งซ้ำจนกว่าจะ dismiss
        private readonly HashSet<string> _activeKeys = new HashSet<string>();

        /// <summary>จำนวน alert ที่ยัง active (ใช้ตรวจสอบ/ทดสอบ)</summary>
        public int ActiveAlertCount => _activeKeys.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnResourceCritical += HandleResourceCritical;
            EventManager.Instance.OnResourceDepleted += HandleResourceDepleted;
            EventManager.Instance.OnDilemmaTriggered += HandleDilemmaTriggered;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnResourceCritical -= HandleResourceCritical;
            EventManager.Instance.OnResourceDepleted -= HandleResourceDepleted;
            EventManager.Instance.OnDilemmaTriggered -= HandleDilemmaTriggered;
        }

        // ───────────────────────────── Event handlers ─────────────────────────────

        private void HandleResourceCritical(ResourceType type)
        {
            var (key, msg) = BuildResourceAlert(type, depleted: false);
            TryShow(key, msg);
        }

        private void HandleResourceDepleted(ResourceType type)
        {
            var (key, msg) = BuildResourceAlert(type, depleted: true);
            TryShow(key, msg);
        }

        private void HandleDilemmaTriggered(DilemmaData data)
        {
            string id = data != null ? data.dilemmaId : "unknown";
            TryShow($"crisis:{id}", "⚠ เกิดสถานการณ์วิกฤต — ต้องตัดสินใจ");
        }

        // ───────────────────────────── Core ─────────────────────────────

        /// <summary>สร้าง (key, ข้อความ) ของ alert ทรัพยากร — แยก static ให้ทดสอบง่าย</summary>
        public static (string key, string message) BuildResourceAlert(ResourceType type, bool depleted)
        {
            string label = type switch
            {
                ResourceType.Food                => "\U0001F33F อาหาร",
                ResourceType.Water               => "\U0001F4A7 น้ำ",
                ResourceType.Energy              => "⚡ พลังงาน",
                ResourceType.RadiationProtection => "☢ การป้องกันรังสี",
                ResourceType.Workers             => "\U0001F477 คนงาน",
                ResourceType.ResearchPoints      => "\U0001F9EA แต้มวิจัย",
                _                                => type.ToString(),
            };
            string suffix = depleted ? "หมดคลังแล้ว!" : "ใกล้หมด";
            string key = (depleted ? "depleted:" : "critical:") + type;
            return (key, $"{label}{suffix}");
        }

        private bool TryShow(string key, string message)
        {
            if (_activeKeys.Contains(key)) return false; // กำลังแสดงอยู่ — กันซ้ำ
            _activeKeys.Add(key);
            SpawnEntry(message, key);
            return true;
        }

        private void SpawnEntry(string message, string key)
        {
            if (alertContainer != null)
            {
                var go = BuildEntry(message);
                if (Application.isPlaying)
                    StartCoroutine(DismissAfter(go, key));
            }
            else
            {
                Debug.LogWarning("[AlertController] alertContainer ยังไม่ถูกผูก — แสดง alert ไม่ได้");
            }
        }

        private static readonly Color WarningBorder = new Color(0.9647f, 0.6784f, 0.3333f, 1f); // #F6AD55
        private static readonly Color BoxColor      = new Color(0.1f, 0.1f, 0.12f, 0.92f);     // dark box

        private GameObject BuildEntry(string message)
        {
            // outer = ขอบสี warning #F6AD55
            var go = new GameObject("Alert", typeof(RectTransform));
            go.transform.SetParent(alertContainer, false);

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 32f;

            var border = go.AddComponent<Image>();
            border.color = WarningBorder;

            // inner = กล่องดำ เว้นขอบ 2px ให้เห็นเส้น warning
            var boxGO = new GameObject("Box", typeof(RectTransform));
            boxGO.transform.SetParent(go.transform, false);
            var boxRect = boxGO.GetComponent<RectTransform>();
            boxRect.anchorMin = Vector2.zero;
            boxRect.anchorMax = Vector2.one;
            boxRect.offsetMin = new Vector2(2, 2);
            boxRect.offsetMax = new Vector2(-2, -2);
            var box = boxGO.AddComponent<Image>();
            box.color = BoxColor;

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(boxGO.transform, false);
            var tr = textGO.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(10, 4);
            tr.offsetMax = new Vector2(-10, -4);

            var text = textGO.AddComponent<Text>();
            text.font = font;
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = message;

            return go;
        }

        private IEnumerator DismissAfter(GameObject go, string key)
        {
            yield return new WaitForSecondsRealtime(displayDuration); // real-time → ไม่ขึ้นกับ pause/ความเร็ว
            _activeKeys.Remove(key);
            if (go != null) Destroy(go);
        }
    }
}
