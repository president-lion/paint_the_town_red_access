using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using Il2CppList = Il2CppSystem.Collections.Generic;

namespace PaintTheTownRedAccess;

/// <summary>
/// "The Blind Brawler's Blunder": a practice scenario built on the game's own arena. Press B
/// outside a level to switch it on, then start any arena challenge: Arena.StartChallenge (a
/// real call from Arena.Start; it sets currentChallenge and calls BeginNextFight, which reads
/// fights[CurrentFightIndex] and fights.Count, so any length works) gets a generated challenge
/// instead. Ten fights (ArenaChallengeIcons has art for ten waves) that grow from three
/// ordinary enemies to a crowd with bosses; every fifth fight is a boss fight. Enemies and
/// weapons are drawn at random from every fight of every real challenge, so they are set up
/// exactly as the game sets them up; no traps. During a fight a random weapon falls from the
/// sky near you every 12 to 20 seconds (EntityManager.CreateWeapon), with a falling whistle
/// from where it lands. The arena gates, crowd, stats between fights and win/lose all stay
/// the game's own.
/// </summary>
internal static class BrawlersBlunder
{
    private const int Fights = 10;

    private static readonly Arena.ChallengeEnemy.Type[] Bosses =
    {
        Arena.ChallengeEnemy.Type.BikerBoss, Arena.ChallengeEnemy.Type.DiscoBoss, Arena.ChallengeEnemy.Type.Blackbeard,
        Arena.ChallengeEnemy.Type.BigPrisoner, Arena.ChallengeEnemy.Type.PirateCaptain, Arena.ChallengeEnemy.Type.Pimp,
    };

    private static readonly System.Random _rnd = new();
    private static readonly List<WeaponInfo> _weaponPool = new();
    private static bool _running;
    private static float _nextDrop;

    public static bool On { get; private set; }

    public static void Toggle()
    {
        On = !On;
        Speech.Say(On ? Strings.BlunderOn : Strings.BlunderOff);
        Log.Info("Blunder", "mode " + (On ? "on" : "off"));
    }

    public static void OnSceneLoaded()
    {
        _running = false;
        _nextDrop = 0f;
    }

    [HarmonyPatch(typeof(Arena), nameof(Arena.StartChallenge))]
    private static class StartPatch
    {
        private static void Prefix(Arena __instance, ref Arena.Challenge challenge)
        {
            if (!On || !NetworkManager.IsSinglePlayerOrHost) return;
            try
            {
                var made = Build(__instance);
                if (made == null) return;
                challenge = made;
                _running = true;
                _nextDrop = Time.time + 15f;
                Speech.Say(string.Format(Strings.BlunderStart, Fights), interrupt: false);
            }
            catch (Exception ex) { Log.Error("Blunder", "could not build the challenge: " + ex); }
        }
    }

