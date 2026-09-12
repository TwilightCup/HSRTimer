using System.Collections.Generic;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// Tracks voiceline (NarrativeBlock) completion for the Voiceline tag
    /// (R3.6 / R5.5). On level enter it scans the scene for every
    /// NarrativeBlock and for the special "Easter" audio source; during play
    /// the Harmony postfixes mark blocks/clips as triggered; on exit it decides
    /// whether all voicelines were satisfied.
    ///
    /// The common voiceline-skip trick leaves the "Easter" AudioSource present
    /// in the scene but never plays it — so on enter we treat "Easter present"
    /// as suspicious (satisfied=false) and only flip it back to satisfied when
    /// we actually observe the Easter clip playing (R5.5.1).
    /// </summary>
    public sealed class VoicelineTracker
    {
        // NarrativeBlock instance ids discovered on level enter that must fire.
        private readonly HashSet<int> _pending = new HashSet<int>();
        private readonly HashSet<int> _triggered = new HashSet<int>();

        private bool _easterPresent;   // an "Easter" AudioSource exists in the scene
        private bool _easterPlayed;    // it was observed playing via PlayNarrative or polling
        private bool _satisfied;       // current satisfied state

        // The AudioClip bound to the Easter source, if found, for matching.
        private AudioClip _easterClip;
        // The actual Easter AudioSource, kept so a direct AudioSource.Play()
        // (the hidden easter-egg triggers) can be detected by polling isPlaying.
        private AudioSource _easterSource;

        /// <summary>The tracker to use for the current level, set by the engine.</summary>
        public static VoicelineTracker Current { get; set; }

        /// <summary>Scan the scene at level start. Call once per segment.</summary>
        public void InitOnLevelEnter()
        {
            _pending.Clear();
            _triggered.Clear();
            _easterClip = null;
            _easterSource = null;
            _easterPresent = false;
            _easterPlayed = false;
            _satisfied = true; // R5.5.1: optimistic until proven otherwise

            try
            {
                foreach (var nb in Object.FindObjectsOfType<NarrativeBlock>())
                {
                    if (nb != null) _pending.Add(nb.GetInstanceID());
                }

                FindEasterSource();
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: voiceline scan failed: {ex.Message}");
            }
        }

        private void FindEasterSource()
        {
            // Look for an AudioSource whose GameObject is named "Easter" (case-insensitive).
            try
            {
                var sources = Object.FindObjectsOfType<AudioSource>();
                if (sources == null) return;
                foreach (var src in sources)
                {
                    if (src == null) continue;
                    if (src.gameObject != null
                        && src.gameObject.name != null
                        && src.gameObject.name.ToLowerInvariant() == "easter")
                    {
                        _easterPresent = true;
                        _easterClip = src.clip;
                        _easterSource = src;
                        _satisfied = false; // present-but-unplayed is suspicious
                        return;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: Easter source scan failed: {ex.Message}");
            }
        }

        /// <summary>Called from the NarrativeBlock.Play() Harmony postfix.</summary>
        public void MarkPlayed(NarrativeBlock block)
        {
            if (block == null) return;
            int id = block.GetInstanceID();
            if (_triggered.Add(id))
                _pending.Remove(id);
        }

        /// <summary>
        /// Called from the SubtitleManager.PlayNarrative() postfix. If the clip
        /// is the Easter clip, the Easter voiceline has genuinely played.
        /// </summary>
        public void MarkNarrativeClip(AudioClip clip)
        {
            if (clip == null) return;
            if (_easterPresent && (_easterClip == clip || _easterClip == null))
            {
                _easterPlayed = true;
                _satisfied = true;
            }
        }

        /// <summary>
        /// Poll the Easter audio source each tick. Some hidden easter-egg
        /// triggers call <c>AudioSource.Play()</c> directly instead of going
        /// through <see cref="SubtitleManager.PlayNarrative"/>, so the
        /// PlayNarrative postfix alone would miss them. isPlaying is pollable,
        /// so no extra Harmony patch is needed.
        /// </summary>
        public void PollEaster()
        {
            if (_easterPlayed || _easterSource == null) return;
            if (_easterSource.isPlaying)
            {
                _easterPlayed = true;
                _satisfied = true;
            }
        }

        /// <summary>
        /// True if the level's voicelines are all satisfied for the Voiceline tag.
        /// Invalid (not satisfied) when: Easter present but never played, OR any
        /// NarrativeBlock never fired.
        /// </summary>
        public bool IsSatisfied()
        {
            if (!_satisfied) return false;
            return _pending.Count == 0;
        }

        /// <summary>True when every NarrativeBlock has been triggered (R3.6.3 green hint).</summary>
        public bool AllBlocksTriggered => _pending.Count == 0;

        /// <summary>True when the Easter source, if present, has played.</summary>
        public bool EasterSatisfied => !_easterPresent || _easterPlayed;

        /// <summary>Number of voicelines triggered so far, including the Easter clip when present.</summary>
        public int TriggeredCount
        {
            get
            {
                int count = _triggered.Count;
                if (_easterPresent && _easterPlayed)
                    count++;
                return count;
            }
        }

        /// <summary>Total voicelines required for the current level, including the Easter clip when present.</summary>
        public int TotalCount
        {
            get
            {
                int count = _triggered.Count + _pending.Count;
                if (_easterPresent)
                    count++;
                return count;
            }
        }
    }
}
