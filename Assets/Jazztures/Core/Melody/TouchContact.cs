namespace Jazztures.Core.Melody
{
    /// <summary>
    /// Decides when one fingertip's contact with one melody target is a <b>touch</b>
    /// (ADR-0045). One of these per (fingertip, target) pair, fed every frame.
    ///
    /// <para>
    /// There are two ways to play a target:
    /// </para>
    /// <list type="number">
    ///   <item>
    ///     <b>Enter it deliberately.</b> The frame the fingertip crosses from outside to inside,
    ///     at <see cref="TouchContactThresholds.EntrySpeed"/> or faster — the original rule, so a
    ///     fast reach or a whole-hand sweep still plays on arrival.
    ///   </item>
    ///   <item>
    ///     <b>Tap it while inside.</b> A fingertip that is already inside — it arrived slowly,
    ///     aiming, or it already played and is hovering — plays on a sudden movement of
    ///     <see cref="TouchContactThresholds.TapSpeed"/> or faster.
    ///   </item>
    /// </list>
    ///
    /// <para>
    /// The second exists because the volume is deep (12 cm, ADR-0018) and a person cannot judge
    /// depth in mid-air (D2): they put the finger "on" the target — which is already inside — and
    /// tap. Under entry-only a tap there produced nothing, and a slow arrival locked the target
    /// until the finger left. The cost of tapping inside is that there is no entry edge to vouch
    /// for intent, so the tap speed is much higher than the entry speed, and after a note the
    /// fingertip must <i>settle</i> before it can play again — a Schmitt trigger on speed
    /// (§3.4's rule), the same shape as the left-hand strike (ADR-0030). That keeps a resting
    /// hand's tracking jitter from sounding notes while still letting a repeated tap play every
    /// time.
    /// </para>
    ///
    /// <para>
    /// A mutable struct held in an array so a frame costs nothing to allocate: call
    /// <c>contacts[finger, target].Feed(...)</c> in place. Pure — no clock, no Unity.
    /// </para>
    /// </summary>
    public struct TouchContact
    {
        private bool _wasInside;
        private bool _armed;
        private int _settledFrames;

        /// <summary>True while a tap inside the volume would play.</summary>
        public bool IsArmed => _armed;

        /// <summary>
        /// Feed one frame: whether the fingertip is inside the target's volume, and its
        /// speed (the caller's smoothed peak). Returns true when this frame is a touch that
        /// should try to play the note.
        /// </summary>
        public bool Feed(bool inside, float speedMetresPerSecond, in TouchContactThresholds thresholds)
        {
            if (!inside)
            {
                // Out of the volume: the next visit starts fresh.
                _wasInside = false;
                _armed = true;
                _settledFrames = 0;
                return false;
            }

            bool entered = !_wasInside;
            _wasInside = true;

            if (entered)
            {
                if (speedMetresPerSecond >= thresholds.EntrySpeed)
                {
                    _armed = false;
                    _settledFrames = 0;
                    return true;
                }

                // Arrived too gently to count — but stay armed, so a tap can still play it.
                _armed = true;
                _settledFrames = 0;
                return false;
            }

            if (_armed)
            {
                if (speedMetresPerSecond >= thresholds.TapSpeed)
                {
                    _armed = false;
                    _settledFrames = 0;
                    return true;
                }

                return false;
            }

            // It already played during this visit. Another tap needs the fingertip to settle first.
            if (speedMetresPerSecond <= thresholds.SettleSpeed)
            {
                _settledFrames++;
                if (_settledFrames >= thresholds.SettleFrames)
                {
                    _armed = true;
                }
            }
            else
            {
                _settledFrames = 0;
            }

            return false;
        }

        /// <summary>Forget everything — for a target that cannot sound, or after the trigger mechanic changes.</summary>
        public void Reset()
        {
            _wasInside = false;
            _armed = false;
            _settledFrames = 0;
        }
    }
}
