using NUnit.Framework;
using Jazztures.Core.Ports;

namespace Jazztures.Tests.EditMode
{
    /// <summary>
    /// M0 sanity check: proves the headless test path works — <c>Jazztures.Core</c>
    /// compiles and its types are reachable from a test assembly both inside Unity's
    /// Test Runner and via <c>dotnet test DotNet/Jazztures.sln</c> (CLAUDE.md §2.1).
    /// Replaced by real domain coverage in M1 (Phase 1).
    /// </summary>
    public class CoreSmokeTests
    {
        [Test]
        public void Handedness_HasTwoDistinctMembers()
        {
            Assert.That(Handedness.Left, Is.Not.EqualTo(Handedness.Right));
        }
    }
}
