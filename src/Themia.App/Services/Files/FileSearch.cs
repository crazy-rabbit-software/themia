using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Sdk;

namespace Themia.App.Services.Files;

/// <summary>
/// [S-13023] A search fence's files from Windows Search, as Explorer's own search box gets them: the query's Advanced Query
/// Syntax (<see cref="FenceSearch.Aqs"/>) parsed and resolved by the shell's query parser (<c>IQueryParserManager</c>,
/// <c>IQueryParser</c>, <c>IQuerySolution</c>, the system index's schema), set with the folder as the scope of a search
/// folder (<c>ISearchFolderItemFactory</c>), whose items are enumerated (<c>BHID_EnumItems</c>, <c>IEnumShellItems</c>);
/// the system index answers where the place is indexed. Each item's file system path is read, its folder flag and change
/// time from the disk, and the rules checked on every answer (<see cref="FenceSearch.Shown"/>). COM through function-pointer
/// vtables as everywhere in the app (AOT-safe), on a thread of its own in a single-threaded apartment, every object released
/// on every path; nothing throws out. (A <c>search-ms:</c> location parsed as a shell item opens but refuses to enumerate.)
/// </summary>
internal static unsafe partial class FileSearch
{
    private const string Scope = "fence-search";

    private const uint SigdnFileSysPath = 0x80058000;
    private const uint ClsctxInproc = 0x1;
    private const uint LocaleUserDefault = 0x400;
    private const uint CoInitApartmentThreaded = 0x2;

    /// <summary>At most this many answers are read before the rules and the count cut them: a runaway search stops.</summary>
    private const int MostRead = 4 * FenceSearch.MostResults;

    private static readonly Guid ClsidQueryParserManager = new("5088B39A-29B4-4d9d-8245-4EE289222F66");
    private static readonly Guid IidQueryParserManager = new("a879e3c4-af77-44fb-8f37-ebd1487cf920");
    private static readonly Guid IidQueryParser = new("2EBDEE67-3505-43f8-9946-EA44ABC8E5B0");
    private static readonly Guid ClsidSearchFolderItemFactory = new("14010e02-bbbd-41f0-88e3-eda371216584");
    private static readonly Guid IidSearchFolderItemFactory = new("a0ffbc28-5482-4366-be27-3e81e78e06c2");
    private static readonly Guid IidShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
    private static readonly Guid IidShellItemArray = new("b63ea76d-1f85-456f-a19c-48159efa858b");
    private static readonly Guid IidEnumShellItems = new("70629033-e363-4a28-a567-0db78006e6d7");
    private static readonly Guid BhidEnumItems = new("94f60519-2850-4924-aa5a-d15e84868039");

