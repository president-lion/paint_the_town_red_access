using Il2Cpp;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// The object scanner, on the Setsuna / Stardew Access keys:
///   PageUp / PageDown        previous / next thing, nearest first
///   Ctrl+PageUp / PageDown   previous / next category (everything, enemies, weapons, items, exits, buttons)
///   Home                     where the selected thing is now
///   End                      beacon on/off: a sound from the selected thing until you reach it
/// Directions are relative to where you face ("ahead left"), distances in metres.
/// Every scan is written to scan.log with the raw object names, so names can be improved.
/// </summary>
internal static class Scanner
{
    private enum Kind { Enemy, Weapon, Item, Exit, Button }

    private sealed class Target
    {
        public Kind Kind;
        public Transform T;
        public string Name;
        public float Dist;
    }

    // Category 0 = everything, then one per Kind.
    private static readonly string[] Categories =
        { Strings.CatAll, Strings.CatEnemies, Strings.CatWeapons, Strings.CatItems, Strings.CatExits, Strings.CatButtons };

    private const float MaxRange = 150f;
    private const float ArriveDistance = 1.5f;

    private static int _category;
    private static Transform _selected;
    private static string _selectedName;
    private static bool _beacon;
    private static float _nextBeacon;

    public static void OnSceneLoaded()
    {
        _selected = null;
        _beacon = false;
        PathGuide.Reset();
    }

    public static void NextCategory(int step)
    {
        if (!Game.InPlay) { Speech.Say(Strings.NotInGame); return; }
        _category = (_category + step + Categories.Length) % Categories.Length;
        var n = Scan().Count;
        var count = n == 0 ? Strings.Count0 : n == 1 ? Strings.Count1 : string.Format(Strings.CountN, n);
        Speech.Say(string.Format(Strings.CategoryCount, Categories[_category], count));
    }

    public static void Cycle(int step)
    {
        if (!Game.InPlay) { Speech.Say(Strings.NotInGame); return; }
        var list = Scan();
        if (list.Count == 0) { Speech.Say(string.Format(Strings.NothingNearby, Categories[_category])); return; }
        // Keep the same thing selected even when the distance order changed.
        var cur = _selected == null ? -1 : list.FindIndex(t => t.T == _selected);
        var index = cur < 0 ? (step > 0 ? 0 : list.Count - 1) : (cur + step + list.Count) % list.Count;
        var target = list[index];
        _selected = target.T;
        _selectedName = target.Name;
        Speech.Say(string.Format(Strings.ScanItem, Describe(target.Name, target.T), index + 1, list.Count));
    }

    public static void SaySelected()
    {
        if (_selected == null) { Speech.Say(Strings.NothingSelected); return; }
        if (!_selected.gameObject.activeInHierarchy) { Speech.Say(string.Format(Strings.Gone, _selectedName)); return; }
        Speech.Say(Describe(_selectedName, _selected));
    }

    public static void StopBeacon() => _beacon = false;

    public static void ToggleBeacon()
    {
        if (!_beacon && _selected == null) { Speech.Say(Strings.NothingSelected); return; }
        _beacon = !_beacon;
        if (_beacon) WeaponSeek.Stop();
        PathGuide.Reset();
        Speech.Say(_beacon ? Strings.BeaconOn : Strings.BeaconOff);
    }

    /// <summary>Beacon pings, faster as you get closer; stops with a chime on arrival.</summary>
    public static void Tick()
    {
        if (!_beacon || !Game.InPlay) return;
        if (_selected == null || !_selected.gameObject.activeInHierarchy)
        {
            _beacon = false;
            Speech.Say(string.Format(Strings.Gone, _selectedName));
            return;
        }
        if (!Game.Frame(_selected.position, out var tr, out var tf, out _)) return;
        var dist = Mathf.Sqrt(tr * tr + tf * tf);
        if (dist < ArriveDistance)
        {
            _beacon = false;
            Cues.Play2D(Cues.Cue.Arrive);
            Speech.Say(string.Format(Strings.Arrived, _selectedName));
            return;
        }
        var now = Time.unscaledTime;
        if (now < _nextBeacon) return;
        // Follow the walkable route (round walls, through doorways), not the straight line.
        var steer = PathGuide.Steer(Game.Player.transform.position, _selected.position);
        if (!Game.Frame(steer, out var r, out var f, out _)) return;
        var route = PathGuide.Remaining > 0 ? PathGuide.Remaining : dist;
        _nextBeacon = now + Spatial.PingInterval(route, 30f);
        Cues.Play(Cues.Cue.Beacon, Spatial.Place(r, f, 40f));
    }

    private static string Describe(string name, Transform t)
    {
        if (!Game.Frame(t.position, out var r, out var f, out var up)) return name;
        var dist = Mathf.Sqrt(r * r + f * f);
        var line = $"{name}, {Spatial.Direction(r, f)}, {Spatial.Distance(dist)}";
        var h = Spatial.Height(up);
        return h.Length > 0 ? $"{line}, {h}" : line;
    }

    // ---- what is around -------------------------------------------------------------------

    private static List<Target> Scan()
    {
        var list = new List<Target>();
        var want = _category == 0 ? (Kind?)null : (Kind)(_category - 1);

        if (want is null or Kind.Enemy && CharacterManager.enemies is { } enemies)
            for (var i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || !e.isActiveAndEnabled || e.IsDead()) continue;
                Add(list, Kind.Enemy, e.transform, EnemyName(e), e.gameObject.name);
            }

