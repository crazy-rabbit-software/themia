using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.RecycleBin;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4860] How full the Recycle Bin is, on the family's blocks: its size as a Readout with the item
/// count under it, and Empty as the one main action in the Controls row at the bottom inset, which
/// hands the emptying to Windows, confirmation and all: all three always there, at one fixed size
/// ([S-11201]); an empty bin reads "0 bytes", "No items", with Empty disabled. A click
/// elsewhere on the tile opens the bin. The answer is the host's one answer
/// (<see cref="IWidgetHost.RecycleBin"/>) the Shortcuts badge reads, on its cadence: one query for
/// every widget showing the bin.
/// </summary>
internal sealed partial class RecycleBinView : Grid, IWidgetView, IWidgetOpens, IWidgetSurfaceAware, IWidgetContextActions
{
    /// <inheritdoc />
    public CardOpen? Opens => RecycleBinModule.Opens;

    private readonly ReadoutBlock _size = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly ControlsBlock _controls = new();
    private readonly Button _empty;

    private IWidgetSurface? _surface;
    private IDisposable? _binWatch;
    private bool _detached;

    /// <summary>The answer drawn: the shared one, which a preview replaces.</summary>
    private Func<RecycleBinState?> _state;

    /// <summary>[S-6913] What the view asks of the host: the launcher, the Recycle Bin.</summary>
    private readonly IWidgetHost _widgetHost;

    public RecycleBinView(IWidgetHost host)
    {
        _widgetHost = host;
        _state = () => host.RecycleBin.Current;
        _size.Margin = Family.SideInset;
        _empty = _controls.Main(RecycleBinStr.RecycleBin_Empty.Text(), () => _ = EmptyAsync());
        _controls.VerticalAlignment = VerticalAlignment.Bottom;
        _controls.Margin = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        _size.Use(FamilyType.Big28);

        Children.Add(_size);
        Children.Add(_controls);

        // The whole tile is the way into the bin, as its icon on the desktop is.
        Background = Family.Brush(ColourRole.Clear);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        Strings.Changed += OnLanguage;
        _widgetHost.RecycleBin.Changed += Render;
        _binWatch ??= _widgetHost.RecycleBin.Watch();
        Render();
    }

    public void ApplyConfig(WidgetConfig config) => Render();

    /// <summary>[S-4863] Open the bin, and Empty: the same command as the button, from the card's menu too.</summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
    [
        new(Glyphs.Open, RecycleBinStr.RecycleBin_Open.Text(), OpenBin),
        new(Glyphs.RecycleBin, RecycleBinStr.RecycleBin_EmptyEllipsis.Text(), () => _ = EmptyAsync()),
    ];

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        Strings.Changed -= OnLanguage;
        _widgetHost.RecycleBin.Changed -= Render;
        _binWatch?.Dispose();
        _binWatch = null;
    }

    private void OnLanguage(Locale locale) => Render();

    /// <summary>[S-4861] [S-11201] The size, its count and Empty, always; Empty disabled while there is nothing to empty.</summary>
    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var locale = Strings.Current;
        var state = _state();
        ToolTipService.SetToolTip(this, RecycleBinState.Tooltip(locale, RecycleBinStr.WidgetsC_Location_RecycleBin.Text(), state));

        if (_empty.Content is TextBlock label)
        {
            label.Text = RecycleBinStr.RecycleBin_Empty.Text();
        }

        var size = ReadoutRules.Split(RecycleBinLayout.SizeText(locale, state));
        _size.Show(size.Value, size.Unit, RecycleBinLayout.CountText(locale, state) ?? string.Empty);

        // Empty is always there; it works only when there is something to empty, and not twice at once (process-wide).
        _empty.IsEnabled = RecycleBinState.CanEmpty(state, _widgetHost.RecycleBin.Emptying);
    }

    /// <summary>[S-4863] Windows asks; the widget never empties on its own say. The shared status guards and redraws.</summary>
    private Task EmptyAsync()
        => _detached || _surface?.IsPreview == true ? Task.CompletedTask : _widgetHost.RecycleBin.EmptyAsync();

    private void OpenBin()
    {
        if (_surface?.IsPreview != true)
        {
            _widgetHost.Launcher.Open(RecycleBinState.Target);
        }
    }
}
