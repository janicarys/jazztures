using Jazztures.Core.Melody;

namespace Jazztures.Input
{
    /// <summary>
    /// Where <see cref="PalmSwingInputSource"/> gets one frame of right-hand state, already in the lane frame
    /// (ADR-0078). The real headset implementation is <see cref="MetaXRPalmSwingSampleSource"/>; the editor
    /// uses <c>EditorSimulatedPalmInput</c> so the whole chain runs in Play mode without a headset.
    /// </summary>
    public interface IPalmSwingSampleSource
    {
        /// <summary>
        /// The current frame. <see cref="PalmSwingSample.GateOpen"/> is false whenever the hand is not usable
        /// (lost, low confidence, palm not facing left, hand not open); the other fields are then irrelevant.
        /// The sample's time is supplied by the caller's clock, so implementations leave it 0.
        /// </summary>
        PalmSwingSample Read();

        /// <summary>Make the lane's origin and axis be where the hand and head are now.</summary>
        void Recenter();
    }
}
