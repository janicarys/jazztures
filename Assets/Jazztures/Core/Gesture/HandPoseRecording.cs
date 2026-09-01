using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Gesture
{
    /// <summary>
    /// A recorded hand-input session: a time-ordered list of <see cref="HandPoseSample"/>.
    /// Serialises to JSONL (one sample per line) so a real Quest session can be captured
    /// once and replayed at the desk to iterate <see cref="GestureInterpreter"/> without
    /// re-donning the headset (CLAUDE.md §2.6). Only the pose candidate and tracking
    /// quality are stored — never raw joint transforms (§4.1).
    /// </summary>
    public sealed class HandPoseRecording
    {
        private readonly HandPoseSample[] _samples;

        public HandPoseRecording(IEnumerable<HandPoseSample> samples)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            var list = new List<HandPoseSample>();
            double previous = double.NegativeInfinity;
            foreach (HandPoseSample sample in samples)
            {
                if (sample.TimeSeconds < previous)
                {
                    throw new ArgumentException("Samples must be in non-decreasing time order.", nameof(samples));
                }

                previous = sample.TimeSeconds;
                list.Add(sample);
            }

            _samples = list.ToArray();
        }

        public static HandPoseRecording Empty { get; } = new HandPoseRecording(Array.Empty<HandPoseSample>());

        public IReadOnlyList<HandPoseSample> Samples => _samples;

        public int Count => _samples.Length;

        public double DurationSeconds => _samples.Length == 0 ? 0.0 : _samples[_samples.Length - 1].TimeSeconds;

        /// <summary>One JSON object per line: <c>{"t":1.234,"c":"Ii","lt":"High","rt":"High"}</c>.</summary>
        public string ToJsonl()
        {
            var builder = new StringBuilder();
            foreach (HandPoseSample sample in _samples)
            {
                builder.Append("{\"t\":")
                    .Append(sample.TimeSeconds.ToString("0.#########", CultureInfo.InvariantCulture))
                    .Append(",\"c\":\"").Append(sample.Frame.LeftCandidate)
                    .Append("\",\"lt\":\"").Append(sample.Frame.LeftTracking)
                    .Append("\",\"rt\":\"").Append(sample.Frame.RightTracking)
                    .Append("\"}\n");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Parses the JSONL produced by <see cref="ToJsonl"/>. Blank lines and lines
        /// starting with <c>#</c> are ignored. Returns false on any malformed line.
        /// </summary>
        public static bool TryParseJsonl(string? jsonl, out HandPoseRecording recording)
        {
            recording = Empty;
            if (jsonl == null)
            {
                return false;
            }

            var samples = new List<HandPoseSample>();
            foreach (string rawLine in jsonl.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                if (!TryParseLine(line, out HandPoseSample sample))
                {
                    return false;
                }

                samples.Add(sample);
            }

            try
            {
                recording = new HandPoseRecording(samples);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool TryParseLine(string line, out HandPoseSample sample)
        {
            sample = default;
            if (line.Length < 2 || line[0] != '{' || line[line.Length - 1] != '}')
            {
                return false;
            }

            double? time = null;
            HandPoseCandidate? candidate = null;
            TrackingQuality? leftTracking = null;
            TrackingQuality? rightTracking = null;

            foreach (string pair in line.Substring(1, line.Length - 2).Split(','))
            {
                int colon = pair.IndexOf(':');
                if (colon < 0)
                {
                    return false;
                }

                string key = Unquote(pair.Substring(0, colon).Trim());
                string value = Unquote(pair.Substring(colon + 1).Trim());

                switch (key)
                {
                    case "t":
                        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double t))
                        {
                            return false;
                        }

                        time = t;
                        break;
                    case "c":
                        candidate = ParseEnum<HandPoseCandidate>(value);
                        break;
                    case "lt":
                        leftTracking = ParseEnum<TrackingQuality>(value);
                        break;
                    case "rt":
                        rightTracking = ParseEnum<TrackingQuality>(value);
                        break;
                }
            }

            if (time == null || candidate == null || leftTracking == null || rightTracking == null)
            {
                return false;
            }

            try
            {
                sample = new HandPoseSample(
                    time.Value,
                    new HandPoseFrame(candidate.Value, leftTracking.Value, rightTracking.Value));
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static string Unquote(string text) =>
            text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"'
                ? text.Substring(1, text.Length - 2)
                : text;

        private static TEnum? ParseEnum<TEnum>(string value) where TEnum : struct =>
            Enum.TryParse(value, ignoreCase: false, out TEnum result) && Enum.IsDefined(typeof(TEnum), result)
                ? result
                : (TEnum?)null;
    }
}
