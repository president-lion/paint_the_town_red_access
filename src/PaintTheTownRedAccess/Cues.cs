namespace PaintTheTownRedAccess;

/// <summary>
/// The mod's sound cues. Each is a generated placeholder unless
/// UserData\PaintTheTownRedAccess\sounds\&lt;name&gt;.wav exists, which replaces it
/// (names: enemy, attack, reach, beacon, arrive, edge).
/// </summary>
internal static class Cues
{
    public enum Cue { Enemy, Attack, Reach, Beacon, Arrive, Edge }

    private static readonly Dictionary<Cue, IntPtr> _sounds = new();
    private static bool _loaded;

    public static string SoundDir => Path.Combine(Log.Dir, "sounds");

    private static float[] Make(Cue cue) => cue switch
    {
        // A short low knock per nearby enemy.
        Cue.Enemy => WavSynth.Tone(320, 260, 0.07f, WavSynth.Wave.Sine, 9f),
        // An enemy starting an attack on you: a bright rising double blip, distinct from the knock.
        Cue.Attack => WavSynth.Concat(WavSynth.Tone(900, 1300, 0.06f, WavSynth.Wave.Square, 4f),
                                      WavSynth.Silence(0.02f),
                                      WavSynth.Tone(1100, 1600, 0.06f, WavSynth.Wave.Square, 4f)),
        // The nearest enemy is in front of you and close enough to hit.
        Cue.Reach => WavSynth.Tone(2000, 2000, 0.03f, WavSynth.Wave.Sine, 10f),
        Cue.Beacon => WavSynth.Tone(1320, 1320, 0.05f, WavSynth.Wave.Sine, 5f),
        Cue.Arrive => WavSynth.Concat(WavSynth.Tone(660, 660, 0.08f), WavSynth.Tone(990, 990, 0.12f)),
        Cue.Edge => WavSynth.Tone(180, 140, 0.05f, WavSynth.Wave.Noise, 8f),
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
            _sounds[c] = Fmod.Load(wav ?? WavSynth.ToWav(Make(c)), false);
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
}
