#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Shell;
using Themia.Sdk.Kit;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// Debug only: draw the open icon picker ([S-1803], [S-13605]) to a PNG, off-screen.
/// <c>Themia.exe --render iconpicker &lt;out.png&gt; [--search text] [--theme light|dark] [--initials] [--raster 100]</c>.
/// <para>
/// The picker stands inline in its card on a page; it is drawn here in a stand-in for that card's opened
/// body (the page's 580 flyout less its gutters, from the card's name column), the default chosen, so its
/// cross is ringed. <c>--initials</c> names the default as a view's initials, as the Views page does.
/// </para>
/// </summary>
internal static class IconPickerRender
{
    public static RenderCommand Command { get; } = new(
        RenderVerb.Render,
        "iconpicker",
        "<out.png> [--search text] [--theme light|dark] [--initials] [--raster 100]",
        """
        the open icon picker in a stand-in for its card's opened body, the default chosen [S-1803] [S-13605]; --search typed
        into its box; --initials: the default named as a view's initials, as the Views page does; --raster 100 [S-2377]
        """,
        RunAsync);

    private static async Task<int> RunAsync(RenderRun run)
    {
        var outPath = run.PathArg(0);
        var search = run.Option("--search");
        var initials = run.Has("--initials");

        // Its theme in force before the stand-in reads its brushes.
        using var harness = RenderHarness.Open(run);
        harness.PutAppThemeInForce();

        // The default chosen: its cross ringed.
        var picker = initials
            ? new IconPicker(null, Str.Shell_View_Initials.Text(), "WK", _ => { })
            : new IconPicker(null, null, null, _ => { });

        // A stand-in for the card's opened body on a page: its fill and rim, the picker from the name column to the end.
        var presenter = new Border
        {
            Child = picker,
            Width = ShellMetrics.Flyout.Cards,
            Padding = new Thickness(ShellMetrics.Page.NameAt, ShellMetrics.Inline.Pad, ShellMetrics.Page.EndInset, ShellMetrics.Inline.Pad),
            CornerRadius = new CornerRadius(ShellMetrics.Page.CardRadius),
            BorderThickness = new Thickness(1),
            BorderBrush = DesignApplier.Brush("CardStrokeColorDefaultBrush", Colors.Gray),
            Background = DesignApplier.Brush("CardBackgroundFillColorDefaultBrush", Colors.Black),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var root = new Grid
        {
            Padding = new Thickness(Fluent.Space4),
            Background = DesignApplier.Brush("SolidBackgroundFillColorBaseBrush", Colors.Black),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { presenter },
        };

        var window = RenderRaster.MakeWindow(harness, "Themia icon picker render", root);
        UiLanguage.Apply(root);
        RenderRaster.Show(window, 660, 560);

        // The box takes text once it is loaded, and the grid realises its rows on the next layout passes: settled each time.
        var name = System.IO.Path.GetFileName(outPath);
        await RenderHarness.SettleAsync(root, name).ConfigureAwait(true);
        if (search is not null)
        {
            picker.SetQuery(search);
            await RenderHarness.SettleAsync(root, name).ConfigureAwait(true);
        }

        await RenderHarness.SaveAsync(root, outPath, "iconpicker").ConfigureAwait(true);
        return RenderCommandLine.Passed;
    }
}
#endif
