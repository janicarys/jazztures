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

        public IReadOnlyList<Session> Sessions => _sessions;
    }
}
