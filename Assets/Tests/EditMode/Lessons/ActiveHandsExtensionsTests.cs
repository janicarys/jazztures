using Jazztures.Core.Lessons;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Lessons
{
    public class ActiveHandsExtensionsTests
    {
        // Only a lesson about the right hand alone leaves the system to select the chords (ADR-0043 decision 3).
        [TestCase(ActiveHands.Right, true)]
        [TestCase(ActiveHands.Left, false)]
        [TestCase(ActiveHands.Both, false)]
        [TestCase(ActiveHands.None, false)]
        public void SystemSelectsChords_OnlyWhenTheRightHandPlaysAlone(ActiveHands hands, bool expected)
        {
            Assert.That(hands.SystemSelectsChords(), Is.EqualTo(expected));
        }
    }
}
