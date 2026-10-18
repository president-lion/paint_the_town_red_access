using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// A thud when you walk into something. While a movement key is held (W/S/Up/Down to walk,
/// A/D to strafe - Left/Right only turn) and the player barely moves (under 0.4 m/s for a
/// quarter of a second), the Bump cue plays from the side you are pushing toward, every
/// half second until you move again or let go.
/// </summary>
internal static class WallBump
{
    private const float MinSpeed = 0.4f;
    private const float BlockedFor = 0.25f;
    private const float Repeat = 0.5f;

    private static Vector3 _lastPos;
    private static bool _havePos;
    private static float _blocked, _nextBump;
    private static int _logged;

    public static void OnSceneLoaded() { _havePos = false; _blocked = 0f; _logged = 0; }

    public static void Tick()
    {
        if (!Game.InPlay || ButtonMenu.BlockingInput) { _havePos = false; return; }
        var p = Game.Player;
        var pos = p.transform.position;
        var dt = Time.deltaTime;
        var moved = _havePos ? new Vector2(pos.x - _lastPos.x, pos.z - _lastPos.z).magnitude : float.MaxValue;
        _lastPos = pos;
        _havePos = true;
        if (dt <= 0f) return;

        float forward = 0f, right = 0f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) forward += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) forward -= 1f;
        if (Input.GetKey(KeyCode.D)) right += 1f;
        if (Input.GetKey(KeyCode.A)) right -= 1f;
        if (forward == 0f && right == 0f) { _blocked = 0f; return; }

        _blocked = moved / dt < MinSpeed ? _blocked + dt : 0f;
        if (_blocked < BlockedFor) return;
        var now = Time.unscaledTime;
        if (now < _nextBump) return;
        _nextBump = now + Repeat;
        Cues.Play(Cues.Cue.Bump, Spatial.Place(right, forward, 0f));
        if (_logged++ < 10) Log.Info("Bump", $"blocked at {pos} pushing {(forward > 0 ? "forward" : forward < 0 ? "back" : "")}{(right > 0 ? " right" : right < 0 ? " left" : "")}");
    }
}
