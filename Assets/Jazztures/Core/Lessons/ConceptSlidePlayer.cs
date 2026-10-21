using System;
using System.Collections.Generic;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// Paces a lesson's opening explanation (§3.9 co-design ask) - the "short discussion" before the lesson itself -
    /// and says when it is over. One slide at a time, each held for its own <see cref="ConceptSlide.Seconds"/>; the first
    /// is due the moment the discussion starts.
    ///
    /// <para>
    /// <b>Over means the last slide has run its course</b>, not that it has appeared: <see cref="IsFinished"/> turns true
    /// only once the time that slide asked for has passed. That moment is what the rest of the lesson waits for - its
    /// cues, its ghost hands, its lanes (ADR-0066). The runner used to tell "slides still going" by whether any slide
    /// was left to show, which went false the instant the last one appeared and let everything else start under it.
    /// </para>
    ///
    /// <para>
    /// Pure and clock-agnostic (times are DSP seconds handed in), like the rest of <c>Core/Lessons</c>, so the pacing is
    /// tested without a headset (§2.6). A slide of 0 seconds - the plain, single explanation - is shown and does not delay
    /// anything.
    /// </para>
    /// </summary>
    public sealed class ConceptSlidePlayer
    {
        private IReadOnlyList<ConceptSlide> _slides = Array.Empty<ConceptSlide>();
        private int _next;
        private double _dueDsp; // when the next slide is due; once none is left, when the last one ends

        /// <summary>Begin a discussion at <paramref name="nowDspTime"/>. Null or empty means there is none: it is already over.</summary>
        public void Start(IReadOnlyList<ConceptSlide>? slides, double nowDspTime)
        {
            _slides = slides ?? Array.Empty<ConceptSlide>();
            _next = 0;
            _dueDsp = nowDspTime;
        }

        /// <summary>Abandon the discussion: it is over, and nothing more is shown.</summary>
        public void Stop() => Start(null, 0.0);

        /// <summary>
        /// The slide that is due at <paramref name="nowDspTime"/>, if one is, and true; otherwise false. A slide is due once
        /// the one before it has held for its time; it is held from the moment it is taken, so a late call
        /// delays the next slide rather than cutting this one short.
        /// </summary>
        public bool TryTake(double nowDspTime, out ConceptSlide slide)
        {
            if (_next < _slides.Count && nowDspTime >= _dueDsp)
            {
                slide = _slides[_next++];
                _dueDsp = nowDspTime + slide.Seconds;
                return true;
            }

            slide = default;
            return false;
        }

        /// <summary>
        /// True when every slide has been shown <b>and the last has held for its time</b> - or when there was no discussion.
        /// </summary>
        public bool IsFinished(double nowDspTime) => _next >= _slides.Count && nowDspTime >= _dueDsp;
    }
}
