using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Files;

namespace Themia.App.Services.Files;

/// <summary>
/// [S-6416] The apps Windows recommends for a file, as Explorer's "Open with" submenu lists them
/// (<c>SHAssocEnumHandlers</c>, recommended only), and opening the file with one. COM through
/// function-pointer vtables; every handler and enumerator is released on every path. UI thread.
/// </summary>
internal static unsafe partial class OpenWithApps
{
    private const string Scope = "open-with";
    private const int AssocFilterRecommended = 0x1;

    /// <summary>The longest submenu: Explorer's shows a handful, then "Choose another app".</summary>
    public const int MaxShown = 8;

    /// <summary>One app: its handler's name (the key to open with it), what the menu says, and a file whose icon is the app's.</summary>
    public sealed record App(string Name, string Label, string? IconFile);

    /// <summary>The recommended apps for <paramref name="path"/>'s type, or none (logged) when the shell has none to say.</summary>
    public static IReadOnlyList<App> For(string path)
    {
        var apps = new List<App>();
        Enumerate(path, handler =>
        {
            var name = Text(handler, 3);
            var label = Text(handler, 4);

            if (name is null || label is null)
            {
                return true;
            }

            apps.Add(new App(name, label, IconFile(handler)));
            return apps.Count < MaxShown;
        });

        return apps;
    }

    /// <summary>Open <paramref name="path"/> with the app whose handler is <paramref name="name"/>. False (logged) when it is gone.</summary>
    public static bool Open(string path, string name)
    {
        var done = false;

        Enumerate(path, handler =>
        {
            if (!string.Equals(Text(handler, 3), name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            using var data = DragDropInterop.CreateDataObject([path], DropEffect.None);

            if (!data.IsNull)
            {
                // IAssocHandler::Invoke is slot 8.
                var invoke = (delegate* unmanaged[Stdcall]<nint, nint, int>)(*(nint**)handler)[8];
                var hr = invoke(handler, data.Pointer);
                done = hr >= 0;

                if (!done)
                {
                    Log.Warn(Scope, $"the app could not open the file (0x{hr:X8})");
                }
            }

            return false;
        });

        if (!done)
        {
            Log.Warn(Scope, "the chosen app is no longer offered for this file");
        }

        return done;
    }

    /// <summary>Walk the recommended handlers; <paramref name="each"/> says whether to go on. Each handler is released after its call.</summary>
    private static void Enumerate(string path, Func<nint, bool> each)
    {
        var extension = Path.GetExtension(path);

        if (string.IsNullOrEmpty(extension))
        {
            return;
        }

        nint enumerator;
        var hr = SHAssocEnumHandlers(extension, AssocFilterRecommended, &enumerator);

        if (hr < 0 || enumerator == 0)
        {
            Log.Debug(Scope, $"no handlers for '{extension}' (0x{hr:X8})");
            return;
        }

        using var owned = new DragDropInterop.ComRef(enumerator);

        // IEnumAssocHandlers::Next is slot 3.
        var next = (delegate* unmanaged[Stdcall]<nint, uint, nint*, uint*, int>)(*(nint**)enumerator)[3];

        for (var guard = 0; guard < 64; guard++)
        {
            nint handler;
            uint fetched;

            if (next(enumerator, 1, &handler, &fetched) != 0 || fetched == 0 || handler == 0)
            {
                return;
            }

            using var one = new DragDropInterop.ComRef(handler);

            if (!each(handler))
            {
                return;
            }
        }
    }

    /// <summary>GetName (slot 3) or GetUIName (slot 4): a CoTaskMem string, freed here.</summary>
    private static string? Text(nint handler, int slot)
    {
        nint text;
        var get = (delegate* unmanaged[Stdcall]<nint, nint*, int>)(*(nint**)handler)[slot];

        if (get(handler, &text) < 0 || text == 0)
        {
            return null;
        }

        try
        {
            var value = Marshal.PtrToStringUni(text);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        finally
        {
            DragDropInterop.CoTaskMemFree(text);
        }
    }

    /// <summary>
    /// GetIconLocation (slot 5): the file the app's icon is in, when it is a file on disk (an exe);
    /// a packaged app's resource string has no file, and its line goes without an icon.
    /// </summary>
    private static string? IconFile(nint handler)
    {
        nint text;
        int index;
        var get = (delegate* unmanaged[Stdcall]<nint, nint*, int*, int>)(*(nint**)handler)[5];

        if (get(handler, &text, &index) < 0 || text == 0)
        {
            return null;
        }

        try
        {
            var value = Environment.ExpandEnvironmentVariables(Marshal.PtrToStringUni(text) ?? string.Empty).Trim('"');
            return value.Length > 0 && value[0] != '@' && Path.IsPathFullyQualified(value) && File.Exists(value) ? value : null;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            Log.Debug(Scope, $"an app's icon location is not a file ({ex.GetType().Name})");
            return null;
        }
        finally
        {
            DragDropInterop.CoTaskMemFree(text);
        }
    }

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHAssocEnumHandlers(string extension, int filter, nint* enumerator);
}
