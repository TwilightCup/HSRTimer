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
    /// <para>
    /// With <see cref="FrameTiming"/> on (the "Force frame timing" option,
    /// R1.12/R2.6) every duration renders as an integer physics-frame count with
    /// 60 frames carrying into one second: <c>MM:SSfFF</c>, <c>H:MM:SSfFF</c>
    /// (>= 1 hour), <c>SSfFF</c> for the wake-up row and <c>±MM:SSfFF</c> for a
    /// signed diff. The <c>f</c> separator is a literal character, never
    /// translated. Persisted values stay in milliseconds; a millisecond diff is
    /// converted to frames here for display only. Every method has an explicit
    /// <c>frames</c> overload so a caller (or the dev console) can render both
    /// representations regardless of the live setting. Wall-clock values
    /// (Real Time / Prev RT / the <c>{realtime}</c> template variable) are
    /// deliberately rendered with <c>frames: false</c> even under frame timing
    /// (R2.6.4) — only game-time values switch to frames.
    /// </para>
    /// </summary>
    public static class TimeFormatter
    {
        /// <summary>
        /// Live "Force frame timing" display mode (R1.12/R2.6): every displayed
        /// time renders as a frame count while the setting is on. Read from the
        /// live config so a panel/console toggle applies to the next drawn frame
        /// without any call-site change.
        /// </summary>
        public static bool FrameTiming
            => ConfigService.Instance != null
               && ConfigService.Instance.Settings != null
               && ConfigService.Instance.Settings.ForceFrameTiming;

        /// <summary>
        /// Format a short per-level duration as SS:mmm (seconds and milliseconds
        /// only, no minute/hour breakdown) or SSfFF under frame timing. Used for
        /// the Wake Up Time display.
        /// </summary>
        public static string FormatWakeUp(double? secondsNullable)
            => FormatWakeUp(secondsNullable, FrameTiming);

        /// <summary>Wake Up Time layout for an explicit mode (see <see cref="FormatWakeUp(double?)"/>).</summary>
        public static string FormatWakeUp(double? secondsNullable, bool frames)
        {
            if (!secondsNullable.HasValue || secondsNullable.Value < 0d)
                return "--:--";
            double seconds = secondsNullable.Value;
            if (frames)
            {
                long totalFrames = FrameCount(seconds);
                long totalSeconds = totalFrames / GameClock.FramesPerSecond;
                long framePart = totalFrames % GameClock.FramesPerSecond;
                return totalSeconds.ToString("D2", CultureInfo.InvariantCulture)
                    + "f" + Two((int)framePart);
            }
            int totalMs = (int)((seconds - (int)seconds) * 1000 + 0.5);
            if (totalMs >= 1000) { seconds += 1; totalMs -= 1000; }
            int secondsPart = (int)seconds;
            return secondsPart.ToString("D2", CultureInfo.InvariantCulture)
                + ":" + Three(totalMs);
        }

        public static string Format(double? secondsNullable)
            => Format(secondsNullable, FrameTiming);

        /// <summary>Nullable layout for an explicit mode (see <see cref="Format(double, bool)"/>).</summary>
        public static string Format(double? secondsNullable, bool frames)
        {
            if (!secondsNullable.HasValue || secondsNullable.Value < 0d)
                return "--:--";
            return Format(secondsNullable.Value, frames);
        }

        public static string Format(double seconds)
            => Format(seconds, FrameTiming);

        /// <summary>
        /// Format a duration for an explicit mode. <paramref name="frames"/> false
        /// is the classic speedrun layout (<c>MM:SS.mmm</c> / <c>H:MM:SS</c>);
        /// true is the frame layout (<c>MM:SSfFF</c> / <c>H:MM:SSfFF</c>).
        /// </summary>
        public static string Format(double seconds, bool frames)
        {
            if (seconds < 0d) seconds = 0d;
            var sb = new StringBuilder();
            if (frames)
            {
                // Total frames first, then carry: a fraction that rounds up to a
                // whole second must roll over instead of rendering "SSf60".
                long totalFrames = FrameCount(seconds);
                int framePart = (int)(totalFrames % GameClock.FramesPerSecond);
                long total = totalFrames / GameClock.FramesPerSecond;
                long h = total / 3600;
                long m = (total % 3600) / 60;
                long s = total % 60;
                if (h > 0)
                    sb.Append(h).Append(':').Append(Two((int)m)).Append(':').Append(Two((int)s));
                else
                    sb.Append(Two((int)m)).Append(':').Append(Two((int)s));
                sb.Append('f').Append(Two(framePart));
                return sb.ToString();
            }
            // Total hours/minutes/seconds, then milliseconds from the fraction.
            int totalMs = (int)((seconds - (int)seconds) * 1000 + 0.5);
            if (totalMs >= 1000) { seconds += 1; totalMs -= 1000; }
            int totalSeconds = (int)seconds;
            int hh = totalSeconds / 3600;
            int mm = (totalSeconds % 3600) / 60;
            int ss = totalSeconds % 60;
            if (hh > 0)
                sb.Append(hh).Append(':').Append(Two(mm)).Append(':').Append(Two(ss));
            else
                sb.Append(Two(mm)).Append(':').Append(Two(ss)).Append('.').Append(Three(totalMs));
            return sb.ToString();
        }

        /// <summary>
        /// Format a signed millisecond diff as <c>+MM:SS.mmm</c> / <c>-MM:SS.mmm</c>
        /// (R8.5.2.2, R10.7.3) or <c>±MM:SSfFF</c> under frame timing. Null or
        /// zero renders as <c>--</c>; the sign and the caller's color always
        /// follow the millisecond value, so a diff below half a frame still shows
        /// the correct direction.
        /// </summary>
        public static string FormatSignedDiff(long? diffMs)
            => FormatSignedDiff(diffMs, FrameTiming);

        /// <summary>Signed-diff layout for an explicit mode (see <see cref="FormatSignedDiff(long?)"/>).</summary>
        public static string FormatSignedDiff(long? diffMs, bool frames)
        {
            if (!diffMs.HasValue || diffMs.Value == 0)
                return "--";
            long d = diffMs.Value;
            char sign = d < 0 ? '-' : d > 0 ? '+' : ' ';
            long abs = d < 0 ? -d : d;
            long totalSeconds;
            long fraction;
            if (frames)
            {
                long totalFrames = GameClock.MsToFrames(abs);
                totalSeconds = totalFrames / GameClock.FramesPerSecond;
                fraction = totalFrames % GameClock.FramesPerSecond;
            }
            else
            {
                totalSeconds = abs / 1000L;
                fraction = abs % 1000L;
            }
            long minutes = totalSeconds / 60L;
            long seconds = totalSeconds % 60L;
            var sb = new StringBuilder();
            // A true zero returned above, so the sign is always '+' or '-'.
            sb.Append(sign == ' ' ? ' ' : sign);
            sb.Append(minutes.ToString("D2", CultureInfo.InvariantCulture));
            sb.Append(':');
            sb.Append(seconds.ToString("D2", CultureInfo.InvariantCulture));
            sb.Append(frames ? 'f' : '.');
            sb.Append(frames
                ? ((int)fraction).ToString("D2", CultureInfo.InvariantCulture)
                : ((int)fraction).ToString("D3", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>Non-negative integer frame count for a duration.</summary>
        private static long FrameCount(double seconds)
        {
            long frames = GameClock.ToFrames(seconds);
            return frames < 0L ? 0L : frames;
        }

        private static string Two(int v) => v.ToString("D2", CultureInfo.InvariantCulture);
        private static string Three(int v) => v.ToString("D3", CultureInfo.InvariantCulture);
    }
}
