using Il2Cpp;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// Arena traps (Trap and its subclasses: ground spikes, spike pillars, spinning blades, fire
/// pits, flamethrowers; laid out per fight by ArenaSetup from the ArenaChallenge assets) are
/// silent until they fire. A low pulsing buzz loops from the nearest trap within 4 m: quiet
/// at a distance, full when you stand on it, and higher and louder while the trap is active
/// (Trap.activated / transitioning). When the set of traps changes (a new fight), the count
/// is spoken once.
/// </summary>
internal static class TrapWarning
{
    private const float Range = 4f;
    private const float Interval = 0.1f;

    private sealed class Info
    {
        public Trap Trap;
        public Bounds Area;
    }

    private static readonly List<Info> _traps = new();
    private static float _nextFind, _nextTick;
    private static IntPtr _channel;
    private static int _lastCount = -1, _logged;

    public static void OnSceneLoaded()
    {
        Silence();
        _traps.Clear();
        _nextFind = 0f;
        _lastCount = -1;
    }

    private static void Silence()
    {
        Fmod.Stop(_channel);
        _channel = IntPtr.Zero;
    }

    private static void Find()
    {
        _traps.Clear();
        foreach (var t in UnityEngine.Object.FindObjectsOfType<Trap>())
        {
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            _traps.Add(new Info { Trap = t, Area = Footprint(t) });
        }
        if (_traps.Count != _lastCount)
        {
            if (_traps.Count > 0)
                Speech.Say(_traps.Count == 1 ? Strings.Traps1 : string.Format(Strings.TrapsN, _traps.Count), interrupt: false);
            if (_logged++ < 20)
                foreach (var i in _traps)
                    Log.Info("Trap", $"{i.Trap.GetIl2CppType().Name} '{i.Trap.name}' area {i.Area.min} - {i.Area.max}");
            _lastCount = _traps.Count;
        }
    }

    /// <summary>The trap's floor area: its renderers' bounds, or 1 m round it when it has none showing.</summary>
    private static Bounds Footprint(Trap t)
    {
        var b = new Bounds(t.transform.position, new Vector3(2f, 2f, 2f));
        var first = true;
        foreach (var r in t.GetComponentsInChildren<Renderer>(false))
        {
            if (r == null || !r.enabled) continue;
            var rb = r.bounds;
            if (rb.size.sqrMagnitude < 1e-4f) continue;
            if (first) { b = rb; first = false; } else b.Encapsulate(rb);
        }
        return b;
    }

    public static void Tick()
    {
        if (!Settings.DangerWarning || !Game.InPlay) { Silence(); return; }
        var now = Time.unscaledTime;
        if (now >= _nextFind) { _nextFind = now + 2f; Find(); }
        if (_traps.Count == 0) { Silence(); return; }
        if (now < _nextTick) return;
        _nextTick = now + Interval;

        var p = Game.Player;
        if (p == null) { Silence(); return; }
        var feet = p.transform.position;

        Info best = null;
        var bestDist = float.MaxValue;
        var bestPoint = Vector3.zero;
        foreach (var i in _traps)
        {
            if (i.Trap == null) continue;
            // Nearest point of the trap's area on the floor plane.
            var a = i.Area;
            var q = new Vector3(Mathf.Clamp(feet.x, a.min.x, a.max.x), feet.y, Mathf.Clamp(feet.z, a.min.z, a.max.z));
            var d = new Vector2(q.x - feet.x, q.z - feet.z).magnitude;
            if (d < bestDist) { bestDist = d; best = i; bestPoint = q; }
        }
        if (best == null || bestDist > Range) { Silence(); return; }

        var hot = best.Trap.activated || best.Trap.transitioning;
        var near = Mathf.Clamp01(1f - bestDist / Range);
        var volume = (hot ? 0.35f : 0.15f) + (hot ? 0.65f : 0.5f) * near * near;
        float pan = 0f, pitch = hot ? 1.6f : 1f;
        if (bestDist > 0.05f && Game.Frame(bestPoint, out var r, out var f, out _))
        {
            var place = Spatial.Place(r, f, Range);
            pan = place.Pan;
            pitch *= place.Pitch;
        }
        _channel = Cues.Loop(Cues.Cue.Trap, _channel, new Placement(pan, volume, pitch));
    }
}
