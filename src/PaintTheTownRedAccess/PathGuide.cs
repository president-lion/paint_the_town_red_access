using Il2Cpp;
using Il2CppPathfinding;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// Turns a beacon target into a point to walk toward along the game's own A* graph (the one
/// enemies path on), so the beacon leads round walls and out through doorways - for example
/// out of the Prison start cell, whose only exit is a narrow gap in the bars - instead of
/// pointing straight through them.
///
/// A path is requested with ABPath.Construct + AstarPath.StartPath (BlockUntilCalculated is
/// stripped), polled with IsDone, and vectorPath copied out. Every tick the steer point is
/// the farthest path point within 10 m that the player can see at chest height, so the beacon
/// jumps corner to corner. With no graph or a failed path it falls back to the target itself.
/// </summary>
internal static class PathGuide
{
    private const float Replan = 0.6f;
    private const float MaxSteer = 10f;
    private const float ChestHeight = 1.2f;

    private static readonly List<Vector3> _points = new();
    private static ABPath _pending;
    private static float _nextPlan;
    private static Vector3 _goal;
    private static int _logged;

    /// <summary>Total walking distance left along the current path, or -1 when there is none.</summary>
    public static float Remaining { get; private set; } = -1f;

    public static void Reset()
    {
        _points.Clear();
        _pending = null;
        _nextPlan = 0f;
        Remaining = -1f;
    }

    /// <summary>The point to head for now to reach <paramref name="goal"/>.</summary>
    public static Vector3 Steer(Vector3 from, Vector3 goal)
    {
        if ((goal - _goal).sqrMagnitude > 4f) { Reset(); _goal = goal; }
        Poll(from, goal);
        if (_points.Count < 2) { Remaining = -1f; return goal; }

        // Drop points already passed: the nearest point and everything before it.
        var nearest = 0;
        var best = float.MaxValue;
        for (var i = 0; i < _points.Count; i++)
        {
            var d = Flat(_points[i] - from).sqrMagnitude;
            if (d < best) { best = d; nearest = i; }
        }

        var steer = _points[Math.Min(nearest + 1, _points.Count - 1)];
        var chest = from + Vector3.up * ChestHeight;
        for (var i = _points.Count - 1; i > nearest; i--)
        {
            var p = _points[i];
            if (Flat(p - from).magnitude > MaxSteer) continue;
            if (CanSee(chest, p + Vector3.up * ChestHeight)) { steer = p; break; }
        }

        var rest = Flat(_points[Math.Min(nearest + 1, _points.Count - 1)] - from).magnitude;
        for (var i = nearest + 1; i < _points.Count - 1; i++) rest += Flat(_points[i + 1] - _points[i]).magnitude;
        Remaining = rest;
        return steer;
    }

    private static void Poll(Vector3 from, Vector3 goal)
    {
        if (_pending != null)
        {
            if (!_pending.IsDone()) return;
            var done = _pending;
            _pending = null;
            if (done.error)
            {
                if (_logged++ < 10) Log.Info("Path", "no path: " + done.errorLog);
                _points.Clear();
                return;
            }
            _points.Clear();
            var vp = done.vectorPath;
            if (vp != null) for (var i = 0; i < vp.Count; i++) _points.Add(vp[i]);
            if (_logged++ < 10) Log.Info("Path", $"{_points.Count} points from {from} to {goal}");
            return;
        }
        var now = Time.unscaledTime;
        if (now < _nextPlan) return;
        _nextPlan = now + Replan;
        if (AstarPath.active == null)
        {
            if (_logged++ < 3) Log.Info("Path", "no A* graph in this level");
            return;
        }
        var path = ABPath.Construct(from, goal, null);
        AstarPath.StartPath(path, false);
        _pending = path;
    }

    private static bool CanSee(Vector3 a, Vector3 b)
    {
        var d = b - a;
        var hits = Physics.RaycastAll(a, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null) return true;
        foreach (var h in hits)
        {
            var c = h.collider;
            if (c == null) continue;
            var rb = c.attachedRigidbody;
            if (rb != null && !rb.isKinematic) continue;
            if (c.GetComponentInParent<Enemy>() != null || c.GetComponentInParent<PTTRPlayer>() != null) continue;
            return false;
        }
        return true;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
