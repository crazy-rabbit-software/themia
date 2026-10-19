using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-3718] [S-3719] The lines a module puts on its properties menu, as the SDK builds them.</summary>
public sealed class WidgetMenuLinesTests
{
    private int _saves;

    private WidgetMenuLines Lines() => new(() => _saves++, _ => { });

    [Fact]
    public void S3718_a_choice_is_a_submenu_of_one_group_with_the_current_option_marked()
    {
        var choice = Lines().Choice("g", "Open with", [(ItemClickMode.Double, "Double"), (ItemClickMode.Single, "Single")], ItemClickMode.Single, _ => { });

        Assert.Null(choice.Action);
        var options = choice.Children!;
        Assert.Equal(["Double", "Single"], options.Select(o => o.Text));
        Assert.Equal([false, true], options.Select(o => o.Checked == true));
        Assert.All(options, o => Assert.Equal("Open with", o.Group));
    }

    [Fact]
    public void S3718_picking_another_option_sets_it_and_saves_and_the_current_one_does_nothing()
    {
        var set = new List<ItemClickMode>();
        var options = Lines().Choice("g", "Open with", [(ItemClickMode.Double, "Double"), (ItemClickMode.Single, "Single")], ItemClickMode.Double, set.Add).Children!;

        options[0].Action!();
        Assert.Empty(set);
        Assert.Equal(0, _saves);

        options[1].Action!();
        Assert.Equal([ItemClickMode.Single], set);
        Assert.Equal(1, _saves);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S3719_a_switch_is_ticked_while_on_and_a_click_turns_it_the_other_way_and_saves(bool on)
    {
        bool? set = null;
        var toggle = Lines().Toggle(null, "Icons only", on, value => set = value);

        Assert.Equal(on, toggle.Checked);
        Assert.Null(toggle.Group);
        toggle.Action!();
        Assert.Equal(!on, set);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S3719_a_module_without_lines_adds_none()
    {
        var lines = Lines();
        ((IWidgetModule)new NoLines()).MenuLines(lines, new ShortcutsConfig());

        Assert.Empty(lines.Lines);
        Assert.Empty(lines.StyleLines);
    }

    private sealed class NoLines : IWidgetModule
    {
        public Themia.Sdk.WidgetDescriptor Descriptor => throw new NotSupportedException();

        public Microsoft.UI.Xaml.FrameworkElement CreateView(IWidgetHost host) => throw new NotSupportedException();
    }
}
