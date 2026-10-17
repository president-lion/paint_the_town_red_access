using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// FPS-style arrow keys: Left/Right turn instead of strafing (A/D still strafe; Up/Down and
/// W/S still walk). Looking is SimpleMouseRotator.LateUpdate, which reads
/// InputSystem.GetAxis(8) and (9) - the gamepad look axes - as real calls (disassembled), so a
/// postfix there turns the player exactly like a gamepad stick, at the game's gamepad look
/// speed (the "gamepad look sensitivity" option scales it, and Settings.TurnSpeed on top).
/// Rewired action ids (rewired.txt): 1 Strafe, 8 LookLeftRight.
/// </summary>
[HarmonyPatch(typeof(InputSystem), nameof(InputSystem.GetAxis))]
internal static class Turning
{
    private const int Strafe = 1, LookLeftRight = 8;
    private static bool _loggedLook, _loggedArrow;
    private static int _calls;

    /// <summary>Diagnostic: on the first arrow press in play, log whether the patch has run at all.</summary>
    public static void Tick()
    {
        if (_loggedArrow || !Game.InPlay) return;
        if (!Input.GetKeyDown(KeyCode.LeftArrow) && !Input.GetKeyDown(KeyCode.RightArrow)) return;
        _loggedArrow = true;
        Log.Info("Turning", $"first arrow press: GetAxis postfix ran {_calls} times, look patched = {_loggedLook}");
    }

    private static void Postfix(int actionID, ref float __result)
    {
        _calls++;
        if (!Settings.ArrowTurning) return;
        if (actionID != Strafe && actionID != LookLeftRight) return;
        if (!PTTRPlayer.CanControl) return;

        var left = Input.GetKey(KeyCode.LeftArrow);
        var right = Input.GetKey(KeyCode.RightArrow);
        if (!left && !right) return;

        if (actionID == LookLeftRight)
        {
            __result += ((right ? 1f : 0f) - (left ? 1f : 0f)) * Settings.TurnSpeed;
            if (!_loggedLook) { _loggedLook = true; Log.Info("Turning", "look axis patched (arrow turning works)"); }
        }
        else
        {
            // Strafe without the arrows' contribution: only A and D count.
            __result = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        }
    }
}
