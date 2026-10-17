using UnityEngine;

namespace PaintTheTownRedAccess;

/// <summary>
/// The mod's own keys, read as raw keyboard state (the game reads input through Rewired).
/// F12 belongs to NVDA and is never bound.
///   F3   repeat the last line
///   Ctrl stop speech
///   F10  write screen.txt (what is on screen now)
/// </summary>
internal static class Hotkeys
{
    public static void Tick()
    {
        if (Input.GetKeyDown(KeyCode.F3)) Speech.Repeat();

        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl)) Speech.Stop();

        if (Input.GetKeyDown(KeyCode.F10))
        {
            UiDump.Write();
            Speech.Say(Strings.UiDumpWritten);
        }
    }
}
