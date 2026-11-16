using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-11850] [S-9513] The desktop icon's sample, the add page's picture of its tile: an own icon on
/// Windows' own Documents location, which every PC has, so the shell draws the same icon on every
/// machine and nothing is fetched. Applied to this view alone, never saved; never an item of the
/// user's desktop.
/// </summary>
internal sealed partial class DesktopItemView : IWidgetSample
{
    /// <summary>[S-5605] A document named on no disk, so the shell draws its type's icon: the desktop item a preview draws.</summary>
    internal static Themia.Sdk.DesktopItem SampleDocument => new(@"C:\Users\Public\Desktop\Report.docx", "Report.docx", DesktopItemKind.File);

    public async Task ShowSampleAsync()
    {
        ShowOwn(config => config.Target = LaunchTargets.ShellTarget("Personal"));

        // A picture is taken at once: the location's icon (the shell's, cached once fetched) is in it.
        await _iconLoad.ConfigureAwait(true);
    }

    /// <summary>Draw an own icon: a copy of the card's config (its name lines, its click) changed by <paramref name="set"/>; nothing is stored.</summary>
    private void ShowOwn(Action<DesktopItemConfig> set)
    {
        var shown = new DesktopItemConfig { LabelLines = _config.LabelLines, ClickMode = _config.ClickMode };
        set(shown);
        _config = shown;
        _fixture.Rest(null);
        Changed();
    }
}
