using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Central color palette — สลับ theme ทั้งเกมจากที่เดียว
    /// ใช้ทั้งฝั่ง runtime UI และ editor setup (PlaceholderSpriteGenerator, HUDCanvasSetup ฯลฯ)
    ///
    /// ค่าปัจจุบัน = LIGHT theme
    /// อยากกลับเป็น DARK: สลับค่าในบล็อก WORLD/UI เป็นชุดในคอมเมนต์ "// DARK เดิม"
    /// </summary>
    public static class Palette
    {
        // ───────────── WORLD (scene / tilemap / silhouette) ─────────────
        public static readonly Color CameraBackground = Hex("#E9EDF3"); // DARK เดิม: #0D1117
        public static readonly Color GroundFill       = Hex("#CBD6E4"); // DARK เดิม: #1C2333
        public static readonly Color GroundEdge       = Hex("#AAB8CC"); // DARK เดิม: #2D3748
        public static readonly Color BuildingFill     = Hex("#6B7B92"); // DARK เดิม: #4A5568
        public static readonly Color BuildingOutline  = Hex("#34425A"); // DARK เดิม: #718096 (light theme = ขอบเข้มกว่าตัว)

        // ───────────── UI (panel / text / accent) ─────────────
        // ยังไม่ wire เข้า UI setup — เตรียมไว้สำหรับ pass ถัดไป (HUD/Codex/Day11)
        public static readonly Color PanelBg     = Hex("#F5F7FB"); // พื้น panel หลัก
        public static readonly Color PanelBgAlt  = Hex("#E7ECF4"); // pane รอง
        public static readonly Color HeaderBg    = Hex("#DCE3EE"); // แถบหัว
        public static readonly Color TextPrimary = Hex("#1C2733"); // ข้อความหลัก (เข้มบนพื้นสว่าง)
        public static readonly Color TextMuted   = Hex("#5C6878"); // ข้อความรอง
        public static readonly Color Accent      = Hex("#1E7FBE"); // ฟ้า accent

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
