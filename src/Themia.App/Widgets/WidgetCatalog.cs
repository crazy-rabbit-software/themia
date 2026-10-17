using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Widgets;

/// <summary>What every widget view can do. Kept tiny on purpose: the card owns chrome and input.</summary>
public interface IWidgetView
{
    /// <summary>Bind the view to the shared ticker and its initial config.</summary>
    void Attach(Ticker ticker, WidgetConfig config);

    /// <summary>Re-read a config the user just changed.</summary>
    void ApplyConfig(WidgetConfig config);

    /// <summary>Release timers and handles. Must be safe to call more than once.</summary>
    void Detach();
}

/// <summary>
/// [S-943] The picker's search document of a widget. What a widget is (its name, description, glyph,
/// size and view) is its module's ([S-6902], <see cref="ModuleCatalog"/>); this is only how search reads it.
/// </summary>
internal static class WidgetCatalog
{
    /// <summary>
    /// [S-943] What the picker's search matches a widget on. Name, description and the add menu's
    /// folders it is in (<paramref name="path"/>, [S-3513]) come in the current language and, when
    /// that is not English, in English as well — a German user who types "weather" still finds the
    /// Wetter widget, and the curated keywords (<see cref="WidgetDescriptor.Keywords"/>, English only) keep
    /// working in every language.
    /// </summary>
    public static WidgetSearchDoc SearchDoc(WidgetDescriptor definition, IReadOnlyList<AddShelf> path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var locale = Strings.Current;
        return new WidgetSearchDoc(
            definition.Id.Value,
            Both(locale, definition.LabelId),
            definition.Keywords,
            Both(locale, definition),
            [.. path.SelectMany(shelf => Both(locale, shelf.Title))],
            definition.ProviderNames);
    }

    /// <summary>The text in <paramref name="locale"/>, then in English when that differs; the first is the shown one.</summary>
    private static string[] Both(Locale locale, Str id) => Both(Strings.Get(locale, id), Strings.Get(Locale.En, id));

    private static string[] Both(Locale locale, WidgetDescriptor widget) => Both(widget.Describe(locale), widget.Describe(Locale.En));

    private static string[] Both(string shown, string english)
    {
        return string.Equals(shown, english, StringComparison.Ordinal) ? [shown] : [shown, english];
    }
}
