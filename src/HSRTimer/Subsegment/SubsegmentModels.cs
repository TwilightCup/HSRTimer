using System;
using System.Collections.Generic;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>A single HSRTimer subsegment sample (R8.1.3).</summary>
    [Serializable]
    public sealed class SubsegmentSample
    {
        public int seq;
        public int level_index;
        public long t_ms;
        public float px;
        public float py;
        public float pz;
        public float dx;
        public float dy;
        public float dz;
        public float plane_radius;

        public Vector3 Position => new Vector3(px, py, pz);
        public Vector3 Displacement => new Vector3(dx, dy, dz);
    }

    /// <summary>Metadata for a PB/load subsegment directory (R8.2.2).</summary>
    [Serializable]
    public sealed class SubsegmentMeta
    {
        public int format_version = 1;
        public string project = "IL";
        public string level_id;
        public string subproject;
        public string category_key;
        public string[] level_ids;
        public long total_ms;
        public int sample_count;
        public string hsrtimer_version;
        public string created_at;
    }

    /// <summary>
    /// One reference item shown in the subsegment leaderboard. It owns its
    /// loaded samples, the generated detection planes, and the latest settled
    /// diff (in ms) for the current level.
    /// </summary>
    public sealed class SubsegmentReference
    {
        public string DisplayId;
        public string SourcePath;

        /// <summary>The raw reference samples used for planning and stale-path checks.</summary>
        public List<SubsegmentSample> Samples = new List<SubsegmentSample>();

        /// <summary>Detection planes built from non-stationary reference samples (R8.4.1).</summary>
        public List<SubsegmentPlane> Planes = new List<SubsegmentPlane>();

        /// <summary>Latest settled diff_ms, or null when no plane has settled.</summary>
        public long? DiffMs;
    }

    /// <summary>
    /// A virtual detection plane generated from one reference sample: center +
    /// normalized displacement normal + radius (R8.4.1). Also carries the
    /// transient per-frame comparator state (PrevD, debounce/settle timing).
    /// </summary>
    public sealed class SubsegmentPlane
    {
        public int Seq;
        public long TMs;
        public Vector3 Position;
        public Vector3 Normal;
        public float Radius;

        // Comparator transient state.
        public float PrevD;
        public bool HasPrevD;
        public float LastDebounceUnscaledTime = float.NegativeInfinity;
        public long? CandidateHitMs;
        public float QuietStartUnscaledTime;
        public bool HasQuiet;
    }
}