    /// <summary>[S-13023] The files the query finds (none when it has no rule, or the search failed: logged).</summary>
    public static Task<IReadOnlyList<FoundFile>> FindAsync(FenceRuleSet rules, FenceQuery query, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(query);
        var now = DateTime.UtcNow;
        var aqs = FenceSearch.Aqs(rules, query, now);
        if (aqs.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<FoundFile>>([]);
        }

        var rulesCopy = rules.Clone();
        var queryCopy = query.Clone();
        var folder = queryCopy.Folder.Length > 0 ? queryCopy.Folder : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var done = new TaskCompletionSource<IReadOnlyList<FoundFile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                done.TrySetResult(FenceSearch.Shown(rulesCopy, queryCopy, Enumerate(aqs, folder, token), now));
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "a search fence's search failed", ex);
                done.TrySetResult([]);
            }
        })
        {
            IsBackground = true,
            Name = "Themia fence search",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return done.Task;
    }

    /// <summary>The search's items in <paramref name="folder"/> as found files, till the token or <see cref="MostRead"/> stops it.</summary>
    private static List<FoundFile> Enumerate(string aqs, string folder, CancellationToken token)
    {
        var found = new List<FoundFile>();
        var initialised = CoInitializeEx(0, CoInitApartmentThreaded);
        try
        {
            if (Resolve(aqs) is not { } condition)
            {
                return found;
            }

            using var ownedCondition = new DragDropInterop.ComRef(condition);
            if (SearchItem(condition, folder) is not { } search)
            {
                return found;
            }

            using var ownedSearch = new DragDropInterop.ComRef(search);
            nint list;
            var bhid = BhidEnumItems;
            var iidEnum = IidEnumShellItems;

            // IShellItem::BindToHandler is slot 3.
            var hr = ((delegate* unmanaged[Stdcall]<nint, nint, Guid*, Guid*, nint*, int>)(*(nint**)search)[3])(search, 0, &bhid, &iidEnum, &list);
            if (hr < 0 || list == 0)
            {
                Log.Warn(Scope, $"the search's items could not be listed (0x{hr:X8})");
                return found;
            }

            using var ownedList = new DragDropInterop.ComRef(list);

            // IEnumShellItems::Next is slot 3.
            var next = (delegate* unmanaged[Stdcall]<nint, uint, nint*, uint*, int>)(*(nint**)list)[3];
            for (var read = 0; read < MostRead && !token.IsCancellationRequested; read++)
            {
                nint item;
                uint fetched;
                hr = next(list, 1, &item, &fetched);
                if (hr != 0 || fetched == 0 || item == 0)
                {
                    if (hr < 0)
                    {
                        Log.Warn(Scope, $"the search stopped (0x{hr:X8}); what it found is shown");
                    }

                    break;
                }

                using var ownedItem = new DragDropInterop.ComRef(item);
                if (PathOf(item) is { } path && Describe(path) is { } file)
                {
                    found.Add(file);
                }
            }
        }
        finally
        {
            if (initialised >= 0)
            {
                CoUninitialize();
            }
        }

        return found;
    }

    /// <summary>The query's condition, parsed against the system index's schema and resolved (dates made absolute); null when the shell refused it (logged).</summary>
    private static nint? Resolve(string aqs)
    {
        nint manager;
        var clsid = ClsidQueryParserManager;
        var iid = IidQueryParserManager;
        var hr = CoCreateInstance(&clsid, 0, ClsctxInproc, &iid, &manager);
        if (hr < 0 || manager == 0)
        {
            Log.Warn(Scope, $"no query parser (0x{hr:X8})");
            return null;
        }

        using var ownedManager = new DragDropInterop.ComRef(manager);
        nint parser;
        var iidParser = IidQueryParser;
        fixed (char* catalog = "SystemIndex")
        {
            // IQueryParserManager::CreateLoadedParser is slot 3, InitializeOptions slot 4.
            hr = ((delegate* unmanaged[Stdcall]<nint, char*, uint, Guid*, nint*, int>)(*(nint**)manager)[3])(manager, catalog, LocaleUserDefault, &iidParser, &parser);
        }

        if (hr < 0 || parser == 0)
        {
            Log.Warn(Scope, $"the query parser could not be loaded (0x{hr:X8})");
            return null;
        }

        using var ownedParser = new DragDropInterop.ComRef(parser);
        hr = ((delegate* unmanaged[Stdcall]<nint, int, int, nint, int>)(*(nint**)manager)[4])(manager, 0, 1, parser);
        if (hr < 0)
        {
            Log.Warn(Scope, $"the query parser's options could not be set (0x{hr:X8})");
            return null;
        }

        nint solution;
        fixed (char* text = aqs)
        {
            // IQueryParser::Parse is slot 3.
            hr = ((delegate* unmanaged[Stdcall]<nint, char*, nint, nint*, int>)(*(nint**)parser)[3])(parser, text, 0, &solution);
        }

        if (hr < 0 || solution == 0)
        {
            Log.Warn(Scope, $"the query could not be parsed (0x{hr:X8})");
            return null;
        }

        using var ownedSolution = new DragDropInterop.ComRef(solution);
        nint condition;

        // IQuerySolution::GetQuery is slot 7 (after IConditionFactory's MakeNot, MakeAndOr, MakeLeaf, Resolve).
        hr = ((delegate* unmanaged[Stdcall]<nint, nint*, nint, int>)(*(nint**)solution)[7])(solution, &condition, 0);
        if (hr < 0 || condition == 0)
        {
            Log.Warn(Scope, $"the query has no condition (0x{hr:X8})");
            return null;
        }

        using var ownedCondition = new DragDropInterop.ComRef(condition);
        SystemTime now;
        GetLocalTime(&now);
        nint resolved;

        // IConditionFactory::Resolve is slot 6.
        hr = ((delegate* unmanaged[Stdcall]<nint, nint, uint, SystemTime*, nint*, int>)(*(nint**)solution)[6])(solution, condition, 0, &now, &resolved);
        if (hr < 0 || resolved == 0)
        {
            Log.Warn(Scope, $"the query could not be resolved (0x{hr:X8})");
            return null;
        }

        return resolved;
    }

    /// <summary>The search folder of <paramref name="condition"/> scoped to <paramref name="folder"/> as a shell item; null when the shell refused it (logged).</summary>
    private static nint? SearchItem(nint condition, string folder)
    {
        nint factory;
        var clsid = ClsidSearchFolderItemFactory;
        var iid = IidSearchFolderItemFactory;
        var hr = CoCreateInstance(&clsid, 0, ClsctxInproc, &iid, &factory);
        if (hr < 0 || factory == 0)
        {
            Log.Warn(Scope, $"no search folder (0x{hr:X8})");
            return null;
        }

        using var ownedFactory = new DragDropInterop.ComRef(factory);
        nint place;
        var iidItem = IidShellItem;
        hr = SHCreateItemFromParsingName(folder, 0, &iidItem, &place);
        if (hr < 0 || place == 0)
        {
            Log.Warn(Scope, $"the search fence's folder could not be opened (0x{hr:X8})");
            return null;
        }

        using var ownedPlace = new DragDropInterop.ComRef(place);
        nint scope;
        var iidArray = IidShellItemArray;
        hr = SHCreateShellItemArrayFromShellItem(place, &iidArray, &scope);
        if (hr < 0 || scope == 0)
        {
            Log.Warn(Scope, $"the search's scope could not be made (0x{hr:X8})");
            return null;
        }

        using var ownedScope = new DragDropInterop.ComRef(scope);

        // ISearchFolderItemFactory: SetScope is slot 11, SetCondition 12, GetShellItem 13.
        var vtable = *(nint**)factory;
        hr = ((delegate* unmanaged[Stdcall]<nint, nint, int>)vtable[11])(factory, scope);
        if (hr >= 0)
        {
            hr = ((delegate* unmanaged[Stdcall]<nint, nint, int>)vtable[12])(factory, condition);
        }

        nint search = 0;
        if (hr >= 0)
        {
            hr = ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)vtable[13])(factory, &iidItem, &search);
        }

        if (hr < 0 || search == 0)
        {
            Log.Warn(Scope, $"the search folder could not be made (0x{hr:X8})");
            return null;
        }

        return search;
    }

    /// <summary>A found path as a file: its name, whether it is a folder, when it changed; null when it is gone or unreadable (logged).</summary>
    private static FoundFile? Describe(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Exists)
            {
                return new FoundFile(path, info.Name, false, info.LastWriteTimeUtc);
            }

            var folder = new DirectoryInfo(path);
            return folder.Exists ? new FoundFile(path, folder.Name, true, folder.LastWriteTimeUtc) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Log.Debug(Scope, $"a found file could not be read ({ex.GetType().Name}); left out");
            return null;
        }
    }

    /// <summary>An item's file system path, or null (an item with none).</summary>
    private static string? PathOf(nint item)
    {
        char* text;

        // IShellItem::GetDisplayName is slot 5.
        var name = (delegate* unmanaged[Stdcall]<nint, uint, char**, int>)(*(nint**)item)[5];
        if (name(item, SigdnFileSysPath, &text) < 0 || text is null)
        {
            return null;
        }

        try
        {
            return new string(text);
        }
        finally
        {
            CoTaskMemFree((nint)text);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemTime
    {
        public ushort Year;
        public ushort Month;
        public ushort DayOfWeek;
        public ushort Day;
        public ushort Hour;
        public ushort Minute;
        public ushort Second;
        public ushort Milliseconds;
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(Guid* clsid, nint outer, uint context, Guid* iid, nint* instance);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHCreateItemFromParsingName(string path, nint bindContext, Guid* iid, nint* item);

    [LibraryImport("shell32.dll")]
    private static partial int SHCreateShellItemArrayFromShellItem(nint item, Guid* iid, nint* array);

    [LibraryImport("kernel32.dll")]
    private static partial void GetLocalTime(SystemTime* time);

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(nint reserved, uint flags);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();

    [LibraryImport("ole32.dll")]
    private static partial void CoTaskMemFree(nint memory);
}
