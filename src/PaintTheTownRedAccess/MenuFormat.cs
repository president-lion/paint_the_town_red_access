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
/// Order: label, role, value, option position, item position. Example:
/// "mode, selector, standard, 1 of 3, 2 of 4".
/// </summary>
internal static class MenuFormat
{
    public static string Item(MenuItem it)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(it.Label)) parts.Add(it.Label);
        if (!string.IsNullOrWhiteSpace(it.Role)) parts.Add(it.Role);
        if (!string.IsNullOrWhiteSpace(it.Value) && it.Value != it.Label) parts.Add(it.Value);
        if (it.OptionIndex >= 0 && it.OptionCount > 1)
            parts.Add(string.Format(Strings.Position, it.OptionIndex + 1, it.OptionCount));
        if (it.Position > 0 && it.Count > 1)
            parts.Add(string.Format(Strings.Position, it.Position, it.Count));
        return string.Join(", ", parts);
    }

    /// <summary>The line spoken when a selector's or slider's value changes in place.</summary>
    public static string ValueChange(MenuItem it)
    {
        if (it.OptionIndex >= 0 && it.OptionCount > 1)
            return $"{it.Value}, {string.Format(Strings.Position, it.OptionIndex + 1, it.OptionCount)}";
        return it.Value;
    }

    /// <summary>Joins the screen title (only when it changed) with the item line.</summary>
    public static string WithContext(string context, string item) =>
        string.IsNullOrWhiteSpace(context) ? item : $"{context}. {item}";
}
