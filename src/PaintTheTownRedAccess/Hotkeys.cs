using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// The mod's own keys, read as raw keyboard state (the game reads input through Rewired;
/// its bindings are in rewired.txt). F12 belongs to NVDA and is never bound. No plain Ctrl:
/// it is the game's crouch key, and NVDA already stops speech on Ctrl.
///   F3   repeat the last line
///   H    health
///   PageUp / PageDown         in a level: previous / next scanned thing; in menus: previous / next tab
///   Ctrl+PageUp / PageDown    scanner category
///   Home                      where the selected thing is
///   End                       beacon on/off
///   Shift+End                 enemy sounds on/off
///   F10  write screen.txt (what is on screen now)
///   F11  write rewired.txt (every input action and its bindings)
/// Left/Right arrows turn in a level: see Turning.
/// </summary>
internal static class Hotkeys
{
    public static void Tick()
    {
        if (Input.GetKeyDown(KeyCode.F3)) Speech.Repeat();

        var ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        var shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        var inPlay = Game.InPlay;

        if (Input.GetKeyDown(KeyCode.PageUp)) Page(-1, ctrl, inPlay);
        if (Input.GetKeyDown(KeyCode.PageDown)) Page(1, ctrl, inPlay);

        if (inPlay)
        {
            if (Input.GetKeyDown(KeyCode.H)) Game.SayHealth();
            if (Input.GetKeyDown(KeyCode.Home)) Scanner.SaySelected();
            if (Input.GetKeyDown(KeyCode.End))
            {
                if (shift) EnemyRadar.Toggle(); else Scanner.ToggleBeacon();
            }
        }

        if (Input.GetKeyDown(KeyCode.F11))
        {
            RewiredDump.Write();
            Speech.Say(Strings.InputDumpWritten);
        }

        if (Input.GetKeyDown(KeyCode.F10))
        {
            UiDump.Write();
            Speech.Say(Strings.UiDumpWritten);
        }
    }

    private static void Page(int step, bool ctrl, bool inPlay)
    {
        if (!inPlay) MenuReader.SwitchTab(step);
        else if (ctrl) Scanner.NextCategory(step);
        else Scanner.Cycle(step);
    }
}
