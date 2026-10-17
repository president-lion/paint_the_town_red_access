using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// M in a level: every world button there (the sandbox spawners and switches, the prison's
/// food slot and showers...) as a tabbed menu. Tabs by what the button's WorldButtonAction
/// does: enemies (enemy spawners, ignore player, kill each other), battle simulator (anything
/// under a *BattleSim* object, one set of spawners per side), weapons (weapon
/// spawners, unbreakable), player (invincibility, unlimited power, KO kick), other.
/// Left/Right switch tab, Up/Down move, Home/End jump, Enter or Space presses
/// (WorldButton.PressButton, no need to walk there), Escape or M closes. The game's input is
/// blocked while it is open, and until the closing key is released, by prefixes on
/// InputSystem's getters (every gameplay action goes through them, rewired.txt has the ids).
/// </summary>
internal static class ButtonMenu
{
    private const int TabEnemies = 0, TabBattle = 1, TabWeapons = 2, TabPlayer = 3, TabOther = 4;
    private static readonly string[] TabNames = { Strings.TabEnemies, Strings.TabBattle, Strings.TabWeapons, Strings.TabPlayer, Strings.TabOther };
    private static readonly List<WorldButton>[] _tabs = { new(), new(), new(), new(), new() };
    // Buttons whose labels are the same (the battle simulator has one set per side) get the
    // first part of their hierarchy path that differs added to the name.
    private static readonly Dictionary<IntPtr, string> _suffix = new();
    private static List<WorldButton> _items = _tabs[0];
    private static int _tab, _index;
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
        foreach (var t in _tabs) t.Clear();
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
            if (_pressed != null) Speech.Say(Name(_pressed), interrupt: false);
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.M)) { Close(silent: false); return; }
        if (Input.GetKeyDown(KeyCode.RightArrow)) SwitchTab(1);
        if (Input.GetKeyDown(KeyCode.LeftArrow)) SwitchTab(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow)) Move(1);
        if (Input.GetKeyDown(KeyCode.UpArrow)) Move(-1);
        if (Input.GetKeyDown(KeyCode.Home)) Move(-_index);
        if (Input.GetKeyDown(KeyCode.End)) Move(_items.Count - 1 - _index);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) Press();
    }

    private static void Show()
    {
        foreach (var t in _tabs) t.Clear();
        var total = 0;
        foreach (var b in UnityEngine.Object.FindObjectsOfType<WorldButton>())
        {
            if (b == null || !b.gameObject.activeInHierarchy) continue;
            _tabs[TabOf(b)].Add(b);
            total++;
        }
        if (total == 0) { Speech.Say(Strings.NoButtons); return; }
        Disambiguate();
        foreach (var t in _tabs)
            t.Sort((a, b) => string.Compare(Name(a), Name(b), StringComparison.OrdinalIgnoreCase));
        _tab = Array.FindIndex(_tabs, t => t.Count > 0);
        _items = _tabs[_tab];
        _index = 0;
        Open = true;
        Speech.Say(Strings.ButtonMenuOpen + " " + TabLine() + " " + Line());
        for (var i = 0; i < _tabs.Length; i++)
            Log.Info("ButtonMenu", $"{TabNames[i]}: " + string.Join("; ", _tabs[i].Select(b => $"{UiDump.PathOf(b.transform)}='{Name(b)}'")));
    }

    private static string Name(WorldButton b)
    {
        var name = Scanner.ButtonName(b);
        return _suffix.TryGetValue(b.Pointer, out var s) ? $"{name}, {s}" : name;
    }

    private static void Disambiguate()
    {
        _suffix.Clear();
        var all = _tabs.SelectMany(t => t).ToList();
        foreach (var group in all.GroupBy(b => Scanner.ButtonName(b)).Where(g => g.Count() > 1))
        {
            var buttons = group.ToList();
            var paths = buttons.Select(b => Path(b.transform)).ToList();
            for (var i = 0; i < buttons.Count; i++)
            {
                // The first ancestor (from the top) whose name is not shared by every button in the group.
                var mine = paths[i];
                string differ = null;
                for (var d = 0; d < mine.Count && differ == null; d++)
                    if (paths.Any(other => d >= other.Count || other[d] != mine[d])) differ = mine[d];
                _suffix[buttons[i].Pointer] = differ != null ? TextClean.ThingName(differ) : (i + 1).ToString();
            }
        }
    }

    private static List<string> Path(Transform t)
    {
        var parts = new List<string>();
        for (; t != null; t = t.parent) parts.Add(t.name);
        parts.Reverse();
        return parts;
    }

    /// <summary>Which tab a button belongs to, from the type of its action.</summary>
    private static int TabOf(WorldButton b)
    {
        for (var t = b.transform; t != null; t = t.parent)
            if (t.name.Contains("BattleSim")) return TabBattle;
        var a = b.buttonAction;
        if (a == null) return TabOther;
        if (a.TryCast<WorldButtonSpawner>() is { } spawner)
        {
            var enemies = spawner.numEnemiesToSpawn > 0 || (spawner.enemyTypes2 != null && spawner.enemyTypes2.Length > 0);
            return enemies ? TabEnemies : TabWeapons;
        }
        if (a.TryCast<WorldButtonEnemiesIgnorePlayer>() != null || a.TryCast<WorldButtonEnemiesKillEachother>() != null) return TabEnemies;
        if (a.TryCast<WorldButtonUnbreakableWeapons>() != null) return TabWeapons;
        if (a.TryCast<WorldButtonInvincibility>() != null || a.TryCast<WorldButtonUnlimitedPower>() != null
            || a.TryCast<WorldButtonAlwaysKOKick>() != null) return TabPlayer;
        return TabOther;
    }

    private static void SwitchTab(int step)
    {
        var start = _tab;
        do { _tab = (_tab + step + _tabs.Length) % _tabs.Length; }
        while (_tabs[_tab].Count == 0 && _tab != start);
        _items = _tabs[_tab];
        _items.RemoveAll(b => b == null);
        _index = 0;
        if (_items.Count == 0) { Close(silent: true); return; }
        Speech.Say(TabLine() + " " + Line());
    }

    private static string TabLine() => string.Format(Strings.ButtonTab, TabNames[_tab], _items.Count);

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
        $"{Name(_items[_index])}, {string.Format(Strings.Position, _index + 1, _items.Count)}";

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
        if (_sayStateAt < 0f) Speech.Say(string.Format(Strings.Pressed, Name(b)));
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
