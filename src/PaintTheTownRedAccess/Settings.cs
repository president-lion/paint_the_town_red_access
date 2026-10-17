using MelonLoader;

namespace PaintTheTownRedAccess;

/// <summary>User settings, stored by MelonLoader in UserData\MelonPreferences.cfg.</summary>
internal static class Settings
{
    private static MelonPreferences_Entry<string> _backend;
    private static MelonPreferences_Entry<bool> _debugLog;
    private static MelonPreferences_Entry<bool> _runInBackground;

    public static string Backend => _backend?.Value ?? "auto";
    public static bool DebugLog => _debugLog?.Value ?? false;
    public static bool RunInBackground => _runInBackground?.Value ?? false;

    public static void Init()
    {
        var cat = MelonPreferences.CreateCategory("PaintTheTownRedAccess", "Paint the Town Red Access");
        _backend = cat.CreateEntry("Backend", "auto", "Speech backend",
            "Prism backend name (nvda, jaws, sapi, onecore, ...) or auto");
        _debugLog = cat.CreateEntry("DebugLog", true, "Verbose logging");
        _runInBackground = cat.CreateEntry("RunInBackground", false, "Keep running without focus",
            "Development aid: the game normally pauses when its window loses focus");
    }
}
