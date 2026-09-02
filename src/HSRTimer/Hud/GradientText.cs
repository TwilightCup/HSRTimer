using System.Globalization;
using System.Text;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// Color parsing/formatting and a small time formatter shared by the HUD
    /// and the config layer. Hex colors accept either RRGGBB or RRGGBBAA; a
    /// missing alpha byte defaults to fully opaque. This keeps the layout file
    /// human-editable while still supporting the per-color alpha R2.3.2 requires.
    /// </summary>
    public static class GradientText
    {
        /// <summary>Parse a hex color (RRGGBB or RRGGBBAA, optional leading #), or a fallback.</summary>
        public static Color ParseColor(string hex, Color fallback)
            => TryParseColor(hex, out Color c) ? c : fallback;

        /// <summary>Try to parse a hex color (RRGGBB or RRGGBBAA, optional leading #).
        /// Returns false for null/empty/invalid input — unlike <see cref="ParseColor"/>
        /// this distinguishes a genuine parse from a fallback, which the live hex
        /// input box needs so an incomplete value (e.g. "FF") isn't applied.</summary>
        public static bool TryParseColor(string hex, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrEmpty(hex)) return false;
            string h = hex.Trim();
            if (h.StartsWith("#")) h = h.Substring(1);
            if (h.Length == 6)
            {
                if (!TryHex(h, out uint rgb)) return false;
                color = new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
                return true;
            }
            if (h.Length == 8)
            {
                if (!TryHex(h, out uint rgba)) return false;
                color = new Color(((rgba >> 24) & 0xFF) / 255f, ((rgba >> 16) & 0xFF) / 255f, ((rgba >> 8) & 0xFF) / 255f, (rgba & 0xFF) / 255f);
                return true;
            }
            return false;
        }

        /// <summary>Emit a color as RRGGBBAA hex (always 8 chars, no #).</summary>
        public static string ToHex(Color c)
        {
            uint r = (uint)(Mathf.Clamp01(c.r) * 255f + 0.5f);
            uint g = (uint)(Mathf.Clamp01(c.g) * 255f + 0.5f);
            uint b = (uint)(Mathf.Clamp01(c.b) * 255f + 0.5f);
            uint a = (uint)(Mathf.Clamp01(c.a) * 255f + 0.5f);
            return string.Format(CultureInfo.InvariantCulture, "{0:X2}{1:X2}{2:X2}{3:X2}", r, g, b, a);
        }

        /// <summary>Per-character gradient color at position i of len (0..1 across the text).</summary>
        public static Color Gradient(Color a, Color b, int i, int len)
        {
            if (len <= 1) return a;
            float t = (float)i / (len - 1);
            return new Color(
                Mathf.Lerp(a.r, b.r, t),
                Mathf.Lerp(a.g, b.g, t),
                Mathf.Lerp(a.b, b.b, t),
                Mathf.Lerp(a.a, b.a, t));
        }

        private static bool TryHex(string s, out uint value)
            => uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Formats durations for the HUD. Speedrun-style: HH:MM:SS for >= 1 hour,
    /// MM:SS.mmm otherwise. Negative or unset values render as "--:--".
    /// </summary>
    public static class TimeFormatter
    {
        /// <summary>
        /// Format a short per-level duration as SS:mmm (seconds and milliseconds
        /// only, no minute/hour breakdown). Used for the Wake Up Time display.
        /// </summary>
        public static string FormatWakeUp(double? secondsNullable)
        {
            if (!secondsNullable.HasValue || secondsNullable.Value < 0d)
                return "--:--";
            double seconds = secondsNullable.Value;
            int totalMs = (int)((seconds - (int)seconds) * 1000 + 0.5);
            if (totalMs >= 1000) { seconds += 1; totalMs -= 1000; }
            int totalSeconds = (int)seconds;
            return totalSeconds.ToString("D2", CultureInfo.InvariantCulture)
                + ":" + Three(totalMs);
        }

        public static string Format(double? secondsNullable)
        {
            if (!secondsNullable.HasValue || secondsNullable.Value < 0d)
                return "--:--";
            return Format(secondsNullable.Value);
        }

        public static string Format(double seconds)
        {
            if (seconds < 0d) seconds = 0d;
            // Total hours/minutes/seconds, then milliseconds from the fraction.
            int totalMs = (int)((seconds - (int)seconds) * 1000 + 0.5);
            if (totalMs >= 1000) { seconds += 1; totalMs -= 1000; }
            int total = (int)seconds;
            int h = total / 3600;
            int m = (total % 3600) / 60;
            int s = total % 60;
            var sb = new StringBuilder();
            if (h > 0)
                sb.Append(h).Append(':').Append(Two(m)).Append(':').Append(Two(s));
            else
                sb.Append(Two(m)).Append(':').Append(Two(s)).Append('.').Append(Three(totalMs));
            return sb.ToString();
        }

        private static string Two(int v) => v.ToString("D2", CultureInfo.InvariantCulture);
        private static string Three(int v) => v.ToString("D3", CultureInfo.InvariantCulture);
    }
}
