using Jazztures.Core.Timing;
using UnityEngine;

namespace Jazztures.Config
{
    /// <summary>
    /// The tuning asset for the audible metronome (CLAUDE.md §3.6; ADR-0043 §2a). Every
    /// value here is `[TUNABLE]` — an engineering default, not a finding. Mirror any change
    /// in <c>Docs/CALIBRATION.md</c>; the click levels and pitches in particular are to be
    /// balanced by ear against the piano, not derived.
    ///
    /// <para>
    /// Lessons carry their own tempo, bar length and swing (§3.9), so for a lesson only the
    /// <i>sound</i> group below applies. The <i>pulse</i> group is the free-play metronome's
    /// starting state (and, later, what a timing drill starts from).
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Config/Metronome", fileName = "MetronomeSettings")]
    public sealed class MetronomeSettings : ScriptableObject
    {
        /// <summary>Fallback pre-roll when no asset is assigned; matches the field default.</summary>
        public const float DefaultPreRollSeconds = 0.15f;

        [Header("Pulse — free play's starting state")]
        [Tooltip("Free play starts with the click running. Turn off to leave free play silent " +
                 "until the metronome is switched on (desk key M). Lessons are unaffected.")]
        [SerializeField] private bool _clickInFreePlay = true;

        [Tooltip("Beats per minute. 80 is the §3.6 default; the validated range is 40–100. " +
                 "Higher needs Allow Unvalidated Tempo.")]
        [SerializeField] private float _tempoBpm = 80f;

        [Tooltip("Above ~100 BPM open-air tracking is unvalidated (§3.6). Tick to allow it anyway.")]
        [SerializeField] private bool _allowUnvalidatedTempo;

        [Tooltip("BPM change per press of the menu's +/- buttons (and the - / = desk keys).")]
        [Range(1f, 20f)]
        [SerializeField] private float _tempoStepBpm = 5f;

        [Tooltip("Pressing +/- on the menu plays this many bars of click at the new tempo, so a " +
                 "change is audible without entering free play.")]
        [Range(1, 8)]
        [SerializeField] private int _tempoPreviewBars = 2;

        [Min(1)]
        [SerializeField] private int _beatsPerBar = Metronome.DefaultBeatsPerBar;

        [Tooltip("None: quarter-note pulse only. Eighths / Swung Eighths add an off-beat click. " +
                 "The quarter pulse is never swung.")]
        [SerializeField] private MetronomeSubdivision _subdivision = MetronomeSubdivision.None;

        [Tooltip("Where the swung off-beat click falls, as a fraction of the beat. 0.66 ≈ 2:1. " +
                 "Used only for Swung Eighths.")]
        [Range(0.5f, 0.85f)]
        [SerializeField] private float _swingRatio = 0.66f;

        [Header("Sound")]
        [Tooltip("Give beat 1 of each bar its own, higher click.")]
        [SerializeField] private bool _accentDownbeat = true;

        [Tooltip("AudioSource volume for the click. Balance against the piano by ear.")]
        [Range(0f, 1f)]
        [SerializeField] private float _clickVolume = 0.6f;

        [Tooltip("Off-beat clicks are the beat click at this fraction of the volume, so the " +
                 "subdivision reads as lighter than the pulse. Not in ADR-0043's table.")]
        [Range(0f, 1f)]
        [SerializeField] private float _offBeatVolumeScale = 0.5f;

        [Tooltip("Pitch of the beat click, Hz. ~2 kHz sits above the right-hand ladder's " +
                 "register (fundamentals ≈ 523–1319 Hz) so it does not mask the learner's notes.")]
        [Range(500f, 8000f)]
        [SerializeField] private float _beatClickHz = 2000f;

        [Tooltip("Pitch of the accented downbeat click, Hz.")]
        [Range(500f, 8000f)]
        [SerializeField] private float _downbeatClickHz = 2800f;

        [Tooltip("Length of each click, seconds.")]
        [Range(0.005f, 0.1f)]
        [SerializeField] private float _clickDurationSeconds = 0.020f;

        [Header("Scheduling")]
        [Tooltip("How far ahead of now clicks are handed to the audio engine. Scheduling " +
                 "horizon only — it adds no latency to a click that is already due.")]
        [Range(0.02f, 0.5f)]
        [SerializeField] private float _lookAheadSeconds = 0.100f;

        [Tooltip("A pulse (and any lesson phrase anchored to it) starts this long after 'now' " +
                 "so its first click can be scheduled ahead rather than played late. Never " +
                 "shorter than Look Ahead.")]
        [Range(0.02f, 1f)]
        [SerializeField] private float _preRollSeconds = DefaultPreRollSeconds;

        public bool ClickInFreePlay => _clickInFreePlay;

        public bool AllowUnvalidatedTempo => _allowUnvalidatedTempo;

        public double TempoStepBpm => Mathf.Max(1f, _tempoStepBpm);

        public int TempoPreviewBars => Mathf.Max(1, _tempoPreviewBars);

        /// <summary>The configured tempo, clamped into <see cref="TempoRange"/>.</summary>
        public Tempo Tempo => TempoRange.ClampToTempo(_tempoBpm, _allowUnvalidatedTempo);

        public int BeatsPerBar => Mathf.Max(1, _beatsPerBar);

        public MetronomeSubdivision Subdivision => _subdivision;

        public SwingRatio Swing => new SwingRatio(Mathf.Clamp(_swingRatio, 0.5f, 0.849f));

        public bool AccentDownbeat => _accentDownbeat;

        public float ClickVolume => _clickVolume;

        public float OffBeatVolumeScale => _offBeatVolumeScale;

        public float BeatClickHz => _beatClickHz;

        public float DownbeatClickHz => _downbeatClickHz;

        public float ClickDurationSeconds => _clickDurationSeconds;

        public float LookAheadSeconds => _lookAheadSeconds;

        /// <summary>Pre-roll, never shorter than <see cref="LookAheadSeconds"/>.</summary>
        public float PreRollSeconds => Mathf.Max(_preRollSeconds, _lookAheadSeconds);

        private void OnValidate()
        {
            _tempoBpm = (float)TempoRange.Clamp(_tempoBpm, _allowUnvalidatedTempo);
            _beatsPerBar = Mathf.Max(1, _beatsPerBar);
            _preRollSeconds = Mathf.Max(_preRollSeconds, _lookAheadSeconds);
        }
    }
}
