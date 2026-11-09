using System.Collections.Generic;
using System.Linq;
using Jazztures.Core.Lessons;
using Jazztures.Core.Menu;
using Jazztures.Lessons;
using NUnit.Framework;
using UnityEditor;

namespace Jazztures.Tests.EditModeUnity
{
    // ADR-0081. These need the real assets, so they run only in the Unity editor (the headless `dotnet test` cannot see them).
    public class SwingLessonAssetTests
    {
        private const string Folder = "Assets/Jazztures/Lessons/";

        private static T Load<T>(string file) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Folder + file);
            Assert.That(asset, Is.Not.Null, file);
            return asset;
        }

        private static List<LessonDefinition> MenuLessons()
        {
            var catalog = Load<LessonCatalog>("LessonCatalog.asset");
            return catalog.Sessions.SelectMany(s => s.lessons).ToList();
        }

        [Test]
        public void TheMenu_IsTheFiveNewCards_InOrder()
        {
            List<LessonDefinition> lessons = MenuLessons();

            Assert.That(lessons.Select(l => l.Title), Is.EqualTo(new[] { "Swing", "Harmony", "Two-hand swing", "Sequences", "Motifs" }));
            Assert.That(lessons.Select(l => l.IsLocked), Is.EqualTo(new[] { false, false, false, false, false })); // the three samples are playable; the locked state is exercised below
        }

        [Test]
        public void CardOne_LaunchesTheSwingSequence_DirectlyAtSixtyBpm()
        {
            LessonDefinition swing = MenuLessons()[0];
            var model = new MenuModel(
                MenuLessons().Select(l => new MenuLessonInfo(l.Modes, l.TempoBpm, l.IsLocked, l.StartsDirectly)),
                offerFreePlay: false);
            var menu = new MenuNavigator(model);
            var intents = new List<MenuIntent>();
            menu.IntentRaised += intents.Add;

            menu.Handle(MenuGesture.Strike);

            Assert.That(swing.Sequence, Is.Not.Null);
            Assert.That(swing.Hands, Is.EqualTo(ActiveHands.Right));
            Assert.That(intents, Has.Count.EqualTo(1));
            Assert.That(intents[0].LessonIndex, Is.EqualTo(0));
            Assert.That(intents[0].TempoBpm, Is.EqualTo(60));
        }

        [Test]
        public void CardTwo_IsTheExistingChordsLesson_AModeScreenStillOpens()
        {
            LessonDefinition harmony = MenuLessons()[1];
            Assert.That(harmony, Is.EqualTo(Load<LessonDefinition>("Lesson_L2_Chords.asset")));
            Assert.That(harmony.StartsDirectly, Is.False);
            Assert.That(harmony.Hands, Is.EqualTo(ActiveHands.Left));
        }

        [Test]
        public void ALockedCard_IsNotStartable()
        {
            var model = new MenuModel(
                new[]
                {
                    new MenuLessonInfo(new[] { LearningMode.WatchAndListen }, 60),
                    new MenuLessonInfo(new[] { LearningMode.WatchAndListen }, 60, locked: true),
                },
                offerFreePlay: false);
            var menu = new MenuNavigator(model);
            var intents = new List<MenuIntent>();
            menu.IntentRaised += intents.Add;

            menu.Handle(MenuGesture.FlickLeft);
            menu.Handle(MenuGesture.Strike);

            Assert.That(menu.Screen, Is.EqualTo(MenuScreen.Main));
            Assert.That(intents, Is.Empty);
        }

        [Test]
        public void TheSequenceAsset_HoldsTheSameStepsAsTheBuilderDefaults()
        {
            var sequence = Load<LessonSequence>("Lesson_L1_SwingSequence.asset");
            IReadOnlyList<SwingStepSpec> asset = sequence.ToSpecs();
            IReadOnlyList<SwingStepSpec> defaults = SwingLessonBuilder.DefaultSteps();

            Assert.That(sequence.TempoBpm, Is.EqualTo(60));
            Assert.That(asset, Has.Count.EqualTo(defaults.Count));
            for (int i = 0; i < defaults.Count; i++)
            {
                Assert.That(asset[i].Bars, Is.EqualTo(defaults[i].Bars), defaults[i].Name);
                Assert.That(asset[i].StraightBars, Is.EqualTo(defaults[i].StraightBars), defaults[i].Name);
                Assert.That(asset[i].Modes, Is.EqualTo(defaults[i].Modes), defaults[i].Name);
                Assert.That(asset[i].Cards, Is.EqualTo(defaults[i].Cards), defaults[i].Name);
            }
        }

        [Test]
        public void EveryStep_BakesIntoALessonTheRunnerCanPlay()
        {
            var sequence = Load<LessonSequence>("Lesson_L1_SwingSequence.asset");
            for (int i = 0; i < sequence.StepCount; i++)
            {
                LessonDefinition step = sequence.BuildStep(i);
                try
                {
                    Assert.That(step.PitchSet, Is.Not.Null);
                    Assert.That(step.PitchSet.RegionCount, Is.EqualTo(9));
                    Assert.That(step.AudibleCountIn, Is.True);
                    Assert.That(step.ChordsOnTheBeat, Is.True);
                    LessonTimeline timeline = step.BuildTimeline(60);
                    Assert.That(timeline.DurationBeats, Is.EqualTo(16.0));
                    Assert.That(step.BuildPlan().Modes.Count, Is.GreaterThan(0));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(step);
                }
            }
        }
    }
}
