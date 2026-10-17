using Il2Cpp;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// E without aiming. The game's pickup (WeaponAttack.InteractWithWeaponOrButton) only takes
/// what a ray and sphere casts straight out of the camera hit, so you must face the weapon,
/// and look down for one on the floor. After E, if the game picked nothing up, this picks up
/// the nearest free weapon within reach (PTTRPlayer.PickUpWeapon) and says its name.
/// </summary>
internal static class PickupAssist
{
    private const float Reach = 2.5f;
    private const float CheckDelay = 0.2f;

    private static float _checkAt = -1f;
    private static IntPtr _heldBefore;

    public static void Tick()
    {
        if (!Settings.PickupAssist || !Game.InPlay) { _checkAt = -1f; return; }
        var p = Game.Player;
        var now = Time.unscaledTime;

        if (Input.GetKeyDown(KeyCode.E))
        {
            _heldBefore = Held(p);
            _checkAt = now + CheckDelay;
            return;
        }

        if (_checkAt < 0f || now < _checkAt) return;
        _checkAt = -1f;
        if (Held(p) != _heldBefore) return; // the game's own pickup worked

        var w = Nearest(p.transform.position);
        if (w == null) { Speech.Say(Strings.NothingInReach); return; }
        var name = TextClean.ThingName(w.gameObject.name);
        Log.Info("Pickup", $"assist: '{w.gameObject.name}' at {Vector3.Distance(w.transform.position, p.transform.position):0.0}m");
        p.PickUpWeapon(w);
        Speech.Say(string.Format(Strings.PickedUp, name));
    }

    private static IntPtr Held(PTTRPlayer p)
    {
        var w = p.CurrentWeapon();
        return w == null ? IntPtr.Zero : w.Pointer;
    }

    private static Weapon Nearest(Vector3 from)
    {
        Weapon best = null;
        var bestDist = Reach;
        foreach (var w in UnityEngine.Object.FindObjectsOfType<Weapon>())
        {
            if (w == null || w.held || w.isStaticNonWeapon || w.hasBroken || !w.gameObject.activeInHierarchy) continue;
            var d = w.transform.position - from;
            if (Mathf.Abs(d.y) > 2f) continue;
            d.y = 0f;
            var dist = d.magnitude;
            if (dist < bestDist) { bestDist = dist; best = w; }
        }
        return best;
    }
}
