using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Desktop;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Fence;

[assembly: ThemiaWidget("fence", typeof(FenceModule))]

namespace Themia.Widget.Fence;

/// <summary>
/// [S-10900] [S-13000] The fence: a box of real desktop items with a title bar of its own, as Stardock Fences' feature
/// (its look is Themia's). Desktop items dragged into it leave the free desktop and live in it; dragged out they are free
/// icons again. Made from Add widget, from a rectangle drawn with the right button on empty desktop, or round the selected
/// icons. Its behaviour is SPEC.md beside this file; the desktop's rules for it are the root SPEC's FENCES section.
/// </summary>
public sealed class FenceModule : IWidgetModule
{
    /// <summary>Its glyph (Segoe Fluent Icons U+F168, a group of boxes), the module's own.</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xF168);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("fence"),
        ConfigType = typeof(FenceConfig),
        CreateDefault = () => new FenceConfig(),
        Json = FenceJsonContext.Default,
        Keywords = ["fences", "group", "icons", "desktop", "box", "organize", "folder", "container"],
        Label = FenceStr.Fence_Label.Local(),
        Description = FenceStr.Fence_Desc.Local(),
        Glyph = Glyph,
        Sizing = c => Sizing((FenceConfig)c),

        // [S-11880] A fence is a box of its own: it joins no widget it touches (its title bar is its own edge).
        Traits = WidgetTraits.NeverJoins,

