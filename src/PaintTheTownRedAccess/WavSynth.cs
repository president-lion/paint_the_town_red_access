namespace PaintTheTownRedAccess;

/// <summary>
/// Makes the mod's placeholder sounds as in-memory 16-bit mono WAV files, which FMOD
/// loads directly. A WAV of the same name in UserData\PaintTheTownRedAccess\sounds
/// replaces any of them. Pure logic - no Unity types - so it is unit tested.
/// </summary>
internal static class WavSynth
{
    public const int Rate = 44100;

    public enum Wave { Sine, Square, Noise }

    /// <summary>One tone (or a glide from f0 to f1) with a short attack and an exponential decay.</summary>
    public static float[] Tone(float f0, float f1, float seconds, Wave wave = Wave.Sine, float decay = 6f)
    {
        var n = (int)(Rate * seconds);
        var s = new float[n];
        var phase = 0.0;
        var rnd = new Random(1234);
        for (var i = 0; i < n; i++)
        {
            var t = (float)i / n;
            var f = f0 + (f1 - f0) * t;
            phase += 2 * Math.PI * f / Rate;
            var v = wave switch
            {
                Wave.Square => Math.Sin(phase) >= 0 ? 0.6f : -0.6f,
                Wave.Noise => (float)(rnd.NextDouble() * 2 - 1),
                _ => (float)Math.Sin(phase),
            };
            var attack = Math.Min(1f, i / (Rate * 0.004f));
            s[i] = v * attack * MathF.Exp(-decay * t);
        }
        return s;
    }

    public static float[] Concat(params float[][] parts)
    {
        var all = new List<float>();
        foreach (var p in parts) all.AddRange(p);
        return all.ToArray();
    }

    public static float[] Silence(float seconds) => new float[(int)(Rate * seconds)];

    public static byte[] ToWav(float[] samples, float gain = 0.8f)
    {
        var data = samples.Length * 2;
        using var ms = new MemoryStream(44 + data);
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8.ToArray()); w.Write(36 + data); w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8.ToArray()); w.Write(data);
        foreach (var s in samples) w.Write((short)Math.Clamp(s * gain * short.MaxValue, short.MinValue, short.MaxValue));
        return ms.ToArray();
    }
}
