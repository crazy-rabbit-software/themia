using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

/// <summary>[S-5303] The Date widget's settings: the Clock's alignment, and the year. Each writes straight to the live config and saves.</summary>
internal static partial class WidgetSettings
{
    public static void Date(List<FrameworkElement> rows, DateConfig date, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(date);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.DateWidget_Label.Text()));

        rows.Add(FluentLayout.Row(Glyphs.View, Str.Clock_AlignLabel.Text(), null,
            FluentLayout.Choice(
                [(ClockAlign.Left, Str.Clock_AlignLeft.Text()), (ClockAlign.Center, Str.Clock_AlignCenter.Text()), (ClockAlign.Right, Str.Clock_AlignRight.Text())],
                date.Align,
                value => { date.Align = value; save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Calendar, Str.DateWidget_ShowYear.Text(), null,
            FluentLayout.Toggle(date.ShowYear, value => { date.ShowYear = value; save(); })));
    }
}
