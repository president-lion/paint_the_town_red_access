using HarmonyLib;
using Il2Cpp;

namespace PaintTheTownRedAccess;

/// <summary>
/// Sounds for your own blows. PTTRPlayer.PlayerHitItemEvaluation is called from
/// Weapon/ArmsWeapon Hit and HitSimple only when playerHitting is set (disassembled), so it
/// fires once per enemy the player's fists or weapon connect with: the Hit cue.
/// Weapon.DoBreak is the break itself (it plays the game's break sound); when the broken
/// weapon is the one in the player's hand, the Break cue plays.
/// </summary>
internal static class WeaponEvents
{
    private static float _lastHit;

    [HarmonyPatch(typeof(PTTRPlayer), nameof(PTTRPlayer.PlayerHitItemEvaluation))]
    private static class HitPatch
    {
        private static void Postfix(Enemy enemy)
        {
            // One sound per swing even when it connects with several body parts at once.
            var now = UnityEngine.Time.unscaledTime;
            if (now - _lastHit < 0.08f) return;
            _lastHit = now;
            Cues.Play2D(Cues.Cue.Hit, 0.7f);
            Log.Debug("Weapon", $"hit {(enemy != null ? EnemyRadar.TypeName(enemy) : "?")}");
        }
    }

    [HarmonyPatch(typeof(Weapon), nameof(Weapon.DoBreak))]
    private static class BreakPatch
    {
        private static void Prefix(Weapon __instance)
        {
            var held = Game.Player?.weaponAttack?.currentWeapon;
            if (held == null || __instance == null || held.Pointer != __instance.Pointer) return;
            Cues.Play2D(Cues.Cue.Break);
            Log.Info("Weapon", $"held weapon broke: {__instance.name}");
        }
    }
}
