using Il2Cpp;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// Warns before you walk into deadly water or off a big drop with a continuous crackle that
/// comes from the direction of the nearest danger and grows louder and faster as you get
/// closer. Pirate Cove's sea kills through KillZone triggers (OnTriggerEnter: SetHealth to
/// kill, or ResetToMostRecentValidPosition when killPlayer is off) and SharkKillZone boxes at
/// the water surface; the ocean meshes are only visuals.
///
/// At level load every collider of a KillZone or SharkKillZone (including child colliders) is
/// collected and logged. Several times a second the ground is probed in 12 directions out to
/// 6 m: a ray down from chest height (12 m) that meets one of those colliders before solid
/// ground is water; one that finds no solid ground within 4 m below your feet is a drop.
/// A direction stops at the first wall, so the floor beyond an outer wall does not count.
/// The nearest danger found is narrowed down by bisection so the volume changes smoothly.
/// </summary>
internal static class DangerProbe
{
    private const int Directions = 12;
    private static readonly float[] Rings = { 0.5f, 1f, 1.5f, 2f, 3f, 4f, 5f, 6f };
    private const float Range = 6f;
    private const float MaxDrop = 4f;
    private const float ChestHeight = 1.2f;
    private const float RayLength = 12f;
    private const float Interval = 0.12f;

    private static readonly HashSet<IntPtr> _killColliders = new();
    private static bool _scanned;
    private static float _nextProbe;
    private static int _logged;
    private static IntPtr _channel;
    private static string _lastWhat;

    public static void OnSceneLoaded()
    {
        Silence();
        _killColliders.Clear();
        _scanned = false;
        _logged = 0;
        _lastWhat = null;
    }

    /// <summary>Collects the kill zones' colliders once the level is playable.</summary>
    private static void Scan()
    {
        _scanned = true;
        foreach (var kz in UnityEngine.Object.FindObjectsOfType<KillZone>())
            if (kz != null) Collect(kz.gameObject, $"KillZone killPlayer={kz.killPlayer} killEnemies={kz.killEnemies}");
        foreach (var sk in UnityEngine.Object.FindObjectsOfType<SharkKillZone>())
            if (sk != null) Collect(sk.gameObject, "SharkKillZone");
        Log.Info("Danger", $"{_killColliders.Count} kill-zone colliders in this level");
    }

