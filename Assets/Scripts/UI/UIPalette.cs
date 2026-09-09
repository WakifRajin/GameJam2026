using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// The one place the game's UI colours are defined.
    ///
    /// The HUD had grown four different panel treatments (translucent white, two different
    /// blacks, and a cream menu skin) plus pure #00FF00 bars. Everything now reads from here
    /// so panels, bars and menus stay one family - a dark instrument console against the
    /// rust-orange Mars exterior.
    /// </summary>
    public static class UIPalette
    {
        // Surfaces
        public static readonly Color PanelBackground     = new Color32(0x0D, 0x11, 0x17, 0xD8);
        public static readonly Color PanelBackgroundSoft = new Color32(0x0D, 0x11, 0x17, 0xA8);
        public static readonly Color PanelBackgroundHeavy= new Color32(0x08, 0x0B, 0x10, 0xF0);
        public static readonly Color BarTrack            = new Color32(0x1E, 0x26, 0x33, 0xFF);
        public static readonly Color ButtonFace          = new Color32(0x1B, 0x23, 0x2E, 0xFF);
        public static readonly Color ButtonFaceHover     = new Color32(0x28, 0x35, 0x45, 0xFF);

        // Type
        public static readonly Color TextPrimary   = new Color32(0xE6, 0xED, 0xF3, 0xFF);
        public static readonly Color TextSecondary = new Color32(0x93, 0x9F, 0xB0, 0xFF);
        public static readonly Color TextMuted     = new Color32(0x66, 0x71, 0x80, 0xFF);

        // Stat accents - each rover system keeps one hue everywhere it appears.
        public static readonly Color Power   = new Color32(0xF5, 0xA5, 0x24, 0xFF); // amber
        public static readonly Color Heat    = new Color32(0xFF, 0x6B, 0x4A, 0xFF); // ember
        public static readonly Color Comm    = new Color32(0x38, 0xBD, 0xF8, 0xFF); // cyan
        public static readonly Color Cargo   = new Color32(0xA7, 0x8B, 0xFA, 0xFF); // violet

        // Semantic
        public static readonly Color Success = new Color32(0x4A, 0xDE, 0x80, 0xFF);
        public static readonly Color Warning = new Color32(0xF0, 0xB4, 0x29, 0xFF);
        public static readonly Color Danger  = new Color32(0xF8, 0x51, 0x49, 0xFF);

        /// <summary>Same colour at a different alpha, for hover/disabled states.</summary>
        public static Color WithAlpha(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);

        /// <summary>Amber to red as a value drains toward zero. Used by bars and countdowns.</summary>
        public static Color Drain(float normalized)
        {
            if (normalized > 0.5f) return Success;
            if (normalized > 0.25f) return Warning;
            return Danger;
        }
    }
}
