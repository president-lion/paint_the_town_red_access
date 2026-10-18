namespace PaintTheTownRedAccess;

/// <summary>
/// The mod's sound cues. Each is a generated placeholder unless
/// UserData\PaintTheTownRedAccess\sounds\&lt;name&gt;.wav exists, which replaces it
/// (names: enemy, attack, reach, beacon, arrive, edge, danger, hit, break, trap). Danger and trap are loops.
/// </summary>
internal static class Cues
{
    public enum Cue { Enemy, Attack, Reach, Beacon, Arrive, Edge, Danger, Hit, Break, Trap }

    private static readonly Dictionary<Cue, IntPtr> _sounds = new();
    private static bool _loaded;

    public static string SoundDir => Path.Combine(Log.Dir, "sounds");

    private static bool Loops(Cue cue) => cue is Cue.Danger or Cue.Trap;

    private static float[] Make(Cue cue) => cue switch
    {
        // A short low knock per nearby enemy.
        Cue.Enemy => WavSynth.Tone(320, 260, 0.07f, WavSynth.Wave.Sine, 9f),
        // An enemy starting an attack on you: a bright rising double blip, distinct from the knock.
        Cue.Attack => WavSynth.Concat(WavSynth.Tone(900, 1300, 0.06f, WavSynth.Wave.Square, 4f),
                                      WavSynth.Silence(0.02f),
                                      WavSynth.Tone(1100, 1600, 0.06f, WavSynth.Wave.Square, 4f)),
        // A swing now would hit someone (HitCheck, every 100 ms).
        Cue.Reach => WavSynth.Tone(1500, 1500, 0.04f, WavSynth.Wave.Sine, 6f),
        Cue.Beacon => WavSynth.Tone(1320, 1320, 0.05f, WavSynth.Wave.Sine, 5f),
        Cue.Arrive => WavSynth.Concat(WavSynth.Tone(660, 660, 0.08f), WavSynth.Tone(990, 990, 0.12f)),
        Cue.Edge => WavSynth.Tone(180, 140, 0.05f, WavSynth.Wave.Noise, 8f),
        // An arena trap nearby: a low pulsing buzz (TrapWarning).
        Cue.Trap => WavSynth.Buzz(110f, 6f, 1f),
        // You landed a hit: 2100 Hz with a 300 ms release.
        Cue.Hit => WavSynth.Tone(2100, 2100, 0.3f, WavSynth.Wave.Sine, 5f),
        // Your weapon broke: a G diminished chord (G4, B flat 4, D flat 5).
        Cue.Break => WavSynth.Chord(new[] { 392.00f, 466.16f, 554.37f }, 0.7f, 3.5f),
        // Deadly water or a big drop nearby: a continuous crackle (DangerProbe).
        Cue.Danger => WavSynth.Crackle(2f, 45f),
        _ => WavSynth.Tone(440, 440, 0.05f),
    };

    private static bool Load()
    {
        if (_loaded) return true;
        if (!Fmod.Ready) return false;
        _loaded = true;
        foreach (Cue c in Enum.GetValues(typeof(Cue)))
        {
            byte[] wav = null;
            var file = Path.Combine(SoundDir, c.ToString().ToLowerInvariant() + ".wav");
            try { if (File.Exists(file)) wav = File.ReadAllBytes(file); } catch { }
            _sounds[c] = Fmod.Load(wav ?? WavSynth.ToWav(Make(c)), Loops(c));
        }
        Log.Info("Cues", "loaded");
        return true;
    }

    public static void Play(Cue cue, Placement p)
    {
        if (!Load()) return;
        Fmod.Play(_sounds[cue], p, Settings.CueVolume);
    }

    public static void Play2D(Cue cue, float volume = 1f) => Play(cue, new Placement(0f, volume, 1f));

    /// <summary>
    /// Keeps a looping cue playing with this placement, starting it if needed, and returns its
    /// channel. Pass the returned channel back in on the next call.
    /// </summary>
    public static IntPtr Loop(Cue cue, IntPtr channel, Placement p)
    {
        if (!Load()) return IntPtr.Zero;
        if (Fmod.IsPlaying(channel)) { Fmod.Apply(channel, p, Settings.CueVolume); return channel; }
        return Fmod.Play(_sounds[cue], p, Settings.CueVolume);
    }
}
