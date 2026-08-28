using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum VelocityLayer { PP = 0, P = 1, MP = 2, MF = 3, F = 4, FF = 5 }

public class SampleBank : MonoBehaviour
{
    private static SampleBank _instance;
    public static SampleBank Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<SampleBank>();
                if (_instance == null)
                {
                    Debug.LogError("SampleBank not found in scene! Add SampleBank component to a GameObject in your scene.");
                }
            }
            return _instance;
        }
    }

    private readonly Dictionary<string, AudioClip> _samples = new();
    private readonly Dictionary<int, AudioClip[]> _noteToClips = new();

    public int SampleRate { get; private set; } = 44100;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        GenerateProceduralSamples();
    }

    private void GenerateProceduralSamples()
    {
        SampleRate = AudioSettings.outputSampleRate;
        
        string[] noteNames = { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" };
        int midiStart = 48; // C3
        int midiEnd = 84;   // C6

        for (int midi = midiStart; midi <= midiEnd; midi++)
        {
            int octave = (midi / 12) - 1;
            string noteName = noteNames[midi % 12] + octave;

            var clips = new AudioClip[6]; // 6 velocity layers
            
            for (int vel = 0; vel < 6; vel++)
            {
                clips[vel] = GeneratePianoTone(midi, (VelocityLayer)vel);
                string key = $"{noteName}_{(VelocityLayer)vel}";
                _samples[key] = clips[vel];
            }

            _noteToClips[midi] = clips;
        }

        Debug.Log($"SampleBank: Generated {_noteToClips.Count} notes × 6 velocity layers at {SampleRate}Hz");
    }

    private AudioClip GeneratePianoTone(int midiNote, VelocityLayer velocity)
    {
        float frequency = MidiToFrequency(midiNote);
        float duration = 2.0f; // 2 seconds per sample
        int samples = Mathf.CeilToInt(duration * SampleRate);
        var clip = AudioClip.Create($"Piano_{midiNote}_{velocity}", samples, 1, SampleRate, false);

        float[] data = new float[samples];
        float velGain = VelocityToGain(velocity);

        // Piano-like additive synthesis: fundamental + harmonics with inharmonicity
        float inharmonicity = 0.0004f; // Piano string stiffness
        float decay = 0.5f + velGain * 0.5f; // Higher velocity = slower decay

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            float envelope = Mathf.Exp(-t * decay) * velGain;

            // Attack transient (first 5ms)
            if (t < 0.005f)
            {
                envelope *= t / 0.005f;
            }

            float sample = 0f;
            int numPartials = 8 + (int)(velGain * 4); // More harmonics at higher velocity

            for (int p = 1; p <= numPartials; p++)
            {
                float partialFreq = frequency * p * Mathf.Sqrt(1f + inharmonicity * p * p);
                float partialGain = 1f / p; // 1/p falloff
                
                // Velocity affects brightness
                partialGain *= Mathf.Pow(velGain, 0.5f);
                
                // Alternating phase for more natural sound
                float phase = (p % 2 == 0) ? 0f : Mathf.PI * 0.5f;
                
                sample += partialGain * Mathf.Sin(2f * Mathf.PI * partialFreq * t + phase);
            }

            // Add slight noise for attack realism
            if (t < 0.02f)
            {
                sample += Random.Range(-0.1f, 0.1f) * (1f - t / 0.02f) * velGain * 0.1f;
            }

            data[i] = Mathf.Clamp(sample * envelope * 0.3f, -1f, 1f);
        }

        clip.SetData(data, 0);
        return clip;
    }

    public static float MidiToFrequency(int midiNote)
    {
        return 440f * Mathf.Pow(2f, (midiNote - 69) / 12f);
    }

    private float VelocityToGain(VelocityLayer velocity)
    {
        return velocity switch
        {
            VelocityLayer.PP => 0.25f,
            VelocityLayer.P => 0.4f,
            VelocityLayer.MP => 0.55f,
            VelocityLayer.MF => 0.7f,
            VelocityLayer.F => 0.85f,
            VelocityLayer.FF => 1.0f,
            _ => 0.7f
        };
    }

    public AudioClip GetClip(string noteName, VelocityLayer velocity)
    {
        string key = $"{noteName}_{velocity}";
        if (_samples.TryGetValue(key, out var clip))
            return clip;
        
        // Fallback: find closest note
        return GetClosestClip(noteName, velocity);
    }

    public AudioClip GetClip(int midiNote, float velocity01)
    {
        if (_noteToClips.TryGetValue(midiNote, out var clips))
        {
            int layer = Mathf.Clamp(Mathf.RoundToInt(velocity01 * 5f), 0, 5);
            return clips[layer];
        }
        return GetClosestClipByMidi(midiNote, velocity01);
    }

    private AudioClip GetClosestClip(string noteName, VelocityLayer velocity)
    {
        float minDist = float.MaxValue;
        AudioClip best = null;
        
        foreach (var kvp in _samples)
        {
            if (kvp.Key.EndsWith($"_{velocity}"))
            {
                // Simple string distance
                int dist = LevenshteinDistance(noteName, kvp.Key.Split('_')[0]);
                if (dist < minDist)
                {
                    minDist = dist;
                    best = kvp.Value;
                }
            }
        }
        return best ?? _samples.Values.First();
    }

    private AudioClip GetClosestClipByMidi(int midiNote, float velocity01)
    {
        int closestMidi = 0;
        int minDist = int.MaxValue;
        
        foreach (var kvp in _noteToClips)
        {
            int dist = Mathf.Abs(kvp.Key - midiNote);
            if (dist < minDist)
            {
                minDist = dist;
                closestMidi = kvp.Key;
            }
        }
        
        if (_noteToClips.TryGetValue(closestMidi, out var clips))
        {
            int layer = Mathf.Clamp(Mathf.RoundToInt(velocity01 * 5f), 0, 5);
            return clips[layer];
        }
        return _samples.Values.First();
    }

    private int LevenshteinDistance(string a, string b)
    {
        int[,] d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;
                d[i, j] = Mathf.Min(
                    d[i - 1, j] + 1,
                    Mathf.Min(d[i, j - 1] + 1, d[i - 1, j - 1] + cost)
                );
            }
        }
        return d[a.Length, b.Length];
    }
}