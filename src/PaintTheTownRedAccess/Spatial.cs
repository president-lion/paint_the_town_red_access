namespace PaintTheTownRedAccess;

/// <summary>How a cue is played: stereo pan, volume and pitch.</summary>
internal readonly struct Placement
{
    public readonly float Pan, Volume, Pitch;
    public Placement(float pan, float volume, float pitch) { Pan = pan; Volume = volume; Pitch = pitch; }
}

/// <summary>
/// Positional maths relative to where the player faces. Inputs are an offset already
/// expressed in the player's frame: right (metres to the right) and forward (metres ahead).
/// Pure logic - no Unity types - so it is unit tested.
/// </summary>
internal static class Spatial
{
    /// <summary>0 = ahead, 1 = ahead right, 2 = right ... 7 = ahead left.</summary>
    public static int Sector(float right, float forward)
    {
        var angle = MathF.Atan2(right, forward) * 180f / MathF.PI; // 0 ahead, 90 right
        if (angle < 0) angle += 360f;
        return (int)MathF.Round(angle / 45f) % 8;
    }

    public static string Direction(float right, float forward) => Strings.Directions[Sector(right, forward)];

    /// <summary>Spoken distance in whole metres; under a metre is "close".</summary>
    public static string Distance(float metres) =>
        metres < 1f ? Strings.Close : string.Format(metres < 1.5f ? Strings.Metre : Strings.Metres, (int)MathF.Round(metres));

    /// <summary>"above" / "below" when the height difference matters, else empty.</summary>
    public static string Height(float up) => up > 1.5f ? Strings.Above : up < -1.5f ? Strings.Below : "";

    /// <summary>
    /// Pan follows the left/right angle; things behind are played lower and a little quieter
    /// so ahead and behind sound different on headphones; volume falls with distance.
    /// </summary>
    public static Placement Place(float right, float forward, float range)
    {
        var dist = MathF.Sqrt(right * right + forward * forward);
        var pan = dist < 0.01f ? 0f : Math.Clamp(right / dist, -1f, 1f);
        var near = range <= 0 ? 1f : Math.Clamp(1f - dist / range, 0f, 1f);
        var volume = 0.2f + 0.8f * near * near;
        var pitch = 1f;
        if (forward < 0)
        {
            var behind = dist < 0.01f ? 0f : -forward / dist; // 0 at the side, 1 straight behind
            pitch = 1f - 0.25f * behind;
            volume *= 1f - 0.2f * behind;
        }
        return new Placement(pan, volume, pitch);
    }

    /// <summary>Within reach and inside a 25 degree cone ahead: a swing would land.</summary>
    public static bool InReach(float right, float forward, float reach)
    {
        if (forward <= 0) return false;
        var dist = MathF.Sqrt(right * right + forward * forward);
        if (dist > reach) return false;
        return MathF.Atan2(MathF.Abs(right), forward) * 180f / MathF.PI <= 25f;
    }

    /// <summary>Seconds between radar pings: quick when close, slow at the edge of range.</summary>
    public static float PingInterval(float dist, float range)
    {
        var t = range <= 0 ? 1f : Math.Clamp(dist / range, 0f, 1f);
        return 0.2f + 1.0f * t;
    }
}
