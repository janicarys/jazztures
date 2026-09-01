using Jazztures.Core.Ports;

namespace Jazztures.Tests.EditMode.TestSupport
{
    /// <summary>An <see cref="IHandPoseSource"/> whose frame the test sets directly.</summary>
    public sealed class FakeHandPoseSource : IHandPoseSource
    {
        public HandPoseFrame CurrentFrame { get; set; } = HandPoseFrame.Untracked;

        public void Set(
            HandPoseCandidate leftCandidate,
            TrackingQuality leftTracking = TrackingQuality.High,
            TrackingQuality rightTracking = TrackingQuality.High)
        {
            CurrentFrame = new HandPoseFrame(leftCandidate, leftTracking, rightTracking);
        }
    }
}
