using Il2Cpp;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>Where the player is and which way they face. CharacterManager.player is the local player.</summary>
internal static class Game
{
    public static PTTRPlayer Player => CharacterManager.player;

    /// <summary>In a level, alive, and in control (not paused, not in a menu or cutscene).</summary>
    public static bool InPlay
    {
        get
        {
            var p = Player;
            return p != null && PTTRPlayer.CanControl && !p.IsDead() && Time.timeScale > 0f;
        }
    }

    public static Transform View
    {
        get
        {
            var p = Player;
            var cam = p != null && p.mainCamera != null ? p.mainCamera : Camera.main;
            return cam != null ? cam.transform : p?.transform;
        }
    }

    /// <summary>An offset from the player in the player's frame: metres right, ahead and up.</summary>
    public static bool Frame(Vector3 target, out float right, out float forward, out float up)
    {
        right = forward = up = 0f;
        var p = Player;
        var view = View;
        if (p == null || view == null) return false;
        var d = target - p.transform.position;
        up = d.y;
        d.y = 0f;
        var fwd = view.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = p.transform.forward;
        fwd.Normalize();
        var rgt = new Vector3(fwd.z, 0f, -fwd.x);
        right = Vector3.Dot(d, rgt);
        forward = Vector3.Dot(d, fwd);
        return true;
    }

    public static void SayHealth()
    {
        var p = Player;
        if (p == null) { Speech.Say(Strings.NotInGame); return; }
        var h = p.health;
        Log.Debug("Health", $"raw {h}");
        Speech.Say(string.Format(Strings.Health, Mathf.RoundToInt(h * 100f)));
    }
}
