using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-1917] The shortcuts' sample: as many items as the config holds (its size follows them,
/// [S-11202]) in its own style ([S-6006]), Windows' own places and folders (local locations only: no
/// favicon is fetched). The items are applied, never saved.
/// </summary>
internal sealed partial class ShortcutsView : IWidgetSample
{
    private static readonly string[] SampleShellNames =
    [
        "RecycleBinFolder", "MyComputerFolder", "Downloads", "Personal", "Desktop", "My Pictures",
        "My Music", "My Video", "UsersFilesFolder", "ControlPanelFolder", "NetworkPlacesFolder", "Profile",
    ];

    private static readonly string[] SamplePaths =
    [
        @"C:\Windows", @"C:\Program Files", @"C:\Users", @"C:\Windows\System32",
        @"C:\Windows\notepad.exe", @"C:\Windows\explorer.exe", @"C:\ProgramData", @"C:\Windows\Fonts",
    ];

    /// <summary>[S-11603] The config the sample (or a render's state) swapped in, which decides the card's size; null while it shows its own.</summary>
    private ShortcutsConfig? _sampleConfig;

    public Task ShowSampleAsync()
    {
        ShowSampleItems(_config.Style, Math.Max(1, _config.Items.Count));
        return Task.CompletedTask;
    }

    /// <summary>
    /// <paramref name="count"/> of the sample's places in <paramref name="style"/>, the card's own config
    /// otherwise; <paramref name="edit"/> changes the items before they are applied.
    /// </summary>
    private void ShowSampleItems(ShortcutsStyle style, int count, Action<List<ShortcutItem>>? edit = null)
    {
        var targets = SampleShellNames.Select(LaunchTargets.ShellTarget).Concat(SamplePaths).Take(count);
        var items = targets.Select(target => ShortcutItem.Create(target)).ToList();
        edit?.Invoke(items);

        var shown = (ShortcutsConfig)_config.Clone();
        shown.Style = style;
        shown.Items = items;
        ApplyConfig(shown);
        _sampleConfig = shown;
        UpdateLayout();
    }
}
