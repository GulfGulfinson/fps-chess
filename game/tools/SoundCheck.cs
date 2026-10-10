using FpsChess.Audio;
using Godot;

namespace FpsChess.Tools;

/// <summary>
/// Development tool: synthesizes every sound, prints length, peak and loudness, and writes WAV files.
///   godot --headless --path game res://tools/SoundCheck.tscn -- --out /tmp/sounds
/// </summary>
public partial class SoundCheck : Node
{
    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        var outIndex = Array.IndexOf(args, "--out");
        var outDir = outIndex >= 0 && outIndex + 1 < args.Length ? args[outIndex + 1] : null;
        if (outDir is not null) DirAccess.MakeDirRecursiveAbsolute(outDir);

        foreach (var sound in Enum.GetValues<Sound>())
        {
            var wav = SoundBank.Get(sound);
            var data = wav.Data;
            var peak = 0f;
            var sum = 0.0;
            for (var i = 0; i + 1 < data.Length; i += 2)
            {
                var value = (short)(data[i] | (data[i + 1] << 8)) / (float)short.MaxValue;
                peak = Math.Max(peak, Math.Abs(value));
                sum += value * value;
            }
            var rms = Math.Sqrt(sum / (data.Length / 2));
            GD.Print($"[sound] {sound,-13} {data.Length / 2f / wav.MixRate:0.00} s  peak {peak:0.00}  rms {rms:0.000}");
            if (outDir is not null) wav.SaveToWav($"{outDir}/{sound}.wav");
        }
        GetTree().Quit();
    }
}
