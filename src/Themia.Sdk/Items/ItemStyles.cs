using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Sdk.Items;

/// <summary>
/// [S-16940] The item base's styles as the style picker offers them ([S-6001]): List, Row list, Column list, Grid and Table, each
/// with its words and glyph. A kind offers the ones that fit its data, the default first.
/// </summary>
public static class ItemStyles
{
    /// <summary>Every arrangement, List first.</summary>
    public static IReadOnlyList<StyleOption> All { get; } = Of(ItemStyle.List, ItemStyle.RowList, ItemStyle.ColumnList, ItemStyle.Grid, ItemStyle.Table);

    /// <summary>The options for <paramref name="styles"/>, in that order.</summary>
    public static IReadOnlyList<StyleOption> Of(params ItemStyle[] styles) => [.. styles.Select(Option)];

    /// <summary>One arrangement's option.</summary>
    public static StyleOption Option(ItemStyle style) => style switch
    {
        ItemStyle.RowList => StyleOption.Of(style, Str.Items_Style_RowList.Local(), 0xE907),
        ItemStyle.ColumnList => StyleOption.Of(style, Str.Items_Style_ColumnList.Local(), 0xE8FD),
        ItemStyle.Grid => StyleOption.Of(style, Str.Items_Style_Grid.Local(), 0xF0E2),
        ItemStyle.Table => StyleOption.Of(style, Str.Items_Style_Table.Local(), 0xE80A),
        _ => StyleOption.Of(style, Str.Items_Style_List.Local(), 0xEA37),
    };
}
