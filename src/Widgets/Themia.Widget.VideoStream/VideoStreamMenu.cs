using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.VideoStream;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-473] The camera stream's options, all on its properties menu ([S-3719]): Appearance
/// ([S-3723]), the address, how the picture fills and turns, how often it is read, the overlay's
/// switches and a note on what the right-click menu does. It has no settings page.
/// </summary>
internal static class VideoStreamMenu
{
    /// <summary>What an address looks like, in the box that asks for one.</summary>
    private const string Example = "http://192.168.1.50:8080/?action=stream";

    public static void Lines(WidgetMenuLines menu, VideoStreamConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.PlaceCard();
        var lines = menu.Lines;
        lines.Add(WidgetMenuLines.SetsUp(AddressLine(menu, config)));

        // [S-466] Whole picture, fill and crop, or stretch.
        lines.Add(menu.Choice(Glyphs.Fit, VideoStreamStr.WidgetsC_VideoStream_Settings_Fit_Label.Text(),
            [(StreamFit.Contain, VideoStreamStr.WidgetsC_VideoStream_Settings_Fit_Whole.Text()), (StreamFit.Cover, VideoStreamStr.WidgetsC_VideoStream_Settings_Fit_Crop.Text()), (StreamFit.Stretch, VideoStreamStr.VideoStream_FitStretch.Text())],
            config.EffectiveFit, value => config.Fit = value));

        // [S-468] For a camera mounted sideways or upside down.
        lines.Add(menu.Choice(Glyphs.Rotate, VideoStreamStr.VideoStream_RotationLabel.Text(),
            [(StreamRotation.None, Str.Common_None.Text()), (StreamRotation.Quarter, "90°"), (StreamRotation.Half, "180°"), (StreamRotation.ThreeQuarter, "270°")],
            config.EffectiveRotation, value => config.Rotation = value));

        // Zero keeps the stream live; otherwise one frame per interval.
        lines.Add(menu.Number(Glyphs.Timer, Str.Common_Refresh.Text(), config.EffectiveRefreshSeconds,
            VideoStreamRules.LiveRefreshSeconds, VideoStreamRules.MaxRefreshSeconds, 1, Interval, value => config.RefreshSeconds = value));

        // [S-1312] Both off the picture until there is room: never on the small sizes.
        lines.Add(ItemMenuEntry.Separator);
        lines.Add(menu.Toggle(Glyphs.Mouse, VideoStreamStr.WidgetsC_VideoStream_Settings_Hover_Label.Text(), config.HoverControls, on => config.HoverControls = on));
        lines.Add(menu.Toggle(Glyphs.Info, VideoStreamStr.WidgetsC_VideoStream_Settings_Chip_Label.Text(), config.ShowStatus, on => config.ShowStatus = on));

        lines.Add(ItemMenuEntry.Separator);
        lines.Add(WidgetMenuLines.Note(VideoStreamStr.WidgetsC_VideoStream_Settings_Note.Text()));
    }

    /// <summary>
    /// [S-473] [S-467] The address, asked for beside the menu ([S-3721]); its host at the line's end.
    /// It is the trust boundary (what is typed is what the widget connects to), so it is checked by
    /// <see cref="WebInput"/> as it is typed and a refusal says why, never repaired into one that
    /// works. Empty clears it: the widget waits for setup again.
    /// </summary>
    private static ItemMenuEntry AddressLine(WidgetMenuLines menu, VideoStreamConfig config)
    {
        var current = config.Url;
        var host = Uri.TryCreate(current, UriKind.Absolute, out var uri) && uri.Host.Length > 0 ? uri.Host : null;

        return new ItemMenuEntry(Glyphs.Link, VideoStreamStr.WidgetsC_VideoStream_Settings_Address_Label.Text(), () => menu.Ask(new WidgetTextAsk(
            VideoStreamStr.WidgetsC_VideoStream_Settings_Address_Label.Text(),
            Example,
            Str.Common_Save.Text(),
            AddressRefusal,
            typed =>
            {
                var address = typed.Trim();
                if (!string.Equals(address, current, StringComparison.Ordinal))
                {
                    config.Url = address;
                    menu.Save();
                }
            },
            Initial: current,
            AllowEmpty: true)), Value: host);
    }

    /// <summary>[S-467] Why a typed address cannot be used, or null when it can (empty: none).</summary>
    internal static string? AddressRefusal(string typed)
    {
        var text = (typed ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        var checkedUrl = WebInput.Url(text);
        if (checkedUrl.IsValid)
        {
            return null;
        }

        return checkedUrl.Problem == WebUrlProblem.UnsupportedScheme
            ? VideoStreamStr.WidgetsC_VideoStream_Settings_OnlyHttp.Text()
            : WebInput.Explain(checkedUrl.Problem);
    }

    /// <summary>"Live" for zero, else the interval in seconds.</summary>
    private static string Interval(int seconds)
        => seconds == VideoStreamRules.LiveRefreshSeconds ? VideoStreamStr.VideoStream_Menu_Live.Text() : LocaleFormat.Number(seconds, 0) + " " + Str.Shell_Refresh_SecondsUnit.Text();
}
