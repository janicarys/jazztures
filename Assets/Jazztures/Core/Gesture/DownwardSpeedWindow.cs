using System;

namespace Jazztures.Core.Gesture
{
    /// <summary>
    /// The peak <b>downward</b> speed of one point over the last few frames, in metres per
    /// second (always non-negative; 0 means "not moving down right now"). The pure core of what
    /// <c>MetaXRHandPoseSource</c> does inline for the strike (ADR-0028/0035), reusable for any
    /// tracked point — used by the strike plate (ADR-0047) so that a touch is judged on how the
    /// <i>hand</i> is moving down, not on how fast some finger happens to be moving.
    ///
    /// <para>
    /// Two properties carry over from the strike detector:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <b>Peak over a short window</b>, so a strike that decelerates on arrival still reports
    ///     the speed it had a frame earlier (ADR-0028).
    ///   </item>
    ///   <item>
    ///     <b>Only the current, unbroken downward run counts</b> (ADR-0035): the moment the point
    ///     stops moving down — a sideways drift, a rest, a rise — it reports 0, however fast the
    ///     motion a frame or two earlier was. A finished chop must not keep "striking".
    ///   </item>
    /// </list>
    ///
    /// <para>Allocation-free after construction. Heights are in any consistent upward-positive unit (metres).</para>
    /// </summary>
    public sealed class DownwardSpeedWindow
    {
        private readonly float[] _signedSpeeds; // metres/second, negative = downward
        private int _cursor;
        private bool _hasPrevious;
        private float _previousHeight;

        public DownwardSpeedWindow(int frames)
        {
            if (frames < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(frames), frames, "Must be at least 1.");
            }

            _signedSpeeds = new float[frames];
        }

        /// <summary>
        /// Feed this frame's height and the time since the last frame; returns the peak downward
        /// speed of the current downward run (0 if the point is not moving down this frame).
        /// </summary>
        public float Update(float height, float deltaSeconds)
        {
            float instant = _hasPrevious && deltaSeconds > 0f
                ? (height - _previousHeight) / deltaSeconds
                : 0f;
            _previousHeight = height;
            _hasPrevious = true;

            _cursor = (_cursor + 1) % _signedSpeeds.Length;
            _signedSpeeds[_cursor] = instant;

            float peakDownward = 0f;
            int count = _signedSpeeds.Length;
            for (int steps = 0; steps < count; steps++)
            {
                float sample = _signedSpeeds[(_cursor - steps + count) % count];
                if (sample >= 0f)
                {
                    break; // the motion is not (or no longer) downward — stop; never look past a reversal
                }

                if (-sample > peakDownward)
                {
                    peakDownward = -sample;
                }
            }

            return peakDownward;
        }

        /// <summary>Forget the history — call when the point stops being tracked, so the next frame cannot differentiate against a stale position.</summary>
        public void Reset()
        {
            _hasPrevious = false;
            _cursor = 0;
            Array.Clear(_signedSpeeds, 0, _signedSpeeds.Length);
        }
    }
}
