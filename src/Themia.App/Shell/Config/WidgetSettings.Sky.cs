using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3101] The rows the Sun and Moon widgets share: where the observer is.
/// <para>
/// [S-1494] The place is picked by city name, as in the original [S-01], through the same search the
/// weather widgets use (<see cref="PlaceSearch"/>). It resolves to coordinates once, at the pick; the
/// astronomy itself stays offline. The exact coordinates stay editable below it, validated, for a
/// place no geocoder knows (a cabin, a ship, a pole).
/// </para>
/// </summary>
internal static partial class WidgetSettings
{
    private static void SkyPlace(List<FrameworkElement> rows, SkyPlaceConfig config, Action save, bool longitude = true)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.SunMoon_LocationTab.Text()));

        // Built before the search so a pick can show its coordinates in them at once.
        var place = new TextBox { Text = config.Place, MinWidth = 200, PlaceholderText = Str.SunMoon_Place_Placeholder.Text() };
        var latitude = new TextBox { MinWidth = 120, HorizontalAlignment = HorizontalAlignment.Right };
        var longitudeBox = new TextBox { MinWidth = 120, HorizontalAlignment = HorizontalAlignment.Right };

        // A Sun or Moon tile always has a place (it starts at Zurich); a pick replaces it and fills
        // the exact fields below.
        PlaceSearch.Add(rows, new PlaceSearchBinding(
            config.Place,
            picked =>
            {
                config.Latitude = picked.Latitude;
                config.Longitude = picked.Longitude;
                config.Place = picked.Label;
                place.Text = picked.Label;
                latitude.Text = FormatCoordinate(picked.Latitude);
                longitudeBox.Text = FormatCoordinate(picked.Longitude);
                save();
            }));

        rows.Add(FluentLayout.Header(Str.WidgetsC_SunMoon_Settings_ExactCoordinates.Text()));

        // On lost focus, not per keystroke: the widget behind this window redraws on every write,
        // and a half-typed place name is not a place name.
        place.LostFocus += (_, _) =>
        {
            var text = place.Text.Trim();

            if (text == config.Place)
            {
                return;
            }

            config.Place = text;
            save();
        };

        rows.Add(FluentLayout.Row(Glyphs.Place, Str.SunMoon_Place_Label.Text(), Str.SunMoon_Place_Desc.Text(), place));

        rows.Add(CoordinateRow(
            latitude,
            Str.SunMoon_Latitude_Label.Text(),
            Str.SunMoon_Latitude_Desc.Text(),
            config.Latitude,
            -90,
            90,
            value => { config.Latitude = value; save(); }));

        // [S-6074] Offered where the style reads it ([S-6050]: the month's phases do not).
        if (!longitude)
        {
            return;
        }

        rows.Add(CoordinateRow(
            longitudeBox,
            Str.SunMoon_Longitude_Label.Text(),
            Str.SunMoon_Longitude_Desc.Text(),
            config.Longitude,
            -180,
            180,
            value => { config.Longitude = value; save(); }));
    }

    /// <summary>
    /// A coordinate entry that rejects what it cannot read.
    /// <para>
    /// Deliberately not a <c>NumberBox</c> and not <c>double.TryParse</c> with a fallback: coercing
    /// unreadable input to 0 would quietly move the user to the Gulf of Guinea and then report
    /// sunrise there as though nothing had happened. Bad input keeps the last good value, says so
    /// on the same line under the box every other refused field uses, and writes nothing.
    /// </para>
    /// </summary>
    private static FrameworkElement CoordinateRow(
        TextBox box,
        string title,
        string description,
        double current,
        double minimum,
        double maximum,
        Action<double> onChanged)
    {
        box.Text = FormatCoordinate(current);

        var verdict = FluentLayout.Verdict();

        void Commit()
        {
            var text = box.Text.Trim();

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                && double.IsFinite(value)
                && value >= minimum
                && value <= maximum)
            {
                FluentLayout.Confirm(verdict, string.Empty);
                box.Text = FormatCoordinate(value);
                onChanged(value);
                return;
            }

            // Refused: keep the setting where it was, and leave what was typed on screen next to the
            // reason, so the user can correct it rather than hunt for what changed.
            FluentLayout.Refuse(
                verdict,
                Str.WidgetsC_SunMoon_Settings_CoordinateRange.Format(("min", FormatCoordinate(minimum)), ("max", FormatCoordinate(maximum))));
        }

        box.LostFocus += (_, _) => Commit();

        box.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter)
            {
                Commit();
                args.Handled = true;
            }
        };

        return FluentLayout.FieldRow(Glyphs.Grid, title, description, box, verdict);
    }

    private static string FormatCoordinate(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
