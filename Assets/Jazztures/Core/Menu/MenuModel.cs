using System;
using System.Collections.Generic;

namespace Jazztures.Core.Menu
{
    /// <summary>
    /// The shape of the menu tree (ADR-0051), flattened from whatever the app's lesson catalog
    /// holds: an ordered list of lessons, whether Free play is offered, and the tempo presets the
    /// tempo card cycles through (none ⇒ no tempo card). Immutable.
    /// </summary>
    public sealed class MenuModel
    {
        private readonly MenuLessonInfo[] _lessons;
        private readonly double[] _tempoPresets;

        public MenuModel(
            IEnumerable<MenuLessonInfo> lessons,
            bool offerFreePlay,
            IEnumerable<double>? tempoPresets = null)
        {
            if (lessons == null)
            {
                throw new ArgumentNullException(nameof(lessons));
            }

            _lessons = new List<MenuLessonInfo>(lessons).ToArray();

            var presets = new SortedSet<double>();
            if (tempoPresets != null)
            {
                foreach (double bpm in tempoPresets)
                {
                    if (double.IsNaN(bpm) || double.IsInfinity(bpm) || bpm <= 0.0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(tempoPresets), bpm, "Presets must be finite and positive.");
                    }

                    presets.Add(bpm);
                }
            }

            _tempoPresets = new double[presets.Count];
            presets.CopyTo(_tempoPresets);
            OfferFreePlay = offerFreePlay;
        }

        public IReadOnlyList<MenuLessonInfo> Lessons => _lessons;

        public bool OfferFreePlay { get; }

        /// <summary>Ascending, distinct.</summary>
        public IReadOnlyList<double> TempoPresets => _tempoPresets;

        public bool OffersTempo => _tempoPresets.Length > 0;
    }
}
