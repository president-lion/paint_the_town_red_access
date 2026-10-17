using System.Runtime.InteropServices;
using System.Text;

namespace PaintTheTownRedAccess;

/// <summary>
/// Speech output via Prism (https://github.com/ethindp/prism), which routes to whatever
/// screen reader is running - NVDA, JAWS, ZoomText, OneCore, UIA, SAPI.
/// Every spoken line is also written to speech.log, so behaviour can be checked without audio.
/// </summary>
internal static class Speech
{
    private const string Lib = "prism";

    // Must match PrismConfig in prism.h exactly: it is returned by value from
    // prism_config_init, so a size mismatch corrupts the stack.
    [StructLayout(LayoutKind.Sequential)]
    private struct PrismConfig
    {
        public byte Version;
        public IntPtr Registry;
        public IntPtr AvailabilityCallback;
        public IntPtr AvailabilityUserdata;
        public uint PollIntervalMs;
        public uint DebounceSamples;
        public uint BackoffMaxMs;
        [MarshalAs(UnmanagedType.U1)] public bool AutoPowerManage;
        public IntPtr AvailabilityBaselineCallback;
    }

    [DllImport(Lib)] private static extern PrismConfig prism_config_init();
    [DllImport(Lib)] private static extern IntPtr prism_init(ref PrismConfig cfg);
    [DllImport(Lib)] private static extern void prism_shutdown(IntPtr ctx);
    [DllImport(Lib)] private static extern nuint prism_registry_count(IntPtr ctx);
    [DllImport(Lib)] private static extern ulong prism_registry_id_at(IntPtr ctx, nuint index);
    [DllImport(Lib)] private static extern IntPtr prism_registry_name(IntPtr ctx, ulong id);
    [DllImport(Lib)] private static extern IntPtr prism_registry_acquire(IntPtr ctx, ulong id);
    [DllImport(Lib)] private static extern IntPtr prism_registry_acquire_best(IntPtr ctx);
    [DllImport(Lib)] private static extern IntPtr prism_backend_name(IntPtr backend);
    [DllImport(Lib)] private static extern int prism_backend_initialize(IntPtr backend);
    [DllImport(Lib)] private static extern int prism_backend_speak(IntPtr backend, byte[] text, [MarshalAs(UnmanagedType.U1)] bool interrupt);
    [DllImport(Lib)] private static extern int prism_backend_output(IntPtr backend, byte[] text, [MarshalAs(UnmanagedType.U1)] bool interrupt);
    [DllImport(Lib)] private static extern int prism_backend_stop(IntPtr backend);
    [DllImport(Lib)] private static extern ulong prism_backend_get_features(IntPtr backend);

    private const int PrismOk = 0;
    private const int PrismAlreadyInitialized = 15;
    private const ulong FeatureOutput = 1UL << 2;
    private const ulong FeatureStop = 1UL << 3;

    private static IntPtr _ctx, _backend;
    private static bool _useOutput, _canStop;
    private static readonly RepeatFilter _repeats = new(TimeSpan.FromMilliseconds(400));

    public static string BackendName { get; private set; } = "<none>";

    /// <summary>Silences output (still logged) - used while the tour runs.</summary>
    public static bool Muted { get; set; }
    public static bool Ready => _backend != IntPtr.Zero;

    /// <summary>The last line spoken, for the repeat key.</summary>
    public static string Last { get; private set; } = "";

    /// <summary>Loads prism.dll out of UserLibs rather than relying on the DLL search path.</summary>
    public static void Preload(string userLibsDir)
    {
        var dll = Path.Combine(userLibsDir, "prism.dll");
        NativeLibrary.SetDllImportResolver(typeof(Speech).Assembly, (name, asm, path) =>
            name == Lib && File.Exists(dll) ? NativeLibrary.Load(dll) : IntPtr.Zero);
    }

    public static bool Init(string requestedBackend)
    {
        try
        {
            var cfg = prism_config_init();
            _ctx = prism_init(ref cfg);
            if (_ctx == IntPtr.Zero) { Log.Error("Speech", "prism_init failed"); return false; }

            ulong wanted = 0;
            var count = (int)prism_registry_count(_ctx);
            for (var i = 0; i < count; i++)
            {
                var id = prism_registry_id_at(_ctx, (nuint)i);
                var nm = Utf8(prism_registry_name(_ctx, id));
                Log.Debug("Speech", $"backend [{i}] {nm}");
                if (!requestedBackend.Equals("auto", StringComparison.OrdinalIgnoreCase)
                    && nm.Equals(requestedBackend, StringComparison.OrdinalIgnoreCase))
                    wanted = id;
            }

            _backend = wanted != 0 ? prism_registry_acquire(_ctx, wanted) : prism_registry_acquire_best(_ctx);
            if (_backend == IntPtr.Zero)
            {
                Log.Warn("Speech", "no usable speech backend (is a screen reader running?)");
                return false;
            }

            var rc = prism_backend_initialize(_backend);
            if (rc != PrismOk && rc != PrismAlreadyInitialized)
                Log.Warn("Speech", $"backend_initialize returned {rc} (continuing)");

            var features = prism_backend_get_features(_backend);
            _useOutput = (features & FeatureOutput) != 0;
            _canStop = (features & FeatureStop) != 0;
            BackendName = Utf8(prism_backend_name(_backend));
            Log.Info("Speech", $"backend: {BackendName}");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Speech", "Prism unavailable, running silently: " + ex.Message);
            _backend = IntPtr.Zero;
            return false;
        }
    }

    /// <summary>Speaks already-built text. Callers should pass it through TextClean first.</summary>
    public static void Say(string text, bool interrupt = true)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!_repeats.Allow(text, DateTime.UtcNow)) return;

        Last = text;
        Log.Append("speech.log", (interrupt ? "! " : "+ ") + text);
        if (_backend == IntPtr.Zero || Muted) return;
        try
        {
            var buf = Encoding.UTF8.GetBytes(text + "\0");
            var rc = _useOutput ? prism_backend_output(_backend, buf, interrupt)
                                : prism_backend_speak(_backend, buf, interrupt);
            if (rc != PrismOk) Log.Debug("Speech", $"speak rc={rc}");
        }
        catch (Exception ex) { Log.Once("Speech.Say", ex); }
    }

    /// <summary>Speaks the last line again, bypassing the repeat filter.</summary>
    public static void Repeat()
    {
        _repeats.Reset();
        Say(Last);
    }

    public static void Stop()
    {
        if (_backend == IntPtr.Zero || !_canStop) return;
        try { prism_backend_stop(_backend); } catch { }
    }

    public static void Shutdown()
    {
        // acquire_* hands back a registry-owned backend, so only the context is released.
        try { if (_ctx != IntPtr.Zero) prism_shutdown(_ctx); } catch { }
        _ctx = _backend = IntPtr.Zero;
    }

    private static string Utf8(IntPtr p) => p == IntPtr.Zero ? "" : (Marshal.PtrToStringUTF8(p) ?? "");
}
