using System;
using System.Collections.Generic;
using Jazztures.Core.Tutorial;
using UnityEngine;

namespace Jazztures.Lessons
{
    /// <summary>
    /// The tutorial script as data (ADR-0053) — the special lesson that runs on startup and introduces
    /// someone who has never worn a headset to the system. Like a <see cref="LessonDefinition"/> it is
    /// authored in the Inspector and baked into its pure <c>Jazztures.Core</c> form
    /// (<see cref="BuildPlan"/>); changing a line, its order or what it waits for needs no C#.
    ///
    /// <para>
    /// A new asset starts with the shipped script: place your hands in front of you → move them out of
    /// view → bring them back → (five seconds of explanation) → make a fist to open the menu → open
    /// the hand, thumb up → flick to navigate → strike to select a lesson. Each step waits for the
    /// learner to <i>do</i> it (except the explanation), and the ghost hand shows how.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Tutorial Definition", fileName = "Tutorial")]
    public sealed class TutorialDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Step
        {
            [Tooltip("What the tutorial says. Plain language (§1.5): the learner may never have used a headset.")]
            [TextArea(1, 3)] public string text = string.Empty;

            [Tooltip("What the learner must do before the next line.")]
            public TutorialGate gate;

            [Tooltip("Only for 'After Seconds': how long the line stays up.")]
            [Min(0f)] public float gateSeconds;

            [Tooltip("What the ghost hand shows while this line is up.")]
            public TutorialDemo demo;

            [Tooltip("From this step on, the lesson menu is on screen (and the sky comes up).")]
            public bool showsMenu;

            [Tooltip("Which menu gestures this step lets through to the real menu. A step lets through only " +
                     "what has been taught so far, so a stray strike cannot pick a lesson early.")]
            public TutorialForward forward;
        }

        [SerializeField] private List<Step> _steps = DefaultSteps();

        public IReadOnlyList<Step> Steps => _steps;

        /// <summary>Bake the authored steps into the pure form the app runs.</summary>
        public TutorialPlan BuildPlan()
        {
            var steps = new List<TutorialStep>(_steps.Count);
            foreach (Step s in _steps)
            {
                steps.Add(new TutorialStep(s.text, s.gate, s.gateSeconds, s.demo, s.showsMenu, s.forward));
            }

            return new TutorialPlan(steps);
        }

        /// <summary>The shipped script (the student's, ADR-0053).</summary>
        private static List<Step> DefaultSteps() => new List<Step>
        {
            new Step { text = "Place your hands in front of you.", gate = TutorialGate.HandsInView },
            new Step { text = "Move your hands to your side, out of view.", gate = TutorialGate.HandsOutOfView },
            new Step { text = "Bring your hands back in front of you.", gate = TutorialGate.HandsInView },
            new Step
            {
                text = "When you put your hands in front of you, the VR headset can detect it.",
                gate = TutorialGate.AfterSeconds,
                gateSeconds = 5f,
            },
            new Step
            {
                text = "To open the menu, make a fist with your right hand.",
                gate = TutorialGate.RightFist,
                demo = TutorialDemo.Fist,
            },
            new Step
            {
                text = "Now open your right hand with your thumb facing up.",
                gate = TutorialGate.OpenHandThumbUp,
                demo = TutorialDemo.ThumbUp,
                showsMenu = true,
            },
            new Step
            {
                text = "To navigate the menu, flick your hand left or right.",
                gate = TutorialGate.Flick,
                demo = TutorialDemo.Flick,
                showsMenu = true,
                forward = TutorialForward.Flick,
            },
            new Step
            {
                text = "Strike your hand down to select a lesson.",
                gate = TutorialGate.Strike,
                demo = TutorialDemo.Strike,
                showsMenu = true,
                forward = TutorialForward.Flick | TutorialForward.Strike,
            },
        };

#if UNITY_EDITOR
        private void OnValidate()
        {
            try
            {
                BuildPlan();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{name}] the tutorial script is not valid yet: {e.Message}", this);
            }
        }
#endif
    }
}
