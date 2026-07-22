using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PhEngine.ThaiTextCare;

namespace NuclearReMind
{
    /// <summary>
    /// Global Thai vowel/tone-mark de-overlapper (★ 2026-07-22).
    ///
    /// Unity's legacy Text ignores OpenType GPOS/GSUB, so Thai stacks like เชื้อ render the
    /// tone mark on top of the upper vowel. The fix has two halves:
    ///   1. the bundled fonts are patched (scratch script patch_thai_pua.py) so the C90 PUA
    ///      codepoints U+F700-F71A point at the positional variants the typefaces already
    ///      contain (.small = raised tone / dropped under-vowel, .narrow = left-shifted),
    ///      and the DEFAULT tone glyphs are the raised ones;
    ///   2. this sweeper routes every visible string through ThaiTextCare's
    ///      ThaiFontAdjuster.Adjust(FullC90), which substitutes those PUA codepoints in all
    ///      the no-upper-vowel contexts so tones drop back down where they belong.
    ///
    /// One auto-spawned singleton sweeps every uGUI Text and 3D TextMesh in LateUpdate —
    /// no call-site changes anywhere. Cost control: the component list refreshes every
    /// REFRESH_FRAMES frames, and per-frame work is a reference-equality check per label
    /// (strings are interned per assignment), so unchanged labels cost ~nothing. Adjust is
    /// idempotent (PUA output contains no characters the scanner matches), and the swept
    /// result is cached by reference so our own writes never loop.
    ///
    /// Spawns on EVERY scene including the main menu (Thai text lives there too) — no
    /// EventManager guard, unlike gameplay auto-spawners.
    /// </summary>
    public class ThaiGlyphFixer : MonoBehaviour
    {
        private const int RefreshFrames = 10; // new popups wait ≤10 frames (~0.17 s), inside their pop-in fade

        private static ThaiGlyphFixer _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (_instance != null) return;
            var go = new GameObject("ThaiGlyphFixer (auto)");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ThaiGlyphFixer>();
        }

        private readonly Dictionary<Text, string> _seenUI = new Dictionary<Text, string>();
        private readonly Dictionary<TextMesh, string> _seen3D = new Dictionary<TextMesh, string>();
        private Text[] _texts = System.Array.Empty<Text>();
        private TextMesh[] _meshes = System.Array.Empty<TextMesh>();
        private int _frame;

        private void LateUpdate()
        {
            if (_frame++ % RefreshFrames == 0)
            {
                _texts = FindObjectsByType<Text>(FindObjectsSortMode.None);
                _meshes = FindObjectsByType<TextMesh>(FindObjectsSortMode.None);
                // Destroyed labels leave dead keys behind; rebuild the caches when they outgrow
                // the live set instead of paying a prune scan every refresh.
                if (_seenUI.Count > _texts.Length * 2 + 32) _seenUI.Clear();
                if (_seen3D.Count > _meshes.Length * 2 + 32) _seen3D.Clear();
            }

            foreach (var t in _texts)
            {
                if (t == null) continue;
                string raw = t.text;
                if (_seenUI.TryGetValue(t, out var seen) && ReferenceEquals(seen, raw)) continue;
                if (ThaiFontAdjuster.IsThaiString(raw))
                {
                    string adjusted = ThaiFontAdjuster.Adjust(raw, ThaiGlyphCorrection.FullC90);
                    if (adjusted != raw) { t.text = adjusted; raw = t.text; }
                }
                _seenUI[t] = raw;
            }

            foreach (var m in _meshes)
            {
                if (m == null) continue;
                string raw = m.text;
                if (_seen3D.TryGetValue(m, out var seen) && ReferenceEquals(seen, raw)) continue;
                if (ThaiFontAdjuster.IsThaiString(raw))
                {
                    string adjusted = ThaiFontAdjuster.Adjust(raw, ThaiGlyphCorrection.FullC90);
                    if (adjusted != raw) { m.text = adjusted; raw = m.text; }
                }
                _seen3D[m] = raw;
            }
        }
    }
}
