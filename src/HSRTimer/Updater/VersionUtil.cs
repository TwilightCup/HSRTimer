using System;

namespace HSRTimer
{
    /// <summary>
    /// Semantic-version parsing/comparison for the updater (R13.2). Understands
    /// the version strings this plugin actually meets:
    ///   "1.5.0", "v1.6.0", "1.6", "1", "1.6.0-beta.1", "1.6.0+build.7"
    /// i.e. an optional 'v'/'V' prefix, major[.minor[.patch]], an optional
    /// -prerelease suffix, and an optional +metadata suffix (metadata ignored).
    /// Comparison is numeric on major/minor/patch; a prerelease version sorts
    /// below the same version without one (release &gt; prerelease), and two
    /// prereleases compare by ordinal string order.
    /// </summary>
    internal static class VersionUtil
    {
        /// <summary>A parsed semantic version. Major is always set; missing
        /// minor/patch default to 0.</summary>
        public struct SemVer
        {
            public int Major;
            public int Minor;
            public int Patch;
            public string Pre; // null/empty = release
        }

        /// <summary>Parse a version string; returns false when the numeric part
        /// cannot be parsed (in which case the caller falls back to treating a
        /// different tag as "newer", see <see cref="UpdaterService"/>).</summary>
        public static bool TryParse(string raw, out SemVer version)
        {
            version = new SemVer { Major = 0, Minor = 0, Patch = 0, Pre = null };
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string s = raw.Trim();
            // Optional leading 'v'/'V'.
            if (s.Length > 1 && (s[0] == 'v' || s[0] == 'V'))
                s = s.Substring(1);

            // Drop +metadata; keep the rest.
            int plus = s.IndexOf('+');
            if (plus >= 0)
                s = s.Substring(0, plus);
            if (s.Length == 0)
                return false;

            // Split off the optional -prerelease.
            string numeric = s;
            int dash = s.IndexOf('-');
            if (dash >= 0)
            {
                numeric = s.Substring(0, dash);
                version.Pre = s.Substring(dash + 1);
                if (version.Pre.Length == 0)
                    version.Pre = null;
            }
            if (numeric.Length == 0)
                return false;

            string[] parts = numeric.Split('.');
            if (parts.Length < 1 || parts.Length > 3)
                return false;
            if (!int.TryParse(parts[0], out version.Major) || version.Major < 0)
                return false;
            if (parts.Length >= 2 && (!int.TryParse(parts[1], out version.Minor) || version.Minor < 0))
                return false;
            if (parts.Length >= 3 && (!int.TryParse(parts[2], out version.Patch) || version.Patch < 0))
                return false;
            return true;
        }

        /// <summary>Strict "greater than": 1 if a &gt; b, -1 if a &lt; b, 0 if equal.</summary>
        public static int Compare(SemVer a, SemVer b)
        {
            int cmp = a.Major.CompareTo(b.Major);
            if (cmp != 0) return cmp;
            cmp = a.Minor.CompareTo(b.Minor);
            if (cmp != 0) return cmp;
            cmp = a.Patch.CompareTo(b.Patch);
            if (cmp != 0) return cmp;

            bool aPre = !string.IsNullOrEmpty(a.Pre);
            bool bPre = !string.IsNullOrEmpty(b.Pre);
            if (!aPre && !bPre) return 0;          // equal releases
            if (aPre && !bPre) return -1;          // 1.6.0-beta < 1.6.0
            if (!aPre && bPre) return 1;
            return string.CompareOrdinal(a.Pre, b.Pre);
        }
    }
}
