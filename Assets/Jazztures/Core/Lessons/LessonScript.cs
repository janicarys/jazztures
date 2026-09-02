using System;
using System.Collections.Generic;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// The authored cue track for a lesson (ADR-0011) — an ordered list of
    /// <see cref="LessonCue"/>. Hand-authored on the Unity <c>LessonDefinition</c> asset;
    /// this is its pure form. Play it with a <see cref="LessonCuePlayer"/>.
    ///
    /// <para>Immutable. Build one with <see cref="LessonScriptBuilder"/>.</para>
    /// </summary>
    public sealed class LessonScript
    {
        private readonly LessonCue[] _cues;

        internal LessonScript(LessonCue[] cues) => _cues = cues;

        /// <summary>An empty script — a lesson with no captions or highlights.</summary>
        public static LessonScript Empty { get; } = new LessonScript(Array.Empty<LessonCue>());

        public IReadOnlyList<LessonCue> Cues => _cues;

        public int Count => _cues.Length;
    }
}
