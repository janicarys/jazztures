using System;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Gesture
{
    /// <summary>
    /// Plays a <see cref="HandPoseRecording"/> back as an <see cref="IHandPoseSource"/>,
    /// driven by an <see cref="IMusicalClock"/> (CLAUDE.md §2.2). Playback position is
    /// <c>clock.Now</c> minus the clock time when this was constructed, so it plays in
    /// real time in a live scene and steps deterministically under a <c>VirtualClock</c>
    /// in tests. <see cref="CurrentFrame"/> returns the most recent sample at or before
    /// the current position — <see cref="HandPoseFrame.Untracked"/> before the first, the
    /// last sample held after the end.
    /// </summary>
    public sealed class ReplayHandPoseSource : IHandPoseSource
    {
        private readonly HandPoseRecording _recording;
        private readonly IMusicalClock _clock;
        private readonly double _origin;
        private int _cursor;

        public ReplayHandPoseSource(HandPoseRecording recording, IMusicalClock clock)
        {
            _recording = recording ?? throw new ArgumentNullException(nameof(recording));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _origin = clock.Now;
        }

        /// <summary>Seconds since playback started.</summary>
        public double Position => _clock.Now - _origin;

        /// <summary>True once the playback position is past the last sample.</summary>
        public bool HasEnded => _recording.Count == 0 || Position >= _recording.DurationSeconds;

        public HandPoseFrame CurrentFrame
        {
            get
            {
                if (_recording.Count == 0)
                {
                    return HandPoseFrame.Untracked;
                }

                double position = Position;
                var samples = _recording.Samples;

                // The cursor only moves forward; a rewound clock is not supported.
                if (_cursor > 0 && samples[_cursor].TimeSeconds > position)
                {
                    _cursor = 0;
                }

                while (_cursor + 1 < samples.Count && samples[_cursor + 1].TimeSeconds <= position)
                {
                    _cursor++;
                }

                return samples[_cursor].TimeSeconds <= position
                    ? samples[_cursor].Frame
                    : HandPoseFrame.Untracked;
            }
        }
    }
}
