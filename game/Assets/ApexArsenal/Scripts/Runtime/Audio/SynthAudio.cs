using System;
using System.Collections.Generic;
using UnityEngine;

public enum Sfx
{
    UiMove,
    UiClick,
    UiBack,
    CountBeep,
    CountGo,
    Join,
    Dash,
    CrateLand,
    Pickup,
    GateWarn,
    GateOn,
    TurretRise,
    TurretCharge,
    TurretShot,
    Eliminated,
    Victory,
    Defeat,
}

// Every sound in Apex Arsenal is synthesized at startup from oscillators and noise: no audio files.
public static class SynthAudio
{
    private const int Rate = 44100;
    private static readonly Dictionary<Sfx, AudioClip> Clips = new();
    private static AudioClip music;
    private static AudioClip gateHum;

    private enum Wave
    {
        Sine,
        Square,
        Triangle,
        Saw,
        Noise,
    }

    public static AudioClip Get(Sfx sfx)
    {
        if (!Clips.TryGetValue(sfx, out var clip) || clip == null)
            Clips[sfx] = clip = Build(sfx);
        return clip;
    }

    public static AudioClip GateHum => gateHum != null ? gateHum : gateHum = Make("Gate Hum", 1f, (data, n) =>
    {
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            // Whole number of cycles in one second so the loop is seamless.
            data[i] = (Osc(Wave.Saw, 110f * t) * 0.25f + Osc(Wave.Sine, 220f * t) * 0.2f) * (0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 6f * t));
        }
    });

    private static AudioClip Build(Sfx sfx)
    {
        switch (sfx)
        {
            case Sfx.UiMove: return Tone("UI Move", 0.05f, Wave.Square, 880f, 990f, 0.18f, 0.002f);
            case Sfx.UiClick: return Sequence("UI Click", Wave.Square, 0.05f, 0.2f, 784f, 1175f);
            case Sfx.UiBack: return Sequence("UI Back", Wave.Square, 0.05f, 0.2f, 988f, 659f);
            case Sfx.CountBeep: return Tone("Count", 0.16f, Wave.Triangle, 660f, 660f, 0.5f, 0.005f);
            case Sfx.CountGo: return Tone("Go", 0.45f, Wave.Square, 990f, 990f, 0.3f, 0.005f);
            case Sfx.Join: return Sequence("Join", Wave.Triangle, 0.07f, 0.45f, 523f, 659f, 784f, 1047f);
            case Sfx.Dash: return Noise("Dash", 0.22f, 0.35f, 4000f, 600f);
            case Sfx.CrateLand: return Layer("Crate Land", Tone(null, 0.35f, Wave.Sine, 160f, 45f, 0.9f, 0.002f), Noise(null, 0.12f, 0.35f, 2500f, 300f));
            case Sfx.Pickup: return Sequence("Pickup", Wave.Square, 0.05f, 0.22f, 659f, 880f, 1319f, 1760f);
            case Sfx.GateWarn: return Tone("Gate Warn", 0.12f, Wave.Square, 440f, 440f, 0.25f, 0.003f);
            case Sfx.GateOn: return Tone("Gate On", 0.5f, Wave.Saw, 120f, 480f, 0.35f, 0.01f);
            case Sfx.TurretRise: return Tone("Turret Rise", 0.45f, Wave.Triangle, 200f, 520f, 0.45f, 0.01f);
            case Sfx.TurretCharge: return Tone("Turret Charge", 0.6f, Wave.Sine, 300f, 1400f, 0.35f, 0.02f);
            case Sfx.TurretShot: return Layer("Turret Shot", Tone(null, 0.16f, Wave.Square, 1600f, 250f, 0.3f, 0.001f), Noise(null, 0.1f, 0.3f, 6000f, 1000f));
            case Sfx.Eliminated: return Layer("Eliminated", Tone(null, 0.4f, Wave.Sine, 500f, 80f, 0.7f, 0.002f), Noise(null, 0.25f, 0.4f, 3000f, 200f));
            case Sfx.Victory: return Sequence("Victory", Wave.Square, 0.13f, 0.28f, 523f, 659f, 784f, 1047f, 784f, 1047f);
            case Sfx.Defeat: return Sequence("Defeat", Wave.Triangle, 0.2f, 0.45f, 392f, 349f, 311f, 262f);
            default: return Tone(sfx.ToString(), 0.1f, Wave.Sine, 440f, 440f, 0.3f, 0.005f);
        }
    }

    // A looping, upbeat lab groove: Am - F - C - G with bass, arpeggio, kick and hats. 8 bars at 112 BPM.
    public static AudioClip Music
    {
        get
        {
            if (music != null)
                return music;
            const float bpm = 112f;
            float beat = 60f / bpm;
            int bars = 8;
            int total = Mathf.RoundToInt(bars * 4 * beat * Rate);
            float[][] chords =
            {
                new[] { 220.00f, 261.63f, 329.63f }, // Am
                new[] { 174.61f, 220.00f, 261.63f }, // F
                new[] { 261.63f, 329.63f, 392.00f }, // C
                new[] { 196.00f, 246.94f, 293.66f }, // G
            };
            music = Make("Lab Groove", (float)total / Rate, (data, n) =>
            {
                var random = new System.Random(486);
                float sixteenth = beat / 4f;
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / Rate;
                    int step = (int)(t / sixteenth);
                    float stepT = t - step * sixteenth;
                    int bar = (int)(t / (beat * 4f)) % bars;
                    float[] chord = chords[bar % chords.Length];
                    int beatInBar = step / 4 % 4;

                    float bassFreq = chord[0] * 0.5f;
                    float bass = Osc(Wave.Triangle, bassFreq * t) * 0.32f * Env(stepT + (step % 2) * sixteenth, 0.01f, beat * 0.45f);

                    float arpFreq = chord[step % 3] * (step % 8 >= 4 ? 2f : 1f);
                    float arp = Osc(Wave.Square, arpFreq * t) * 0.07f * Env(stepT, 0.002f, sixteenth * 0.9f);

                    float pad = 0f;
                    foreach (float f in chord)
                        pad += Osc(Wave.Sine, f * t) * 0.045f;

                    float beatT = t % beat;
                    float kick = beatInBar % 2 == 0 ? Mathf.Sin(2f * Mathf.PI * (50f + 120f * Mathf.Exp(-beatT * 30f)) * beatT) * Mathf.Exp(-beatT * 9f) * 0.5f : 0f;
                    float snare = beatInBar % 2 == 1 ? ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-beatT * 22f) * 0.16f : 0f;
                    float hat = step % 2 == 1 ? ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-stepT * 80f) * 0.06f : 0f;

                    data[i] = Mathf.Clamp(bass + arp + pad + kick + snare + hat, -1f, 1f) * 0.8f;
                }
            });
            return music;
        }
    }

    private static AudioClip Tone(string name, float duration, Wave wave, float startHz, float endHz, float volume, float attack)
    {
        return Make(name ?? "tone", duration, (data, n) =>
        {
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float k = t / duration;
                float hz = Mathf.Lerp(startHz, endHz, k);
                phase += hz / Rate;
                data[i] = Osc(wave, (float)phase) * volume * Env(t, attack, duration);
            }
        });
    }

    private static AudioClip Sequence(string name, Wave wave, float noteLength, float volume, params float[] notes)
    {
        float duration = noteLength * notes.Length + 0.1f;
        return Make(name, duration, (data, n) =>
        {
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                int note = Mathf.Min(notes.Length - 1, (int)(t / noteLength));
                float local = t - note * noteLength;
                float length = note == notes.Length - 1 ? noteLength + 0.1f : noteLength;
                data[i] = Osc(wave, notes[note] * t) * volume * Env(local, 0.003f, length);
            }
        });
    }

    private static AudioClip Noise(string name, float duration, float volume, float startCutoff, float endCutoff)
    {
        return Make(name ?? "noise", duration, (data, n) =>
        {
            var random = new System.Random(name?.GetHashCode() ?? 7);
            float smoothed = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float cutoff = Mathf.Lerp(startCutoff, endCutoff, t / duration);
                float alpha = Mathf.Clamp01(2f * Mathf.PI * cutoff / Rate);
                smoothed += alpha * (((float)random.NextDouble() * 2f - 1f) - smoothed);
                data[i] = smoothed * volume * 2f * Env(t, 0.005f, duration);
            }
        });
    }

    private static AudioClip Layer(string name, params AudioClip[] clips)
    {
        int length = 0;
        foreach (var clip in clips)
            length = Mathf.Max(length, clip.samples);
        var mix = new float[length];
        foreach (var clip in clips)
        {
            var data = new float[clip.samples];
            clip.GetData(data, 0);
            for (int i = 0; i < data.Length; i++)
                mix[i] += data[i];
        }
        var result = AudioClip.Create(name, length, 1, Rate, false);
        result.SetData(mix, 0);
        return result;
    }

    private static AudioClip Make(string name, float duration, Action<float[], int> fill)
    {
        int samples = Mathf.Max(1, Mathf.RoundToInt(duration * Rate));
        var data = new float[samples];
        fill(data, samples);
        var clip = AudioClip.Create(name, samples, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Phase is in cycles.
    private static float Osc(Wave wave, float phase)
    {
        float p = phase - Mathf.Floor(phase);
        switch (wave)
        {
            case Wave.Square: return p < 0.5f ? 1f : -1f;
            case Wave.Triangle: return 1f - 4f * Mathf.Abs(p - 0.5f);
            case Wave.Saw: return 2f * p - 1f;
            case Wave.Noise: return UnityEngine.Random.value * 2f - 1f;
            default: return Mathf.Sin(2f * Mathf.PI * p);
        }
    }

    // Attack then exponential-ish release over the note length.
    private static float Env(float t, float attack, float length)
    {
        if (t < 0f || t > length)
            return 0f;
        float rise = attack > 0f ? Mathf.Clamp01(t / attack) : 1f;
        float fall = 1f - t / length;
        return rise * fall * fall;
    }
}
