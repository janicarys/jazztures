using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jazztures.Lessons
{
    /// <summary>
    /// The ordered set of sessions and their lessons (CLAUDE.md §3.9 — S1 = L1+L2+L3 …
    /// S5 = L8). The data source for the in-VR lesson selector.
    ///
    /// <para>
    /// §3.9: "adding Lesson 9 must require no C# changes" — create a
    /// <see cref="LessonDefinition"/> asset and drag it into a session here.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Lesson Catalog", fileName = "LessonCatalog")]
    public sealed class LessonCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Session
        {
            [Tooltip("Learner-facing, e.g. \"Session 1\".")]
            public string title = "Session";

            [Tooltip("Lessons in the order the learner works through them.")]
            public List<LessonDefinition> lessons = new List<LessonDefinition>();
        }

        [SerializeField] private List<Session> _sessions = new List<Session>();

        [Tooltip("Timing-drill presets for the \"Timing test\" menu (ADR-0043) — the measuring " +
                 "instrument for comparing right-hand mechanics against a beat. Not part of any " +
                 "session; a new preset is a new asset.")]
        [SerializeField] private List<TimingDrillDefinition> _timingDrills = new List<TimingDrillDefinition>();

        public IReadOnlyList<Session> Sessions => _sessions;

        public IReadOnlyList<TimingDrillDefinition> TimingDrills => _timingDrills;
    }
}
