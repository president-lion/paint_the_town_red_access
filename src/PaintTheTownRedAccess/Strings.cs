namespace PaintTheTownRedAccess;

/// <summary>
/// Every piece of wording the mod speaks on its own behalf, in English. Game text is read
/// as the game shows it.
/// </summary>
internal static class Strings
{
    public const string Loaded = "Paint the Town Red Access loaded";
    public const string UiDumpWritten = "Screen dump written";
    public const string InputDumpWritten = "Input dump written";

    public const string Position = "{0} of {1}";
    public const string RoleSelector = "selector";
    public const string RoleSlider = "slider";
    public const string RoleDropdown = "dropdown";
    public const string RoleEdit = "edit";
    public const string RoleScrollbar = "scrollbar";
    public const string Checked = "checked";
    public const string NotChecked = "not checked";
    public const string Tab = "{0} tab, {1}";
    public const string Loading = "loading {0}";
    public const string By = "{0}, by {1}";
    public const string Cost = "cost {0}";
    public const string Owned = "owned {0}";
    public const string Unavailable = "unavailable";
    public const string Paused = "paused";
    public const string NoTabs = "no tabs here";

    // Directions relative to where the player faces, by Spatial.Sector.
    public static readonly string[] Directions =
        { "ahead", "ahead right", "right", "behind right", "behind", "behind left", "left", "ahead left" };
    public const string Close = "close";
    public const string Metre = "{0} metre";
    public const string Metres = "{0} metres";
    public const string Above = "above";
    public const string Below = "below";

    public const string Health = "health {0}";
    public const string NotInGame = "not in a level";

    // Scanner (Stardew Access / Setsuna layout).
    public const string CatAll = "everything";
    public const string CatEnemies = "enemies";
    public const string CatWeapons = "weapons";
    public const string CatItems = "items";
    public const string CatExits = "exits and doors";
    public const string CatButtons = "buttons";
    public const string Button = "button";
    public const string On = "on";
    public const string Off = "off";
    public const string Pressed = "pressed {0}";
    public const string NoButtons = "no buttons here";
    public const string ButtonMenuOpen = "buttons menu.";
    public const string MenuClosed = "closed";
    public const string ButtonTab = "{0}, {1} buttons.";
    public const string TabEnemies = "enemies";
    public const string TabWeapons = "weapons";
    public const string TabBattle = "battle simulator";
    public const string NoEnemies = "no enemies";
    public const string TabPlayer = "player";
    public const string TabOther = "other";
    public const string CategoryCount = "{0}, {1}";
    public const string Count0 = "none";
    public const string Count1 = "1 thing";
    public const string CountN = "{0} things";
    public const string NothingNearby = "no {0} nearby";
    public const string ScanItem = "{0}, {1} of {2}";
    public const string NothingSelected = "nothing selected";
    public const string Gone = "{0} is gone";
    public const string BeaconOn = "beacon on";
    public const string BeaconOff = "beacon off";
    public const string Arrived = "arrived at {0}";
    public const string Angry = "angry";
    public const string Neutral = "neutral";
    public const string Friendly = "friendly";
    public const string Down = "down";
    public const string PickedUp = "picked up {0}";
    public const string NothingInReach = "nothing in reach";
    public const string Exit = "exit";
    public const string Person = "person";
    public const string ExitDoor = "exit door";
    public const string Door = "door";
    public const string EnemiesLeft = "{0} enemies left";
    public const string RadarOn = "enemy sounds on";
    public const string RadarOff = "enemy sounds off";
    public const string SeekOff = "weapon guide off";
    public const string NoWeapons = "no loose weapons";
    public const string Traps1 = "1 trap on the floor";
    public const string TrapsN = "{0} traps on the floor";
}
