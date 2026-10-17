using MelonLoader;

namespace PaintTheTownRedAccess;

/// <summary>User settings, stored by MelonLoader in UserData\MelonPreferences.cfg.</summary>
internal static class Settings
{
    private static MelonPreferences_Entry<string> _backend;
    private static MelonPreferences_Entry<bool> _debugLog;
    private static MelonPreferences_Entry<bool> _runInBackground;
    private static MelonPreferences_Entry<bool> _radar;
    private static MelonPreferences_Entry<float> _radarRange;
    private static MelonPreferences_Entry<bool> _arrowTurning;
    private static MelonPreferences_Entry<float> _turnSpeed;
    private static MelonPreferences_Entry<float> _cueVolume;

    public static string Backend => _backend?.Value ?? "auto";
    public static bool DebugLog => _debugLog?.Value ?? false;
    public static bool RunInBackground => _runInBackground?.Value ?? false;
    public static float RadarRange => Math.Clamp(_radarRange?.Value ?? 20f, 3f, 100f);
    public static bool ArrowTurning => _arrowTurning?.Value ?? true;
    public static float TurnSpeed => Math.Clamp(_turnSpeed?.Value ?? 1f, 0.1f, 5f);
    public static float CueVolume => Math.Clamp(_cueVolume?.Value ?? 0.8f, 0f, 1f);
    public static bool PickupAssist => _pickupAssist?.Value ?? true;
    public static float Reach => Math.Clamp(_reach?.Value ?? 1.8f, 0.5f, 6f);
    public static bool HitBeep => _hitBeep?.Value ?? true;
    public static bool DangerWarning => _danger?.Value ?? true;
    public static bool KeyRemap => _keyRemap?.Value ?? true;
    private static MelonPreferences_Entry<bool> _danger, _keyRemap;
    private static MelonPreferences_Entry<bool> _hitBeep;
    private static MelonPreferences_Entry<float> _reach;
    private static MelonPreferences_Entry<bool> _pickupAssist;

    public static bool Radar
    {
        get => _radar?.Value ?? true;
        set { if (_radar != null) _radar.Value = value; }
    }

    public static void Init()
    {
        var cat = MelonPreferences.CreateCategory("PaintTheTownRedAccess", "Paint the Town Red Access");
        _backend = cat.CreateEntry("Backend", "auto", "Speech backend",
            "Prism backend name (nvda, jaws, sapi, onecore, ...) or auto");
        _radar = cat.CreateEntry("EnemySounds", true, "Enemy sounds",
            "A positional sound for each nearby hostile enemy, and a warning when one attacks you");
        _radarRange = cat.CreateEntry("EnemySoundRange", 20f, "Enemy sound range",
            "How far away, in metres, enemies are heard");
        _arrowTurning = cat.CreateEntry("ArrowTurning", true, "Arrow keys turn",
            "Left and right arrows turn instead of strafing (A and D still strafe)");
        _turnSpeed = cat.CreateEntry("TurnSpeed", 1f, "Turn speed",
            "Multiplies the game's gamepad turn speed for the arrow keys");
        _cueVolume = cat.CreateEntry("CueVolume", 0.8f, "Mod sound volume", "0 to 1");
        _danger = cat.CreateEntry("DangerWarning", true, "Water and drop warning",
            "A crackle from the direction of deadly water or a big drop near you");
        _keyRemap = cat.CreateEntry("KeyRemap", true, "Keyboard attack and block",
            "G attacks, T blocks, X drops (instead of G)");
        _hitBeep = cat.CreateEntry("HitBeep", true, "Swing-would-hit beep",
            "A quiet beep every 100 ms while a swing straight ahead would hit someone");
        _reach = cat.CreateEntry("Reach", 1.8f, "Swing distance",
            "Metres ahead from your eyes that a swing reaches, for the hit beep");
        _pickupAssist = cat.CreateEntry("PickupAssist", true, "Pick up without aiming",
            "If E picks nothing up, take the nearest weapon within reach");
        _debugLog = cat.CreateEntry("DebugLog", true, "Verbose logging");
        _runInBackground = cat.CreateEntry("RunInBackground", false, "Keep running without focus",
            "Development aid: the game normally pauses when its window loses focus");
    }
}
