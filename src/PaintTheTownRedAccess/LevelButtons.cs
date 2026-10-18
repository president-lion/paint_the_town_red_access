using Il2Cpp;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// Buttons placed in workshop levels (ButtonBasicLevelEditor, ButtonPedestalLevelEditor) have
/// no label; what they do is their WorldButtonLevelEditorLogic: <c>logics</c> (LogicTrigger
/// subclasses it switches) and <c>enemies</c> (it starts a fight with them: PlayerTriggeredLogic
/// -> EntityManager.StartCombat). Describes them as "button, moves 2 things, starts a fight
/// with 3 enemies", named after the nearest text sign the level maker put within 3 m.
/// LevelEditorPrimitive only acts through its path, so a primitive means something moves.
/// </summary>
internal static class LevelButtons
{
    private const float SignRange = 3f;
    private static readonly Dictionary<IntPtr, (string Sign, string Effect)> _cache = new();
    private static LevelEditorText[] _signs;

    public static void OnSceneLoaded() { _cache.Clear(); _signs = null; }

    /// <summary>The sign next to a level editor button and what it does, or null for other buttons.</summary>
    public static (string Sign, string Effect)? Describe(WorldButton b)
    {
        var logic = b.buttonAction?.TryCast<WorldButtonLevelEditorLogic>();
        if (logic == null) return null;
        if (_cache.TryGetValue(b.Pointer, out var known)) return known;
        var result = (NearestSign(b.transform.position), Effect(logic));
        _cache[b.Pointer] = result;
        Log.Info("LevelButton", $"{UiDump.PathOf(b.transform)} sign='{result.Item1}' does '{result.Item2}'");
        return result;
    }

    private static string Effect(WorldButtonLevelEditorLogic logic)
    {
        int moves = 0, teleporters = 0, traps = 0, lights = 0, music = 0, trackers = 0, other = 0;
        var win = false;
        var logics = logic.logics;
        if (logics != null)
            foreach (var l in logics)
            {
                if (l == null) continue;
                if (l.TryCast<LevelEditorPrimitive>() != null) moves++;
                else if (l.TryCast<LevelEditorTeleporter>() != null) teleporters++;
                else if (l.TryCast<LevelEditorTrap>() != null) traps++;
                else if (l.TryCast<LogicLight>() != null) lights++;
                else if (l.TryCast<LogicMusic>() != null) music++;
                else if (l.TryCast<LogicVictory>() != null) win = true;
                else if (l.TryCast<LogicEnemyTracker>() != null) trackers++;
                else other++;
            }
        var enemies = 0;
        if (logic.enemies != null) foreach (var e in logic.enemies) if (e != null) enemies++;

        var parts = new List<string>();
        if (win) parts.Add(Strings.LbWins);
        if (enemies > 0) parts.Add(string.Format(enemies == 1 ? Strings.LbFight1 : Strings.LbFightN, enemies));
        if (moves > 0) parts.Add(string.Format(moves == 1 ? Strings.LbMoves1 : Strings.LbMovesN, moves));
        if (teleporters > 0) parts.Add(Strings.LbTeleporter);
        if (traps > 0) parts.Add(Strings.LbTraps);
        if (lights > 0) parts.Add(Strings.LbLights);
        if (music > 0) parts.Add(Strings.LbMusic);
        if (trackers > 0) parts.Add(Strings.LbTracker);
        if (other > 0) parts.Add(string.Format(Strings.LbOther, other));
        return parts.Count > 0 ? string.Join(", ", parts) : Strings.LbNothing;
    }

    private static string NearestSign(Vector3 at)
    {
        _signs ??= UnityEngine.Object.FindObjectsOfType<LevelEditorText>();
        string best = null;
        var bestDist = SignRange;
        foreach (var s in _signs)
        {
            if (s == null || !s.gameObject.activeInHierarchy) continue;
            var text = TextClean.Clean(s.textString ?? s.text?.text);
            if (string.IsNullOrWhiteSpace(text)) continue;
            var d = Vector3.Distance(s.transform.position, at);
            if (d < bestDist) { bestDist = d; best = text; }
        }
        return best;
    }
}
