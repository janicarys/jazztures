using System.Collections.Generic;
using Jazztures.Core.Lessons;
using Jazztures.Core.Menu;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Menu
{
    public class MenuNavigatorTests
    {
        private static readonly double[] Presets = { 60, 70, 80, 90, 100 };

        private static MenuModel Model(bool freePlay = true, double[]? presets = null) => new MenuModel(
            new[]
            {
                new MenuLessonInfo(new[] { LearningMode.GestureLearning }, 80),
                new MenuLessonInfo(
                    new[] { LearningMode.WatchAndListen, LearningMode.TryYourself, LearningMode.TestYourself }, 80),
                new MenuLessonInfo(new[] { LearningMode.WatchAndListen, LearningMode.TryYourself }, 90),
            },
            freePlay,
            presets ?? Presets);

        private MenuNavigator _menu = null!;
        private List<MenuIntent> _intents = null!;
        private int _changes;
        private int _aborts;

        [SetUp]
        public void SetUp() => Build(Model());

        private void Build(MenuModel model)
        {
            _menu = new MenuNavigator(model);
            _intents = new List<MenuIntent>();
            _changes = 0;
            _aborts = 0;
            _menu.IntentRaised += _intents.Add;
            _menu.Changed += () => _changes++;
            _menu.RunAborted += () => _aborts++;
        }

        private void Flick(int steps)
        {
            for (int i = 0; i < System.Math.Abs(steps); i++)
            {
                _menu.Handle(steps > 0 ? MenuGesture.FlickLeft : MenuGesture.FlickRight);
            }
        }

        private void Strike() => _menu.Handle(MenuGesture.Strike);

        private void OpenLesson(int lesson)
        {
            Flick(lesson);
            Strike();
        }

        private void StartMode(int lesson, int modeIndex)
        {
            OpenLesson(lesson);
            Flick(modeIndex);
            Strike();
        }

        private MenuItem Current => _menu.Items[_menu.Selected];

        // --- main screen -----------------------------------------------------------------

        [Test]
        public void ItStartsOnTheMainScreen_WithTheLessonsThenFreePlay()
        {
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Main));
            Assert.That(_menu.IsRunning, Is.False);
            Assert.That(_menu.Items, Is.EqualTo(new[]
            {
                new MenuItem(MenuItemKind.Lesson, 0),
                new MenuItem(MenuItemKind.Lesson, 1),
                new MenuItem(MenuItemKind.Lesson, 2),
                new MenuItem(MenuItemKind.FreePlay),
            }));
            Assert.That(_menu.Selected, Is.EqualTo(0));
        }

        [Test]
        public void FreePlay_CanBeLeftOutOfTheMenu()
        {
            Build(Model(freePlay: false));

            Assert.That(_menu.Items, Has.Count.EqualTo(3));
            Assert.That(_menu.Items, Has.None.Matches<MenuItem>(i => i.Kind == MenuItemKind.FreePlay));
        }

        [Test]
        public void FlickingMovesTheHighlight()
        {
            Flick(+1);
            Assert.That(_menu.Selected, Is.EqualTo(1));

            Flick(+2);
            Assert.That(_menu.Selected, Is.EqualTo(3));

            Flick(-3);
            Assert.That(_menu.Selected, Is.EqualTo(0));
        }

        [Test]
        public void TheHighlightWrapsAround_PastTheLastCardToTheFirst_AndBack()
        {
            Flick(+3);
            Assert.That(_menu.Selected, Is.EqualTo(3), "the last of four cards");

            Flick(+1);
            Assert.That(_menu.Selected, Is.EqualTo(0), "past the end: the first");

            Flick(-1);
            Assert.That(_menu.Selected, Is.EqualTo(3), "before the start: the last");
        }

        [Test]
        public void ALongRunOfFlicks_StaysOnTheRing()
        {
            Flick(+23); // 23 mod 4 = 3
            Assert.That(_menu.Selected, Is.EqualTo(3));

            Flick(-23);
            Assert.That(_menu.Selected, Is.EqualTo(0));
        }

        [Test]
        public void FlickingLeftAdvancesToTheNextCard_AndFlickingRightGoesBack()
        {
            _menu.Handle(MenuGesture.FlickLeft);
            Assert.That(_menu.Selected, Is.EqualTo(1));

            _menu.Handle(MenuGesture.FlickLeft);
            Assert.That(_menu.Selected, Is.EqualTo(2));

            _menu.Handle(MenuGesture.FlickRight);
            Assert.That(_menu.Selected, Is.EqualTo(1));
        }

        [Test]
        public void AFlickPastTheEnd_WrapsAndRaisesChanged_NotBumped()
        {
            int bumps = 0;
            _menu.Bumped += _ => bumps++;
            _changes = 0;

            Flick(-1); // from the first card, wraps to the last

            Assert.That(_menu.Selected, Is.EqualTo(3));
            Assert.That(_changes, Is.EqualTo(1));
            Assert.That(bumps, Is.EqualTo(0));
        }

        [Test]
        public void ASingleCardRow_CannotMove_ButBumpsInTheDirectionOfTheFlick()
        {
            Build(new MenuModel(
                new[] { new MenuLessonInfo(new[] { LearningMode.WatchAndListen }, 80) },
                offerFreePlay: false));
            Assume.That(_menu.Items, Has.Count.EqualTo(1));

            var bumps = new List<int>();
            _menu.Bumped += bumps.Add;
            _changes = 0;

            Flick(+1);
            Flick(-1);

            Assert.That(bumps, Is.EqualTo(new[] { +1, -1 }));
            Assert.That(_changes, Is.EqualTo(0));
            Assert.That(_menu.Selected, Is.EqualTo(0));
        }

        [Test]
        public void AFlickThatMoves_DoesNotBump()
        {
            int bumps = 0;
            _menu.Bumped += _ => bumps++;

            Flick(+1);

            Assert.That(bumps, Is.EqualTo(0));
        }

        [Test]
        public void AFlickOnAnEmptyMenu_DoesNotBump()
        {
            Build(new MenuModel(new MenuLessonInfo[0], offerFreePlay: false));
            int bumps = 0;
            _menu.Bumped += _ => bumps++;

            Flick(+1);

            Assert.That(bumps, Is.EqualTo(0));
        }

        [Test]
        public void AFlick_RaisesChanged()
        {
            Flick(+1);

            Assert.That(_changes, Is.EqualTo(1));
        }

        // --- modes screen ----------------------------------------------------------------

        [Test]
        public void StrikingALesson_OpensItsModes_ThenTempo_ThenBack()
        {
            OpenLesson(1);

            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Modes));
            Assert.That(_menu.LessonIndex, Is.EqualTo(1));
            Assert.That(_menu.Items, Is.EqualTo(new[]
            {
                new MenuItem(MenuItemKind.Mode, 1, LearningMode.WatchAndListen),
                new MenuItem(MenuItemKind.Mode, 1, LearningMode.TryYourself),
                new MenuItem(MenuItemKind.Mode, 1, LearningMode.TestYourself),
                new MenuItem(MenuItemKind.Tempo, 1),
                new MenuItem(MenuItemKind.Back, 1),
            }));
            Assert.That(_menu.Selected, Is.EqualTo(0));
        }

        [Test]
        public void TheTempoCard_IsLeftOut_WhenThereAreNoPresets()
        {
            Build(Model(presets: new double[0]));

            OpenLesson(1);

            Assert.That(_menu.Items, Has.None.Matches<MenuItem>(i => i.Kind == MenuItemKind.Tempo));
        }

        [Test]
        public void ALessonWithOneMode_StillGetsTempoAndBack()
        {
            OpenLesson(0);

            Assert.That(_menu.Items, Has.Count.EqualTo(3));
            Assert.That(Current.Mode, Is.EqualTo(LearningMode.GestureLearning));
        }

        [Test]
        public void TheBackCard_ReturnsToMain_HighlightingTheLessonYouCameFrom()
        {
            OpenLesson(2);
            Flick(+3);                       // to Back, the last of four cards
            Assert.That(Current.Kind, Is.EqualTo(MenuItemKind.Back));

            Strike();

            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Main));
            Assert.That(_menu.Selected, Is.EqualTo(2));
        }

        [Test]
        public void AFist_GoesHome_FromTheModesScreen()
        {
            OpenLesson(1);

            _menu.Handle(MenuGesture.Fist);

            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Main));
            Assert.That(_menu.Selected, Is.EqualTo(1));
            Assert.That(_aborts, Is.EqualTo(0));
        }

        [Test]
        public void AFist_OnTheMainScreen_DoesNothing()
        {
            _changes = 0;

            _menu.Handle(MenuGesture.Fist);

            Assert.That(_changes, Is.EqualTo(0));
        }

        // --- tempo -----------------------------------------------------------------------

        [Test]
        public void StrikingTheTempoCard_StepsToTheNextPreset_AndStaysPut()
        {
            OpenLesson(1);              // authored at 80
            Flick(+3);
            Assert.That(Current.Kind, Is.EqualTo(MenuItemKind.Tempo));

            Strike();

            Assert.That(_menu.TempoBpm(1), Is.EqualTo(90));
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Modes));
            Assert.That(_menu.Selected, Is.EqualTo(3));
            Assert.That(_intents, Is.Empty);
        }

        [Test]
        public void TheTempoCard_WrapsFromTheFastestPresetToTheSlowest()
        {
            OpenLesson(1);
            Flick(+3);

            Strike(); // 90
            Strike(); // 100
            Strike(); // wraps

            Assert.That(_menu.TempoBpm(1), Is.EqualTo(60));
        }

        [Test]
        public void ATempoOffThePresetGrid_StepsToTheNextPresetAbove()
        {
            Build(new MenuModel(
                new[] { new MenuLessonInfo(new[] { LearningMode.WatchAndListen }, 75) },
                offerFreePlay: false,
                tempoPresets: Presets));

            OpenLesson(0);
            Flick(+1);
            Strike();

            Assert.That(_menu.TempoBpm(0), Is.EqualTo(80));
        }

        [Test]
        public void EachLessonRemembersItsOwnTempo()
        {
            OpenLesson(1);
            Flick(+3);
            Strike();                       // lesson 1 -> 90
            _menu.Handle(MenuGesture.Fist);

            Assert.That(_menu.TempoBpm(1), Is.EqualTo(90));
            Assert.That(_menu.TempoBpm(2), Is.EqualTo(90)); // its own authored 90, untouched
            Assert.That(_menu.TempoBpm(0), Is.EqualTo(80));
        }

        // --- starting a run --------------------------------------------------------------

        [Test]
        public void StrikingAMode_RaisesAnIntent_AndStepsAside()
        {
            StartMode(lesson: 1, modeIndex: 1);

            Assert.That(_menu.IsRunning, Is.True);
            Assert.That(_intents, Has.Count.EqualTo(1));
            Assert.That(_intents[0].Kind, Is.EqualTo(MenuIntentKind.StartLesson));
            Assert.That(_intents[0].LessonIndex, Is.EqualTo(1));
            Assert.That(_intents[0].Mode, Is.EqualTo(LearningMode.TryYourself));
            Assert.That(_intents[0].TempoBpm, Is.EqualTo(80));
        }

        [Test]
        public void TheIntent_CarriesTheTempoTheLearnerChose()
        {
            OpenLesson(1);
            Flick(+3);
            Strike();                       // 90
            Flick(-3);
            Strike();                       // first mode

            Assert.That(_intents[0].TempoBpm, Is.EqualTo(90));
        }

        [Test]
        public void StrikingFreePlay_RaisesAFreePlayIntent()
        {
            Flick(+3);

            Strike();

            Assert.That(_menu.IsRunning, Is.True);
            Assert.That(_intents, Has.Count.EqualTo(1));
            Assert.That(_intents[0].Kind, Is.EqualTo(MenuIntentKind.StartFreePlay));
        }

        [Test]
        public void WhileARunIsGoing_FlicksAndStrikes_AreIgnored()
        {
            StartMode(1, 0);
            _changes = 0;

            Flick(+2);
            Strike();

            Assert.That(_changes, Is.EqualTo(0));
            Assert.That(_intents, Has.Count.EqualTo(1));
        }

        [Test]
        public void AFist_DuringARun_AbandonsIt_AndLandsOnThatLesson()
        {
            StartMode(2, 0);

            _menu.Handle(MenuGesture.Fist);

            Assert.That(_menu.IsRunning, Is.False);
            Assert.That(_aborts, Is.EqualTo(1));
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Main));
            Assert.That(_menu.Selected, Is.EqualTo(2));
        }

        [Test]
        public void AFist_DuringFreePlay_LandsOnTheFreePlayCard()
        {
            Flick(+3);
            Strike();

            _menu.Handle(MenuGesture.Fist);

            Assert.That(_aborts, Is.EqualTo(1));
            Assert.That(Current.Kind, Is.EqualTo(MenuItemKind.FreePlay));
        }

        [Test]
        public void GoHome_WhenARunCouldNotStart_ReturnsToMain()
        {
            StartMode(1, 0);

            _menu.GoHome();

            Assert.That(_menu.IsRunning, Is.False);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Main));
        }

        // --- after a run -----------------------------------------------------------------

        [Test]
        public void WhenAModeFinishes_ThePostRunScreenOffersBackToModes_Restart_Next()
        {
            StartMode(1, 0);                            // Watch and listen

            _menu.RunCompleted();

            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.PostRun));
            Assert.That(_menu.IsRunning, Is.False);
            Assert.That(_menu.Items, Is.EqualTo(new[]
            {
                new MenuItem(MenuItemKind.BackToModes, 1),
                new MenuItem(MenuItemKind.Restart, 1, LearningMode.WatchAndListen),
                new MenuItem(MenuItemKind.Next, 1, LearningMode.TryYourself),
            }));
            Assert.That(_menu.Selected, Is.EqualTo(0), "the safe card is highlighted");
        }

        [Test]
        public void Restart_RunsTheSameModeAgain_AtTheSameTempo()
        {
            StartMode(1, 1);
            _menu.RunCompleted();
            _intents.Clear();
            Flick(+1);

            Strike();

            Assert.That(_menu.IsRunning, Is.True);
            Assert.That(_intents, Has.Count.EqualTo(1));
            Assert.That(_intents[0].LessonIndex, Is.EqualTo(1));
            Assert.That(_intents[0].Mode, Is.EqualTo(LearningMode.TryYourself));
        }

        [Test]
        public void Next_StartsTheNextModeOfTheSameLesson()
        {
            StartMode(1, 0);
            _menu.RunCompleted();
            _intents.Clear();
            Flick(+2);

            Strike();

            Assert.That(_intents, Has.Count.EqualTo(1));
            Assert.That(_intents[0].LessonIndex, Is.EqualTo(1));
            Assert.That(_intents[0].Mode, Is.EqualTo(LearningMode.TryYourself));
        }

        [Test]
        public void Next_AfterTheLastMode_OpensTheNextLessonsModes_WithoutStartingAnything()
        {
            StartMode(1, 2);                            // Test yourself: lesson 1's last mode
            _menu.RunCompleted();
            _intents.Clear();
            Flick(+2);
            Assert.That(Current.Kind, Is.EqualTo(MenuItemKind.Next));

            Strike();

            Assert.That(_intents, Is.Empty);
            Assert.That(_menu.IsRunning, Is.False);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Modes));
            Assert.That(_menu.LessonIndex, Is.EqualTo(2));
            Assert.That(_menu.Selected, Is.EqualTo(0));
        }

        [Test]
        public void AfterTheLastModeOfTheLastLesson_ThereIsNoNext()
        {
            StartMode(2, 1);
            _menu.RunCompleted();

            Assert.That(_menu.Items, Is.EqualTo(new[]
            {
                new MenuItem(MenuItemKind.BackToModes, 2),
                new MenuItem(MenuItemKind.Restart, 2, LearningMode.TryYourself),
            }));
        }

        [Test]
        public void BackToModes_ReturnsToTheLessonsModes_WithTheModeJustPlayedHighlighted()
        {
            StartMode(1, 2);
            _menu.RunCompleted();

            Strike();                                   // BackToModes is highlighted

            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Modes));
            Assert.That(_menu.LessonIndex, Is.EqualTo(1));
            Assert.That(Current.Mode, Is.EqualTo(LearningMode.TestYourself));
        }

        [Test]
        public void AFist_OnThePostRunScreen_GoesHome()
        {
            StartMode(1, 0);
            _menu.RunCompleted();

            _menu.Handle(MenuGesture.Fist);

            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Main));
            Assert.That(_menu.Selected, Is.EqualTo(1));
        }

        [Test]
        public void RunCompleted_WhenNothingIsRunning_IsIgnored()
        {
            _changes = 0;

            _menu.RunCompleted();

            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Main));
            Assert.That(_changes, Is.EqualTo(0));
        }

        [Test]
        public void RunCompleted_DuringFreePlay_IsIgnored()
        {
            Flick(+3);
            Strike();

            _menu.RunCompleted();

            Assert.That(_menu.IsRunning, Is.True);
        }

        // --- degenerate ------------------------------------------------------------------

        [Test]
        public void AnEmptyMenu_DoesNotThrow_AndDoesNothing()
        {
            Build(new MenuModel(new MenuLessonInfo[0], offerFreePlay: false));

            Assert.DoesNotThrow(() =>
            {
                Flick(+1);
                Strike();
                _menu.Handle(MenuGesture.Fist);
            });
            Assert.That(_menu.Items, Is.Empty);
            Assert.That(_intents, Is.Empty);
        }

        [Test]
        public void ALessonWithNoModes_IsRejectedWhenTheModelIsBuilt()
        {
            Assert.Throws<System.ArgumentException>(() => new MenuLessonInfo(new LearningMode[0], 80));
        }
    }
}
