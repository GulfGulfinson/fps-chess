using FpsChess.Arena;
using Godot;

namespace FpsChess.Audio;

/// <summary>
/// Background music per room theme, synthesized once: four slow chords as soft pads, looping seamlessly.
/// The library is warm and major, the castle hall dark and minor, the lab synthetic with an arpeggio.
/// </summary>
public static class MusicBank
{
    private const int Rate = 22050;
    private const float ChordSeconds = 4.8f;
    private static readonly Dictionary<ArenaThemeId, AudioStreamWav> Cache = [];

    public static AudioStreamWav For(ArenaThemeId theme)
    {
        if (Cache.TryGetValue(theme, out var cached)) return cached;
        var (chords, arpeggio, brightness) = theme switch
        {
            // Frequencies in Hz: Cmaj7, Am7, Fmaj7, G6.
            ArenaThemeId.Library => (new[]
            {
                new[] { 130.81f, 164.81f, 196.00f, 246.94f },
                new[] { 110.00f, 130.81f, 164.81f, 196.00f },
                new[] { 87.31f, 110.00f, 130.81f, 164.81f },
                new[] { 98.00f, 123.47f, 146.83f, 164.81f },
            }, false, 0.35f),
            // Dm, Bb, C, Am, an octave lower.
            ArenaThemeId.CastleHall => (new[]
            {
                new[] { 73.42f, 110.00f, 146.83f, 174.61f },
                new[] { 58.27f, 87.31f, 116.54f, 146.83f },
                new[] { 65.41f, 98.00f, 130.81f, 164.81f },
                new[] { 55.00f, 82.41f, 110.00f, 130.81f },
            }, false, 0.25f),
            // Em9, Cmaj7, Am9, Bsus4.
            _ => (new[]
            {
                new[] { 82.41f, 123.47f, 185.00f, 246.94f },
                new[] { 65.41f, 130.81f, 164.81f, 246.94f },
                new[] { 110.00f, 130.81f, 196.00f, 246.94f },
                new[] { 61.74f, 123.47f, 164.81f, 185.00f },
            }, true, 0.5f),
        };

        var total = (int)(chords.Length * ChordSeconds * Rate);
        var samples = new float[total];
        for (var c = 0; c < chords.Length; c++)
        {
            var start = (int)(c * ChordSeconds * Rate);
            // Each chord rings into the next; the last one wraps to the start so the loop is seamless.
            var length = (int)(ChordSeconds * 1.6f * Rate);
            for (var i = 0; i < length; i++)
            {
                var t = i / (float)Rate;
                var envelope = MathF.Min(1f, t / 1.2f) * MathF.Max(0f, 1f - t / (ChordSeconds * 1.6f));
                var value = 0f;
                for (var n = 0; n < chords[c].Length; n++)
                {
                    var hz = chords[c][n];
                    // Two slightly detuned sines per note give a soft, moving pad.
                    value += MathF.Sin(MathF.Tau * hz * t) + 0.6f * MathF.Sin(MathF.Tau * hz * 1.003f * t);
                    value += brightness * 0.3f * MathF.Sin(MathF.Tau * hz * 2f * t);
                }
                if (arpeggio)
                {
                    var step = (int)(t / 0.3f);
                    var note = chords[c][step % chords[c].Length] * 2f;
                    var local = t - step * 0.3f;
                    value += 1.2f * MathF.Sin(MathF.Tau * note * local) * MathF.Exp(-local * 9f);
                }
                samples[(start + i) % total] += value * envelope * 0.05f;
            }
        }

        var peak = samples.Max(MathF.Abs);
        var scale = peak > 0.6f ? 0.6f / peak : 1f;
        var data = new byte[total * 2];
        for (var i = 0; i < total; i++)
        {
            var v = (short)(Math.Clamp(samples[i] * scale, -1f, 1f) * short.MaxValue);
            data[i * 2] = (byte)(v & 0xff);
            data[i * 2 + 1] = (byte)((v >> 8) & 0xff);
        }
        var stream = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = Rate,
            Stereo = false,
            Data = data,
            LoopMode = AudioStreamWav.LoopModeEnum.Forward,
            LoopBegin = 0,
            LoopEnd = total,
        };
        Cache[theme] = stream;
        return stream;
    }
}
