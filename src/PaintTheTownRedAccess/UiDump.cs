using System.Text;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaintTheTownRedAccess;

/// <summary>
/// Writes what is on screen right now to UserData\PaintTheTownRedAccess\screen.txt: the
/// EventSystem selection, the game's menus (BaseMenu), every active Selectable, and every
/// active uGUI Text and TextMeshPro text with its hierarchy path. The quickest way to see
/// why something is not being read.
/// </summary>
internal static class UiDump
{
    public static void Write() => Write(Path.Combine(Log.Dir, "screen.txt"));

    public static void Write(string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        var sel = EventSystem.current?.currentSelectedGameObject;
        sb.AppendLine($"EventSystem selected: {(sel != null ? PathOf(sel.transform) : "<none>")}");

        Section(sb, "BaseMenu", () =>
        {
            foreach (var m in UnityEngine.Object.FindObjectsOfType<Il2Cpp.BaseMenu>())
                if (m != null) sb.AppendLine($"{PathOf(m.transform)} active={m.isActiveAndEnabled}");
        });

        Section(sb, "Active Selectables", () =>
        {
            foreach (var s in UnityEngine.Object.FindObjectsOfType<Selectable>())
                if (s != null && s.isActiveAndEnabled)
                    sb.AppendLine($"{PathOf(s.transform)} type={s.GetIl2CppType().Name} interactable={s.interactable}");
        });

        Section(sb, "Active UI Text", () =>
        {
            foreach (var t in UnityEngine.Object.FindObjectsOfType<Text>())
                if (t != null && t.isActiveAndEnabled && !string.IsNullOrWhiteSpace(t.text))
                    sb.AppendLine($"{PathOf(t.transform)} = \"{Flat(t.text)}\"");
        });

        Section(sb, "Active TextMeshPro", () =>
        {
            foreach (var t in UnityEngine.Object.FindObjectsOfType<TMP_Text>())
                if (t != null && t.isActiveAndEnabled && !string.IsNullOrWhiteSpace(t.text))
                    sb.AppendLine($"{PathOf(t.transform)} = \"{Flat(t.text)}\"");
        });

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, sb.ToString());
        Log.Info("UiDump", "written " + path);
    }

    private static void Section(StringBuilder sb, string title, Action body)
    {
        sb.AppendLine().AppendLine("== " + title);
        try { body(); }
        catch (Exception ex) { sb.AppendLine($"{title} section failed: {ex.Message}"); }
    }

    private static string Flat(string s) => s.Replace("\n", "\\n");

    public static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (; t != null; t = t.parent) parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }
}
