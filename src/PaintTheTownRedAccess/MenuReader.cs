using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaintTheTownRedAccess;

/// <summary>
/// Reads every menu by following the EventSystem selection, which keyboard and gamepad
/// navigation both move. All of the game's menus (main menu, options, pause, win/lose,
/// Beneath, level lists, input remapping) are uGUI built from the same widgets:
///   FinalMenuStandardButton   label
///   FinalMenuScrollerButton   header label + current option from labels[index] (left/right change it)
///   FinalMenuScrollerGraphicsOption  same, for graphics settings
///   FinalMenuStandardToggle   label + SEGToggle state
///   SettingSlider*/Slider     sibling "SettingTitle" whose bracket holds the value
///   MenuElementUGCLevel       workshop level row: title + creator
///   PersistentUpgradeButton   Beneath upgrade: name, cost, owned, description
///   FinalMenuNeedsTip         tooltip text, read after the item
///   TabMenu                   tab strip; its index change is announced
/// Anything else falls back to its first text, then its object name.
/// On selection change: the screen title (when it changed), then the item. While the same
/// item stays selected, a changed value is spoken on its own.
/// </summary>
internal static class MenuReader
{
    private static int _lastId;
    private static string _lastValue;
    private static string _lastContext = "";
    private static float _nextValuePoll;
    private static float _queueUntil;

    private static readonly List<TabMenu> _tabs = new();
    private static readonly Dictionary<int, int> _tabIndex = new();
    private static float _nextTabScan;

    public static void OnSceneLoaded()
    {
        _lastId = 0;
        _lastContext = "";
        _tabs.Clear();
        _tabIndex.Clear();
        _nextTabScan = 0;
    }

    public static void Tick()
    {
        TickTabs();

        var sel = EventSystem.current?.currentSelectedGameObject;
        if (sel == null) { _lastId = 0; return; }

        var now = Time.unscaledTime;
        var id = sel.GetInstanceID();
        if (id != _lastId)
        {
            _lastId = id;
            var item = Describe(sel);
            _lastValue = ValueKey(item);
            var context = ContextOf(sel.transform);
            var line = MenuFormat.Item(item);
            if (context != _lastContext && !string.IsNullOrWhiteSpace(context))
                line = MenuFormat.WithContext(context, line);
            if (!string.IsNullOrWhiteSpace(context)) _lastContext = context;
            Speech.Say(line, interrupt: now >= _queueUntil);
            if (!string.IsNullOrWhiteSpace(item.Tip)) Speech.Say(item.Tip, interrupt: false);
            Log.Debug("Menu", $"{UiDump.PathOf(sel.transform)} -> {line}");
            return;
        }

        if (now < _nextValuePoll) return;
        _nextValuePoll = now + 0.1f;
        var again = Describe(sel);
        var key = ValueKey(again);
        if (key == _lastValue) return;
        _lastValue = key;
        Speech.Say(MenuFormat.ValueChange(again));
    }

    private static string ValueKey(MenuItem it) => $"{it.Value}|{it.OptionIndex}|{it.OptionCount}";

    // ---- tabs ---------------------------------------------------------------------------

    private static void TickTabs()
    {
        var now = Time.unscaledTime;
        if (now >= _nextTabScan)
        {
            _nextTabScan = now + 2f;
            _tabs.Clear();
            foreach (var t in UnityEngine.Object.FindObjectsOfType<TabMenu>()) if (t != null) _tabs.Add(t);
        }

        foreach (var tab in _tabs)
        {
            if (tab == null) continue;
            var id = tab.GetInstanceID();
            var index = tab.currentTabIndex;
            var known = _tabIndex.TryGetValue(id, out var was);
            _tabIndex[id] = index;
            if (!known || was == index || !UiText.Visible(tab.transform)) continue;
            var names = tab.tabNames;
            var name = names != null && index >= 0 && index < names.Length ? TextClean.Clean(names[index]) : "";
            var count = names?.Length ?? 0;
            Speech.Say(string.Format(Strings.Tab, name, string.Format(Strings.Position, index + 1, count)));
            _queueUntil = now + 0.3f; // the selection moves into the new tab right after: queue it
        }
    }

    // ---- what is selected -----------------------------------------------------------------

    private static T On<T>(Transform t) where T : Component
    {
        var c = t.GetComponent<T>();
        if (c == null && t.parent != null) c = t.parent.GetComponent<T>();
        return c;
    }

