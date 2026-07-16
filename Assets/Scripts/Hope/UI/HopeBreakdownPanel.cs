using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Hope breakdown panel (GDD §18 UI — mandatory). Shows the current hope value,
    /// the last committed day's entries split into losses/gains, and a 7-day trend.
    ///
    ///   HOPE 62 ▼ −6
    ///   ✕ คนหิว 4 คน   −8     ✓ CORE% +5    +8
    ///   รวม −6 · แนวโน้ม 7 วัน ▃▄▄▅▄▃▂
    ///
    /// Reads from WorkerManager.Instance.Hope (read-only query — allowed exception).
    /// Wire in scene: headerText + list container + a disabled Text row template.
    /// </summary>
    public class HopeBreakdownPanel : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private GameObject panelRoot;      // toggled by the HUD hope button
        [SerializeField] private Text headerText;           // "HOPE 62 ▼ −6"
        [SerializeField] private Transform rowContainer;    // vertical layout group
        [SerializeField] private Text rowTemplate;          // disabled template row
        [SerializeField] private Text trendText;            // "รวม −6 · แนวโน้ม 7 วัน ▃▄▄▅▄▃▂"

        [Header("Colors")]
        [SerializeField] private Color gainColor = new Color(0.45f, 0.85f, 0.45f);
        [SerializeField] private Color lossColor = new Color(0.95f, 0.45f, 0.40f);

        private static readonly char[] TrendBlocks = { '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };
        private readonly List<Text> _rows = new List<Text>();

        private void OnEnable()
        {
            if (WorkerManager.Instance != null)
            {
                WorkerManager.Instance.OnWorkersChanged += Refresh;
                Refresh();
            }
        }

        private void OnDisable()
        {
            if (WorkerManager.Instance != null)
                WorkerManager.Instance.OnWorkersChanged -= Refresh;
        }

        /// <summary>HUD hope button hooks this (onClick) — panel is closed by default.</summary>
        public void TogglePanel()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(!panelRoot.activeSelf);
            if (panelRoot.activeSelf) Refresh();
        }

        public void Refresh()
        {
            var wm = WorkerManager.Instance;
            if (wm == null || wm.Hope == null) return;
            var ledger = wm.Hope;

            if (headerText != null)
            {
                float d = ledger.LastDelta;
                string arrow = d > 0.01f ? "▲" : d < -0.01f ? "▼" : "—";
                headerText.text = $"HOPE {ledger.Current:0} {arrow} {FormatDelta(d)}";
            }

            RebuildRows(ledger.GetCommittedBreakdown());

            if (trendText != null)
                trendText.text = $"รวม {FormatDelta(ledger.LastDelta)} · แนวโน้ม 7 วัน {BuildTrend(ledger.History)}";
        }

        private void RebuildRows(IReadOnlyList<HopeEntry> entries)
        {
            if (rowContainer == null || rowTemplate == null) return;

            // grow the row pool as needed, hide leftovers
            while (_rows.Count < entries.Count)
            {
                var row = Instantiate(rowTemplate, rowContainer);
                _rows.Add(row);
            }
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].gameObject.SetActive(i < entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                bool gain = e.value > 0f;
                _rows[i].text = $"{(gain ? "✓" : "✕")} {e.text}   {FormatDelta(e.value)}";
                _rows[i].color = gain ? gainColor : lossColor;
            }
        }

        /// <summary>Last 7 committed days as block glyphs (0-100 → ▁-█). Static for tests.</summary>
        public static string BuildTrend(IReadOnlyList<float> history)
        {
            if (history == null || history.Count == 0) return "—";
            var sb = new StringBuilder();
            int start = Mathf.Max(0, history.Count - 7);
            for (int i = start; i < history.Count; i++)
            {
                int idx = Mathf.Clamp(Mathf.FloorToInt(history[i] / 100f * TrendBlocks.Length),
                    0, TrendBlocks.Length - 1);
                sb.Append(TrendBlocks[idx]);
            }
            return sb.ToString();
        }

        private static string FormatDelta(float v) => v >= 0f ? $"+{v:0.#}" : $"−{Mathf.Abs(v):0.#}";
    }
}
