using System;
using System.Collections.Generic;

namespace HSRTimer
{
    /// <summary>
    /// Marker kinds (R10.1.4). Persisted as the <c>type</c> string on
    /// <see cref="MarkerDef"/>; parsed tolerantly via <see cref="MarkerKindUtil"/>.
    /// </summary>
    public enum MarkerKind
    {
        Range,
        Checkpoint,
        GrabObject,
    }

    /// <summary>
    /// Tolerant string&lt;=&gt;<see cref="MarkerKind"/> conversion. Unknown values
    /// fall back to <see cref="MarkerKind.Range"/> for display purposes but are
    /// flagged as inactive by the engine (R10.5/§6 robustness).
    /// </summary>
    public static class MarkerKindUtil
    {
        public static MarkerKind Parse(string type, MarkerKind fallback = MarkerKind.Range)
        {
            switch ((type ?? "").Trim().ToLowerInvariant())
            {
                case "range":
                    return MarkerKind.Range;
                case "checkpoint":
                case "checkpointload":
                    return MarkerKind.Checkpoint;
                case "grab":
                case "grabobject":
                    return MarkerKind.GrabObject;
                default:
                    return fallback;
            }
        }

        public static string ToString(MarkerKind kind)
        {
            switch (kind)
            {
                case MarkerKind.Range: return "Range";
                case MarkerKind.Checkpoint: return "Checkpoint";
                case MarkerKind.GrabObject: return "GrabObject";
                default: return "Range";
            }
        }

        /// <summary>True when the stored type string is a known marker type.</summary>
        public static bool IsKnown(string type)
        {
            switch ((type ?? "").Trim().ToLowerInvariant())
            {
                case "range":
                case "checkpoint":
                case "grab":
                case "grabobject":
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>The whole-level marker PB record (R10.3.1).</summary>
    [Serializable]
    public sealed class MarkerPb
    {
        /// <summary>Level segment time (ms) of the PB run, used as the beat threshold.</summary>
        public long total_ms;

        public string created_at;
    }

    /// <summary>One marker's trigger time inside a PB record (R10.3.1/2).</summary>
    [Serializable]
    public sealed class MarkerPbEntry
    {
        public string id;
        public long t_ms;
    }

    /// <summary>
    /// One user-defined marker (R10.1.3). All type-specific fields live on the
    /// same object because the file format has no polymorphism; unused fields
    /// keep their defaults and are ignored by the engine based on
    /// <see cref="type"/>.
    /// </summary>
    [Serializable]
    public sealed class MarkerDef
    {
        public string id = "";

        /// <summary>User-editable display name.</summary>
        public string name = "";

        /// <summary>Stored kind: "Range" / "Checkpoint" / "GrabObject" (R10.1.4).</summary>
        public string type = "Range";

        /// <summary>Per-marker on/off (supplemental to R10.1.3).</summary>
        public bool enabled = true;

        // ── Range (R10.2.1) ──
        public float cx, cy, cz;   // box center
        public float sx, sy, sz;   // box size per axis
        public bool requireGrab;   // player must be grabbing any object
        public bool requireJump;   // player must be jumping

        // ── Checkpoint (R10.2.3/4) ──
        public int checkpointIndex;
        public bool triggerOnLoad;

        // ── GrabObject (R10.2.2) ──
        public uint objectSceneId;      // NetIdentity.sceneId when available
        public string objectPath = "";  // hierarchy path from the scene root
        public string objectName = "";  // display name captured at setup time
        public float ox, oy, oz;        // world position recorded at setup time (diagnostics/fallback)

        /// <summary>Parsed kind; falls back to <see cref="MarkerKind.Range"/> for unknown types.</summary>
        public MarkerKind Kind => MarkerKindUtil.Parse(type);
    }

    /// <summary>
    /// One level's marker definitions + PB, persisted as a single JSON file
    /// (R10.1/§4.1). Serialized by <see cref="MarkerJson"/> (an explicit writer
    /// with a tolerant parser), so every field — including empty marker arrays —
    /// is always present in the file.
    /// </summary>
    [Serializable]
    public sealed class MarkerSet
    {
        public int format_version = MarkerJson.FormatVersion;

        /// <summary>Sanitized storage level key (R10.1.2).</summary>
        public string level_id = "";

        public string level_source = "";

        public int level_number = -1;

        /// <summary>Category key (R8.2.4) this set belongs to.</summary>
        public string category_key = "";

        public string hsrtimer_version = "";

        public string updated_at = "";

        /// <summary>Whole-level PB record; null when no PB exists yet (R10.3).</summary>
        public MarkerPb pb;

        public List<MarkerDef> markers = new List<MarkerDef>();

        /// <summary>PB trigger times keyed by marker id (R10.3.1).</summary>
        public List<MarkerPbEntry> pbTimes = new List<MarkerPbEntry>();

        /// <summary>Find a marker by id; null when missing.</summary>
        public MarkerDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < markers.Count; i++)
                if (markers[i] != null && markers[i].id == id)
                    return markers[i];
            return null;
        }

        /// <summary>PB time for a marker id, or null when the marker has no PB entry.</summary>
        public long? PbTimeOf(string id)
        {
            if (pbTimes == null) return null;
            for (int i = 0; i < pbTimes.Count; i++)
                if (pbTimes[i] != null && pbTimes[i].id == id)
                    return pbTimes[i].t_ms;
            return null;
        }
    }
}
