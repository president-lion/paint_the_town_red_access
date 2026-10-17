using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// Keyboard attack and block (user's request): G attacks, T blocks, and drop moves from G to X
/// so G does not also drop your weapon. The mouse buttons keep working. Every gameplay
/// action is read through InputSystem.GetButton / GetButtonDown / GetButtonUp /
/// GetButtonTimePressed (action ids in rewired.txt: 2 Attack, 4 Drop, 7 Block), so postfixes
/// there add the keys. Off with Settings.KeyRemap.
/// </summary>
internal static class KeyRemap
{
    private const int Attack = 2, Drop = 4, Block = 7;

    private static readonly Dictionary<int, KeyCode> Added = new()
    {
        [Attack] = KeyCode.G,
        [Block] = KeyCode.T,
        [Drop] = KeyCode.X,
    };

    private static readonly Dictionary<KeyCode, float> _downAt = new();

    private static bool Active => Settings.KeyRemap && PTTRPlayer.CanControl && !ButtonMenu.BlockingInput;

    /// <summary>Records when the added keys went down, for GetButtonTimePressed.</summary>
    public static void Tick()
    {
        foreach (var key in Added.Values)
        {
            if (Input.GetKeyDown(key)) _downAt[key] = Time.time;
            else if (!Input.GetKey(key)) _downAt.Remove(key);
        }
    }

    [HarmonyPatch(typeof(InputSystem), nameof(InputSystem.GetButton))]
    private static class Held
    {
        private static void Postfix(int actionID, ref bool __result)
        {
            if (!Active) return;
            if (actionID == Drop && Input.GetKey(KeyCode.G)) __result = false;
            if (Added.TryGetValue(actionID, out var key) && Input.GetKey(key)) __result = true;
        }
    }

    [HarmonyPatch(typeof(InputSystem), nameof(InputSystem.GetButtonDown))]
    private static class Down
    {
        private static void Postfix(int actionID, ref bool __result)
        {
            if (!Active) return;
            if (actionID == Drop && Input.GetKeyDown(KeyCode.G)) __result = false;
            if (Added.TryGetValue(actionID, out var key) && Input.GetKeyDown(key)) __result = true;
        }
    }

    [HarmonyPatch(typeof(InputSystem), nameof(InputSystem.GetButtonUp))]
    private static class Up
    {
        private static void Postfix(int actionID, ref bool __result)
        {
            if (!Active) return;
            if (actionID == Drop && Input.GetKeyUp(KeyCode.G)) __result = false;
            if (Added.TryGetValue(actionID, out var key) && Input.GetKeyUp(key)) __result = true;
        }
    }

    [HarmonyPatch(typeof(InputSystem), nameof(InputSystem.GetButtonTimePressed))]
    private static class TimePressed
    {
        private static void Postfix(int actionID, ref float __result)
        {
            if (!Active || __result > 0f) return;
            if (Added.TryGetValue(actionID, out var key) && _downAt.TryGetValue(key, out var at))
                __result = Time.time - at;
        }
    }
}
