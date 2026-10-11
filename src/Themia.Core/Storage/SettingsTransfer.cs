using System.Globalization;
using System.Text.Json;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Storage;

/// <summary>What an import would do, in the user's words, shown before anything is changed.</summary>
/// <param name="Changes">One line per thing that would be different. Empty when the file matches.</param>
/// <param name="Notes">What the user should know that is not a change: repairs, kept settings.</param>
public sealed record ImportPreview(IReadOnlyList<string> Changes, IReadOnlyList<string> Notes);

/// <summary>
/// Exporting the whole settings document to a file and importing one back. [S-494..S-498]
/// <para>
/// The file is the state document itself, byte for byte what <c>state.json</c> holds, so there is
/// one format and one reader: an import goes through <see cref="AppStateStore.Parse"/>, the same
/// trust boundary the app's own file crosses at startup. A file that would be refused at startup is
/// refused here, before it can replace anything.
/// </para>
/// </summary>
public static class SettingsTransfer
{
    private const string Scope = "transfer";

    /// <summary>The document as it is exported: the same JSON the store writes.</summary>
    public static string Serialize(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return JsonSerializer.Serialize(state, AppStateJsonContext.Default.AppState);
    }

    /// <summary>
    /// [S-494] Write a document to <paramref name="path"/>, atomically: a sibling temp file moved
    /// over the target, so an export interrupted half way never leaves a truncated file that a later
    /// import would refuse. Returns null on success, otherwise what went wrong in the user's words.
    /// </summary>
    public static string? WriteFile(AppState state, string path)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var temp = path + ".tmp";
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(temp, Serialize(state));
            File.Move(temp, path, overwrite: true);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            Log.Error(Scope, $"could not write settings to {path}", ex);

            try
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch (IOException cleanup)
            {
                Log.Warn(Scope, "could not remove the temp settings file", cleanup);
            }
            catch (UnauthorizedAccessException cleanup)
            {
                Log.Warn(Scope, "could not remove the temp settings file", cleanup);
            }