    private static void Collect(GameObject go, string what)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
        {
            if (c == null) continue;
            var b = c.bounds;
            Log.Info("Danger", $"{what} '{UiDump.PathOf(c.transform)}' {c.GetIl2CppType().Name} trigger={c.isTrigger} " +
                               $"min {b.min} max {b.max}");
            // Zones that only send you back to safe ground are still worth avoiding.
            _killColliders.Add(c.Pointer);
        }
    }

    private static void Silence()
    {
        Fmod.Stop(_channel);
        _channel = IntPtr.Zero;
    }

    public static void Tick()
    {
        if (!Settings.DangerWarning || !Game.InPlay) { Silence(); return; }
        if (!_scanned) Scan();
        var now = Time.unscaledTime;
        if (now < _nextProbe) return;
        _nextProbe = now + Interval;

        var p = Game.Player;
        var view = Game.View;
        if (p == null || view == null) { Silence(); return; }
        var feet = p.transform.position;
        var fwd = view.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) return;
        fwd.Normalize();
        var rgt = new Vector3(fwd.z, 0f, -fwd.x);
        var chest = feet + Vector3.up * ChestHeight;

        // Nearest danger over all directions: its distance, its safe inner bound, its direction.
        var best = float.MaxValue;
        var bestSafe = 0f;
        var bestDir = Vector3.zero;
        string bestWhat = null;
        for (var s = 0; s < Directions; s++)
        {
            var a = s * 2f * Mathf.PI / Directions;
            var dir = rgt * Mathf.Sin(a) + fwd * Mathf.Cos(a);
            var wall = WallDistance(chest, dir);
            var safe = 0f;
            foreach (var r in Rings)
            {
                if (r >= best) break;          // cannot beat what was already found
                if (r > wall - 0.2f) break;    // the floor beyond a wall does not count
                var what = Probe(chest + dir * r, feet.y);
                if (what == null) { safe = r; continue; }
                best = r; bestSafe = safe; bestDir = dir; bestWhat = what;
                break;
            }
        }

        if (bestWhat == null)
        {
            Silence();
            _lastWhat = null;
            return;
        }

        // Narrow the edge down between the last safe ring and the first dangerous one.
        var lo = bestSafe;
        var hi = best;
        for (var i = 0; i < 3; i++)
        {
            var mid = (lo + hi) * 0.5f;
            if (Probe(chest + bestDir * mid, feet.y) != null) hi = mid; else lo = mid;
        }
        var dist = hi;

        var right = Vector3.Dot(bestDir, rgt);
        var forward = Vector3.Dot(bestDir, fwd);
        var place = Spatial.Place(right, forward, Range);
        // Louder and faster as you close in: full volume and double speed at the edge itself.
        var near = Mathf.Clamp01(1f - dist / Range);
        var volume = 0.12f + 0.88f * near * near;
        var pitch = place.Pitch * (0.8f + 1.2f * near);
        _channel = Cues.Loop(Cues.Cue.Danger, _channel, new Placement(place.Pan, volume, pitch));

        if (bestWhat != _lastWhat && _logged < 40)
        {
            _logged++;
            Log.Info("Danger", $"{bestWhat} {dist:F1} m {Spatial.Direction(right, forward)}, player at {feet}");
        }
        _lastWhat = bestWhat;
    }

    /// <summary>Distance to the first wall at chest height in this direction, or more than the range.</summary>
    private static float WallDistance(Vector3 chest, Vector3 dir)
    {
        var hits = Physics.RaycastAll(chest, dir, Range + 0.5f, ~0, QueryTriggerInteraction.Ignore);
        var first = float.MaxValue;
        if (hits != null)
            foreach (var h in hits)
                if (IsSolid(h.collider)) first = Mathf.Min(first, h.distance);
        return first;
    }

    /// <summary>Static level geometry: not a trigger, a kill zone, a loose prop or a person.</summary>
    private static bool IsSolid(Collider c)
    {
        if (c == null || c.isTrigger || _killColliders.Contains(c.Pointer)) return false;
        var rb = c.attachedRigidbody;
        if (rb != null && !rb.isKinematic) return false;
        return c.GetComponentInParent<Enemy>() == null && c.GetComponentInParent<PTTRPlayer>() == null;
    }

    /// <summary>What is below this point: "water" for a kill zone, "drop" for no ground, or null when safe.</summary>
    private static string Probe(Vector3 from, float feetY)
    {
        var hits = Physics.RaycastAll(from, Vector3.down, RayLength, ~0, QueryTriggerInteraction.Collide);
        var firstSolid = float.MaxValue;
        var firstKill = float.MaxValue;
        if (hits != null)
            foreach (var h in hits)
            {
                var c = h.collider;
                if (c == null) continue;
                if (_killColliders.Contains(c.Pointer)) { firstKill = Mathf.Min(firstKill, h.distance); continue; }
                if (IsSolid(c)) firstSolid = Mathf.Min(firstSolid, h.distance);
            }
        if (firstKill < firstSolid) return "water";
        // Solid ground more than MaxDrop below the feet (or none at all) is a drop.
        var groundBelowFeet = firstSolid - (from.y - feetY);
        if (firstSolid == float.MaxValue || groundBelowFeet > MaxDrop) return "drop";
        return null;
    }

    /// <summary>Called when the player dies: where it happened, for diagnosing missed warnings.</summary>
    public static void LogDeath()
    {
        var p = Game.Player;
        if (p != null) Log.Info("Danger", $"player died at {p.transform.position}");
    }
}
