using System.Runtime.InteropServices;
using Microsoft.Win32;
using Themia.Core.Diagnostics;

namespace Themia.App.Services.Files;

/// <summary>
/// [S-1331] The New submenu's file types, read from the registry the way Explorer builds its own:
/// every <c>HKCR\.ext</c> with a <c>ShellNew</c> key (directly or under its ProgID) that makes a file
/// by itself — <c>NullFile</c>, <c>FileName</c> or <c>Data</c>. <c>Command</c> entries (wizards such as
/// New Shortcut) are not offered; they run programs this widget does not start.
/// <para>
/// Read once per run on the thread pool and cached: walking HKCR is thousands of keys.
/// </para>
/// </summary>
internal static partial class ShellNewTemplates
{
    private const string Scope = "shellnew";
    private static Task<IReadOnlyList<ShellNewTemplate>>? _load;

    /// <summary>The templates, sorted by label. Started on first ask; later asks share the one read.</summary>
    public static Task<IReadOnlyList<ShellNewTemplate>> GetAsync() => _load ??= Task.Run(Read);

    private static IReadOnlyList<ShellNewTemplate> Read()
    {
        var found = new List<ShellNewTemplate>();

        try
        {
            // Not disposed: ClassesRoot is the shared static key.
            var root = Registry.ClassesRoot;

            foreach (var name in root.GetSubKeyNames())
            {
                if (name.Length < 2 || name[0] != '.' || name is ".lnk" or ".library-ms")
                {
                    continue;
                }

                try
                {
                    if (Describe(root, name) is { } template)
                    {
                        found.Add(template);
                    }
                }
                catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
                {
                    // ignore: one unreadable class key costs one menu entry, not the menu.
                    Log.Debug(Scope, $"skipping {name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            Log.Warn(Scope, "could not read the New menu's file types", ex);
        }

        return [.. found.OrderBy(t => t.Label, StringComparer.CurrentCultureIgnoreCase)];
    }

    private static ShellNewTemplate? Describe(RegistryKey root, string extension)
    {
        using var ext = root.OpenSubKey(extension);

        if (ext is null)
        {
            return null;
        }

        var progId = ext.GetValue(null) as string;
        using var direct = ext.OpenSubKey("ShellNew");
        using var viaProgId = direct is null && !string.IsNullOrEmpty(progId) ? ext.OpenSubKey(progId + "\\ShellNew") : null;
        var shellNew = direct ?? viaProgId;

        if (shellNew is null)
        {
            return null;
        }

        var names = shellNew.GetValueNames();
        ShellNewKind kind;
        string? source = null;
        byte[]? data = null;

        if (names.Contains("FileName", StringComparer.OrdinalIgnoreCase))
        {
            kind = ShellNewKind.Copy;
            source = TemplatePath(shellNew.GetValue("FileName") as string);
        }
        else if (names.Contains("Data", StringComparer.OrdinalIgnoreCase))
        {
            kind = ShellNewKind.Data;
            data = shellNew.GetValue("Data") switch
            {
                byte[] bytes => bytes,
                string text => System.Text.Encoding.Default.GetBytes(text),
                _ => null,
            };
        }
        else if (names.Contains("NullFile", StringComparer.OrdinalIgnoreCase))
        {
            kind = ShellNewKind.Empty;
        }
        else
        {
            return null;
        }

        var label = Label(root, progId) ?? $"{extension[1..].ToUpperInvariant()} File";
        return new ShellNewTemplate(extension, label, kind, source, data);
    }

    /// <summary>The ProgID's friendly name ("Text Document"), resolving an indirect "@dll,-id" string.</summary>
    private static string? Label(RegistryKey root, string? progId)
    {
        if (string.IsNullOrEmpty(progId))
        {
            return null;
        }

        using var key = root.OpenSubKey(progId);
        var raw = key?.GetValue("FriendlyTypeName") as string ?? key?.GetValue(null) as string;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.StartsWith('@') ? Indirect(raw) : raw;
    }

    private static unsafe string? Indirect(string source)
    {
        var buffer = stackalloc char[260];
        return SHLoadIndirectString(source, buffer, 260, 0) >= 0 ? new string(buffer) : null;
    }

    /// <summary>A template named without a folder lives in the Templates folders Explorer searches.</summary>
    private static string? TemplatePath(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var expanded = Environment.ExpandEnvironmentVariables(name);

        if (Path.IsPathRooted(expanded))
        {
            return expanded;
        }

        string[] folders =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Templates),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonTemplates),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "ShellNew"),
        ];

        return folders.Select(f => Path.Combine(f, expanded)).FirstOrDefault(File.Exists);
    }

    [LibraryImport("shlwapi.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static unsafe partial int SHLoadIndirectString(string source, char* output, uint size, nint reserved);
}