        if (want is null or Kind.Weapon or Kind.Item)
            foreach (var w in UnityEngine.Object.FindObjectsOfType<Weapon>())
            {
                if (w == null || w.held || w.isStaticNonWeapon || w.hasBroken || !w.gameObject.activeInHierarchy) continue;
                var isItem = w.TryCast<BeneathItemPickup>() != null;
                if (want == (isItem ? Kind.Weapon : Kind.Item)) continue;
                Add(list, isItem ? Kind.Item : Kind.Weapon, w.transform, TextClean.ThingName(w.gameObject.name), w.gameObject.name);
            }

        if (want is null or Kind.Item)
            foreach (var p in UnityEngine.Object.FindObjectsOfType<Pickup>())
                if (p != null && p.gameObject.activeInHierarchy)
                    Add(list, Kind.Item, p.transform, TextClean.ThingName(p.gameObject.name), p.gameObject.name);

        if (want is null or Kind.Exit)
        {
            foreach (var x in UnityEngine.Object.FindObjectsOfType<BeneathLevelExit>())
                if (x != null && x.gameObject.activeInHierarchy) Add(list, Kind.Exit, x.transform, Strings.Exit, x.gameObject.name);
            foreach (var x in UnityEngine.Object.FindObjectsOfType<BeneathExitDoor>())
                if (x != null && x.gameObject.activeInHierarchy) Add(list, Kind.Exit, x.transform, Strings.ExitDoor, x.gameObject.name);
            foreach (var x in UnityEngine.Object.FindObjectsOfType<Door>())
                if (x != null && x.gameObject.activeInHierarchy) Add(list, Kind.Exit, x.transform, Strings.Door, x.gameObject.name);
            foreach (var x in UnityEngine.Object.FindObjectsOfType<DoorController>())
                if (x != null && x.gameObject.activeInHierarchy) Add(list, Kind.Exit, x.transform, Strings.Door, x.gameObject.name);
        }

        if (want is null or Kind.Button)
            foreach (var b in UnityEngine.Object.FindObjectsOfType<WorldButton>())
                if (b != null && b.gameObject.activeInHierarchy)
                    Add(list, Kind.Button, b.transform, ButtonName(b), b.gameObject.name);

        list.Sort((a, b) => a.Dist.CompareTo(b.Dist));
        Number(list);
        Log.Append("scan.log", $"[{Categories[_category]}] {list.Count}: " +
                               string.Join("; ", list.Take(40).Select(t => $"{t.Name} {t.Dist:0.0}m")));
        return list;
    }

    private static void Add(List<Target> list, Kind kind, Transform t, string name, string raw)
    {
        if (!Game.Frame(t.position, out var r, out var f, out var up)) return;
        var dist = Mathf.Sqrt(r * r + f * f);
        if (dist > MaxRange) return;
        list.Add(new Target { Kind = kind, T = t, Name = name, Dist = dist });
        if (Settings.DebugLog) Log.Append("scan.log", $"  {kind} '{raw}' -> '{name}' {dist:0.0}m up {up:0.0}");
    }

    /// <summary>Two beer bottles become "beer bottle" and "beer bottle 2" (nearest first).</summary>
    private static void Number(List<Target> list)
    {
        var seen = new Dictionary<string, int>();
        foreach (var t in list)
        {
            seen.TryGetValue(t.Name, out var n);
            seen[t.Name] = ++n;
            if (n > 1) t.Name = $"{t.Name} {n}";
        }
    }

    private static readonly HashSet<string> GenericNames = new(StringComparer.OrdinalIgnoreCase)
        { "button", "box", "object", "label", "button pedestal", "pedestal", "dynamic" };

    /// <summary>A world button (sandbox spawners and switches, prison food slot, showers...):
    /// its label, and on/off for buttons that stay pressed.</summary>
    public static string ButtonName(WorldButton b)
    {
        var label = b.label;
        if (string.IsNullOrWhiteSpace(label)) label = b.interactUIText;
        if (b.attemptTranslation) label = UiText.Translate(label);
        var name = TextClean.Clean(label);
        // Some buttons keep the prefab's placeholder text "Label" (Pirate Cove's cannons): name them
        // after the nearest ancestor with a meaningful name (Cannon/Cannon/Box380/Button -> cannon).
        if (string.IsNullOrWhiteSpace(name) || name.Equals("label", StringComparison.OrdinalIgnoreCase))
        {
            name = null;
            for (var t = b.transform; t != null && name == null; t = t.parent)
            {
                var n = TextClean.ThingName(t.name);
                var key = System.Text.RegularExpressions.Regex.Replace(n, @"\d+$", "").Trim();
                if (!GenericNames.Contains(key)) name = n;
            }
        }
        if (string.IsNullOrWhiteSpace(name)) name = Strings.Button;
        if (b.stayPressed || b.canUnpress) name += ", " + (b.pressed ? Strings.On : Strings.Off);
        return name;
    }

    private static string EnemyName(Enemy e)
    {
        var name = EnemyRadar.TypeName(e);
        if (e.IsDown()) name += ", " + Strings.Down;
        var rel = e.playerRelation;
        if (!EnemyRadar.Hostile(e))
            name += ", " + (rel == Enemy.PlayerRelation.Neutral ? Strings.Neutral : Strings.Friendly);
        return name;
    }
}
