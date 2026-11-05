using System;
using TMPro;
using UnityEngine;

namespace ARMenu
{
    /// <summary>
    /// Colours and fonts for the AR menu. Editable in the Inspector on the ARMenuApp component.
    /// Palette: lavender-purple pastels with white and near-black.
    /// </summary>
    [Serializable]
    public class UITheme
    {
        [Header("Surfaces")]
        public Color background = Hex(0xFAF8FE);       // soft lavender-white page
        public Color backgroundRaised = Hex(0xFFFFFF); // pure white bars
        public Color surface = Hex(0xFFFFFF);          // cards
        public Color surfaceHigh = Hex(0xF0EAFB);      // pastel lavender wells, chips, tiles
        public Color stroke = Hex(0xE3D9F4);
        public Color ink = Hex(0x14111B);              // near-black feature surfaces
        public Color shadow = Hex(0x4A3780, 0.16f);

        [Header("Text")]
        public Color textPrimary = Hex(0x15121C);
        public Color textSecondary = Hex(0x5D5571);
        public Color textMuted = Hex(0x9890AC);
        public Color onAccent = Hex(0xFFFFFF);
        public Color onInk = Hex(0xFFFFFF);

        [Header("Accents")]
        public Color accent = Hex(0x7C5CC6);       // lavender purple (buttons, prices)
        public Color accentLight = Hex(0xC9B8F2);  // pastel lavender
        public Color accentDeep = Hex(0x4B3787);   // deep purple text on pastel
        public Color veg = Hex(0x2F9A4A);
        public Color nonVeg = Hex(0xB3402A);
        public Color spice = Hex(0xE2603F);
        public Color success = Hex(0x2F9A4A);
        public Color warning = Hex(0xC9781F);

        [Header("Fonts (optional overrides; defaults are loaded from TextMesh Pro resources)")]
        public TMP_FontAsset displayFont;
        public TMP_FontAsset bodyFont;
        public TMP_FontAsset strongFont;

        public TMP_FontAsset Display => displayFont ? displayFont : bodyFont ? bodyFont : TMP_Settings.defaultFontAsset;
        public TMP_FontAsset Body => bodyFont ? bodyFont : TMP_Settings.defaultFontAsset;
        public TMP_FontAsset Strong => strongFont ? strongFont : Body;

        /// <summary>Loads the fonts bundled with TextMesh Pro's Examples & Extras if no overrides were set.</summary>
        public void ResolveFonts()
        {
            if (!bodyFont) bodyFont = TMP_Settings.defaultFontAsset;
            if (!displayFont) displayFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
            if (!strongFont) strongFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF");
        }

        public static Color Hex(uint rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
