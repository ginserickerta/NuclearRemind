using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// The one place that knows where the UI font lives.
    ///
    /// Every code-built panel used to carry its own copy of this lookup, and most of them asked for
    /// "Fonts/Kanit-Regular" — a path that has never existed. The fonts are under Resources/HUD/Fonts,
    /// so those calls returned null and the panels silently fell back to Unity's built-in face, which
    /// is why the game shipped a mix of typefaces. Copies drift; a single loader cannot.
    ///
    /// Order is deliberate: Chakra Petch SemiBold is the chosen face, Regular is its own fallback,
    /// Kanit is the previous house font (still present), and LegacyRuntime is the last resort so a
    /// missing asset degrades to "wrong font" rather than "no text at all".
    /// </summary>
    public static class UIFonts
    {
        private static Font _body;

        /// <summary>UI face for every runtime-built panel. Cached; never returns null in a normal build.</summary>
        public static Font Body
        {
            get
            {
                if (_body != null) return _body;
                _body = Resources.Load<Font>("HUD/Fonts/ChakraPetch-SemiBold");
                if (_body == null) _body = Resources.Load<Font>("HUD/Fonts/ChakraPetch-Regular");
                if (_body == null) _body = Resources.Load<Font>("HUD/Fonts/Kanit-Regular");
                if (_body == null) _body = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_body == null)
                    Debug.LogError("[UIFonts] โหลดฟอนต์ไม่ได้เลยสักตัว — ตรวจ Assets/Resources/HUD/Fonts");
                return _body;
            }
        }

        /// <summary>Editor setup scripts run outside play mode and re-import assets; drop the cache.</summary>
        public static void ClearCache() => _body = null;
    }
}