        // [S-13000] Among the desktop's own things, beside the desktop icon.
        Add = new(AddFolderId.Files, 0, 4),
    };

    /// <summary>
    /// [S-10901] A fence holds a few rows of the desktop's cells under its title bar and grows as the user makes it
    /// (relaxed, [S-12411]); [S-13012] rolled up, its height is the title bar's alone, which the relax keeps
    /// (<see cref="WidgetSizing.KeepsHeight"/>). [S-13040] At least two icon boxes across (one of text beside's wide ones,
    /// [S-13976]) and one down, its title bar grid-fitted to exactly <see cref="FenceGrid.TitleCells"/> rows. [S-14501] Both
    /// axes take every whole cell (owner, 2026-11-20: "lets not limit the user in the alignment"): its icons stay on the
    /// desktop icon grid, whole boxes from the body's top-left, the sliver a box does not fill at its far edge and foot
    /// empty.
    /// </summary>
    internal static WidgetSizing Sizing(FenceConfig config)
    {
        var cell = DesktopCell.Grid(DesktopCell.Current);
        var natural = FenceGrid.Around(6, cell);
        var min = new SizeDip(FenceGrid.MinAcross(cell) * cell.Width, FenceGrid.TitleDip + cell.Height);
        if (config.RolledUp)
        {
            return WidgetSizing.Range(min with { Height = FenceGrid.TitleDip }, natural with { Height = FenceGrid.TitleDip }, new SizeDip(natural.Width * 2, FenceGrid.TitleDip))
                with { KeepsHeight = true, Room = StretchRoom.Picture };
        }

        return WidgetSizing.Range(min, natural, new SizeDip(natural.Width * 2, natural.Height * 2)) with { Room = StretchRoom.Picture };
    }

    public FrameworkElement CreateView(IWidgetHost host) => new FenceView(host);

    /// <summary>
    /// [S-10914] Its page: the title (asked), what it shows ([S-13022]: what is put in, the desktop items that match, or files
    /// that match from a folder or drive, [S-13023]: where, how recent, how many), the standing sort, how the title bar
    /// rolls it up, and the rules ([S-13004]: file types, other extensions, a name's text).
    /// </summary>
    public void MenuLines(WidgetMenuLines menu, Themia.Core.Model.WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        if (config is not FenceConfig fence)
        {
            return;
        }

        menu.Lines.Add(Ask(menu, Glyphs.Rename, FenceStr.Fence_Title.Text(), fence.Name, null, typed => fence.Name = FenceConfig.CleanName(typed)));
        menu.Lines.Add(menu.Choice(
            Glyphs.Folder,
            FenceStr.Fence_Source.Text(),
            [
                (FenceSource.Held, FenceStr.Fence_Source_Held.Text()),
                (FenceSource.Desktop, FenceStr.Fence_Source_Desktop.Text()),
                (FenceSource.Search, FenceStr.Fence_Source_Search.Text()),
            ],
            fence.Source,
            source => fence.Source = source));
        if (fence.Source == FenceSource.Search)
        {
            menu.Lines.Add(menu.PickFolder(null, FenceStr.Fence_Query_Folder.Text(), fence.Query.Folder, folder =>
            {
                fence.Query.Folder = folder;
                FenceSearch.Repair(fence.Query);
            }));
            menu.Lines.Add(menu.Choice(
                null,
                FenceStr.Fence_Query_Modified.Text(),
                [
                    (FenceRecency.Any, FenceStr.Fence_Query_Any.Text()),
                    (FenceRecency.Day, FenceStr.Fence_Query_Day.Text()),
                    (FenceRecency.Week, FenceStr.Fence_Query_Week.Text()),
                    (FenceRecency.Month, FenceStr.Fence_Query_Month.Text()),
                    (FenceRecency.Year, FenceStr.Fence_Query_Year.Text()),
                ],
                fence.Query.Modified,
                recency => fence.Query.Modified = recency));
            menu.Lines.Add(menu.Number(null, FenceStr.Fence_Query_Max.Text(), fence.Query.Max, 10, FenceSearch.MostResults, 10,
                n => LocaleFormat.Integer(n), n => fence.Query.Max = n));
        }

        // A choice holds no null: -1 is "As placed".
        menu.Lines.Add(menu.Choice(
            Glyphs.Sort,
            FenceStr.Fence_Sort.Text(),
            [
                (-1, FenceStr.Fence_Sort_None.Text()),
                ((int)DesktopSortBy.Name, Str.Desktop_Menu_Sort_Name.Text()),
                ((int)DesktopSortBy.ItemType, Str.Desktop_Menu_Sort_Type.Text()),
                ((int)DesktopSortBy.DateModified, Str.Desktop_Menu_Sort_Date.Text()),
                ((int)DesktopSortBy.Size, Str.Desktop_Menu_Sort_Size.Text()),
            ],
            fence.Sort is { } sort ? (int)sort : -1,
            by => fence.Sort = Enum.IsDefined((DesktopSortBy)by) && by >= 0 ? (DesktopSortBy)by : null));
        menu.Lines.Add(menu.Toggle(Glyphs.ChevronUp, FenceStr.Fence_RollUpOnClick.Text(), fence.RollUpOnClick, on => fence.RollUpOnClick = on));
        menu.Lines.Add(menu.Toggle(null, FenceStr.Fence_HoverPeek.Text(), fence.HoverPeek, on => fence.HoverPeek = on));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(WidgetMenuLines.Submenu(Glyphs.Folder, (fence.Source == FenceSource.Held ? FenceStr.Fence_Rules : FenceStr.Fence_Rules_Which).Text(), RuleLines(menu, fence)));
    }

    /// <summary>[S-10914] [S-13004] The rules' page: a switch per file type, the user's own extensions and a name's text, each asked.</summary>
    private static IReadOnlyList<ItemMenuEntry> RuleLines(WidgetMenuLines menu, FenceConfig fence)
    {
        var rules = fence.Rules;
        var note = fence.Source switch
        {
            FenceSource.Desktop => FenceStr.Fence_Rules_NoteDesktop,
            FenceSource.Search => FenceStr.Fence_Rules_NoteSearch,
            _ => FenceStr.Fence_Rules_Note,
        };
        var lines = new List<ItemMenuEntry> { WidgetMenuLines.Note(note.Text()) };
        foreach (var type in Enum.GetValues<FenceFileType>())
        {
            lines.Add(menu.Toggle(null, TypeName(type), rules.Types.Contains(type), on =>
            {
                rules.Types.Remove(type);
                if (on)
                {
                    rules.Types.Add(type);
                }
            }));
        }

        lines.Add(Ask(menu, null, FenceStr.Fence_Rules_Extensions.Text(), string.Join(", ", rules.Extensions), FenceStr.Fence_Rules_ExtensionsHint.Text(),
            typed => rules.Extensions = FenceRules.ParseExtensions(typed)));
        lines.Add(Ask(menu, null, FenceStr.Fence_Rules_Name.Text(), rules.NameContains, null, typed =>
        {
            rules.NameContains = typed;
            FenceRules.Repair(rules);
        }));
        return lines;
    }

    /// <summary>[S-13004] A file type's name, as the rules' page shows it.</summary>
    internal static string TypeName(FenceFileType type) => (type switch
    {
        FenceFileType.Images => FenceStr.Fence_Type_Images,
        FenceFileType.Documents => FenceStr.Fence_Type_Documents,
        FenceFileType.Installers => FenceStr.Fence_Type_Installers,
        FenceFileType.Archives => FenceStr.Fence_Type_Archives,
        FenceFileType.Videos => FenceStr.Fence_Type_Videos,
        FenceFileType.Music => FenceStr.Fence_Type_Music,
        FenceFileType.Shortcuts => FenceStr.Fence_Type_Shortcuts,
        _ => FenceStr.Fence_Type_Folders,
    }).Text();

    /// <summary>A line of text asked beside the page; an empty one clears it.</summary>
    private static ItemMenuEntry Ask(WidgetMenuLines menu, string? glyph, string text, string current, string? hint, Action<string> set)
        => new(glyph, text, () => menu.Ask(new WidgetTextAsk(
            text,
            hint ?? text,
            Str.Common_Save.Text(),
            _ => null,
            typed =>
            {
                if (!string.Equals(typed, current, StringComparison.Ordinal))
                {
                    set(typed);
                    menu.Save();
                }
            },
            Initial: current,
            AllowEmpty: true)), Asks: true, Value: current);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(FenceConfig))]
internal sealed partial class FenceJsonContext : JsonSerializerContext;
