using Il2Cpp;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// Warns before you walk into deadly water or off a big drop. Pirate Cove's sea kills through
/// KillZone triggers (OnTriggerEnter: SetHealth to kill, or ResetToMostRecentValidPosition when
/// killPlayer is off) and SharkKillZone boxes; the ocean meshes are only visuals.
///
/// At level load every collider of a KillZone or SharkKillZone (including child colliders - the
/// first version looked for the component on the hit collider itself and never fired) is
/// collected and logged. Five times a second the ground is probed in 8 directions around you at
/// 1.2 m and 2.5 m: a ray down from chest height (12 m) that meets one of those colliders before
/// solid ground is water; one that finds no solid ground within 4 m below your feet is a drop.
/// The Edge cue plays from the nearest dangerous direction, louder and more often on the near ring.
/// </summary>
internal static class DangerProbe
{
    private static readonly float[] Rings = { 1.2f, 2.5f };
    private const float MaxDrop = 4f;
    private const float ChestHeight = 1.2f;
    private const float RayLength = 12f;

    private static readonly HashSet<IntPtr> _killColliders = new();
    private static bool _scanned;
    private static float _nextProbe, _nextCue;
    private static int _logged;

    public static void OnSceneLoaded()
    {
        _killColliders.Clear();
        _scanned = false;
        _logged = 0;
    }

    /// <summary>Collects the kill zones' colliders once the level is playable.</summary>
    private static void Scan()
    {
        _scanned = true;
        foreach (var kz in UnityEngine.Object.FindObjectsOfType<KillZone>())
            if (kz != null) Collect(kz.gameObject, $"KillZone killPlayer={kz.killPlayer} killEnemies={kz.killEnemies}", kz.killPlayer);
        foreach (var sk in UnityEngine.Object.FindObjectsOfType<SharkKillZone>())
            if (sk != null) Collect(sk.gameObject, "SharkKillZone", true);
        Log.Info("Danger", $"{_killColliders.Count} kill-zone colliders in this level");
    }

    private static void Collect(GameObject go, string what, bool deadly)
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

    public static void Tick()
    {
        if (!Settings.DangerWarning || !Game.InPlay) return;
        if (!_scanned) Scan();
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

                if (_logged < 5) { _logged++; Log.Info("Danger", $"warning: {what} ring {Rings[ring]} m sector {s}, player at {feet}"); }
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
        var hits = Physics.RaycastAll(from, Vector3.down, RayLength, ~0, QueryTriggerInteraction.Collide);
        var firstSolid = float.MaxValue;
        var firstKill = float.MaxValue;
        if (hits != null)
            foreach (var h in hits)
            {
                var c = h.collider;
                if (c == null) continue;
                if (_killColliders.Contains(c.Pointer)) { firstKill = Mathf.Min(firstKill, h.distance); continue; }
                if (c.isTrigger) continue;
                // Ignore things that move about: people, props, weapons.
                var rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                if (c.GetComponentInParent<Enemy>() != null || c.GetComponentInParent<PTTRPlayer>() != null) continue;
                firstSolid = Mathf.Min(firstSolid, h.distance);
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
