using Jazztures.Core.Ports;
using UnityEngine;

namespace Jazztures.Diagnostics
{
    /// <summary>
    /// Logs every change in an <see cref="IHandPoseSource"/>'s frame — pose candidate plus
    /// both hands' tracking quality — so the recogniser layer can be diagnosed from the
    /// Console or <c>adb logcat</c> without an in-headset HUD (CLAUDE.md §2.5,
    /// <c>Diagnostics/</c>). Bring-up aid for M3; add it next to a
    /// <c>MetaXRHandPoseSource</c> and point <see cref="_source"/> at it.
    ///
    /// <para>
    /// Logs only on change, so a held pose does not spam. Only the pose classification and
    /// tracking flags are printed — never raw joints (§4.1).
    /// </para>
    ///
    /// <para>
    /// <b>Off by default.</b> "On change" turned out to mean "every frame while a hand moves": the frame
    /// carries the hand's speeds, so it is never equal to the last one, and the log ran to several lines a
    /// frame, overwriting the rest of <c>adb logcat</c> within seconds. The poses are reliably detected now
    /// (the student, 2026-10-21), so it stays off until a recogniser bring-up session ticks
    /// <see cref="_logPoses"/>. It also builds a string every frame, which §2.4 does not allow on a
    /// release path.
    /// </para>
    /// </summary>
    public sealed class HandPoseFrameLogger : MonoBehaviour
    {
        [Tooltip("Log the pose frame to the Console / logcat. OFF by default: on a moving hand it is several lines per " +
                 "frame and drowns everything else. Tick it for a recogniser bring-up session.")]
        [SerializeField] private bool _logPoses;

        [Tooltip("The hand-pose source to watch (a component implementing IHandPoseSource).")]
        [SerializeField] private MonoBehaviour _source;

        [Tooltip("Re-log the unchanged frame this often, so a silent Console is never ambiguous. 0 disables.")]
        [SerializeField] private float _heartbeatSeconds = 2f;

        private IHandPoseSource _poseSource;
        private HandPoseFrame _last;
        private bool _hasLast;
        private float _nextHeartbeat;

        private void Awake()
        {
            if (!_logPoses)
            {
                enabled = false; // Update never runs, so nothing is read and no string is built
                return;
            }

            _poseSource = _source as IHandPoseSource;
            if (_poseSource == null)
            {
                Debug.LogError(
                    $"{nameof(HandPoseFrameLogger)}: '{(_source == null ? "<none>" : _source.GetType().Name)}' " +
                    $"is not an {nameof(IHandPoseSource)}.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            HandPoseFrame frame = _poseSource.CurrentFrame;

            // Time.time, not the DSP clock: this is a wall-clock diagnostic heartbeat, not
            // musical timing (§2.4 governs the latter).
            bool heartbeat = _heartbeatSeconds > 0f && Time.time >= _nextHeartbeat;
            if (_hasLast && frame == _last && !heartbeat)
            {
                return;
            }

            _last = frame;
            _hasLast = true;
            _nextHeartbeat = Time.time + _heartbeatSeconds;
            Debug.Log($"[HandPose] {frame}", this);
        }
    }
}
