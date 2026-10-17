using System.Text;
using System.Text.RegularExpressions;

namespace PaintTheTownRedAccess;

/// <summary>
/// Turns on-screen text and object names into something a screen reader speaks well.
/// Pure logic - no Unity types - so it is unit tested.
/// </summary>
internal static class TextClean
{
    private static readonly Regex Tags = new("<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex TrailingValue = new(@"^(.*\S)\s*\(([^()]*)\)\s*$", RegexOptions.Compiled);

    /// <summary>Strips rich-text tags, collapses whitespace, and lowercases ALL-CAPS text
    /// (the menus are drawn in capitals, which some voices spell out).</summary>
    public static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = Tags.Replace(s, " ");
        s = Spaces.Replace(s, " ").Trim();
        if (IsAllCaps(s)) s = s.ToLowerInvariant();
        return s;
    }

    private static bool IsAllCaps(string s)
    {
        var letters = 0;
        foreach (var c in s)
        {
            if (!char.IsLetter(c)) continue;
            if (char.IsLower(c)) return false;
            if (char.IsUpper(c)) letters++;
        }
        return letters >= 2;
    }

    /// <summary>"ButtonStandard-ControlRemapper" -> "control remapper"; "ButtonToggle-Punching Only" -> "punching only".</summary>
    public static string FromObjectName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        var dash = name.LastIndexOf('-');
        if (dash >= 0 && dash < name.Length - 1) name = name[(dash + 1)..];
        name = Regex.Replace(name, @"\s*\((\d+|Clone)\)$", "");
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c == '_') { sb.Append(' '); continue; }
            if (i > 0 && char.IsUpper(c) && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1]))))
                sb.Append(' ');
            sb.Append(c);
        }
        return Spaces.Replace(sb.ToString(), " ").Trim().ToLowerInvariant();
    }

    /// <summary>Slider titles carry their value: "MOUSE SENSITIVITY (1.0)" -> ("MOUSE SENSITIVITY", "1.0").
    /// Returns the input and null when there is no trailing bracket.</summary>
    public static (string title, string value) SplitTrailingValue(string s)
    {
        if (string.IsNullOrEmpty(s)) return ("", null);
        var m = TrailingValue.Match(s);
        if (!m.Success) return (s, null);
        // "FIELD OF VIEW (VERTICAL) (65.0)": only the last bracket is the value.
        return (m.Groups[1].Value, m.Groups[2].Value);
    }
}
