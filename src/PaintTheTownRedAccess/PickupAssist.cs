using Il2Cpp;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// E without aiming. The game's E (WeaponAttack.InteractWithWeaponOrButton) only takes what a
/// ray and sphere casts straight out of the camera hit - a weapon or a WorldButton - so you
/// must face it, and look down for things on the floor. After E, if the game neither picked
/// up a weapon nor pressed the nearest button, this does whichever of the two is nearer
/// within reach (PTTRPlayer.PickUpWeapon / WorldButton.PressButton) and says what it did.
/// </summary>
internal static class PickupAssist
{
    private const float Reach = 2.5f;
    private const float CheckDelay = 0.2f;

    private static float _checkAt = -1f;
    private static IntPtr _heldBefore;
    private static WorldButton _buttonBefore;
    private static bool _buttonWasPressed;

    public static void Tick()
    {
        if (!Settings.PickupAssist || !Game.InPlay || ButtonMenu.BlockingInput) { _checkAt = -1f; return; }
        var p = Game.Player;
        var from = p.transform.position;
        var now = Time.unscaledTime;

        if (Input.GetKeyDown(KeyCode.E))
        {
            _heldBefore = Held(p);
            _buttonBefore = NearestButton(from, out _);
            _buttonWasPressed = _buttonBefore != null && _buttonBefore.pressed;
            _checkAt = now + CheckDelay;
            return;
        }

        if (_checkAt < 0f || now < _checkAt) return;
        _checkAt = -1f;
        if (Held(p) != _heldBefore) return; // the game picked something up
        if (_buttonBefore != null && _buttonBefore.pressed != _buttonWasPressed) return; // the game pressed it

        var weapon = NearestWeapon(from, out var weaponDist);
        var button = NearestButton(from, out var buttonDist);
        if (button != null && (weapon == null || buttonDist <= weaponDist))
        {
            Log.Info("Pickup", $"assist: press '{button.gameObject.name}' at {buttonDist:0.0}m");
            button.PressButton();
            Speech.Say(string.Format(Strings.Pressed, Scanner.ButtonName(button)));
            return;
        }
        if (weapon == null) { Speech.Say(Strings.NothingInReach); return; }
        var name = TextClean.ThingName(weapon.gameObject.name);
        Log.Info("Pickup", $"assist: '{weapon.gameObject.name}' at {weaponDist:0.0}m");
        p.PickUpWeapon(weapon);
        Speech.Say(string.Format(Strings.PickedUp, name));
    }

    private static IntPtr Held(PTTRPlayer p)
    {
        var w = p.CurrentWeapon();
        return w == null ? IntPtr.Zero : w.Pointer;
    }

    /// <summary>Ground distance within reach and within 2 m of height, else float.MaxValue.</summary>
    private static float Dist(Vector3 from, Vector3 to)
    {
        var d = to - from;
        if (Mathf.Abs(d.y) > 2f) return float.MaxValue;
        d.y = 0f;
        var dist = d.magnitude;
        return dist <= Reach ? dist : float.MaxValue;
    }

    private static Weapon NearestWeapon(Vector3 from, out float best)
    {
        Weapon found = null;
        best = float.MaxValue;
        foreach (var w in UnityEngine.Object.FindObjectsOfType<Weapon>())
        {
            if (w == null || w.held || w.isStaticNonWeapon || w.hasBroken || !w.gameObject.activeInHierarchy) continue;
            var d = Dist(from, w.transform.position);
            if (d < best) { best = d; found = w; }
        }
        return found;
    }

    private static WorldButton NearestButton(Vector3 from, out float best)
    {
        WorldButton found = null;
        best = float.MaxValue;
        foreach (var b in UnityEngine.Object.FindObjectsOfType<WorldButton>())
        {
            if (b == null || b.tempCantUse || !b.gameObject.activeInHierarchy) continue;
            var d = Dist(from, b.transform.position);
            if (d < best) { best = d; found = b; }
        }
        return found;
    }
}
