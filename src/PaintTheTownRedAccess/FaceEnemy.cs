using Il2Cpp;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// Z: turn to face the nearest hostile enemy that is on its feet (or the nearest one down,
/// if none is standing), and say what it is and how far. Uses PTTRPlayer.SetRotation, the
/// game's own way of setting the player's facing, which also resets the SimpleMouseRotator
/// look state so the next mouse or arrow turn starts from the new facing.
/// </summary>
internal static class FaceEnemy
{
    public static void Turn()
    {
        var p = Game.Player;
        var list = CharacterManager.enemies;
        if (p == null || list == null) return;
        var from = p.transform.position;

        Enemy best = null;
        var bestScore = float.MaxValue;
        for (var i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e == null || !e.isActiveAndEnabled || e.IsDead() || !EnemyRadar.Hostile(e)) continue;
            var d = e.transform.position - from;
            d.y = 0f;
            var score = d.magnitude + (e.IsDown() ? 1000f : 0f); // standing ones first
            if (score < bestScore) { bestScore = score; best = e; }
        }
        if (best == null) { Speech.Say(Strings.NoEnemies); return; }

        var to = best.transform.position - from;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return;
        var yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        p.SetRotation(Quaternion.Euler(0f, yaw, 0f));
        Log.Info("Face", $"turned to {best.gameObject.name} yaw {yaw:0}; view now {Game.View?.eulerAngles.y:0}");

        var name = EnemyRadar.TypeName(best) + (best.IsDown() ? ", " + Strings.Down : "");
        Speech.Say($"{name}, {Spatial.Distance(to.magnitude)}");
    }
}
