using Il2Cpp;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// Warns before you walk into deadly water or off a big drop. Pirate Cove's sea kills through
/// KillZone triggers (killPlayer) and SharkKillZone boxes; the ocean meshes are only visuals.
/// Five times a second, the ground is probed in 8 directions around you at 1.2 m and 2.5 m:
/// a ray down from chest height that meets a kill zone before solid ground, or finds no
/// ground within 4 m, is danger. The Edge cue plays from the nearest dangerous direction,
/// louder and more often when it is the near ring.
/// </summary>
internal static class DangerProbe
{
    private static readonly float[] Rings = { 1.2f, 2.5f };
    private const float MaxDrop = 4f;
    private const float ChestHeight = 1.2f;

    private static float _nextProbe, _nextCue;
    private static bool _logged;

    public static void Tick()
    {
        if (!Settings.DangerWarning || !Game.InPlay) return;
        var now = Time.unscaledTime;
        if (now < _nextProbe) return;
        _nextProbe = now + 0.2f;

        var p = Game.Player;
        var view = Game.View;
        if (p == null || view == null) return;
        var feet = p.transform.position;
        var fwd = view.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) return;
        fwd.Normalize();
        var rgt = new Vector3(fwd.z, 0f, -fwd.x);

        for (var ring = 0; ring < Rings.Length; ring++)
        {
            for (var s = 0; s < 8; s++)
            {
                var a = s * Mathf.PI / 4f;
                var right = Mathf.Sin(a) * Rings[ring];
                var forward = Mathf.Cos(a) * Rings[ring];
                var point = feet + rgt * right + fwd * forward;
                var what = Probe(point + Vector3.up * ChestHeight, feet.y);
                if (what == null) continue;

                if (!_logged) { _logged = true; Log.Info("Danger", $"first warning: {what} at ring {Rings[ring]} m, sector {s}"); }
                if (now < _nextCue) return;
                _nextCue = now + (ring == 0 ? 0.25f : 0.5f);
                var place = Spatial.Place(right, forward, 4f);
                Cues.Play(Cues.Cue.Edge, new Placement(place.Pan, ring == 0 ? 1f : 0.55f, place.Pitch));
                return; // the nearest ring and first direction found is enough
            }
        }
    }

    /// <summary>What is below this point: "water" for a kill zone, "drop" for no ground, or null when safe.</summary>
    private static string Probe(Vector3 from, float feetY)
    {
        var depth = ChestHeight + MaxDrop;
        var hits = Physics.RaycastAll(from, Vector3.down, depth, ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return "drop";

        var firstSolid = float.MaxValue;
        var firstKill = float.MaxValue;
        foreach (var h in hits)
        {
            var c = h.collider;
            if (c == null) continue;
            if (c.isTrigger)
            {
                var kz = c.GetComponent<KillZone>();
                if ((kz != null && kz.killPlayer) || c.GetComponent<SharkKillZone>() != null)
                    firstKill = Mathf.Min(firstKill, h.distance);
                continue;
            }
            // Ignore things that move about: people, props, weapons.
            var rb = c.attachedRigidbody;
            if (rb != null && !rb.isKinematic) continue;
            if (c.GetComponentInParent<Enemy>() != null || c.GetComponentInParent<PTTRPlayer>() != null) continue;
            firstSolid = Mathf.Min(firstSolid, h.distance);
        }
        if (firstKill < firstSolid) return "water";
        if (firstSolid == float.MaxValue) return "drop";
        return null;
    }
}
