using Il2Cpp;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// A quiet beep every 100 ms while a swing would land. Melee hits in this game are physical:
/// WeaponAttack.Swing / PunchAttack only animate the arm and the fist or weapon collider hits
/// whatever body part it touches (no target is chosen in code; FindTargetsInFrontOfPlayer is
/// only used by spells). So this sweeps a 0.25 m sphere from the camera straight ahead over
/// swing distance, ignoring triggers, the player's own body and loose or held weapons; if the
/// first solid thing it meets belongs to a living character, a swing now would hit.
/// </summary>
internal static class HitCheck
{
    private const float Radius = 0.25f;
    private const float Interval = 0.1f;

    private static float _next;
    private static bool _logged;
    private static IntPtr _channel;

    public static void Tick()
    {
        if (!Settings.HitBeep || !Game.InPlay) return;
        var now = Time.unscaledTime;
        if (now < _next) return;
        _next = now + Interval;

        var view = Game.View;
        if (view == null) return;
        var target = FirstHit(view.position, view.forward, Settings.Reach);
        if (target == null) return;
        // A replacement reach.wav can be longer than the 100 ms tick: wait for it to finish
        // rather than stacking copies, so a long sound simply repeats back to back.
        if (Fmod.IsPlaying(_channel)) return;
        _channel = Cues.Play2D(Cues.Cue.Reach, 0.35f);
        if (!_logged) { _logged = true; Log.Info("HitCheck", $"first in-reach target: {target.gameObject.name}"); }
    }

    private static Enemy FirstHit(Vector3 origin, Vector3 dir, float reach)
    {
        var hits = Physics.SphereCastAll(origin, Radius, dir, reach, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return null;

        Enemy best = null;
        var bestDist = float.MaxValue;
        var blockDist = float.MaxValue;
        foreach (var h in hits)
        {
            var c = h.collider;
            if (c == null) continue;
            if (c.GetComponentInParent<PTTRPlayer>() != null) continue; // our body, our held weapon
            if (c.GetComponentInParent<WeaponBase>() != null) continue; // loose props and weapons
            var dist = h.distance;
            var e = c.GetComponentInParent<Enemy>();
            if (e != null && !e.IsDead())
            {
                if (dist < bestDist) { bestDist = dist; best = e; }
            }
            else if (dist < blockDist) blockDist = dist; // a wall or furniture in the way
        }
        return best != null && bestDist <= blockDist ? best : null;
    }
}
