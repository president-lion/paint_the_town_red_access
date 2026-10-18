using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// J: which ways are open. Casts 72 rays (every 5 degrees) at chest height out to 30 m against
/// level geometry (people, loose props and swinging doors do not count as walls), groups the
/// directions with at least 3 m of room into openings, and speaks the four that reach
/// farthest, relative to where you face: "open ahead left, 14 metres; behind, 6 metres".
/// A far opening is usually the way out of a room; a doorway shows up as a narrow one.
/// </summary>
internal static class Openings
{
    private const int Rays = 72;
    private const float MaxDist = 30f;
    private const float MinOpen = 3f;
    private const float ChestHeight = 0.5f; // the player's transform is already about 1 m above the floor

    public static void Say()
    {
        if (!Game.InPlay) return;
        var p = Game.Player;
        var view = Game.View;
        if (p == null || view == null) return;
        var fwd = view.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) return;
        fwd.Normalize();
        var rgt = new Vector3(fwd.z, 0f, -fwd.x);
        var chest = p.transform.position + Vector3.up * ChestHeight;

        var dist = new float[Rays];
        for (var i = 0; i < Rays; i++)
        {
            var a = i * 2f * Mathf.PI / Rays;
            var dir = rgt * Mathf.Sin(a) + fwd * Mathf.Cos(a);
            dist[i] = MaxDist;
            var hits = Physics.RaycastAll(chest, dir, MaxDist, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null) continue;
            foreach (var h in hits)
                if (DangerProbe.IsSolid(h.collider)) dist[i] = Mathf.Min(dist[i], h.distance);
        }

        // Start the walk round the circle at a closed direction so no opening is split in two.
        var start = -1;
        for (var i = 0; i < Rays; i++) if (dist[i] < MinOpen) { start = i; break; }
        if (start < 0) { Speech.Say(Strings.OpenAllRound); return; }

        var found = new List<(float far, int best, int width)>();
        var run = 0;
        var runBest = -1;
        for (var k = 1; k <= Rays; k++)
        {
            var i = (start + k) % Rays;
            if (dist[i] >= MinOpen)
            {
                run++;
                if (runBest < 0 || dist[i] > dist[runBest]) runBest = i;
                continue;
            }
            if (run > 0) found.Add((dist[runBest], runBest, run));
            run = 0;
            runBest = -1;
        }
        if (found.Count == 0) { Speech.Say(Strings.WallsAllRound); return; }

        var parts = new List<string>();
        foreach (var o in found.OrderByDescending(o => o.far).Take(4))
        {
            var a = o.best * 2f * Mathf.PI / Rays;
            var r = Mathf.Sin(a);
            var f = Mathf.Cos(a);
            var how = o.far >= MaxDist ? Strings.Far : Spatial.Distance(o.far);
            parts.Add($"{Spatial.Direction(r, f)}, {how}{(o.width <= 3 ? ", " + Strings.Narrow : "")}");
        }
        Speech.Say(string.Format(Strings.OpenWays, string.Join("; ", parts)));
        Log.Info("Openings", $"at {p.transform.position}: {string.Join(" | ", found.Select(o => $"ray {o.best} {o.far:F1} m width {o.width}"))}");
    }
}
