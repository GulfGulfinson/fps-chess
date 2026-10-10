using Godot;

namespace FpsChess.Audio;

/// <summary>Fire-and-forget playback. Players free themselves when the sound ends.</summary>
public static class Sfx
{
    /// <summary>A sound heard as if at the listener (own weapon, interface).</summary>
    public static void Play(Node parent, Sound sound, float volumeDb = 0f, float pitchJitter = 0.04f)
    {
        var player = new AudioStreamPlayer
        {
            Stream = SoundBank.Get(sound),
            VolumeDb = volumeDb,
            PitchScale = 1f + (float)GD.RandRange(-pitchJitter, pitchJitter),
        };
        parent.AddChild(player);
        player.Finished += player.QueueFree;
        player.Play();
    }

    /// <summary>A sound coming from a place in the world (the opponent's shots, explosions).</summary>
    public static void PlayAt(Node3D parent, Sound sound, Vector3 position, float volumeDb = 0f, float pitchJitter = 0.04f)
    {
        var player = new AudioStreamPlayer3D
        {
            Stream = SoundBank.Get(sound),
            VolumeDb = volumeDb,
            UnitSize = 12f,
            MaxDistance = 140f,
            PitchScale = 1f + (float)GD.RandRange(-pitchJitter, pitchJitter),
        };
        parent.AddChild(player);
        player.GlobalPosition = position;
        player.Finished += player.QueueFree;
        player.Play();
    }
}
