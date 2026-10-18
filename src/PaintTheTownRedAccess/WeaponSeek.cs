using Il2Cpp;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// V: get a weapon fast. Picks the nearest loose weapon (not held, not broken, not a static
/// prop), says what and how far, turns you toward the first step of the walkable route
/// (PathGuide, the game's A* graph) and pings the beacon from that route. On reaching it
/// (PickupDistance-like reach, 2.5 m) it is picked up with PTTRPlayer.PickUpWeapon - the same
/// as E with the pickup assist. If someone else takes it first the next nearest is chosen.
/// V again cancels. Pressing V with a weapon already in reach picks it up at once.
/// </summary>
internal static class WeaponSeek
{
    private const float Reach = 2.5f;
    private const float MaxRange = 80f;

    private static Weapon _target;
    private static string _name;
    private static float _nextPing;

    public static bool Active => _target != null;

    // Weapons V gave up on this level (could not get closer for a while: on a shelf, behind a counter).
    private static readonly HashSet<IntPtr> _unreachable = new();
    private static float _closest, _progressAt;
    private const float GiveUpAfter = 8f;
    // Standing under it waiting for you to jump: give that longer before giving up.
    private const float GiveUpUnder = 15f;
    // Pick-up works up to this far above or below (TryTake); jumping raises you, so weapons up
    // to this plus the jump height are worth going for.
    private const float TakeHeight = 2f;
    private static float _jump = -1f;
    private static bool _saidJump;

    public static void OnSceneLoaded() { _target = null; _unreachable.Clear(); _jump = -1f; }

    /// <summary>How high a jump lifts you: v^2 / 2g from the controller's jump speed and gravity.</summary>
    private static float JumpHeight()
    {
        if (_jump >= 0f) return _jump;
        _jump = 1f;
        var c = UnityEngine.Object.FindObjectOfType<PTTRFirstPersonController>();
        if (c != null)
        {
            var g = Mathf.Abs(Physics.gravity.y) * Mathf.Max(0.1f, c.m_GravityMultiplier);
            if (g > 0.1f && c.m_JumpSpeed > 0f) _jump = Mathf.Clamp(c.m_JumpSpeed * c.m_JumpSpeed / (2f * g), 0f, 3f);
            Log.Info("Seek", $"jump speed {c.m_JumpSpeed} gravity x{c.m_GravityMultiplier} -> jump height {_jump:0.00} m");
        }
        return _jump;
    }

    private static bool High(PTTRPlayer p) => _target.transform.position.y - p.transform.position.y > TakeHeight - 0.3f;

    public static void Toggle()
    {
        if (!Game.InPlay) return;
        if (_target != null) { Stop(); Speech.Say(Strings.SeekOff); return; }
        if (!Pick(null)) { Speech.Say(Strings.NoWeapons); return; }
        Scanner.StopBeacon();
        PathGuide.Reset();
        var p = Game.Player;
        var d = Flat(_target.transform.position - p.transform.position).magnitude;
        if (TryTake(p)) return;
        Face(p);
        Speech.Say($"{_name}, {Spatial.Distance(d)}" + (High(p) ? ", " + Strings.SeekHigh : ""));
    }

    public static void Stop()
    {
        _target = null;
        PathGuide.Reset();
    }

    public static void Tick()
    {
        if (_target == null) return;
        if (!Game.InPlay) return;
        var p = Game.Player;
        if (p == null) return;

        if (!Free(_target))
        {
            // Someone took it or it broke: go for the next nearest instead.
            var old = _target;
            if (!Pick(old)) { Stop(); Speech.Say(Strings.NoWeapons); return; }
            PathGuide.Reset();
            Speech.Say($"{_name}, {Spatial.Distance(Flat(_target.transform.position - p.transform.position).magnitude)}");
        }

        if (TryTake(p)) return;

        var now = Time.unscaledTime;
        // Give up on a weapon you cannot get any closer to, and go for the next one.
        var flatDist = Flat(_target.transform.position - p.transform.position).magnitude;
        // Right under a weapon that is up high: say once to jump for it.
        if (flatDist <= Reach && !_saidJump && High(p)) { _saidJump = true; Speech.Say(Strings.SeekJump); }
        if (flatDist < _closest - 0.5f) { _closest = flatDist; _progressAt = now; }
        else if (now - _progressAt > (flatDist <= Reach ? GiveUpUnder : GiveUpAfter))
        {
            Log.Info("Seek", $"no progress to '{_target.gameObject.name}' for {GiveUpAfter} s, skipping it");
            _unreachable.Add(_target.Pointer);
            var lost = _name;
            if (!Pick(null)) { Stop(); Speech.Say(string.Format(Strings.CantReach, lost) + ". " + Strings.NoWeapons); return; }
            PathGuide.Reset();
            Speech.Say(string.Format(Strings.CantReach, lost) + ". " +
                       $"{_name}, {Spatial.Distance(Flat(_target.transform.position - p.transform.position).magnitude)}");
            return;
        }
        if (now < _nextPing) return;
        var steer = PathGuide.Steer(p.transform.position, _target.transform.position);
        if (!Game.Frame(steer, out var r, out var f, out _)) return;
        var straight = Flat(_target.transform.position - p.transform.position).magnitude;
        var route = PathGuide.Remaining > 0 ? PathGuide.Remaining : straight;
        _nextPing = now + Spatial.PingInterval(route, 20f);
        Cues.Play(Cues.Cue.Beacon, Spatial.Place(r, f, 30f));
    }

    private static bool TryTake(PTTRPlayer p)
    {
        var d = _target.transform.position - p.transform.position;
        if (Mathf.Abs(d.y) > TakeHeight || Flat(d).magnitude > Reach) return false;
        Log.Info("Seek", $"pick up '{_target.gameObject.name}'");
        p.PickUpWeapon(_target);
        Cues.Play2D(Cues.Cue.Arrive);
        Speech.Say(string.Format(Strings.PickedUp, _name));
        Stop();
        return true;
    }

    /// <summary>Turns the player toward the first step of the route (or the weapon itself).</summary>
    private static void Face(PTTRPlayer p)
    {
        var steer = PathGuide.Steer(p.transform.position, _target.transform.position);
        var to = Flat(steer - p.transform.position);
        if (to.sqrMagnitude < 0.25f) to = Flat(_target.transform.position - p.transform.position);
        if (to.sqrMagnitude < 0.01f) return;
        p.SetRotation(Quaternion.Euler(0f, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 0f));
    }

    private static bool Free(Weapon w) =>
        w != null && w.gameObject.activeInHierarchy && !w.held && !w.hasBroken && !w.isStaticNonWeapon;

    private static bool Pick(Weapon except)
    {
        var p = Game.Player;
        _target = null;
        if (p == null) return false;
        var from = p.transform.position;
        var best = MaxRange;
        var above = TakeHeight + JumpHeight() - 0.2f;
        foreach (var w in UnityEngine.Object.FindObjectsOfType<Weapon>())
        {
            if (!Free(w) || (except != null && w.Pointer == except.Pointer)) continue;
            var d = w.transform.position - from;
            // Out of reach even with a jump (high shelves, other floors), or given up on before.
            if (d.y > above || d.y < -1.8f || _unreachable.Contains(w.Pointer)) continue;
            var dist = Flat(d).magnitude;
            if (dist < best) { best = dist; _target = w; }
        }
        if (_target == null) return false;
        _name = TextClean.ThingName(_target.gameObject.name);
        _closest = best;
        _saidJump = false;
        _progressAt = Time.unscaledTime;
        Log.Info("Seek", $"target '{_target.gameObject.name}' {best:0.0} m");
        return true;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
