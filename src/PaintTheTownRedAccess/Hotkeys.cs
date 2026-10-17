using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// The mod's own keys, read as raw keyboard state (the game reads input through Rewired).
/// F12 belongs to NVDA and is never bound.
///   F3   repeat the last line
///   (no Ctrl: it is the game's crouch key, and NVDA already stops speech on Ctrl)
///   F10  write screen.txt (what is on screen now)
///   F11  write rewired.txt (every input action and its bindings)
///   PageUp / PageDown  previous / next tab (options menus)
/// </summary>
internal static class Hotkeys
{
    public static void Tick()
    {
        if (Input.GetKeyDown(KeyCode.F3)) Speech.Repeat();

        if (Input.GetKeyDown(KeyCode.PageUp)) MenuReader.SwitchTab(-1);
        if (Input.GetKeyDown(KeyCode.PageDown)) MenuReader.SwitchTab(1);

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
}
