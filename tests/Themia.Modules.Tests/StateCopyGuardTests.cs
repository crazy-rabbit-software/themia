using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-886] [S-498] [S-889] The hand-written copies of the state (a widget's arrangement and clone, a view's, the design's
/// and the preferences' clones, the whole state's copy an import and a taken-back change make) against what the file
/// stores: each walks the
/// source-generated contract of its type (<see cref="JsonTypeInfo.Properties"/>, as load validation does) and fails
/// on a stored property the copy leaves out, unless this file declares it apart (an id, a widget's content).
/// <para>
/// Each fixture gives every stored property a value of its own: a property added later and left at its default
/// fails the fixture first, so the guard covers it from the start.
/// </para>
/// </summary>
public sealed class StateCopyGuardTests
{
    private static readonly WidgetId Clock = new("clock");

    /// <summary>A widget's identity: never copied from another.</summary>
    private const string WidgetId = "id";

    /// <summary>[S-886] A widget's content: held by reference, never part of the arrangement.</summary>
    private const string WidgetContent = "config";

    /// <summary>What the file holds of <typeparamref name="T"/>: its contract's properties that are written (an ignored one has no getter).</summary>
    private static List<JsonPropertyInfo> Stored<T>() => [.. StateJson.Options.GetTypeInfo(typeof(T)).Properties.Where(p => p.Get is not null)];

    private static string Json(JsonPropertyInfo property, object owner)
        => JsonSerializer.Serialize(property.Get!(owner), property.PropertyType, StateJson.Options);

    /// <summary>The fixture holds a value of its own in every stored property but <paramref name="except"/>.</summary>
    private static void Differs<T>(T fixture, T plain, params string[] except)
        where T : class
    {
        foreach (var property in Stored<T>().Where(p => !except.Contains(p.Name)))
        {
            Assert.False(
                Json(property, fixture) == Json(property, plain),
                $"the fixture leaves {typeof(T).Name}.{property.Name} as a plain one has it: give it a value of its own so the guard covers it");
        }
    }

    /// <summary>Every stored property but <paramref name="except"/> holds the same value in both.</summary>
    private static void Same<T>(T expected, T actual, string copy, params string[] except)
        where T : class
    {
        foreach (var property in Stored<T>().Where(p => !except.Contains(p.Name)))
        {
            Assert.True(
                Json(property, expected) == Json(property, actual),
                $"{copy} leaves out {typeof(T).Name}.{property.Name}: copy it, or declare why it is apart in {nameof(StateCopyGuardTests)}");
        }
    }

    private static WidgetInstance PlainWidget() => new() { Id = "plain", Config = WidgetConfig.CreateDefault(Clock) };

    private static WidgetInstance VariedWidget()
    {
        var widget = WidgetInstance.Create(Clock, new FracRect(0.11, 0.12, 0.13, 0.14));
        widget.Background = FillChoice.Of("#102030", 0.5);
        widget.BackgroundImage = @"C:\walls\lake.png";
        widget.ItemBackground = FillChoice.Of("#405060", 0.7);
        widget.ItemTextColor = "#708090";
        widget.Scale = 1.5;
        widget.Align = ContentAlign.Center;
        ((ClockConfig)widget.Config).ShowSeconds = true;
        return widget;
    }

    private static DesignState VariedDesign() => new()
    {
        Theme = AppTheme.Dark,
        FontFamily = "Georgia",
        FontScale = 1.25,
        CornerRadiusPx = 12,
        Background = FillChoice.Of("#112233", 0.6),
        BlurBehind = false,
        Material = CardMaterial.LiquidGlass,
        Tokens = new LookTokens { FrostBlur = 12, Rendering = RenderingMode.Monochrome },
        ItemBackground = FillChoice.Of("#445566", 0.4),
        ItemTextColor = "#778899",
        PrimaryColor = "#AA0000",
        BorderStyle = WidgetBorderStyle.Dashed,
        BorderWidthPx = 3,
        BorderColor = "#00AA00",
        BorderOpacity = 0.5,
        IntroAnimation = IntroAnimation.Fade,
        IntroAnimationMs = 300,
        TransitionAnimation = IntroAnimation.Flip,
        TransitionAnimationMs = 250,
        UnlockAnimation = IntroAnimation.None,
        UnlockAnimationMs = 200,
    };

    private static AppPreferences VariedPreferences()
    {
        var preferences = new AppPreferences
        {
            Language = AppLanguage.German,
            GameModeSuspend = true,
            DesktopIconSize = DesktopIconSize.Large,
            DesktopIconLayout = DesktopIconLayout.Beside,
            LockWidgets = true,
            RunAtStartup = true,
            Region = new RegionPreferences { Clock = ClockHours.Twelve, WeekStart = FirstWeekday.Monday, Temperature = TemperatureScale.Fahrenheit },
            RecentColours = ["#123456"],
        };
        preferences.Refresh["weather"] = 7;
        return preferences;
    }

    private static AppState VariedState()
    {
        var view = ViewState.Create("Imported");
        view.Widgets.Add(VariedWidget());
        return new AppState
        {
            SchemaVersion = AppState.CurrentSchemaVersion + 1,
            Views = [view],
            ActiveViewId = view.Id,
            MonitorViews = { ["DISPLAY1"] = view.Id },
            Design = VariedDesign(),
            Preferences = VariedPreferences(),
            DefaultViewForNewDisplays = view.Id,
            DisabledMonitors = ["DISPLAY2"],
            MonitorScales = { ["DISPLAY1"] = 1.5 },
            ViewBars = { ["DISPLAY1"] = new ViewBarSettings { Hidden = true } },
            Desktop = new DesktopState { Known = ["c:\\desktop\\notes.txt"], Sort = DesktopSortBy.Size },
        };
    }

    [Fact]
    public void S886_the_arrangement_copy_takes_every_stored_property_but_the_id_and_the_content()
    {
        var source = VariedWidget();
        var target = PlainWidget();
        Differs(source, target);
        var config = target.Config;

        target.CopyArrangementFrom(source);

        Same(source, target, nameof(WidgetInstance.CopyArrangementFrom), WidgetId, WidgetContent);
        Assert.Equal("plain", target.Id);
        Assert.Same(config, target.Config);
    }

    [Fact]
    public void S886_the_arrangement_compares_every_property_it_copies()
    {
        var source = VariedWidget();

        foreach (var property in Stored<WidgetInstance>().Where(p => p.Name is not (WidgetId or WidgetContent)))
        {
            var plain = PlainWidget();
            var changed = PlainWidget();
            property.Set!(changed, property.Get!(source));

            Assert.False(plain.SameArrangement(changed), $"{nameof(WidgetInstance.SameArrangement)} does not compare WidgetInstance.{property.Name}");
        }
    }

    [Fact]
    public void S886_a_change_of_any_arranged_property_is_one_step_and_its_undo_takes_it_back()
    {
        var source = VariedWidget();

        foreach (var property in Stored<WidgetInstance>().Where(p => p.Name is not (WidgetId or WidgetContent)))
        {
            var widget = PlainWidget();
            var before = PlainWidget();
            var view = ViewState.Create("Desk");
            view.Widgets.Add(widget);
            var state = new AppState { Views = [view], ActiveViewId = view.Id };
            var history = new LayoutHistory(state);

            property.Set!(widget, property.Get!(source));

            Assert.True(history.Commit(state, UndoAction.Change), $"a change of WidgetInstance.{property.Name} alone is no step");
            history.Undo(state);
            Assert.Same(widget, state.Views[0].Widgets[0]);
            Assert.True(Json(property, before) == Json(property, widget), $"an undo does not take WidgetInstance.{property.Name} back");
        }
    }

    [Fact]
    public void S886_a_deleted_widget_comes_back_whole()
    {
        var widget = VariedWidget();
        var view = ViewState.Create("Desk");
        view.Widgets.Add(widget);
        var state = new AppState { Views = [view], ActiveViewId = view.Id };
        var history = new LayoutHistory(state);

        view.Widgets.Remove(widget);
        history.Commit(state, UndoAction.Delete);
        history.Undo(state);

        var back = Assert.Single(state.Views[0].Widgets);
        Same(widget, back, "the undo snapshot");
        Assert.Same(widget.Config, back.Config);
    }

    [Fact]
    public void S886_a_clone_takes_every_stored_property_under_a_new_id()
    {
        var source = VariedWidget();
        Differs(source, PlainWidget());

        var clone = source.Clone();

        Same(source, clone, nameof(WidgetInstance.Clone), WidgetId);
        Assert.NotEqual(source.Id, clone.Id);
        Assert.NotSame(source.Config, clone.Config);
    }

    [Fact]
    public void S2130_a_design_clone_takes_every_stored_property()
    {
        var source = VariedDesign();
        Differs(source, new DesignState());

        Same(source, source.Clone(), nameof(DesignState.Clone));
    }

    [Fact]
    public void S702_a_preferences_clone_takes_every_stored_property()
    {
        var source = VariedPreferences();
        Differs(source, new AppPreferences());

        Same(source, source.Clone(), nameof(AppPreferences.Clone));
    }

    [Fact]
    public void S889_a_views_copy_takes_every_stored_property_but_the_id_and_the_widgets()
    {
        var source = ViewState.Create("Imported");
        source.Icon = "";
        source.Color = "#336699";
        source.DesktopIcons = ViewDesktopIcons.None;
        source.WorkArea = new FracRect(0.028, 0, 0.944, 1);
        source.Widgets.Add(VariedWidget());
        var target = ViewState.Create("Plain");
        Differs(source, target);

        target.CopyFrom(source);

        Same(source, target, nameof(ViewState.CopyFrom), "id", "widgets");
        Assert.Empty(target.Widgets);
    }

    [Fact]
    public void S498_an_import_replaces_every_stored_property_of_the_state()
    {
        var incoming = VariedState();
        var target = new AppState();
        Differs(incoming, target);

        // Start with Windows is kept from this PC ([S-498], its own test); here both say the same.
        target.Preferences.RunAtStartup = incoming.Preferences.RunAtStartup;
        SettingsTransfer.Apply(target, incoming);

        Same(incoming, target, nameof(SettingsTransfer.Apply));
    }

    [Fact]
    public void S498_an_import_takes_the_desktop_layers_memory_with_the_views_it_belongs_to()
    {
        var state = new AppState { Desktop = new DesktopState { Known = ["c:\\desktop\\old.txt"] } };
        var incoming = VariedState();

        SettingsTransfer.Apply(state, incoming);

        Assert.Equal(["c:\\desktop\\notes.txt"], state.Desktop.Known);
        Assert.Equal(DesktopSortBy.Size, state.Desktop.Sort);
    }
}
