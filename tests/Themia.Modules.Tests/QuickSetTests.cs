using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The selected widgets' properties strip ([S-3701]..[S-3705], [S-3711]).</summary>
public sealed class QuickSetTests
{
    private static WidgetInstance Widget(WidgetId kind, string id)
    {
        var widget = WidgetInstance.Create(kind, new FracRect(0, 0, 0.1, 0.1));
        widget.Id = id;
        return widget;
    }

    private static (AppState State, ViewState View, ViewState Other) State(params WidgetInstance[] widgets)
    {
        var view = ViewState.Create("v");
        view.Widgets.AddRange(widgets);
        var other = ViewState.Create("o");
        other.Widgets.Add(Widget(CpuConfig.Descriptor.Id, "elsewhere"));
        return (new AppState { Views = [view, other] }, view, other);
    }

    private static WidgetSelection Selecting(ViewState view, params string[] ids)
    {
        var selection = new WidgetSelection();
        selection.Replace(view.Id, ids);
        return selection;
    }

    [Fact]
    public void S3701_nothing_selected_shows_no_group()
    {
        var (state, view, _) = State(Widget(CpuConfig.Descriptor.Id, "a"));

        Assert.False(QuickSet.For(state, new WidgetSelection(), view.Id).Shows);
    }

    [Fact]
    public void S3701_a_display_showing_another_view_shows_no_group()
    {
        var (state, view, other) = State(Widget(CpuConfig.Descriptor.Id, "a"));

        Assert.False(QuickSet.For(state, Selecting(view, "a"), other.Id).Shows);
        Assert.True(QuickSet.For(state, Selecting(view, "a"), view.Id).Shows);
    }

    [Fact]
    public void S3701_ids_no_longer_in_the_view_are_not_counted()
    {
        var (state, view, _) = State(Widget(CpuConfig.Descriptor.Id, "a"));

        var offer = QuickSet.For(state, Selecting(view, "a", "gone"), view.Id);

        Assert.Equal(1, offer.Count);
        Assert.Equal("a", offer.TitleWidgetId);
        Assert.False(QuickSet.For(state, Selecting(view, "gone"), view.Id).Shows);
    }

    [Fact]
    public void S3702_one_widget_offers_header_background_and_title()
    {
        var clock = Widget(new WidgetId("clock"), "a");
        clock.Header = HeaderPlacement.Bottom;
        clock.Background = FillChoice.Of("#202020", 0.6);
        clock.Label = "Zürich";
        var (state, view, _) = State(clock);

        var offer = QuickSet.For(state, Selecting(view, "a"), view.Id);

        Assert.True(offer.HasTitle);
        Assert.Equal(new WidgetId("clock"), offer.TitleKind);
        Assert.Equal("Zürich", offer.Label);
        Assert.Equal(HeaderPlacement.Bottom, offer.Header);
        Assert.True(FillChoice.Of("#202020", 0.6).SameAs(offer.Background));
    }

    [Fact]
    public void S3702_several_widgets_offer_no_title()
    {
        var (state, view, _) = State(Widget(CpuConfig.Descriptor.Id, "a"), Widget(MemoryConfig.Descriptor.Id, "b"));

        var offer = QuickSet.For(state, Selecting(view, "a", "b"), view.Id);

        Assert.Equal(2, offer.Count);
        Assert.False(offer.HasTitle);
        Assert.Null(offer.TitleKind);
    }

    [Fact]
    public void S3703_a_mixed_selection_reads_as_no_value_and_a_shared_one_as_that_value()
    {
        var a = Widget(CpuConfig.Descriptor.Id, "a");
        var b = Widget(MemoryConfig.Descriptor.Id, "b");
        a.Header = HeaderPlacement.Top;
        b.Header = HeaderPlacement.Hidden;
        a.Background = FillChoice.Transparent();
        b.Background = FillChoice.Transparent();
        var (state, view, _) = State(a, b);

        var offer = QuickSet.For(state, Selecting(view, "a", "b"), view.Id);

        Assert.Null(offer.Header);
        Assert.Equal(FillKind.Transparent, offer.Background?.Kind);
    }

    [Fact]
    public void S3703_common_of_nothing_is_nothing()
    {
        Assert.Null(QuickSet.Common(Array.Empty<HeaderPlacement>()));
        Assert.Equal(HeaderPlacement.Bottom, QuickSet.Common([HeaderPlacement.Bottom, HeaderPlacement.Bottom]));
    }

    [Fact]
    public void S3703_a_choice_reaches_every_selected_widget_of_the_view_and_no_other()
    {
        var a = Widget(CpuConfig.Descriptor.Id, "a");
        var b = Widget(MemoryConfig.Descriptor.Id, "b");
        var c = Widget(NetworkConfig.Descriptor.Id, "c");
        b.Background = FillChoice.Transparent();
        var (_, view, other) = State(a, b, c);

        Assert.Equal(1, QuickSet.SetBackground(view, ["a", "b", "elsewhere"], FillChoice.Transparent()));
        Assert.Equal(FillKind.Transparent, a.Background.Kind);
        Assert.Equal(FillKind.Default, c.Background.Kind);
        Assert.Equal(FillKind.Default, other.Widgets[0].Background.Kind);

        Assert.Equal(2, QuickSet.SetHeader(view, ["a", "b"], HeaderPlacement.Hidden));
        Assert.Equal(0, QuickSet.SetHeader(view, ["a", "b"], HeaderPlacement.Hidden));
        Assert.Equal(HeaderPlacement.Top, c.Header);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("Clock", null)]
    [InlineData("  Clock ", null)]
    [InlineData("clock", "clock")]
    [InlineData("  Zürich  ", "Zürich")]
    public void S3704_a_blank_title_or_the_kinds_name_means_the_kinds_name(string? typed, string? stored)
    {
        Assert.Equal(stored, QuickSet.Title(typed, "Clock"));
    }

