using MelonLoader;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(PaintTheTownRedAccess.Mod), "Paint the Town Red Access", "0.1.0", "sunduijav")]
[assembly: MelonGame("South East Games", "Paint The Town Red")]

namespace PaintTheTownRedAccess;

/// <summary>Entry point: starts speech and runs the per-frame tick.</summary>
public sealed class Mod : MelonMod
{
    private float _nextHeartbeat;

    public override void OnInitializeMelon()
    {
        Log.Init(LoggerInstance);
        Settings.Init();
        Speech.Preload(MelonEnvironment.UserLibsDirectory);
        Speech.Init(Settings.Backend);
        Speech.Say(Strings.Loaded);
    }

    public override void OnLateInitializeMelon()
    {
        if (Settings.RunInBackground) UnityEngine.Application.runInBackground = true;
    }

    public override void OnUpdate()
    {
        if (Settings.DebugLog && UnityEngine.Time.unscaledTime >= _nextHeartbeat)
        {
            _nextHeartbeat = UnityEngine.Time.unscaledTime + 3f;
            try { Heartbeat(); } catch (Exception ex) { Log.Once("Heartbeat", ex); }
        }

        try { Hotkeys.Tick(); }
        catch (Exception ex) { Log.Once("Hotkeys", ex); }

        try { RewiredDump.Tick(); }
        catch (Exception ex) { Log.Once("RewiredDump", ex); }

        try { MenuReader.Tick(); }
        catch (Exception ex) { Log.Once("MenuReader", ex); }

        try { ScreenWatch.Tick(); }
        catch (Exception ex) { Log.Once("ScreenWatch", ex); }
    }

    /// <summary>Diagnostic: proves the tick runs and shows the scene and UI selection.</summary>
    private static void Heartbeat()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        var sel = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        Log.Info("Beat", $"t={UnityEngine.Time.unscaledTime:0} scene={scene} selected={(sel != null ? UiDump.PathOf(sel.transform) : "<none>")}");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        Log.Info("Scene", $"loaded {buildIndex} {sceneName}");
        try { MenuReader.OnSceneLoaded(); ScreenWatch.OnSceneLoaded(); }
        catch (Exception ex) { Log.Once("SceneLoaded", ex); }
    }

    public override void OnDeinitializeMelon() => Speech.Shutdown();
}
