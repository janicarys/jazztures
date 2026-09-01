using System;
using System.Collections.Generic;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Gesture
{
    /// <summary>
    /// Accumulates timestamped hand-input frames into a <see cref="HandPoseRecording"/>.
    /// Pure — the Unity wrapper feeds it <c>IHandPoseSource.CurrentFrame</c> and the DSP
    /// time each frame, then writes <see cref="Build"/>'s JSONL to disk.
    /// </summary>
    public sealed class HandPoseRecorder
    {
        private readonly List<HandPoseSample> _samples = new List<HandPoseSample>();
        private double _startTime = double.NaN;
        private double _lastTime = double.NegativeInfinity;

        /// <summary>Frames captured so far.</summary>
        public int Count => _samples.Count;

        /// <summary>
        /// Record the frame observed at absolute time <paramref name="dspTime"/>. The
        /// first call sets the recording's zero; times are stored relative to it. Calls
        /// must be in non-decreasing time order.
        /// </summary>
        public void Capture(double dspTime, HandPoseFrame frame)
        {
            if (double.IsNaN(dspTime) || double.IsInfinity(dspTime))
            {
                throw new ArgumentOutOfRangeException(nameof(dspTime), dspTime, "Must be finite.");
            }

            if (double.IsNaN(_startTime))
            {
                _startTime = dspTime;
            }

            double relative = dspTime - _startTime;
            if (relative < _lastTime)
            {
                throw new ArgumentException("Capture time went backwards.", nameof(dspTime));
            }

            _lastTime = relative;
            _samples.Add(new HandPoseSample(Math.Max(relative, 0.0), frame));
        }

        public HandPoseRecording Build() => new HandPoseRecording(_samples);

        public void Clear()
        {
            _samples.Clear();
            _startTime = double.NaN;
            _lastTime = double.NegativeInfinity;
        }
    }
}
