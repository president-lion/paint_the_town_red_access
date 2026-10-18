using System.Diagnostics;
using System.Security.Cryptography;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Serialized;
using Il2CppInterop.Common;
using Il2CppInterop.Generator;
using Il2CppInterop.Generator.Runners;
using Microsoft.Extensions.Logging;

// Usage: InteropGen <game dir>
string game = args.Length > 0 ? args[0] : @"D:\steamlib\steamapps\common\Paint the Town Red";
string ml = Path.Combine(game, "MelonLoader");
string gen = Path.Combine(ml, "Dependencies", "Il2CppAssemblyGenerator");
string gameAssembly = Path.Combine(game, "GameAssembly.dll");
string exeName = Path.GetFileNameWithoutExtension(Directory.GetFiles(game, "*.exe")
    .First(f => !Path.GetFileName(f).StartsWith("UnityCrashHandler")));
string work = Path.Combine(Path.GetTempPath(), "ptr_interopgen");
string dumpDir = Path.Combine(work, "cpp2il_out");
string outDir = Path.Combine(work, "Il2CppAssemblies");
if (Directory.Exists(work)) Directory.Delete(work, true);

// 1. Cpp2IL dummy dlls. attributeinjector adds the Address/Token attributes Il2CppInterop binds
//    methods with; attributeanalyzer (ML also runs it) is the step that exhausts memory, so skip it.
var psi = new ProcessStartInfo(Path.Combine(gen, "Cpp2IL", "Cpp2IL.exe"),
    $"--game-path \"{game}\" --exe-name \"{exeName}\" --output-as dummydll --use-processor attributeinjector --output-to \"{dumpDir}\"")
{ UseShellExecute = false };
psi.Environment["NO_COLOR"] = "1";
using (var p = Process.Start(psi)) { p.WaitForExit(); if (p.ExitCode != 0) { Console.Error.WriteLine("Cpp2IL failed"); return 1; } }

// 2. Il2CppInterop, same options as MelonLoader's Packages/Il2CppInterop.cs.
var resolver = new Resolver();
var source = Directory.GetFiles(dumpDir, "*.dll")
    .Select(f => ModuleDefinition.FromFile(f, new ModuleReaderParameters { ModuleResolver = resolver }))
    .Select(m => { resolver.Add(m); return m.Assembly; }).ToList();
string unityDeps = Path.Combine(gen, "UnityDependencies");
var opts = new GeneratorOptions
{
    GameAssemblyPath = gameAssembly,
    Source = source,
    OutputDir = outDir,
    UnityBaseLibsDir = unityDeps,
    Parallel = true,
    Il2CppPrefixMode = GeneratorOptions.PrefixMode.OptOut,
};
string tpa = (string)AppDomain.CurrentDomain.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
AppDomain.CurrentDomain.SetData("TRUSTED_PLATFORM_ASSEMBLIES",
    tpa + Path.PathSeparator + string.Join(Path.PathSeparator, Directory.GetFiles(unityDeps, "*.dll")));
Il2CppInteropGenerator.Create(opts).AddLogger(new ConsoleLogger()).AddInteropAssemblyGenerator().Run();

// 3. Move into MelonLoader\Il2CppAssemblies and mark generation as done.
string target = Path.Combine(ml, "Il2CppAssemblies");
Directory.CreateDirectory(target);
var names = new List<string>();
foreach (var f in Directory.GetFiles(outDir))
{
    string name = Path.GetFileName(f);
    names.Add(name);
    File.Copy(f, Path.Combine(target, name), true);
}
string hash = Convert.ToHexString(SHA512.HashData(File.ReadAllBytes(gameAssembly)));
string cfgPath = Path.Combine(gen, "Config.cfg");
var cfg = File.ReadAllLines(cfgPath).Where(l => !l.StartsWith("GameAssemblyHash") && !l.StartsWith("OldFiles")).ToList();
cfg.Insert(1, $"GameAssemblyHash = \"{hash}\"");
cfg.Add("OldFiles = [ " + string.Join(", ", names.Select(n => $"\"{n}\"")) + ", ]");
File.WriteAllLines(cfgPath, cfg);
Console.WriteLine($"Wrote {names.Count} assemblies to {target}");
return 0;

class Resolver : INetModuleResolver
{
    readonly Dictionary<string, ModuleDefinition> cache = new();
    public void Add(ModuleDefinition m) => cache[m.Name] = m;
    public ModuleDefinition Resolve(string name) => cache.GetValueOrDefault(name);
}

class ConsoleLogger : ILogger
{
    public IDisposable BeginScope<T>(T state) => null;
    public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;
    public void Log<T>(LogLevel level, EventId id, T state, Exception ex, Func<T, Exception, string> fmt)
    {
        if (IsEnabled(level)) Console.WriteLine($"[{level}] {fmt(state, ex)}");
    }
}