    public static MenuItem Describe(GameObject sel)
    {
        var t = sel.transform;
        var it = new MenuItem();

        var scroller = On<FinalMenuScrollerButton>(t);
        var gfx = scroller == null ? On<FinalMenuScrollerGraphicsOption>(t) : null;
        if (scroller != null)
            Selector(it, UiText.Of(scroller.textHeaderLabelTMP) ?? UiText.Of(scroller.textHeaderLabel) ?? scroller.label,
                     UiText.Of(scroller.text1TMP) ?? UiText.Of(scroller.text1), scroller.labels, scroller.index);
        else if (gfx != null)
            Selector(it, UiText.Of(gfx.textHeaderLabelTMP) ?? UiText.Of(gfx.textHeaderLabel) ?? gfx.label,
                     UiText.Of(gfx.text1TMP) ?? UiText.Of(gfx.text1), gfx.labels, gfx.index);
        else if (On<FinalMenuStandardToggle>(t) is { } st)
        {
            it.Label = TextClean.Clean(UiText.Of(st.text1TMP) ?? UiText.Of(st.text1) ?? st.label);
            var tg = sel.GetComponent<Toggle>();
            if (tg != null) it.Value = tg.isOn ? Strings.Checked : Strings.NotChecked;
        }
        else if (On<MenuElementUGCLevel>(t) is { } lvl)
        {
            var title = TextClean.Clean(UiText.Of(lvl.levelTitleTMP) ?? UiText.Of(lvl.levelTitle));
            var by = TextClean.Clean(UiText.Of(lvl.levelCreatorTMP) ?? UiText.Of(lvl.levelCreator));
            it.Label = string.IsNullOrEmpty(by) ? title : string.Format(Strings.By, title, by);
        }
        else if (On<PersistentUpgradeButton>(t) is { } up)
        {
            it.Label = TextClean.Clean(UiText.Of(up.nameTextTMP) ?? UiText.Of(up.nameText));
            var cost = TextClean.Clean(UiText.Of(up.costTMP) ?? UiText.Of(up.cost));
            var owned = TextClean.Clean(UiText.Of(up.ownedTMP) ?? UiText.Of(up.owned));
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(cost)) parts.Add(string.Format(Strings.Cost, cost));
            if (!string.IsNullOrEmpty(owned)) parts.Add(string.Format(Strings.Owned, owned));
            it.Value = string.Join(", ", parts);
            it.Tip = TextClean.Clean(UiText.Of(up.descriptionTextTMP) ?? UiText.Of(up.descriptionText));
        }
        else if (On<FinalMenuStandardButton>(t) is { } sb)
            it.Label = TextClean.Clean(UiText.Of(sb.text1TMP) ?? UiText.Of(sb.text1) ?? UiText.First(t) ?? sb.Label);
        else if (sel.GetComponent<Slider>() is { } slider)
        {
            var (title, value) = TextClean.SplitTrailingValue(LabelNear(t) ?? "");
            it.Label = TextClean.Clean(title);
            it.Role = Strings.RoleSlider;
            it.Value = value ?? FormatNumber(slider.value, slider.wholeNumbers);
        }
        else if (sel.GetComponent<Dropdown>() is { } dd)
        {
            it.Label = TextClean.Clean(LabelNear(t));
            it.Role = Strings.RoleDropdown;
            it.Value = TextClean.Clean(UiText.Of(dd.captionText));
            OptionOf(it, dd.value, dd.options?.Count ?? 0);
        }
        else if (sel.GetComponent<TMP_Dropdown>() is { } tdd)
        {
            it.Label = TextClean.Clean(LabelNear(t));
            it.Role = Strings.RoleDropdown;
            it.Value = TextClean.Clean(UiText.Of(tdd.captionText));
            OptionOf(it, tdd.value, tdd.options?.Count ?? 0);
        }
        else if (sel.GetComponent<InputField>() is { } inf)
        {
            it.Label = TextClean.Clean(LabelNear(t));
            it.Role = Strings.RoleEdit;
            it.Value = inf.text;
        }
        else if (sel.GetComponent<TMP_InputField>() is { } tin)
        {
            it.Label = TextClean.Clean(LabelNear(t));
            it.Role = Strings.RoleEdit;
            it.Value = tin.text;
        }
        else if (sel.GetComponent<Toggle>() is { } toggle)
        {
            it.Label = TextClean.Clean(UiText.First(t) ?? t.name);
            it.Value = toggle.isOn ? Strings.Checked : Strings.NotChecked;
        }
        else if (sel.GetComponent<Scrollbar>() is { } bar)
        {
            it.Label = TextClean.Clean(LabelNear(t));
            it.Role = Strings.RoleScrollbar;
            it.Value = $"{Mathf.RoundToInt(bar.value * 100)}%";
        }
        else
            it.Label = TextClean.Clean(UiText.First(t));