    /// <summary>Collects every enemy and weapon entry from the real challenges, then deals out ten fights.</summary>
    private static Arena.Challenge Build(Arena arena)
    {
        var enemies = new List<Arena.ChallengeEnemy>();
        var bosses = new List<Arena.ChallengeEnemy>();
        var weapons = new List<WeaponInfo>();
        var seen = new HashSet<IntPtr>();
        foreach (var set in new[] { arena.challenges, arena.arenaChallenges })
        {
            if (set == null) continue;
            foreach (var ac in set)
            {
                if (ac == null || ac.challenge == null || !seen.Add(ac.challenge.Pointer)) continue;
                foreach (var fight in ac.challenge.fights)
                {
                    if (fight == null) continue;
                    if (fight.enemies != null)
                        foreach (var e in fight.enemies)
                            if (e != null) (Bosses.Contains(e.type) ? bosses : enemies).Add(e);
                    if (fight.weapons != null)
                        foreach (var w in fight.weapons)
                            if (w != null && w.weaponInfo != null) weapons.Add(w.weaponInfo);
                }
            }
        }
        Log.Info("Blunder", $"pool: {enemies.Count} enemies, {bosses.Count} bosses, {weapons.Count} weapons");
        if (enemies.Count == 0 || weapons.Count == 0) return null;
        _weaponPool.Clear();
        _weaponPool.AddRange(weapons);

        var challenge = new Arena.Challenge();
        challenge.environmentMode = _rnd.Next(2);
        challenge.fights = new Il2CppList.List<Arena.Challenge.Fight>();
        for (var n = 1; n <= Fights; n++)
        {
            var fight = new Arena.Challenge.Fight
            {
                popularity = 1f,
                environmentMode = challenge.environmentMode,
                pickRandomPersonFirstChance = 0f,
                playerStartPosition = new Vector3(0f, 1f, 0f),
                playerStartRotation = Quaternion.identity,
                traps = new Il2CppList.List<Arena.ChallengeTrap>(),
                enemies = new Il2CppList.List<Arena.ChallengeEnemy>(),
                weapons = new Il2CppList.List<Arena.ChallengeWeapon>(),
            };

            // Every fifth fight: bosses with a few helpers. Otherwise a growing crowd, with a
            // chance of one boss from fight six on.
            var boss = n % 5 == 0 && bosses.Count > 0;
            var count = boss ? 2 + n / 5 : Math.Min(2 + n, 12);
            var bossCount = boss ? n / 5 : (n >= 6 && bosses.Count > 0 && _rnd.NextDouble() < 0.4 ? 1 : 0);
            for (var i = 0; i < bossCount; i++) fight.enemies.Add(Copy(bosses[_rnd.Next(bosses.Count)]));
            for (var i = 0; i < count; i++) fight.enemies.Add(Copy(enemies[_rnd.Next(enemies.Count)]));

            var weaponCount = 8 + n;
            for (var i = 0; i < weaponCount; i++)
            {
                var ang = (float)(_rnd.NextDouble() * Math.PI * 2);
                var dist = 3f + (float)_rnd.NextDouble() * 9f;
                fight.weapons.Add(new Arena.ChallengeWeapon
                {
                    weaponInfo = weapons[_rnd.Next(weapons.Count)],
                    position = new Vector3(Mathf.Sin(ang) * dist, 0.5f, Mathf.Cos(ang) * dist),
                    rotation = Quaternion.Euler(0f, (float)_rnd.NextDouble() * 360f, 0f),
                });
            }
            challenge.fights.Add(fight);
            Log.Info("Blunder", $"fight {n}: {fight.enemies.Count} enemies ({bossCount} bosses), {weaponCount} weapons");
        }
        return challenge;
    }

    /// <summary>A fresh enemy entry like the original, placed on a ring round the middle, facing in.</summary>
    private static Arena.ChallengeEnemy Copy(Arena.ChallengeEnemy src)
    {
        var ang = (float)(_rnd.NextDouble() * Math.PI * 2);
        var dist = 12f + (float)_rnd.NextDouble() * 5f;
        var pos = new Vector3(Mathf.Sin(ang) * dist, 0f, Mathf.Cos(ang) * dist);
        return new Arena.ChallengeEnemy
        {
            enemyType = src.enemyType,
            type = src.type,
            bodyType = src.bodyType,
            eAppearance = src.eAppearance,
            position = pos,
            rotation = Quaternion.LookRotation(-pos),
            enemySettings = src.enemySettings,
            enemyInstanceSettings = src.enemyInstanceSettings,
            weaponInfo = src.weaponInfo,
            shieldInfo = src.shieldInfo,
            dontAttackPlayerFirst = false,
            faction = src.faction,
            noWalkout = src.noWalkout,
        };
    }

    /// <summary>Weapons falling from the sky near the player while a fight is on.</summary>
    public static void Tick()
    {
        if (!_running || !Game.InPlay || !Arena.CombatStarted || _weaponPool.Count == 0) return;
        var p = Game.Player;
        if (p == null || p.health <= 0f) return;
        if (Time.time < _nextDrop) return;
        _nextDrop = Time.time + 12f + (float)_rnd.NextDouble() * 8f;

        var from = p.transform.position;
        var ang = (float)(_rnd.NextDouble() * Math.PI * 2);
        var dist = 3f + (float)_rnd.NextDouble() * 4f;
        var land = from + new Vector3(Mathf.Sin(ang) * dist, 0f, Mathf.Cos(ang) * dist);
        // Keep it on the arena floor.
        var flat = new Vector2(land.x, land.z);
        if (flat.magnitude > 16f) { flat = flat.normalized * 16f; land = new Vector3(flat.x, from.y, flat.y); }

        var info = _weaponPool[_rnd.Next(_weaponPool.Count)];
        if (info.assetRef == null) return;
        EntityManager.CreateWeapon(info.assetRef, land + Vector3.up * 4f, Quaternion.Euler(0f, (float)_rnd.NextDouble() * 360f, 0f),
                                   Vector3.down * 2f);
        if (Game.Frame(land, out var r, out var f, out _)) Cues.Play(Cues.Cue.Drop, Spatial.Place(r, f, 12f));
        Log.Info("Blunder", $"weapon drop at {land}");
    }
}