            return ex is UnauthorizedAccessException
                ? Str.Shell_Transfer_NotAllowed.Text()
                : Str.Shell_Transfer_WriteFailed.Format(("reason", ex.Message));
        }
    }

    /// <summary>
    /// [S-495] Read a file the user picked to import. A missing or unreadable file and a document the
    /// store would refuse both come back as a refusal with a reason; nothing is ever half-read.
    /// </summary>
    public static ParseResult ReadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (FileNotFoundException ex)
        {
            return ParseResult.Refused(ParseRefusal.Unreadable, Str.Shell_Transfer_Gone.Text(), ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ParseResult.Refused(ParseRefusal.Unreadable, Str.Shell_Transfer_ReadFailed.Format(("reason", ex.Message)), ex);
        }

        return Read(json, path);
    }

    /// <summary>
    /// [S-495] The content half of <see cref="ReadFile"/>: a document the store would refuse is
    /// refused, and so is one with no views in it.
    /// <para>
    /// The store repairs a document with no views by seeding an empty one, which is right for the
    /// app's own file — a half-valid layout is still the user's layout — and wrong for an import:
    /// any JSON object at all, another app's config included, would be accepted as "settings with
    /// one blank view" and replace every view the user has.
    /// </para>
    /// </summary>
    public static ParseResult Read(string json, string source)
    {
        if (!string.IsNullOrWhiteSpace(json) && !HasViews(json))
        {
            Log.Warn(Scope, $"refused an import from {source}: no views in it");
            return ParseResult.Refused(ParseRefusal.NotState, Explain(ParseRefusal.NotState));
        }

        var parsed = AppStateStore.Parse(json);
        if (parsed.State is null)
        {
            Log.Warn(Scope, $"refused an import from {source}: {parsed.Problem}");
            return ParseResult.Refused(parsed.Refusal, Explain(parsed.Refusal), parsed.Error);
        }

        return parsed;
    }

    /// <summary>Whether the document's top level carries a non-empty <c>views</c> array.</summary>
    private static bool HasViews(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("views", out var views)
                && views.ValueKind == JsonValueKind.Array
                && views.GetArrayLength() > 0;
        }
        catch (JsonException)
        {
            // ignore: not JSON at all; the store's own parse says so in its own words
            return true;
        }
    }

    /// <summary>The store's reason, said the way the import dialog says it.</summary>
    public static string Explain(ParseRefusal refusal) => Explain(Strings.Current, refusal);

    /// <summary><see cref="Explain(ParseRefusal)"/> in <paramref name="locale"/>.</summary>
    public static string Explain(Locale locale, ParseRefusal refusal) => Strings.Get(locale, refusal switch
    {
        ParseRefusal.Empty => Str.Shell_Transfer_Empty,
        ParseRefusal.NotJson => Str.Shell_Transfer_NotJson,
        ParseRefusal.NotState => Str.Shell_Transfer_NotState,
        ParseRefusal.NewerSchema => Str.Shell_Transfer_Newer,
        _ => Str.Shell_Transfer_Unrecognised,
    });

    /// <summary>The occasion an import's backup is named for: <c>state.before-import-….json</c>.</summary>
    public const string ImportOccasion = "import";

    /// <summary>[S-892] The occasion a reset's backup is named for: <c>state.before-reset-….json</c>.</summary>
    public const string ResetOccasion = "reset";

    /// <summary>
    /// [S-497] Where the settings the user has now are kept when an import (or [S-892] a reset)
    /// replaces them: beside the state file, named for the occasion and the moment, never
    /// overwriting an earlier one.
    /// </summary>
    public static string BackupPath(string statePath, DateTime now, string occasion = ImportOccasion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(occasion);

        var dir = Path.GetDirectoryName(statePath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(statePath);
        var stamp = now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return Path.Combine(dir, $"{name}.before-{occasion}-{stamp}.json");
    }

    /// <summary>
    /// [S-498] Replace <paramref name="target"/>'s contents with <paramref name="incoming"/>'s, in
    /// place, so everything holding the live state sees the new one.
    /// <para>
    /// Start with Windows is the exception: it is a fact about this PC's registry rather than a
    /// preference, and importing another machine's answer would leave the setting saying something
    /// the registry does not.
    /// </para>
    /// </summary>
    public static void Apply(AppState target, AppState incoming)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(incoming);

        var runAtStartup = target.Design.RunAtStartup;

        target.SchemaVersion = incoming.SchemaVersion;
        target.Views = incoming.Views;
        target.ActiveViewId = incoming.ActiveViewId;
        target.MonitorViews = incoming.MonitorViews;
        target.Design = incoming.Design;
        target.Design.RunAtStartup = runAtStartup;
        target.Preferences = incoming.Preferences;
        target.DefaultViewForNewDisplays = incoming.DefaultViewForNewDisplays;
        target.DisabledMonitors = incoming.DisabledMonitors;
        target.MonitorScales = incoming.MonitorScales;
        target.ViewBars = incoming.ViewBars;
    }

    /// <summary>
    /// [S-496] Say what an import would change, before it does.
    /// <para>
    /// Views are matched by id, then by name: a file exported from this install keeps its ids, and
    /// one from another PC has its own, where the name is the only thing the two have in common.
    /// Widgets are matched by id within a matched view and compared as their serialised form, so
    /// any difference at all — position, size, a setting — counts as a change.
    /// </para>
    /// </summary>
    /// <param name="repairs">What reading the file had to repair, from <see cref="ParseResult.Repairs"/>.</param>
    /// <param name="connectedDisplays">Device names of the displays attached now.</param>
    public static ImportPreview Compare(
        AppState current,
        AppState incoming,
        IReadOnlyList<string> repairs,
        IReadOnlyCollection<string> connectedDisplays)
        => Compare(Strings.Current, current, incoming, repairs, connectedDisplays);

    /// <summary>The same preview, worded in <paramref name="locale"/>.</summary>
    public static ImportPreview Compare(
        Locale locale,
        AppState current,
        AppState incoming,
        IReadOnlyList<string> repairs,
        IReadOnlyCollection<string> connectedDisplays)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(repairs);
        ArgumentNullException.ThrowIfNull(connectedDisplays);

        var changes = new List<string>();
        CompareViews(locale, current, incoming, changes);
        CompareDesign(locale, current.Design, incoming.Design, changes);
        ComparePreferences(locale, current.Preferences, incoming.Preferences, changes);

        var notes = new List<string>();

        var elsewhere = incoming.MonitorViews.Keys.Count(id => !connectedDisplays.Contains(id));
        if (elsewhere > 0)
        {
            notes.Add(Strings.Plural(locale, Str.Shell_Transfer_DisplaysAway, elsewhere));
        }

        if (repairs.Count > 0)
        {
            notes.Add(repairs.Count == 1
                ? Strings.Format(locale, Str.Shell_Transfer_RepairedOne, ("detail", repairs[0]))
                : Strings.Plural(locale, Str.Shell_Transfer_Repaired, repairs.Count));
        }

        if (current.Design.RunAtStartup != incoming.Design.RunAtStartup)
        {
            notes.Add(Strings.Get(locale, Str.Shell_Transfer_StartupKept));
        }

        return new ImportPreview(changes, notes);
    }

    private static void CompareViews(Locale locale, AppState current, AppState incoming, List<string> changes)
    {
        var unmatched = new List<ViewState>(current.Views);
        var pairs = new List<(ViewState Old, ViewState New)>();
        var added = new List<ViewState>();

        // Ids first across the board, then names for what is left, so a renamed view in a file
        // from this install is a rename rather than a removal and an unrelated addition.
        var pending = new List<ViewState>();
        foreach (var view in incoming.Views)
        {
            var match = unmatched.Find(v => string.Equals(v.Id, view.Id, StringComparison.Ordinal));
            if (match is null)
            {
                pending.Add(view);
                continue;
            }

            unmatched.Remove(match);
            pairs.Add((match, view));
        }

        foreach (var view in pending)
        {
            var match = unmatched.Find(v => string.Equals(v.Name.Trim(), view.Name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                added.Add(view);
                continue;
            }

            unmatched.Remove(match);
            pairs.Add((match, view));
        }

        foreach (var (old, @new) in pairs)
        {
            if (!string.Equals(old.Name, @new.Name, StringComparison.Ordinal))
            {
                changes.Add(Strings.Format(locale, Str.Shell_Transfer_Renames, ("old", old.Name), ("new", @new.Name)));
            }

            var detail = CompareWidgets(locale, old, @new);
            if (detail is not null)
            {
                changes.Add(Strings.Format(locale, Str.Shell_Transfer_ViewDetail, ("name", @new.Name), ("detail", detail)));
            }
        }

        foreach (var view in added)
        {
            changes.Add(Strings.Plural(locale, Str.Shell_Transfer_AddsView, view.Widgets.Count, ("name", view.Name)));
        }

        foreach (var view in unmatched)
        {
            changes.Add(view.Widgets.Count == 0
                ? Strings.Format(locale, Str.Shell_Transfer_RemovesView, ("name", view.Name))
                : Strings.Plural(locale, Str.Shell_Transfer_RemovesViewWidgets, view.Widgets.Count, ("name", view.Name)));
        }
    }

    /// <summary>Added, removed and changed widgets between two versions of one view, or null when equal.</summary>
    private static string? CompareWidgets(Locale locale, ViewState old, ViewState @new)
    {
        var before = old.Widgets.ToDictionary(w => w.Id, StringComparer.Ordinal);
        var addedCount = 0;
        var changedCount = 0;

        foreach (var widget in @new.Widgets)
        {
            if (!before.Remove(widget.Id, out var previous))
            {
                addedCount++;
                continue;
            }

            if (!string.Equals(Fingerprint(previous), Fingerprint(widget), StringComparison.Ordinal))
            {
                changedCount++;
            }
        }

        var removedCount = before.Count;

        var parts = new List<string>(3);
        if (addedCount > 0)
        {
            parts.Add(Strings.Plural(locale, Str.Shell_Transfer_WidgetsAdded, addedCount));
        }

        if (removedCount > 0)
        {
            parts.Add(Strings.Plural(locale, Str.Shell_Transfer_WidgetsRemoved, removedCount));
        }

        if (changedCount > 0)
        {
            parts.Add(Strings.Plural(locale, Str.Shell_Transfer_WidgetsChanged, changedCount));
        }

        return parts.Count == 0 ? null : string.Join(Strings.Get(locale, Str.Shell_Transfer_ListSeparator), parts);
    }

    private static string Fingerprint(WidgetInstance widget)
        => JsonSerializer.Serialize(widget, AppStateJsonContext.Default.WidgetInstance);

    private static void CompareDesign(Locale locale, DesignState old, DesignState @new, List<string> changes)
    {
        void Line<T>(Str label, T before, T after, Func<T, string> say)
        {
            if (!EqualityComparer<T>.Default.Equals(before, after))
            {
                changes.Add(Strings.Format(locale, Str.Shell_Transfer_Change,
                    ("label", Strings.Get(locale, label)), ("before", say(before)), ("after", say(after))));
            }
        }

        string Colour(string hex) => string.IsNullOrEmpty(hex) ? Strings.Get(locale, Str.Shell_Transfer_ThemeColour) : hex;
        string Px(double value) => Strings.Format(locale, Str.Shell_Transfer_Px, ("value", LocaleFormat.Number(locale, value, value % 1 == 0 ? 0 : 1)));
        string Percent(double value) => LocaleFormat.Percent(locale, Math.Round(value * 100));
        string Ms(double ms) => Strings.Format(locale, Str.Shell_Transfer_Ms, ("value", LocaleFormat.Integer(locale, (long)Math.Round(ms))));
        string Font(string family) => family == "system" ? Strings.Get(locale, Str.Shell_Appearance_SystemFont) : family;
        string OnOff(bool on) => Strings.Get(locale, on ? Str.Toggle_On : Str.Toggle_Off);
        string Theme(AppTheme theme) => Strings.Get(locale, theme switch
        {
            AppTheme.Light => Str.Settings_Theme_Light,
            AppTheme.Dark => Str.Settings_Theme_Dark,
            _ => Str.Settings_Theme_System,
        });
        string Border(WidgetBorderStyle style) => Strings.Get(locale, style switch
        {
            WidgetBorderStyle.Solid => Str.Settings_Style_Solid,
            WidgetBorderStyle.Dashed => Str.Settings_Style_Dashed,
            WidgetBorderStyle.Dotted => Str.Settings_Style_Dotted,
            _ => Str.Settings_Style_None,
        });
        string Intro(IntroAnimation animation) => Strings.Get(locale, animation switch
        {
            IntroAnimation.Fade => Str.Settings_Style_StartupAnim_Fade,
            IntroAnimation.Scale => Str.Settings_Style_StartupAnim_Scale,
            IntroAnimation.Slide => Str.Shell_Motion_Slide,
            IntroAnimation.Sides => Str.Shell_Motion_Sides,
            IntroAnimation.TopBottom => Str.Shell_Motion_TopBottom,
            IntroAnimation.Flip => Str.Settings_Style_StartupAnim_Flip,
            IntroAnimation.Blur => Str.Settings_Style_StartupAnim_Blur,
            IntroAnimation.Hyperspace => Str.Settings_Style_StartupAnim_Hyperspace,
            IntroAnimation.Hacker => Str.Settings_Style_StartupAnim_Hacker,
            _ => Str.Settings_Style_StartupAnim_None,
        });

        Line(Str.Settings_Theme_Label, old.Theme, @new.Theme, Theme);
        Line(Str.Shell_Transfer_Accent, old.PrimaryColor, @new.PrimaryColor, Colour);
        Line(Str.Settings_Style_WidgetBg_Label, old.WidgetBackgroundColor, @new.WidgetBackgroundColor, Colour);
        Line(Str.Settings_Style_WidgetOpacity_Label, old.SurfaceOpacity, @new.SurfaceOpacity, Percent);
        Line(Str.Settings_Style_CornerRadius_Label, old.CornerRadiusPx, @new.CornerRadiusPx, Px);
        Line(Str.Settings_Style_Border, old.BorderStyle, @new.BorderStyle, Border);
        Line(Str.Settings_Style_BorderWidth_Label, old.BorderWidthPx, @new.BorderWidthPx, Px);
        Line(Str.Shell_Appearance_BorderColour, old.BorderColor, @new.BorderColor, Colour);
        Line(Str.Settings_Style_BorderOpacity_Label, old.BorderOpacity, @new.BorderOpacity, Percent);
        Line(Str.Settings_Font_FamilyLabel, old.FontFamily, @new.FontFamily, Font);
        Line(Str.Shell_Appearance_TextSize, old.FontScale, @new.FontScale, Percent);
        Line(Str.Display_WidgetScale_Label, old.UiScale, @new.UiScale, Percent);
        Line(Str.Shell_Motion_Startup, old.IntroAnimation, @new.IntroAnimation, Intro);
        Line(Str.Shell_Transfer_StartupLength, old.IntroAnimationMs, @new.IntroAnimationMs, Ms);
        Line(Str.Shell_Layout_Gap, old.WidgetGapPx, @new.WidgetGapPx, Px);
        Line(Str.Settings_Style_GridSnap_Label, old.GridSnap, @new.GridSnap, OnOff);
        Line(Str.Settings_Style_HeaderBg_Label, old.HeaderBackgroundColor, @new.HeaderBackgroundColor, Colour);
        Line(Str.Settings_Style_HeaderOpacity_Label, old.HeaderOpacity, @new.HeaderOpacity, Percent);
        Line(Str.Shell_Motion_Transition, old.TransitionAnimation, @new.TransitionAnimation, Intro);
        Line(Str.Shell_Transfer_TransitionLength, old.TransitionAnimationMs, @new.TransitionAnimationMs, Ms);
        Line(Str.Shell_Motion_Unlock, old.UnlockAnimation, @new.UnlockAnimation, Intro);
        Line(Str.Shell_Transfer_UnlockLength, old.UnlockAnimationMs, @new.UnlockAnimationMs, Ms);
    }

    private static void ComparePreferences(Locale locale, AppPreferences old, AppPreferences @new, List<string> changes)
    {
        string OnOff(bool on) => Strings.Get(locale, on ? Str.Toggle_On : Str.Toggle_Off);

        void Toggle(Str label, bool before, bool after)
        {
            if (before != after)
            {
                changes.Add(Strings.Format(locale, Str.Shell_Transfer_Change,
                    ("label", Strings.Get(locale, label)), ("before", OnOff(before)), ("after", OnOff(after))));
            }
        }

        if (old.Language != @new.Language)
        {
            changes.Add(Strings.Format(locale, Str.Shell_Transfer_Change,
                ("label", Strings.Get(locale, Str.Settings_Language_Label)),
                ("before", old.Language.ToString()),
                ("after", @new.Language.ToString())));
        }

        Toggle(Str.Shell_Settings_General_GameMode, old.GameModeSuspend, @new.GameModeSuspend);
        Toggle(Str.Settings_DesktopIcons_Label, old.DesktopIconsVisible, @new.DesktopIconsVisible);

        // [S-3770] [S-3780] The formats and cadences every widget shares.
        void Value(Str label, string before, string after)
        {
            if (before != after)
            {
                changes.Add(Strings.Format(locale, Str.Shell_Transfer_Change,
                    ("label", Strings.Get(locale, label)), ("before", before), ("after", after)));
            }
        }

        Value(Str.WidgetPrefs_Region_Clock, old.Region.Clock.ToString(), @new.Region.Clock.ToString());
        Value(Str.Calendar_WeekStart_Label, old.Region.WeekStart.ToString(), @new.Region.WeekStart.ToString());
        Value(Str.Weather_UnitLabel, old.Region.Temperature.ToString(), @new.Region.Temperature.ToString());

        foreach (var (source, label) in new[]
        {
            (Sync.UpdateSource.Weather, Str.WidgetPrefs_Updates_Weather),
            (Sync.UpdateSource.Markets, Str.WidgetPrefs_Updates_Markets),
            (Sync.UpdateSource.Feeds, Str.WidgetPrefs_Updates_Feeds),
            (Sync.UpdateSource.Accounts, Str.WidgetPrefs_Updates_Accounts),
        })
        {
            Value(label, Strings.Plural(locale, Str.Shell_Refresh_EveryMinutes, Sync.UpdateCadence.Minutes(old.Refresh, source)),
                Strings.Plural(locale, Str.Shell_Refresh_EveryMinutes, Sync.UpdateCadence.Minutes(@new.Refresh, source)));
        }
    }

}
