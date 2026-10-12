using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.System;

namespace Themia.App.Shell.Config;

/// <summary>
/// The settings rows for the camera stream.
/// <para>
/// The address box is the trust boundary on the page — what is typed is what the widget connects
/// to — so it is checked by <see cref="WebInput"/> on commit and a refused address is explained
/// under the box rather than stored and silently refused later.
/// </para>
/// </summary>
internal static partial class WidgetSettings
{
    public static void VideoStream(List<FrameworkElement> rows, VideoStreamConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsC_VideoStream_Settings_Stream.Text()));

        rows.Add(AddressRow(
            Str.WidgetsC_VideoStream_Settings_Address_Label.Text(),
            Str.WidgetsC_VideoStream_Settings_Address_Desc.Text(),
            "http://192.168.1.50:8080/?action=stream",
            () => config.Url,
            value => { config.Url = value; save(); }));

        rows.Add(FluentLayout.Header(Str.WidgetsC_VideoStream_Settings_Picture.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Fit, Str.WidgetsC_VideoStream_Settings_Fit_Label.Text(), Str.VideoStream_FitDesc.Text(),
            FluentLayout.Choice(
                [(StreamFit.Contain, Str.WidgetsC_VideoStream_Settings_Fit_Whole.Text()), (StreamFit.Cover, Str.WidgetsC_VideoStream_Settings_Fit_Crop.Text()), (StreamFit.Stretch, Str.VideoStream_FitStretch.Text())],
                config.EffectiveFit,
                value => { config.Fit = value; save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Rotate, Str.VideoStream_RotationLabel.Text(), Str.VideoStream_RotationDesc.Text(),
            FluentLayout.Choice(
                [
                    (StreamRotation.None, Str.Common_None.Text()),
                    (StreamRotation.Quarter, "90°"),
                    (StreamRotation.Half, "180°"),
                    (StreamRotation.ThreeQuarter, "270°"),
                ],
                config.EffectiveRotation,
                value => { config.Rotation = value; save(); })));

        FluentLayout.RefreshSeconds(
            rows,
            Str.WidgetsC_VideoStream_Settings_Refresh_Desc.Format(("zero", 0)),
            config.EffectiveRefreshSeconds,
            VideoStreamRules.LiveRefreshSeconds,
            VideoStreamRules.MaxRefreshSeconds,
            1,
            value => { config.RefreshSeconds = value; save(); });

        rows.Add(FluentLayout.Header(Str.WidgetsC_VideoStream_Settings_Overlay.Text()));

        // [S-1312] Both off the picture until there is room: never on the small sizes.
        rows.Add(FluentLayout.Row(Glyphs.Mouse, Str.WidgetsC_VideoStream_Settings_Hover_Label.Text(),
            Str.WidgetsC_VideoStream_Settings_Hover_Desc.Text(),
            FluentLayout.Toggle(config.HoverControls, value => { config.HoverControls = value; save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Info, Str.WidgetsC_VideoStream_Settings_Chip_Label.Text(),
            Str.WidgetsC_VideoStream_Settings_Chip_Desc.Text(),
            FluentLayout.Toggle(config.ShowStatus, value => { config.ShowStatus = value; save(); })));

        rows.Add(FluentLayout.Note(Str.WidgetsC_VideoStream_Settings_Note.Text()));
    }

    /// <summary>
    /// An address box that commits on Enter or on leaving it, checks with <see cref="WebInput"/>,
    /// and says why a refused address was refused. Never repaired into one that works.
    /// </summary>
    private static Border AddressRow(
        string title,
        string description,
        string placeholder,
        Func<string> current,
        Action<string> commit)
    {
        var input = new TextBox
        {
            Text = current(),
            Width = 300,
            MaxLength = WebInput.MaxUrlLength,
            PlaceholderText = placeholder,
        };

        var verdict = FluentLayout.Verdict();

        void Commit()
        {
            var typed = input.Text.Trim();

            if (typed == current())
            {
                return;
            }

            var checkedUrl = WebInput.Url(typed);

            if (typed.Length > 0 && !checkedUrl.IsValid)
            {
                FluentLayout.Refuse(verdict, checkedUrl.Problem == WebUrlProblem.UnsupportedScheme
                    ? Str.WidgetsC_VideoStream_Settings_OnlyHttp.Text()
                    : WebInput.Explain(checkedUrl.Problem));
                return;
            }

            FluentLayout.Confirm(verdict, string.Empty);
            commit(typed);
        }

        input.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Enter)
            {
                args.Handled = true;
                Commit();
            }
        };

        input.LostFocus += (_, _) => Commit();

        return FluentLayout.FieldRow(Glyphs.Link, title, description, input, verdict);
    }
}
