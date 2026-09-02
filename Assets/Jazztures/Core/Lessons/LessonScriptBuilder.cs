using System.Collections.Generic;

namespace Jazztures.Core.Lessons
{
    /// <summary>Assembles a <see cref="LessonScript"/>. Cues keep their authored order.</summary>
    public sealed class LessonScriptBuilder
    {
        private readonly List<LessonCue> _cues = new List<LessonCue>();

        public LessonScriptBuilder Cue(CueTrigger trigger, CueAction action)
        {
            _cues.Add(new LessonCue(trigger, action));
            return this;
        }

        public LessonScriptBuilder Cue(LessonCue cue)
        {
            _cues.Add(cue);
            return this;
        }

        public LessonScript Build() => new LessonScript(_cues.ToArray());
    }
}