        if (string.IsNullOrWhiteSpace(it.Label)) it.Label = TextClean.FromObjectName(t.name);

        var selectable = sel.GetComponent<Selectable>();
        if (selectable != null && !selectable.IsInteractable())
            it.Role = string.IsNullOrEmpty(it.Role) ? Strings.Unavailable : $"{it.Role}, {Strings.Unavailable}";

        if (string.IsNullOrEmpty(it.Tip) && On<FinalMenuNeedsTip>(t) is { } tip && !string.IsNullOrWhiteSpace(tip.tip))
            it.Tip = TextClean.Clean(UiText.Translate(tip.tip));

        (it.Position, it.Count) = PositionOf(t);
        return it;
    }

    private static void Selector(MenuItem it, string label, string value, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray labels, int index)
    {
        it.Label = TextClean.Clean(label);
        it.Role = Strings.RoleSelector;
        var count = labels?.Length ?? 0;
        if (string.IsNullOrWhiteSpace(value) && index >= 0 && index < count) value = labels[index];
        it.Value = TextClean.Clean(value);
        OptionOf(it, index, count);
    }

    private static void OptionOf(MenuItem it, int index, int count)
    {
        it.OptionIndex = index;
        it.OptionCount = count;
    }

    private static string FormatNumber(float v, bool whole) =>
        whole ? Mathf.RoundToInt(v).ToString() : v.ToString("0.##");

    /// <summary>The label of a control that has no text of its own: a sibling text called
    /// *Title*, *Label* or Text (SettingTitle, Label-Search, the language dropdown's "Language").</summary>
    private static string LabelNear(Transform t)
    {
        var parent = t.parent;
        if (parent == null) return null;
        string loose = null;
        for (var i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c == t || !c.gameObject.activeInHierarchy || c.GetComponent<Selectable>() != null) continue;
            var text = UiText.Own(c);
            if (text == null) continue;
            var n = c.name;
            if (n.Contains("Title") || n.Contains("Label") || n == "Text") return text;
            loose ??= text;
        }
        return loose ?? TextClean.FromObjectName(parent.name);
    }

    /// <summary>Place among the visible, interactable selectables that share the parent.</summary>
    private static (int, int) PositionOf(Transform t)
    {
        var parent = t.parent;
        if (parent == null) return (0, 0);
        int pos = 0, count = 0;
        for (var i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            var s = c.GetComponent<Selectable>();
            if (s == null || !s.IsInteractable() || !UiText.Visible(c)) continue;
            count++;
            if (c == t) pos = count;
        }
        return (pos, count);
    }

    // ---- which screen ---------------------------------------------------------------------

    /// <summary>The title of the screen the selection is on: the paused banner of the pause
    /// menu, else the nearest visible child called *Header* of an ancestor (HeaderArea,
    /// Header), else the name of the outermost Panel-* ancestor ("main menu").</summary>
    public static string ContextOf(Transform sel)
    {
        var wl = sel.GetComponentInParent<WinLoseUI>();
        if (wl != null && UiText.Showing(wl.pausedText)) return TextClean.Clean(wl.pausedText.text);
        if (wl != null) return ""; // win/lose/arena banners are spoken by ScreenWatch when they appear

        string panel = null;
        for (var a = sel.parent; a != null; a = a.parent)
        {
            for (var i = 0; i < a.childCount; i++)
            {
                var c = a.GetChild(i);
                if (c.name.IndexOf("Header", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (sel.IsChildOf(c) || !UiText.Visible(c)) continue;
                var text = UiText.First(c);
                if (!string.IsNullOrWhiteSpace(text)) return TextClean.Clean(text);
            }
            if (a.name.StartsWith("Panel-")) panel = a.name;
        }
        return panel != null ? TextClean.FromObjectName(panel) : "";
    }
}
