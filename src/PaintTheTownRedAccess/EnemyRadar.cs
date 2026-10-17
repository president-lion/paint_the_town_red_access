using Il2Cpp;
using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// A sound for each nearby hostile enemy, placed by direction (pan, and lower pitch when
/// behind) and distance (volume, and quicker repeats when close). An enemy that starts an
/// attack on you plays a distinct warning sound at its position, so you know when to block.
/// Hostile = targeting the player (Enemy.IsFightingPlayer) or relation Angry. Enemies on the
/// ground (IsDown) stay quiet. Only the nearest few knock, so a crowd does not turn into noise.
/// </summary>
internal static class EnemyRadar
{
    private const int MaxVoices = 4;

    private sealed class Track
    {
        public float NextPing;
        public bool WasAttacking;
    }

    private sealed class Seen
    {
        public Enemy Enemy;
        public float Right, Forward, Dist;
        public bool Fighting;
    }

    private static readonly Dictionary<int, Track> _tracks = new();
    private static readonly List<Seen> _seen = new();
    private static float _nextLog;

    public static void OnSceneLoaded() => _tracks.Clear();

    public static void Toggle()
    {
        Settings.Radar = !Settings.Radar;
        Speech.Say(Settings.Radar ? Strings.RadarOn : Strings.RadarOff);
    }

    public static bool Hostile(Enemy e) =>
        e.IsFightingPlayer() || e.playerRelation == Enemy.PlayerRelation.Angry;

    public static void Tick()
    {
        if (!Settings.Radar || !Game.InPlay) return;
        var list = CharacterManager.enemies;
        if (list == null) return;

        var range = Settings.RadarRange;
        var now = Time.unscaledTime;
        _seen.Clear();
        int angry = 0, neutral = 0, friendly = 0;
        for (var i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e == null || !e.isActiveAndEnabled || e.IsDead()) continue;
            var rel = e.playerRelation;
            if (rel == Enemy.PlayerRelation.Angry) angry++;
            else if (rel == Enemy.PlayerRelation.Neutral) neutral++;
            else friendly++;
            if (!Hostile(e)) continue;
            if (!Game.Frame(e.transform.position, out var r, out var f, out _)) return;
            var dist = Mathf.Sqrt(r * r + f * f);
            if (dist > range) continue;
            _seen.Add(new Seen { Enemy = e, Right = r, Forward = f, Dist = dist, Fighting = e.IsFightingPlayer() });
        }
        _seen.Sort((a, b) => a.Dist.CompareTo(b.Dist));

        if (Settings.DebugLog && now >= _nextLog)
        {
            _nextLog = now + 5f;
            Log.Info("Radar", $"enemies {list.Count}: angry {angry}, neutral {neutral}, friendly {friendly}; in range {_seen.Count}");
        }

        for (var i = 0; i < _seen.Count; i++)
        {
            var s = _seen[i];
            var id = s.Enemy.GetInstanceID();
            if (!_tracks.TryGetValue(id, out var t)) _tracks[id] = t = new Track { NextPing = now + 0.05f * i };
            var place = Spatial.Place(s.Right, s.Forward, range);

            // Attack warning: the moment an enemy that is after you starts an attack.
            var attacking = s.Fighting && s.Enemy.IsAttacking();
            if (attacking && !t.WasAttacking) Cues.Play(Cues.Cue.Attack, place);
            t.WasAttacking = attacking;

            if (i >= MaxVoices || s.Enemy.IsDown() || now < t.NextPing) continue;
            t.NextPing = now + Spatial.PingInterval(s.Dist, range);
            Cues.Play(Cues.Cue.Enemy, place);
        }
    }
}
