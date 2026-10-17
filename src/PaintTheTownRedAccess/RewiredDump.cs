using System.Text;
using Il2CppRewired;

namespace PaintTheTownRedAccess;

/// <summary>
/// Writes UserData\PaintTheTownRedAccess\rewired.txt: every Rewired action (id, name) and,
/// per player, every controller map with its bindings. The action names and key bindings
/// live in Rewired's serialized data, not in code, so this is how mod keys are checked for
/// collisions and how action ids seen in the code (InputSystem.GetButtonDown(32)) are named.
/// Written once automatically when Rewired is ready, and again on F11.
/// </summary>
internal static class RewiredDump
{
    private static bool _done;

    public static string File => Path.Combine(Log.Dir, "rewired.txt");

    public static void Tick()
    {
        if (_done || !ReInput.isReady) return;
        _done = true;
        Write();
    }

    public static void Write()
    {
        var sb = new StringBuilder();
        Section(sb, "Actions", () =>
        {
            for (var id = 0; id < 512; id++)
            {
                var a = ReInput.mapping.GetAction(id);
                if (a != null) sb.AppendLine($"{a.id,4} {a.name} | {a.descriptiveName}");
            }
        });

        Section(sb, "Players", () =>
        {
            for (var p = 0; p < ReInput.players.playerCount; p++)
            {
                var player = ReInput.players.GetPlayer(p);
                if (player == null) continue;
                sb.AppendLine($"Player {player.id} '{player.name}'");
                var maps = new Il2CppSystem.Collections.Generic.List<ControllerMap>();
                player.controllers.maps.GetAllMaps(maps);
                for (var m = 0; m < maps.Count; m++)
                {
                    var map = maps[m];
                    if (map == null) continue;
                    sb.AppendLine($"  map {map.controllerType} category={map.categoryId} layout={map.layoutId} enabled={map.enabled}");
                    var all = map.AllMaps;
                    var count = all.Cast<Il2CppSystem.Collections.Generic.ICollection<ActionElementMap>>().Count;
                    for (var e = 0; e < count; e++)
                    {
                        var em = all[e];
                        if (em == null) continue;
                        var action = ReInput.mapping.GetAction(em.actionId);
                        var mods = em.modifierKey1 == ModifierKey.None ? "" : $" +{em.modifierKey1}";
                        sb.AppendLine($"    {em.actionId,4} {action?.name,-28} {em.elementIdentifierName} key={em.keyCode}{mods} pole={em.axisContribution} enabled={em.enabled}");
                    }
                }
            }
        });

        Directory.CreateDirectory(Log.Dir);
        System.IO.File.WriteAllText(File, sb.ToString());
        Log.Info("RewiredDump", "written " + File);
    }

    private static void Section(StringBuilder sb, string title, Action body)
    {
        sb.AppendLine("== " + title);
        try { body(); }
        catch (Exception ex) { sb.AppendLine($"{title} failed: {ex}"); }
        sb.AppendLine();
    }
}
