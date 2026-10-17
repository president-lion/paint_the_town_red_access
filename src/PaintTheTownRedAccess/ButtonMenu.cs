using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// M in a level: every world button there (the sandbox spawners and switches, the prison's
/// food slot and showers...) as a menu. Up/Down move, Home/End jump, Enter or Space presses
/// (WorldButton.PressButton, no need to walk there), Escape or M closes. The game's input is
/// blocked while it is open, and until the closing key is released, by prefixes on
/// InputSystem's getters (every gameplay action goes through them, rewired.txt has the ids).
/// </summary>
internal static class ButtonMenu
{
    private static readonly List<WorldButton> _items = new();
    private static int _index;
    private static WorldButton _pressed;
    private static float _sayStateAt = -1f;

    public static bool Open { get; private set; }

    /// <summary>True while open and until every key that could close it is released.</summary>
    public static bool BlockingInput
    {
        get
        {
            if (Open) return true;
            if (Time.unscaledTime - _closedAt > 1f) return false;
            // Just closed: keep blocking while the closing key is still down, so Escape does not
            // also pause the game and Enter does not also swing.
            return Input.GetKey(KeyCode.Escape) || Input.GetKey(KeyCode.M) || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.Space);
        }
    }

    private static float _closedAt = -10f;

    public static void OnSceneLoaded()
    {
        Open = false;
        _items.Clear();
    }

    public static void Tick()
    {
        if (!Open)
        {
            if (Input.GetKeyDown(KeyCode.M) && Game.InPlay) Show();
            return;
        }

        if (!Game.InPlay) { Close(silent: true); return; }

        if (_sayStateAt >= 0f && Time.unscaledTime >= _sayStateAt)
        {
            _sayStateAt = -1f;
            if (_pressed != null) Speech.Say(Scanner.ButtonName(_pressed), interrupt: false);
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.M)) { Close(silent: false); return; }
        if (Input.GetKeyDown(KeyCode.DownArrow)) Move(1);
        if (Input.GetKeyDown(KeyCode.UpArrow)) Move(-1);
        if (Input.GetKeyDown(KeyCode.Home)) Move(-_index);
        if (Input.GetKeyDown(KeyCode.End)) Move(_items.Count - 1 - _index);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) Press();
    }

    private static void Show()
    {
        _items.Clear();
        foreach (var b in UnityEngine.Object.FindObjectsOfType<WorldButton>())
            if (b != null && b.gameObject.activeInHierarchy) _items.Add(b);
        if (_items.Count == 0) { Speech.Say(Strings.NoButtons); return; }
        _items.Sort((a, b) => string.Compare(Scanner.ButtonName(a), Scanner.ButtonName(b), StringComparison.OrdinalIgnoreCase));
        _index = 0;
        Open = true;
        Speech.Say(string.Format(Strings.ButtonMenuOpen, _items.Count) + " " + Line());
        Log.Info("ButtonMenu", "open: " + string.Join("; ", _items.Select(b => $"{b.gameObject.name}='{Scanner.ButtonName(b)}'")));
    }

    private static void Close(bool silent)
    {
        Open = false;
        _closedAt = Time.unscaledTime;
        if (!silent) Speech.Say(Strings.MenuClosed);
    }

    private static void Move(int step)
    {
        _items.RemoveAll(b => b == null);
        if (_items.Count == 0) { Close(silent: true); return; }
        _index = Math.Clamp(_index + step, 0, _items.Count - 1);
        Speech.Say(Line());
    }

    private static string Line() =>
        $"{Scanner.ButtonName(_items[_index])}, {string.Format(Strings.Position, _index + 1, _items.Count)}";

    private static void Press()
    {
        var b = _index < _items.Count ? _items[_index] : null;
        if (b == null) return;
        if (b.tempCantUse) { Speech.Say(Strings.Unavailable); return; }
        b.PressButton();
        Log.Info("ButtonMenu", $"pressed {b.gameObject.name}");
        Cues.Play2D(Cues.Cue.Arrive, 0.6f);
        // Switches change state over a few frames; say the new state once it has.
        _pressed = b;
        _sayStateAt = b.stayPressed || b.canUnpress ? Time.unscaledTime + 0.3f : -1f;
        if (_sayStateAt < 0f) Speech.Say(string.Format(Strings.Pressed, Scanner.ButtonName(b)));
    }

    // ---- game input is blocked while the menu is open --------------------------------------

    [HarmonyPatch(typeof(InputSystem), nameof(InputSystem.GetButtonDown))]
    private static class BlockDown { private static bool Prefix(ref bool __result) { if (!BlockingInput) return true; __result = false; return false; } }

    [HarmonyPatch(typeof(InputSystem), nameof(InputSystem.GetButton))]
    private static class BlockHeld { private static bool Prefix(ref bool __result) { if (!BlockingInput) return true; __result = false; return false; } }

    [HarmonyPatch(typeof(InputSystem), nameof(InputSystem.GetButtonUp))]
    private static class BlockUp { private static bool Prefix(ref bool __result) { if (!BlockingInput) return true; __result = false; return false; } }

    [HarmonyPatch(typeof(InputSystem), nameof(InputSystem.GetAxis))]
    private static class BlockAxis { private static bool Prefix(ref float __result) { if (!BlockingInput) return true; __result = 0f; return false; } }
}
