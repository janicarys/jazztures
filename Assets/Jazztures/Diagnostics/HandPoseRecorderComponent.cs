using System;
using System.IO;
using Jazztures.Core.Gesture;
using Jazztures.Core.Ports;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Jazztures.Diagnostics
{
    /// <summary>
    /// Captures an <see cref="IHandPoseSource"/> to a JSONL fixture so a Quest session can
    /// be recorded once and replayed at the desk (CLAUDE.md §2.6). Add it next to a
    /// <c>MetaXRHandPoseSource</c>, point <see cref="_source"/> at it, and toggle
    /// recording with <see cref="_toggleKey"/>. On stop the file is written to
    /// <c>Application.persistentDataPath/HandPoseRecordings/</c> and its path logged.
    ///
    /// <para>Only pose candidate + tracking quality are stored — never raw joints (§4.1).</para>
    /// </summary>
    public sealed class HandPoseRecorderComponent : MonoBehaviour
    {
        [Tooltip("The hand-pose source to record (a component implementing IHandPoseSource).")]
        [SerializeField] private MonoBehaviour _source;

        [SerializeField] private Key _toggleKey = Key.F9;

        [SerializeField] private bool _recordFromStart;

        private IHandPoseSource _poseSource;
        private readonly HandPoseRecorder _recorder = new HandPoseRecorder();
        private bool _recording;

        public bool IsRecording => _recording;

        public int FrameCount => _recorder.Count;

        private void Awake()
        {
            _poseSource = _source as IHandPoseSource;
            if (_poseSource == null)
            {
                Debug.LogError(
                    $"{nameof(HandPoseRecorderComponent)}: '{(_source == null ? "<none>" : _source.GetType().Name)}' " +
                    $"is not an {nameof(IHandPoseSource)}.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            if (_recordFromStart)
            {
                StartRecording();
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[_toggleKey].wasPressedThisFrame)
            {
                if (_recording)
                {
                    StopRecording();
                }
                else
                {
                    StartRecording();
                }
            }

            if (_recording)
            {
                _recorder.Capture(AudioSettings.dspTime, _poseSource.CurrentFrame);
            }
        }

        private void OnDisable()
        {
            if (_recording)
            {
                StopRecording();
            }
        }

        public void StartRecording()
        {
            _recorder.Clear();
            _recording = true;
            Debug.Log($"{nameof(HandPoseRecorderComponent)}: recording started.", this);
        }

        public void StopRecording()
        {
            _recording = false;
            HandPoseRecording recording = _recorder.Build();
            if (recording.Count == 0)
            {
                Debug.LogWarning($"{nameof(HandPoseRecorderComponent)}: nothing recorded.", this);
                return;
            }

            string directory = Path.Combine(Application.persistentDataPath, "HandPoseRecordings");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"handpose_{DateTime.Now:yyyyMMdd_HHmmss}.jsonl");
            File.WriteAllText(path, recording.ToJsonl());

            Debug.Log(
                $"{nameof(HandPoseRecorderComponent)}: wrote {recording.Count} frames " +
                $"({recording.DurationSeconds:0.0}s) to {path}", this);
        }
    }
}
