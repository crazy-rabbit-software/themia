using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.App.Widgets.Details;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.DriveFacts;

/// <summary>
/// [S-14670] One kind of the drive facts family: one of a drive's facts as a single metric (<see cref="MetricModule"/>),
/// at home in its family (<see cref="AddFolderId.DriveFacts"/>); the card is its drive on the view's links ([S-11950]).
/// SPEC.md beside this file.
/// </summary>
public abstract class DriveMetricModule(string key)
    : MetricModule(DriveKinds.Of(key), DriveFactsJsonContext.Default, AddFolderId.DriveFacts, DriveKinds.Rank(key), LinkUse.Of([LinkTopicId.HoveredDrive], [LinkTopicId.HoveredDrive]))
{
    /// <summary>[S-4674] Which drive (the drive atoms' shared line).</summary>
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config) => Lines(menu, (DriveMetricConfig)config, DriveList.Now());

    internal static void Lines(WidgetMenuLines menu, DriveMetricConfig config, IReadOnlyList<DriveEntry> drives)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        menu.Lines.Add(DriveMenuLines.Drive(menu, config.Drive, value => config.Drive = value, drives));
    }

    public override FrameworkElement CreateView(IWidgetHost host) => new DriveMetricView(host, Kind);
}

/// <summary>[S-14670] What one drive fact card remembers: its drive (empty: the system drive) and its trend.</summary>
public abstract class DriveMetricConfig(string key) : MetricConfig, IDriveChoice
{
    /// <summary>The fact its kind shows.</summary>
    [JsonIgnore]
    public string Key { get; } = key;

    /// <inheritdoc />
    protected override MetricKind Metric => DriveKinds.Of(Key);

    /// <summary>The drive's root (<c>C:\</c>); empty for the system drive.</summary>
    public string Drive { get; set; } = string.Empty;


    public override WidgetConfig Clone()
    {
        var copy = (DriveMetricConfig)DriveKinds.Of(Key).Create();
        CopyTo(copy);
        copy.Drive = Drive;
        return copy;
    }

    /// <summary>Anything that is not a drive letter is the system drive again, loudly; the trend as every metric's.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());
        Drive ??= string.Empty;
        if (Drive.Length > 0)
        {
            if (DiskRules.Root(Drive) is { } root)
            {
                Drive = root;
            }
            else
            {
                notes.Add($"{Kind} named '{Drive}', which is not a drive; showing the system drive");
                Drive = string.Empty;
            }
        }

        return notes;
    }
}

/// <summary>
/// [S-14670] A drive's one fact on the single metric's face (a Header widget over a group names the drive, [S-12481]); a
/// drive without figures keeps its row, "--" dimmed. The drives come from the host's one walk (<see cref="IDrives"/>), as
/// the Drive reading's; each walk is a new reading (an arrow compares the last two).
/// </summary>
internal sealed partial class DriveMetricView : MetricView, IWidgetSurfaceAware, ISystemMetricHost
{
    private readonly IWidgetHost _host;
    private readonly MetricKind _kind;

    /// <summary>
    /// [S-11950] The card is its drive on the view's links: the pointer on it publishes the drive
    /// (<see cref="LinkTopics.HoveredDrive"/>), and the whole card takes the item layer's hover fill while the drive is
    /// pointed at elsewhere ([S-11903]).
    /// </summary>
    private readonly WidgetLinks _links = new();
    private LinkCard<DriveLink>? _card;
    private IWidgetSurface? _surface;
    private IDisposable? _watch;
    private DriveMetricConfig _config;
    private long _reading;

    /// <summary>[S-11850] A sample's or a render's own drive, in place of the walk.</summary>
    private DriveEntry? _previewDrive;

    /// <summary>
    /// [S-14714] The activity's reading and graph: the hub's series of the card's drive through the generic part (a rate,
    /// a fold its peak), the drive's links the card's as every drive fact's; null for every other fact (the drives' walk).
    /// </summary>
    private readonly SeriesMetricPart? _activity;

    public DriveMetricView(IWidgetHost host, MetricKind kind)
        : base(host, kind)
    {
        _host = host;
        _kind = kind;
        _config = (DriveMetricConfig)kind.Create();
        _activity = kind.Key == "activity" ? new SeriesMetricPart(Formatting.Rate) : null;
    }

    /// <inheritdoc />
    protected override IReadOnlyList<WidgetAction> OwnActions
        => _activity?.Refresh is { } refresh ? [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), refresh)] : [];

    /// <inheritdoc />
    void ISystemMetricHost.Present(MetricShown shown)
    {
        Card.Key = LinkKeys.DriveLink(DiskRules.Root(_config.Drive) ?? SystemDrive.Root);
        Present(shown);
    }

    /// <summary>[S-14714] The activity watches its drive's series (the hub's, the disk meter's own).</summary>
    private void WatchActivity()
    {
        if (_activity is not { } activity)
        {
            return;
        }

        if (_host.System is { } hub)
        {
            activity.Watch(hub, _config, this);
        }
        else
        {
            Present(new MetricShown(MetricValue.Unread) { Why = Str.WidgetsB_Meters_Unavailable.Text() });
        }
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    private LinkCard<DriveLink> Card => _card ??= new LinkCard<DriveLink>(_links, LinkTopics.HoveredDrive, Item);

    protected override void OnAttach(Ticker ticker, WidgetConfig config)
    {
        _config = config as DriveMetricConfig ?? _config;
        _watch?.Dispose();
        _watch = _activity is null
            ? _host.Drives.Watch(ticker, () =>
            {
                _reading++;
                Show();
            })
            : null;
        _links.Join(_surface?.Links);
        Card.Publishes(this);
        WatchActivity();
        Show();
    }

    protected override void OnApply(WidgetConfig config)
    {
        var own = config as DriveMetricConfig ?? _config;
        if (!string.Equals(own.Drive, _config.Drive, StringComparison.OrdinalIgnoreCase))
        {
            Restart();
        }

        _config = own;
        WatchActivity();
        Show();
    }

    protected override void OnDetach()
    {
        _activity?.Detach();
        _watch?.Dispose();
        _watch = null;
        Card.Release();
        _links.Leave();
    }

    private void Show()
    {
        if (_activity is not null || (_previewDrive is null && !_host.Drives.HasValue))
        {
            return;
        }

        var locale = Strings.Current;
        var drive = _previewDrive ?? DriveRules.Pick(_host.Drives.Drives, _config.Drive, SystemDrive.Root);
        Card.Key = LinkKeys.DriveLink(drive.Root);
        Present(MetricRules.Pick(DriveRules.Sheet(locale, drive), _kind.Key) with { Reading = _reading });
    }
}

/// <summary>[S-6903] The family's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DriveActivityConfig))]
[JsonSerializable(typeof(DriveUsedConfig))]
[JsonSerializable(typeof(DriveFreeConfig))]
[JsonSerializable(typeof(DriveCapacityConfig))]
[JsonSerializable(typeof(DriveFileSystemConfig))]
[JsonSerializable(typeof(DriveTypeConfig))]
internal sealed partial class DriveFactsJsonContext : JsonSerializerContext;
