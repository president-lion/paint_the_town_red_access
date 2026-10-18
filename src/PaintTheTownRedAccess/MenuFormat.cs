namespace PaintTheTownRedAccess;

/// <summary>What the reader knows about one menu item, gathered from the Unity objects.</summary>
internal sealed class MenuItem
{
    public string Label = "";
    public string Role = "";       // Strings.Role*; empty for a plain button-like item
    public string Value = "";      // current setting, if any
    public int OptionIndex = -1;   // for selectors: which option (0-based) of OptionCount
    public int OptionCount;
    public int Position;           // 1-based place among its siblings, 0 if unknown
    public int Count;
    public string Tip = "";
}

/// <summary>
/// Builds the spoken line for a menu item. Pure logic - no Unity types - so it is unit tested.
/// Order: label, role, value, item position. Example: "mode, selector, standard, 2 of 4".
/// A selector's option index is not spoken (the user asked for positions on items only);
/// it is still tracked so a change of option is noticed.
/// </summary>
internal static class MenuFormat
{
    public static string Item(MenuItem it)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(it.Label)) parts.Add(it.Label);
        if (!string.IsNullOrWhiteSpace(it.Role)) parts.Add(it.Role);
        if (!string.IsNullOrWhiteSpace(it.Value) && it.Value != it.Label) parts.Add(it.Value);
        if (it.Position > 0 && it.Count > 1)
            parts.Add(string.Format(Strings.Position, it.Position, it.Count));
        return string.Join(", ", parts);
    }

    /// <summary>The line spoken when a selector's or slider's value changes in place.</summary>
    public static string ValueChange(MenuItem it) => it.Value;

    /// <summary>Joins the screen title (only when it changed) with the item line.</summary>
    public static string WithContext(string context, string item) =>
        string.IsNullOrWhiteSpace(context) ? item : $"{context}. {item}";

    /// <summary>
    /// Joins a column of labels with a column of values line by line: "wave time:" and
    /// "31.857" become "wave time 31.9". Numbers are rounded to one decimal.
    /// </summary>
    public static string PairLines(string labels, string values)
    {
        static string[] Lines(string s) => s.Replace("<br>", "\n").Replace("\r", "").Split('\n');
        var l = Lines(labels);
        var v = Lines(values);
        var parts = new List<string>();
        for (var i = 0; i < Math.Max(l.Length, v.Length); i++)
        {
            var label = i < l.Length ? TextClean.Clean(l[i]).TrimEnd(':', ' ') : "";
            var value = i < v.Length ? TextClean.Clean(v[i]) : "";
            if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
                value = Math.Round(d, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var both = (label + " " + value).Trim();
            if (both.Length > 0) parts.Add(both);
        }
        return string.Join(", ", parts);
    }
}
