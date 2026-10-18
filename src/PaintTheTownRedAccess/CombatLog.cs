using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// Diagnostic: logs every enemy blow aimed at the player (PTTRPlayer.GetPunched): who, whether
/// it landed, whether the player was blocking, the attacker's angle from the view, and health
/// before and after. A block only counts when the attacker is within 80 degrees of where the
/// player faces (disassembled GetPunched), so the angle says why a block failed.
/// </summary>
[HarmonyPatch(typeof(PTTRPlayer), nameof(PTTRPlayer.GetPunched))]
internal static class CombatLog
{
    private static int _logged;
    private static float _before;

    private static void Prefix(PTTRPlayer __instance) => _before = __instance.health;

    private static void Postfix(PTTRPlayer __instance, Transform enemyTransform, float damageAmount, Enemy hittingEnemy, bool __result)
    {
        if (_logged >= 300) return;
        _logged++;
        var angle = float.NaN;
        var view = Game.View;
        if (view != null && enemyTransform != null)
        {
            var to = enemyTransform.position - __instance.transform.position; to.y = 0f;
            var fwd = view.forward; fwd.y = 0f;
            angle = Vector3.Angle(fwd, to);
        }
        var who = hittingEnemy != null ? EnemyRadar.TypeName(hittingEnemy) : "?";
        Log.Info("Combat", $"blow from {who}: landed={__result} blocking={__instance.blocking} angle {angle:F0} " +
                           $"damage {damageAmount:F2} health {_before:F2} -> {__instance.health:F2}");
    }
}
