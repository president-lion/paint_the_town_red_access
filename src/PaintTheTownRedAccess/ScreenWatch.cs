using Il2Cpp;
using Il2CppTMPro;

namespace PaintTheTownRedAccess;

/// <summary>
/// Speaks screen text that is not attached to a menu item, when it appears or changes:
/// the loading screen's level name, the win / lose / arena banner, the arena stats between fights, the Beneath area
/// messages (area name, challenge, pursuer, shard, find the exit), the Beneath death screen
/// and the Beneath save-management popup. Polled a few times a second; each text is spoken
/// once per appearance (hiding it re-arms it).
/// </summary>
internal static class ScreenWatch
{
    private static readonly Dictionary<string, string> _last = new();
    private static float _nextPoll, _nextFind, _quietUntil;
    private static WinLoseUI _winLose;
    private static ArenaStatsUI _arenaStats;
    private static EndOfArenaWaveStats _waveStats;

    public static void OnSceneLoaded()
    {
        // A freshly loaded level still shows its UI's placeholder texts ("level complete",
        // "you died", "time 24:31, kills 80/80") for a moment: note them without speaking.
        _quietUntil = UnityEngine.Time.unscaledTime + 2f;
        _winLose = null;
        _arenaStats = null;
        _waveStats = null;
        _nextFind = 0;
    }

    public static void Tick()
    {
        var now = UnityEngine.Time.unscaledTime;
        if (now < _nextPoll) return;
        _nextPoll = now + 0.2f;

        if ((_winLose == null || _arenaStats == null || _waveStats == null) && now >= _nextFind)
        {
            _nextFind = now + 2f;
            _winLose ??= UnityEngine.Object.FindObjectOfType<WinLoseUI>();
            _arenaStats ??= UnityEngine.Object.FindObjectOfType<ArenaStatsUI>();
            _waveStats ??= UnityEngine.Object.FindObjectOfType<EndOfArenaWaveStats>();
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
            if (Watch("lose", _winLose.loseText)) DangerProbe.LogDeath();
            Watch("arena", _winLose.arenaText);
        }

        // Arena: the stats shown between fights and at the end of a challenge.
        if (_arenaStats != null) Watch("arenaStats", _arenaStats.statsLeftText);
        if (_waveStats != null)
        {
            // Labels ("wave time:") and values ("31.857") are two separate texts, one line each.
            var labels = UiText.Showing(_waveStats.statsLeftText) ? _waveStats.statsLeftText.text : null;
            var values = UiText.Showing(_waveStats.leftSideText) ? _waveStats.leftSideText.text : null;
            WatchText("waveStats", labels == null || values == null ? "" : MenuFormat.PairLines(labels, values));
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

    /// <summary>Speaks the text when it appears or changes; true when it did.</summary>
    private static bool Watch(string key, TMP_Text t, string format = null) =>
        WatchText(key, UiText.Showing(t) ? TextClean.Clean(t.text) : "", format);

    private static bool WatchText(string key, string text, string format = null)
    {
        _last.TryGetValue(key, out var was);
        if (text == (was ?? "")) return false;
        _last[key] = text;
        if (text.Length == 0) return false;
        if (key != "loading" && UnityEngine.Time.unscaledTime < _quietUntil)
        {
            Log.Debug("Screen", $"{key} (quiet after load): {text}");
            return false;
        }
        Speech.Say(format == null ? text : string.Format(format, text), interrupt: false);
        Log.Debug("Screen", $"{key}: {text}");
        return true;
    }
}
