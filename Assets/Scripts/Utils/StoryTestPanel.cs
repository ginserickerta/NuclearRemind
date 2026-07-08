using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// [DEV ONLY] แผงทดสอบระบบเนื้อเรื่อง — force วิกฤตทันที + โชว์ค่า effect state real-time
    /// เพิ่มเข้า scene ผ่านเมนู NuclearReMind/Setup Story Test Panel (dev) แล้วกด Play
    ///   ปุ่ม F1 = Plasma · F2 = โรครังสี · F3 = อาหาร · F4 = Decree · N = จบวัน (ใช้ F-key เลี่ยงชน hotbar วางอาคาร 1-9)
    ///   เปิด/ปิดแผงด้วย ` (backquote/tilde) · ปิดถาวร: uncheck/ลบ GameObject "StoryTestPanel" · build จริงไม่ทำงานอยู่แล้ว (#if UNITY_EDITOR)
    /// logic ทั้งหมดอยู่ใน #if UNITY_EDITOR → ไม่ทำงาน/ไม่กระทบ build จริง (ถ้าลืมถอด GameObject ก็ไม่มีผล)
    /// </summary>
    public class StoryTestPanel : MonoBehaviour
    {
        [Tooltip("วิกฤต story-driven 4 ตัว (ปุ่ม 1-4) — assign อัตโนมัติโดยเมนู Setup Story Test Panel")]
        public DilemmaData[] crises;

#if UNITY_EDITOR
        private string _status = "กด F1-F4 เพื่อ force วิกฤต · N เพื่อจบวัน";
        private bool _visible = true; // ` (backquote) สลับเปิด/ปิดแผง

        private void Update()
        {
            // ` (backquote/tilde) = เปิด/ปิดแผง — คีย์ debug console คลาสสิก (ไม่ชนกับอะไรในเกม)
            if (Input.GetKeyDown(KeyCode.BackQuote)) { _visible = !_visible; return; }
            if (!_visible) return;

            // ใช้ F1-F4 (ไม่ใช่ 1-4) เพราะ PlacementController จอง Alpha1-9 ไว้เป็น hotbar วางอาคาร
            if (Input.GetKeyDown(KeyCode.F1)) Force(0, "Plasma");
            else if (Input.GetKeyDown(KeyCode.F2)) Force(1, "โรครังสี");
            else if (Input.GetKeyDown(KeyCode.F3)) Force(2, "อาหาร");
            else if (Input.GetKeyDown(KeyCode.F4)) Force(3, "Decree");
            else if (Input.GetKeyDown(KeyCode.N))
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.RequestEndDay();
                    _status = "จบวัน → timed effect นับถอยหลัง −1";
                }
            }
        }

        private void Force(int i, string label)
        {
            if (crises == null || i >= crises.Length || crises[i] == null)
            { _status = $"<color=#ff6>ไม่พบ crises[{i}] — รัน Setup Story Test Panel ใหม่</color>"; return; }
            if (EventManager.Instance == null) { _status = "ไม่มี EventManager ใน scene"; return; }

            EventManager.Instance.RaiseDilemmaTriggerRequested(crises[i]);
            _status = $"force วิกฤต: {label} ({crises[i].dilemmaId})";
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                GUI.Label(new Rect(10, 10, 260, 24), "` = เปิด Story Test Panel (dev)");
                return;
            }

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft, fontSize = 13, richText = true,
                padding = new RectOffset(10, 10, 10, 10)
            };

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>[DEV] Story Test Panel</b>");
            sb.AppendLine("F1 Plasma · F2 โรครังสี · F3 อาหาร · F4 Decree · N จบวัน");
            sb.AppendLine("` = ปิดแผง (เปิดใหม่ด้วย ` เหมือนกัน)");
            sb.AppendLine("─────────────────────");

            var ce = CrisisEffectManager.Instance;
            if (ce != null)
            {
                sb.AppendLine($"FoodYield   ×{ce.FoodYieldMultiplier:0.00}");
                sb.AppendLine($"SpoilRate   {ce.FoodSpoilRatePerDay:0.00}/วัน");
                sb.AppendLine($"Efficiency  ×{ce.WorkerEfficiencyMultiplier:0.00}");
                sb.AppendLine($"BusyWorkers {ce.BusyWorkers}");
            }
            else sb.AppendLine("<color=#ff6>ไม่มี CrisisEffectManager — รัน Setup Crisis Effects!</color>");

            var rm = ResourceManager.Instance;
            if (rm != null) sb.AppendLine($"Food {rm.Current.food:0} · Energy {rm.Current.energy:0} · Water {rm.Current.water:0}");
            var pm = PopulationManager.Instance;
            if (pm != null) sb.AppendLine($"Hope {pm.Current.hope:0} · Workers {pm.Current.workers} · Sick {pm.Current.sick}");
            var rad = RadiationManager.Instance;
            if (rad != null) sb.AppendLine($"Exposure {rad.CurrentExposure:0.0}");
            var ct = CoreTowerManager.Instance;
            if (ct != null) sb.AppendLine($"HEAT {ct.Current.coreHeat:0} · CORE% {ct.Current.corePercent:0}");
            var gm = GameManager.Instance;
            if (gm != null) sb.AppendLine($"Day {gm.CurrentDay}/{GameManager.MaxDay}");

            sb.AppendLine("─────────────────────");
            sb.AppendLine(_status);

            GUI.Label(new Rect(10, 10, 330, 350), sb.ToString(), style);
        }
#endif
    }
}
