using System.Reflection;
using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;

namespace Themia.App.Services;

/// <summary>
/// What the app can say about itself, for the log and ⚙ › About Themia.
/// <para>
/// Read once. None of it changes while the process is alive, and the status bar asks for it every
/// time anything at all happens.
/// </para>
/// </summary>
internal static class AppInfo
{
    /// <summary>The informational version, falling back to the assembly version.</summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>
    /// Whether this build was compiled ahead of time. Worth showing: it is the whole point of the
    /// port, and it is the first thing to check when a performance question comes up.
    /// </summary>
    public static bool IsAot { get; } = !System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;

    public static string Runtime => IsAot ? "native" : "managed";

    /// <summary>[S-3580] ".NET 10.0.0 · Windows App SDK 1.8.x": what the build actually runs on, as About says it.</summary>
    public static string RuntimeLine => RuntimeRead.Value;

    /// <summary>Read the first time About is drawn, not at start: the release info is in the runtime DLL.</summary>
    private static readonly Lazy<string> RuntimeRead = new(ReadRuntimeLine);

    private static string ReadRuntimeLine()
    {
        var runtime = $".NET {Environment.Version.ToString(3)}";

        try
        {
            var sdk = Microsoft.Windows.ApplicationModel.WindowsAppRuntime.ReleaseInfo.AsString;
            return string.IsNullOrWhiteSpace(sdk) ? runtime : $"{runtime} · Windows App SDK {sdk}";
        }
        catch (Exception ex) when (ex is COMException or TypeLoadException or DllNotFoundException)
        {
            // The release info lives in the runtime DLL; a build that cannot reach it still has a
            // .NET version worth showing.
            Log.Warn("about", "could not read the Windows App SDK release", ex);
            return runtime;
        }
    }

    private static string ReadVersion()
    {
        // Reflection over the entry assembly's own attributes is rooted by the AOT compiler, so this
        // is safe here; anything reflecting over *other* assemblies would not be.
        var assembly = Assembly.GetEntryAssembly();

        var informational = assembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Strip the source-revision suffix the SDK appends ("1.0.0+abc123"): a commit hash is
            // not what a status bar is for.
            var plus = informational.IndexOf('+');
            return plus > 0 ? informational[..plus] : informational;
        }

        return assembly?.GetName().Version?.ToString(3) ?? "dev";
    }
}
