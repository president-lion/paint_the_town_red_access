using Il2Cpp;
using Il2CppTMPro;

namespace PaintTheTownRedAccess;

/// <summary>
/// Speaks screen text that is not attached to a menu item, when it appears or changes:
/// the loading screen's level name, the win / lose / arena banner, the Beneath area
/// messages (area name, challenge, pursuer, shard, find the exit), the Beneath death screen
/// and the Beneath save-management popup. Polled a few times a second; each text is spoken
/// once per appearance (hiding it re-arms it).
/// </summary>
internal static class ScreenWatch
{
    private static readonly Dictionary<string, string> _last = new();
    private static float _nextPoll, _nextFind;
    private static WinLoseUI _winLose;

    public static void OnSceneLoaded()
    {
        _winLose = null;
        _nextFind = 0;
    }

    public static void Tick()
    {
        var now = UnityEngine.Time.unscaledTime;
        if (now < _nextPoll) return;
        _nextPoll = now + 0.2f;

        if (_winLose == null && now >= _nextFind)
        {
            _nextFind = now + 2f;
            _winLose = UnityEngine.Object.FindObjectOfType<WinLoseUI>();
        }

        var loading = LoadingScreen.Instance;
        if (loading != null && loading.levelNameTexts != null)
        {
            TMP_Text shown = null;
            foreach (var t in loading.levelNameTexts) if (UiText.Showing(t)) { shown = t; break; }
            Watch("loading", shown, Strings.Loading);
        }

        if (_winLose != null)
        {
            Watch("win", _winLose.winText);
            Watch("lose", _winLose.loseText);
            Watch("arena", _winLose.arenaText);
        }

        var area = BeneathAreaUI.Instance;
        if (area != null)
        {
            Watch("area", area.areaTextTMP);
            Watch("areaInfo", area.areaInfoTMP);
            Watch("areaInfo2", area.areaInfo2TMP);
            Watch("areaInfo3", area.areaInfo3TMP);
            Watch("challenge", area.challengeInfoTMP);
            Watch("pursuer", area.pursuerInfoTMP);
            Watch("pursuer2", area.pursuerInfo2TMP);
            Watch("trickster", area.tricksterPursuerInfoTMP);
            Watch("shard", area.shardAcquiredInfoTMP);
            Watch("findExit", area.findExitTMP);
        }

        var death = BeneathDeathUI.Instance;
        var dead = death != null && death.showing;
        Watch("deathHeader", dead ? death.header : null);
        Watch("killedBy", dead ? death.killedBy : null);
        Watch("energyLost", dead ? death.energyLost : null);
        Watch("shardsGained", dead ? death.shardsGained : null);

        var save = BeneathSaveManagementPopup._Instance;
        Watch("saveState", BeneathSaveManagementPopup.IsShowing && save != null ? save.saveStateText : null);
    }

    private static void Watch(string key, TMP_Text t, string format = null)
    {
        var text = UiText.Showing(t) ? TextClean.Clean(t.text) : "";
        _last.TryGetValue(key, out var was);
        if (text == (was ?? "")) return;
        _last[key] = text;
        if (text.Length == 0) return;
        Speech.Say(format == null ? text : string.Format(format, text), interrupt: false);
        Log.Debug("Screen", $"{key}: {text}");
    }
}
