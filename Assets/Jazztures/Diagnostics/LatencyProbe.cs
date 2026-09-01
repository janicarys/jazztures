using Jazztures.Core.Diagnostics;
using Jazztures.Core.Gesture;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Jazztures.Diagnostics
{
    /// <summary>
    /// Collects gesture→sound latency samples and reports percentiles (CLAUDE.md §4.3 —
    /// "Report these numbers in the thesis"). Bound by the composition root.
    ///
    /// <para>
    /// Currently wired: <see cref="LatencyStage.PoseToConfirm"/> (from the interpreter's
    /// hold time) and <see cref="LatencyStage.NoteEventToScheduled"/> (from the sampler,
    /// which shares this probe's <see cref="Recorder"/>). The remaining stages fill in
    /// once the real gesture path exists.
    /// </para>
    ///
    /// <para>Press <see cref="_reportKey"/> to log a summary; it also logs on quit.</para>
    /// </summary>
    public sealed class LatencyProbe : MonoBehaviour
    {
        [SerializeField] private Key _reportKey = Key.F10;

        public LatencyRecorder Recorder { get; } = new LatencyRecorder();

        private GestureInterpreter _interpreter;

        public void Bind(GestureInterpreter interpreter)
        {
            _interpreter = interpreter;
            if (_interpreter != null)
            {
                _interpreter.ConfirmedFunctionChanged += OnConfirmed;
            }
        }

        private void OnConfirmed(Core.Harmony.ChordFunction? _)
        {
            double holdSeconds = _interpreter.LastConfirmationHoldSeconds;
            if (!double.IsNaN(holdSeconds))
            {
                Recorder.Record(LatencyStage.PoseToConfirm, holdSeconds * 1000.0);
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[_reportKey].wasPressedThisFrame)
            {
                Report();
            }
        }

        private void OnApplicationQuit() => Report();

        public void Report()
        {
            var builder = new System.Text.StringBuilder("Latency (ms):\n");
            foreach (LatencyStage stage in System.Enum.GetValues(typeof(LatencyStage)))
            {
                builder.Append("  ").Append(Recorder.Summarize(stage)).Append('\n');
            }

            Debug.Log(builder.ToString(), this);
        }
    }
}