    [Fact]
    public void S3704_set_title_reports_a_change_only_when_the_stored_title_moves()
    {
        var clock = Widget(new WidgetId("clock"), "a");
        var (_, view, _) = State(clock);

        Assert.True(QuickSet.SetTitle(view, "a", " Home ", "Clock"));
        Assert.Equal("Home", clock.Label);
        Assert.False(QuickSet.SetTitle(view, "a", "Home", "Clock"));
        Assert.True(QuickSet.SetTitle(view, "a", "", "Clock"));
        Assert.Null(clock.Label);
        Assert.False(QuickSet.SetTitle(view, "gone", "x", "Clock"));
    }

    [Fact]
    public void S3711_a_toggle_offers_no_header_choice()
    {
        var (state, view, _) = State(Widget(new WidgetId("wifiToggle"), "t"));

        var offer = QuickSet.For(state, Selecting(view, "t"), view.Id);

        Assert.True(offer.Shows);
        Assert.True(offer.HasTitle);
        Assert.False(offer.HasHeader);
        Assert.Null(offer.Header);
    }

    [Fact]
    public void S3711_a_selection_with_a_toggle_in_it_shows_only_what_all_share()
    {
        var (state, view, _) = State(Widget(CpuConfig.Descriptor.Id, "a"), Widget(new WidgetId("bluetoothToggle"), "t"));

        var offer = QuickSet.For(state, Selecting(view, "a", "t"), view.Id);

        Assert.False(offer.HasHeader);
        Assert.Equal(FillKind.Default, offer.Background?.Kind);
        Assert.True(QuickSet.For(state, Selecting(view, "a"), view.Id).HasHeader);
    }

    [Fact]
    public void S3711_setting_a_header_never_reaches_a_toggle()
    {
        var cpu = Widget(CpuConfig.Descriptor.Id, "a");
        var toggle = Widget(new WidgetId("wifiToggle"), "t");
        var (_, view, _) = State(cpu, toggle);

        Assert.Equal(1, QuickSet.SetHeader(view, ["a", "t"], HeaderPlacement.Bottom));
        Assert.Equal(HeaderPlacement.Bottom, cpu.Header);
        Assert.Equal(HeaderPlacement.Hidden, toggle.Header);
    }

    [Theory]
    [InlineData(AudioVisualizerStyle.Bars)]
    [InlineData(AudioVisualizerStyle.Mirrored)]
    [InlineData(AudioVisualizerStyle.Wave)]
    [InlineData(AudioVisualizerStyle.Led)]
    [InlineData(AudioVisualizerStyle.Radial)]
    [InlineData(AudioVisualizerStyle.Waveform)]
    public void S3711_no_audio_visualizer_style_can_have_a_header(AudioVisualizerStyle style)
    {
        Assert.False(WidgetInstance.CanHaveHeader(AudioVisualizerConfig.Descriptor.Id));
        var widget = Widget(AudioVisualizerConfig.Descriptor.Id, "v");
        ((AudioVisualizerConfig)widget.Config).Style = style;
        widget.Header = HeaderPlacement.Top;
        Assert.Equal(HeaderPlacement.Hidden, widget.ShownHeader);
    }

    [Fact]
    public void S3711_every_toggle_and_visualizer_kind_is_headerless_and_other_kinds_may_have_one()
    {
        foreach (var kind in ModuleCatalog.Widgets.Select(w => w.Id).ToArray())
        {
            var toggle = Themia.Core.Widgets.QuickToggleRules.ForKind(kind) is not null
                || kind == AudioVisualizerConfig.Descriptor.Id;
            Assert.Equal(!toggle, WidgetInstance.CanHaveHeader(kind));
            if (toggle)
            {
                var widget = Widget(kind, "t");
                Assert.Equal(HeaderPlacement.Hidden, widget.Header);
                widget.Header = HeaderPlacement.Top;
                Assert.Equal(HeaderPlacement.Hidden, widget.ShownHeader);
            }
        }

        var cpu = Widget(CpuConfig.Descriptor.Id, "a");
        cpu.Header = HeaderPlacement.Bottom;
        Assert.Equal(HeaderPlacement.Bottom, cpu.ShownHeader);
    }

    [Fact]
    public void S3711_load_validation_takes_the_header_off_a_toggle_loudly()
    {
        var state = AppState.CreateDefault();
        var toggle = WidgetInstance.Create(MonoAudioToggleConfig.Descriptor.Id, new FracRect(0.1, 0.1, 0.1, 0.1));
        toggle.Header = HeaderPlacement.Top;
        state.Views[0].Widgets.Add(toggle);

        var repairs = Themia.Core.Storage.AppStateStore.Validate(state);

        Assert.Equal(HeaderPlacement.Hidden, toggle.Header);
        Assert.Contains(repairs, r => r.Contains("cannot have a header", StringComparison.Ordinal));
    }
}
