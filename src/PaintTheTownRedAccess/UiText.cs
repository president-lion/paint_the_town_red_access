using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaintTheTownRedAccess;

/// <summary>
/// Reading text and visibility out of the uGUI hierarchy. Every menu panel stays active
/// in the scene and is hidden with CanvasGroup alpha (see FINDINGS), so "active" is not
/// "visible": Visible() multiplies the CanvasGroup alphas up the parent chain.
/// </summary>
internal static class UiText
{
    public static bool Visible(Transform t)
    {
        if (t == null || !t.gameObject.activeInHierarchy) return false;
        var alpha = 1f;
        for (var p = t; p != null; p = p.parent)
        {
            var g = p.GetComponent<CanvasGroup>();
            if (g == null || !g.enabled) continue;
            alpha *= g.alpha;
            if (alpha < 0.05f) return false;
            if (g.ignoreParentGroups) break;
        }
        return true;
    }

    /// <summary>The text of a TMP or uGUI text component on this object, or null.</summary>
    public static string Own(Transform t)
    {
        var tmp = t.GetComponent<TMP_Text>();
        if (tmp != null && tmp.enabled && !string.IsNullOrWhiteSpace(tmp.text)) return tmp.text;
        var txt = t.GetComponent<Text>();
        if (txt != null && txt.enabled && !string.IsNullOrWhiteSpace(txt.text)) return txt.text;
        return null;
    }

    /// <summary>First non-empty active text at or under root, in hierarchy order, not looking
    /// inside other selectables (a scroller's arrow buttons, a list row's play button).</summary>
    public static string First(Transform root)
    {
        if (root == null) return null;
        var own = Own(root);
        if (own != null) return own;
        for (var i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (!c.gameObject.activeSelf || c.GetComponent<Selectable>() != null) continue;
            var found = First(c);
            if (found != null) return found;
        }
        return null;
    }

    public static string Of(TMP_Text t) => t != null && t.isActiveAndEnabled ? t.text : null;
    public static string Of(Text t) => t != null && t.isActiveAndEnabled ? t.text : null;

    /// <summary>A TMP text that is on screen: enabled, under visible canvas groups, not faded out.</summary>
    public static bool Showing(TMP_Text t) =>
        t != null && t.isActiveAndEnabled && t.alpha > 0.05f && Visible(t.transform) && !string.IsNullOrWhiteSpace(t.text);

    /// <summary>Translates through the game's I2 Localization if s is a term; otherwise returns s.</summary>
    public static string Translate(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        try
        {
            var tr = Il2CppI2.Loc.LocalizationManager.GetTranslation(s, true, 0, true, false, null, null);
            return string.IsNullOrWhiteSpace(tr) ? s : tr;
        }
        catch { return s; }
    }
}
