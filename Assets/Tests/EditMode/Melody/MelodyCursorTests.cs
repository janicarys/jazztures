using Jazztures.Core.Melody;
using Jazztures.Core.Music;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    public class MelodyCursorTests
    {
        // Cmaj7 over 72..90: 72 74 76 79 83 84 86 88
        private MelodyCursor _cursor = null!;

        [SetUp]
        public void SetUp()
        {
            _cursor = new MelodyCursor();
            _cursor.SetPitchSet(PitchSet.ChordTones(Chord.Cmaj7, 72, 18));
        }

        private int[] Steps(params StepDirection[] directions)
        {
            var result = new int[directions.Length];
            for (int i = 0; i < directions.Length; i++)
            {
                result[i] = _cursor.Step(directions[i]).Midi;
            }

            return result;
        }

        [Test]
        public void ANewCursor_StartsInTheMiddleOfTheRange()
        {
            Assert.That(_cursor.CurrentPitch.Midi, Is.EqualTo(83)); // index 4 of 8
        }

        [Test]
        public void UU_FromTheBottom() { _cursor.Reset(new Pitch(72)); Assert.That(Steps(StepDirection.Up, StepDirection.Up), Is.EqualTo(new[] { 74, 76 })); }

        [Test]
        public void DD_FromTheTop() { _cursor.Reset(new Pitch(88)); Assert.That(Steps(StepDirection.Down, StepDirection.Down), Is.EqualTo(new[] { 86, 84 })); }

        [Test]
        public void UD_ReturnsToWhereItStarted() { _cursor.Reset(new Pitch(79)); Assert.That(Steps(StepDirection.Up, StepDirection.Down), Is.EqualTo(new[] { 83, 79 })); }

        [Test]
        public void DU_ReturnsToWhereItStarted() { _cursor.Reset(new Pitch(79)); Assert.That(Steps(StepDirection.Down, StepDirection.Up), Is.EqualTo(new[] { 76, 79 })); }

        [Test]
        public void Same_RepeatsThePitch()
        {
            _cursor.Reset(new Pitch(79));

            Assert.That(Steps(StepDirection.Same, StepDirection.Same), Is.EqualTo(new[] { 79, 79 }));
        }

        [Test]
        public void AtTheTop_ClampsAndRaisesAtLimit_ThenClearsOnTheNextStepThatMoves()
        {
            _cursor.Reset(new Pitch(86));
            Assert.That(_cursor.Step(StepDirection.Up).Midi, Is.EqualTo(88));
            Assert.That(_cursor.AtLimit, Is.False);

            Assert.That(_cursor.Step(StepDirection.Up).Midi, Is.EqualTo(88));
            Assert.That(_cursor.AtLimit, Is.True);

            _cursor.Step(StepDirection.Same);
            Assert.That(_cursor.AtLimit, Is.False);
        }

        [Test]
        public void AtTheBottom_ClampsAndRaisesAtLimit()
        {
            _cursor.Reset(new Pitch(72));

            Assert.That(_cursor.Step(StepDirection.Down).Midi, Is.EqualTo(72));
            Assert.That(_cursor.AtLimit, Is.True);
        }

        [Test]
        public void NeverWraps_AnUpGestureAtTheTopDoesNotSoundLower()
        {
            _cursor.Reset(new Pitch(88));

            Assert.That(_cursor.Step(StepDirection.Up).Midi, Is.GreaterThanOrEqualTo(88));
        }

        [Test]
        public void ChordChange_SnapsToTheNearestPitchInTheNewSet()
        {
            _cursor.Reset(new Pitch(79)); // G
            _cursor.SetPitchSet(PitchSet.ChordTones(Chord.G7, 72, 18)); // has 79

            Assert.That(_cursor.CurrentPitch.Midi, Is.EqualTo(79));

            _cursor.Reset(new Pitch(76));
            _cursor.SetPitchSet(PitchSet.ChordTones(Chord.Cmaj7, 72, 18)); // back; E exists
            _cursor.SetPitchSet(PitchSet.ChordTones(Chord.G7, 72, 18));   // 74 and 77 around 76

            Assert.That(_cursor.CurrentPitch.Midi, Is.EqualTo(77));
        }

        [Test]
        public void ChordChange_TieGoesToTheLowerPitch()
        {
            _cursor.Reset(new Pitch(79));
            _cursor.SetPitchSet(PitchSet.ChordTones(Chord.Dm7, 72, 18)); // 77 and 81 are both 2 away

            Assert.That(_cursor.CurrentPitch.Midi, Is.EqualTo(77));
        }

        [Test]
        public void Reset_PlaysNothing_AndTheNextStepStartsFromTheStartPitch()
        {
            _cursor.Step(StepDirection.Up);
            _cursor.Reset(new Pitch(72));

            Assert.That(_cursor.CurrentPitch.Midi, Is.EqualTo(72));
            Assert.That(_cursor.Step(StepDirection.Up).Midi, Is.EqualTo(74));
        }

        [Test]
        public void Reset_ToAPitchOutsideTheSet_SnapsToTheNearestMember_LowerOnATie()
        {
            _cursor.Reset(new Pitch(75)); // 74 and 76 are both 1 away
            Assert.That(_cursor.CurrentPitch.Midi, Is.EqualTo(74));

            _cursor.Reset(new Pitch(110));
            Assert.That(_cursor.CurrentPitch.Midi, Is.EqualTo(88));
        }

        [Test]
        public void Reset_ClearsAtLimit()
        {
            _cursor.Reset(new Pitch(72));
            _cursor.Step(StepDirection.Down);
            _cursor.Reset(new Pitch(72));

            Assert.That(_cursor.AtLimit, Is.False);
        }

        [Test]
        public void ResetToChordRoot_UsesTheRootNearestTheMiddle()
        {
            _cursor.ResetToChordRoot(); // Cmaj7, range 72..90, middle 81: C84 is 3 away, C72 is 9 away

            Assert.That(_cursor.CurrentPitch.Midi, Is.EqualTo(84));
        }

        [Test]
        public void WithNoSet_StepThrows()
        {
            Assert.Throws<System.InvalidOperationException>(() => new MelodyCursor().Step(StepDirection.Up));
        }
    }
}
