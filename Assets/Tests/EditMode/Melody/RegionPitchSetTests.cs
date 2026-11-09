using Jazztures.Core.Melody;
using Jazztures.Core.Music;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    public class RegionPitchSetTests
    {
        private static int[] Midis(RegionPitchSet set)
        {
            var result = new int[set.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = set[i].Midi;
            }

            return result;
        }

        // The default count; the fixed-9 tests below pin the count parameter's behaviour, not the branch default.
        private const int DefaultCount = HeightRegionLayout.DefaultRegionCount;

        [Test]
        public void BothLessonOctaves_AscendingChordTones_FromTheDefaultLow()
        {
            Assert.That(Midis(RegionPitchSet.Build(PitchSetKind.ChordTones, Chord.Cmaj7, 72, DefaultCount)),
                Is.EqualTo(new[] { 72, 74, 76, 79, 83, 84, 86, 88, 91, 95, 96, 98, 100, 103, 107 }));
            Assert.That(Midis(RegionPitchSet.Build(PitchSetKind.ChordTones, Chord.Dm7, 72, DefaultCount)),
                Is.EqualTo(new[] { 72, 74, 76, 77, 81, 84, 86, 88, 89, 93, 96, 98, 100, 101, 105 }));
            Assert.That(Midis(RegionPitchSet.Build(PitchSetKind.ChordTones, Chord.G7, 72, DefaultCount)),
                Is.EqualTo(new[] { 74, 77, 79, 81, 83, 86, 89, 91, 93, 95, 98, 101, 103, 105, 107 }));
        }

        [Test]
        public void Dm7_FillsTheRegions() =>
            Assert.That(Midis(RegionPitchSet.Build(PitchSetKind.ChordTones, Chord.Dm7, 72, 9)),
                Is.EqualTo(new[] { 72, 74, 76, 77, 81, 84, 86, 88, 89 }));

        [Test]
        public void G7_FillsTheRegions() =>
            Assert.That(Midis(RegionPitchSet.Build(PitchSetKind.ChordTones, Chord.G7, 72, 9)),
                Is.EqualTo(new[] { 74, 77, 79, 81, 83, 86, 89, 91, 93 }));

        [Test]
        public void Cmaj7_FillsTheRegions() =>
            Assert.That(Midis(RegionPitchSet.Build(PitchSetKind.ChordTones, Chord.Cmaj7, 72, 9)),
                Is.EqualTo(new[] { 72, 74, 76, 79, 83, 84, 86, 88, 91 }));

        [Test]
        public void CMajorScale_IsTheSameWhateverTheChord() =>
            Assert.That(Midis(RegionPitchSet.Build(PitchSetKind.CMajorScale, Chord.G7, 72, 9)),
                Is.EqualTo(new[] { 72, 74, 76, 77, 79, 81, 83, 84, 86 }));

        [Test]
        public void Pitches_AscendWithRegion()
        {
            foreach (Chord chord in new[] { Chord.Dm7, Chord.G7, Chord.Cmaj7 })
            {
                RegionPitchSet set = RegionPitchSet.Build(PitchSetKind.ChordTones, chord, 72, 9);
                for (int i = 1; i < set.Count; i++)
                {
                    Assert.That(set[i].Midi, Is.GreaterThan(set[i - 1].Midi));
                }
            }
        }

        // Every pitch the current lessons use, both octaves: chord-tone indices 0-9 (ChordToneSet.TargetCount) of each
        // chord must land on a region, whether the low pitch is the default 72 or the lowest 70 that 15 regions fit.
        [Test]
        public void EveryLessonPitch_LandsOnARegion_ForItsChord()
        {
            foreach (int low in new[] { 70, 72 })
            {
                foreach (Chord chord in new[] { Chord.Dm7, Chord.G7, Chord.Cmaj7 })
                {
                    RegionPitchSet set = RegionPitchSet.Build(PitchSetKind.ChordTones, chord, low, DefaultCount);
                    ChordToneSet tones = ChordToneSet.For(chord);
                    for (int i = 0; i < ChordToneSet.TargetCount; i++)
                    {
                        Assert.That(set.TryRegionOf(tones[i].Pitch, out _), Is.True,
                            $"{chord} lowPitch {low}: target {i} = {tones[i].Pitch}");
                    }
                }
            }
        }

        [Test]
        public void TryRegionOf_ReportsAMissAndTheNearestRegion()
        {
            RegionPitchSet set = RegionPitchSet.Build(PitchSetKind.ChordTones, Chord.Cmaj7, 72, 9);
            Assert.That(set.TryRegionOf(new Pitch(73), out int region), Is.False);
            Assert.That(region, Is.EqualTo(0)); // 72 and 74 tie: the lower
        }
    }
}
