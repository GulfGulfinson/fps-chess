using Chess.Core;
using Godot;

namespace FpsChess.Audio;

public enum Sound
{
    RifleShot,
    ShotgunShot,
    SniperShot,
    MinigunShot,
    AssaultShot,
    RevolverShot,
    Hit,
    Headshot,
    Hurt,
    Reload,
    Step,
    Beep,
    Go,
    Win,
    Lose,
    Explosion,
    PieceMove,
    Capture,
    Dash,
}

/// <summary>
/// All sounds are synthesized once at first use (16-bit mono PCM), so the game ships without audio files:
/// shots are filtered noise bursts with a low thump, interface sounds are enveloped sine tones.
/// </summary>
public static class SoundBank
{
    private const int Rate = 22050;
    private static readonly Dictionary<Sound, AudioStreamWav> Cache = [];

    public static Sound ShotFor(PieceType piece) => piece switch
    {
        PieceType.Knight => Sound.ShotgunShot,
        PieceType.Bishop => Sound.SniperShot,
        PieceType.Rook => Sound.MinigunShot,
        PieceType.Queen => Sound.AssaultShot,
        PieceType.King => Sound.RevolverShot,
        _ => Sound.RifleShot,
    };

    public static AudioStreamWav Get(Sound sound)
    {
        if (Cache.TryGetValue(sound, out var cached)) return cached;
        var stream = ToWav(Limit(Synthesize(sound), 0.9f));
        Cache[sound] = stream;
        return stream;
    }

    private static float[] Synthesize(Sound sound) => sound switch
    {
        Sound.RifleShot => Shot(0.16f, 30f, 0.35f, 140f, 0.5f),
        Sound.ShotgunShot => Shot(0.35f, 14f, 0.22f, 85f, 0.9f),
        Sound.SniperShot => Mix(Shot(0.6f, 9f, 0.55f, 110f, 0.7f), Echo(Shot(0.6f, 9f, 0.2f, 0f, 0f), 0.18f, 0.35f)),
        Sound.MinigunShot => Shot(0.07f, 60f, 0.4f, 160f, 0.35f),
        Sound.AssaultShot => Shot(0.12f, 34f, 0.32f, 130f, 0.45f),
        Sound.RevolverShot => Shot(0.3f, 16f, 0.28f, 95f, 0.8f),
        Sound.Hit => Tone(0.05f, 1900f, 0.35f, decay: 60f),
        Sound.Headshot => Mix(Tone(0.14f, 1400f, 0.35f, decay: 18f), Delay(Tone(0.18f, 2100f, 0.35f, decay: 16f), 0.06f)),
        Sound.Hurt => Mix(Tone(0.18f, 85f, 0.6f, decay: 18f), Shot(0.12f, 25f, 0.15f, 0f, 0f)),
        Sound.Reload => Mix(Click(0.0f), Click(0.22f), Click(0.5f)),
        Sound.Step => Shot(0.07f, 45f, 0.05f, 70f, 0.25f),
        Sound.Beep => Tone(0.12f, 880f, 0.3f, decay: 8f),
        Sound.Go => Tone(0.35f, 1320f, 0.35f, decay: 5f),
        Sound.Win => Melody([523.25f, 659.25f, 783.99f, 1046.5f], 0.13f),
        Sound.Lose => Melody([392f, 329.63f, 261.63f], 0.2f),
        Sound.Explosion => Shot(0.9f, 5f, 0.5f, 55f, 1f),
        Sound.PieceMove => Shot(0.06f, 70f, 0.12f, 220f, 0.5f),
        Sound.Capture => Mix(Shot(0.25f, 12f, 0.25f, 70f, 0.8f), Tone(0.25f, 220f, 0.25f, decay: 10f)),
        _ => Sweep(0.18f, 300f, 900f, 0.25f),
    };

    /// <summary>A gunshot: low-passed noise (bright crack, darker with a lower cutoff) plus a decaying thump.</summary>
    private static float[] Shot(float seconds, float decay, float noise, float thumpHz, float thump)
    {
        var samples = new float[(int)(seconds * Rate)];
        var random = new Random(samples.Length);
        var filtered = 0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var t = i / (float)Rate;
            var envelope = MathF.Exp(-t * decay);
            // The cutoff drops over time: the crack first, then a muffled tail.
            var cutoff = 0.6f * MathF.Exp(-t * 12f) + 0.05f;
            filtered += cutoff * ((float)random.NextDouble() * 2f - 1f - filtered);
            var body = thumpHz > 0 ? MathF.Sin(MathF.Tau * thumpHz * t * (1f - t)) * MathF.Exp(-t * decay * 0.8f) * thump : 0f;
            samples[i] = (filtered * noise * 2.2f + body) * envelope;
        }
        return samples;
    }

    private static float[] Tone(float seconds, float hz, float volume, float decay)
    {
        var samples = new float[(int)(seconds * Rate)];
        for (var i = 0; i < samples.Length; i++)
        {
            var t = i / (float)Rate;
            var attack = MathF.Min(1f, t * 400f);
            samples[i] = MathF.Sin(MathF.Tau * hz * t) * volume * attack * MathF.Exp(-t * decay);
        }
        return samples;
    }

    private static float[] Sweep(float seconds, float fromHz, float toHz, float volume)
    {
        var samples = new float[(int)(seconds * Rate)];
        var phase = 0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var t = i / (float)Rate;
            phase += MathF.Tau * (fromHz + (toHz - fromHz) * t / seconds) / Rate;
            samples[i] = MathF.Sin(phase) * volume * MathF.Sin(MathF.PI * t / seconds);
        }
        return samples;
    }

    private static float[] Melody(float[] notes, float noteSeconds)
    {
        var result = new float[(int)((notes.Length * noteSeconds + 0.3f) * Rate)];
        for (var n = 0; n < notes.Length; n++)
        {
            var tone = Tone(noteSeconds + 0.3f, notes[n], 0.3f, decay: 6f);
            var offset = (int)(n * noteSeconds * Rate);
            for (var i = 0; i < tone.Length && offset + i < result.Length; i++) result[offset + i] += tone[i];
        }
        return result;
    }

    private static float[] Click(float at) => Delay(Shot(0.04f, 90f, 0.25f, 600f, 0.2f), at);

    private static float[] Delay(float[] samples, float seconds)
    {
        var offset = (int)(seconds * Rate);
        var result = new float[samples.Length + offset];
        Array.Copy(samples, 0, result, offset, samples.Length);
        return result;
    }

    private static float[] Echo(float[] samples, float seconds, float volume)
    {
        var delayed = Delay(samples, seconds);
        for (var i = 0; i < delayed.Length; i++) delayed[i] *= volume;
        return delayed;
    }

    private static float[] Mix(params float[][] parts)
    {
        var result = new float[parts.Max(p => p.Length)];
        foreach (var part in parts)
        {
            for (var i = 0; i < part.Length; i++) result[i] += part[i];
        }
        return result;
    }

    /// <summary>Scales a sound down so its loudest sample stays below <paramref name="peak"/> (no clipping).</summary>
    private static float[] Limit(float[] samples, float peak)
    {
        var loudest = samples.Length == 0 ? 0f : samples.Max(MathF.Abs);
        if (loudest <= peak) return samples;
        var scale = peak / loudest;
        for (var i = 0; i < samples.Length; i++) samples[i] *= scale;
        return samples;
    }

    private static AudioStreamWav ToWav(float[] samples)
    {
        var data = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var value = (short)(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);
            data[i * 2] = (byte)(value & 0xff);
            data[i * 2 + 1] = (byte)((value >> 8) & 0xff);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = data };
    }
}
