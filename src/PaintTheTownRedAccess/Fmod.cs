using System.Runtime.InteropServices;

namespace PaintTheTownRedAccess;

/// <summary>
/// Plays the mod's sounds through the game's own FMOD core system. Unity audio is
/// disabled in this game (globalgamemanagers AudioManager m_DisableAudio = 1), so an
/// AudioSource would be silent. The native C API is called directly (FMOD 2.01.08,
/// VERSION.number 0x20108, all of it inside fmodstudio.dll), with the core system handle
/// taken from FMODUnity.RuntimeManager.CoreSystem. FMOD mixes any number of channels.
/// </summary>
internal static class Fmod
{
    private const string Lib = "fmodstudio";

    private const uint ModeLoopOff = 0x1, ModeLoopNormal = 0x2, Mode2D = 0x8, ModeCreateSample = 0x100, ModeOpenMemory = 0x800;

    // FMOD_CREATESOUNDEXINFO for 2.01: 0xE0 bytes (see the game's CREATESOUNDEXINFO in dump.cs).
    // Only cbsize and length are needed for an in-memory WAV; the rest stays zero.
    [StructLayout(LayoutKind.Explicit, Size = 0xE0)]
    private struct CreateSoundExInfo
    {
        [FieldOffset(0)] public int cbsize;
        [FieldOffset(4)] public uint length;
    }

    [DllImport(Lib)] private static extern int FMOD_System_CreateSound(IntPtr system, byte[] data, uint mode, ref CreateSoundExInfo exinfo, out IntPtr sound);
    [DllImport(Lib)] private static extern int FMOD_System_PlaySound(IntPtr system, IntPtr sound, IntPtr channelGroup, int paused, out IntPtr channel);
    [DllImport(Lib)] private static extern int FMOD_Channel_SetVolume(IntPtr channel, float volume);
    [DllImport(Lib)] private static extern int FMOD_Channel_SetPitch(IntPtr channel, float pitch);
    [DllImport(Lib)] private static extern int FMOD_Channel_SetPan(IntPtr channel, float pan);
    [DllImport(Lib)] private static extern int FMOD_Channel_SetPaused(IntPtr channel, int paused);
    [DllImport(Lib)] private static extern int FMOD_Channel_Stop(IntPtr channel);
    [DllImport(Lib)] private static extern int FMOD_Channel_IsPlaying(IntPtr channel, out int playing);

    private static IntPtr _system;
    private static bool _failed;

    public static bool Ready
    {
        get
        {
            if (_system != IntPtr.Zero) return true;
            if (_failed) return false;
            try
            {
                if (!Il2CppFMODUnity.RuntimeManager.IsInitialized) return false;
                _system = Il2CppFMODUnity.RuntimeManager.CoreSystem.handle;
                if (_system != IntPtr.Zero) Log.Info("Fmod", $"core system 0x{_system.ToInt64():X}");
            }
            catch (Exception ex) { _failed = true; Log.Error("Fmod", "no FMOD core system: " + ex.Message); }
            return _system != IntPtr.Zero;
        }
    }

    /// <summary>Loads a WAV (bytes in memory) as a sample. Returns 0 on failure.</summary>
    public static IntPtr Load(byte[] wav, bool loop)
    {
        if (!Ready) return IntPtr.Zero;
        var info = new CreateSoundExInfo { cbsize = Marshal.SizeOf<CreateSoundExInfo>(), length = (uint)wav.Length };
        var mode = ModeOpenMemory | ModeCreateSample | Mode2D | (loop ? ModeLoopNormal : ModeLoopOff);
        var rc = FMOD_System_CreateSound(_system, wav, mode, ref info, out var sound);
        if (rc != 0) { Log.Error("Fmod", $"createSound failed rc={rc}"); return IntPtr.Zero; }
        return sound;
    }

    /// <summary>Starts a sound with its placement applied before it is heard.</summary>
    public static IntPtr Play(IntPtr sound, Placement p, float gain)
    {
        if (sound == IntPtr.Zero || !Ready) return IntPtr.Zero;
        if (FMOD_System_PlaySound(_system, sound, IntPtr.Zero, 1, out var ch) != 0) return IntPtr.Zero;
        Apply(ch, p, gain);
        FMOD_Channel_SetPaused(ch, 0);
        return ch;
    }

    public static void Apply(IntPtr ch, Placement p, float gain)
    {
        if (ch == IntPtr.Zero) return;
        FMOD_Channel_SetPan(ch, p.Pan);
        FMOD_Channel_SetVolume(ch, Math.Clamp(p.Volume * gain, 0f, 1f));
        FMOD_Channel_SetPitch(ch, p.Pitch);
    }

    public static bool IsPlaying(IntPtr ch) =>
        ch != IntPtr.Zero && FMOD_Channel_IsPlaying(ch, out var playing) == 0 && playing != 0;

    public static void Stop(IntPtr ch)
    {
        if (ch != IntPtr.Zero) FMOD_Channel_Stop(ch);
    }
}
