using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Bluetooth;

namespace Themia.App.Widgets;

/// <summary>
/// The paired Bluetooth devices, as a Windows 11 list: a kit list ([S-7074]) over the host's one pass over the radios
/// (<see cref="IWidgetHost.Bluetooth"/>, [S-4474]: every Bluetooth widget shares it, a connected device's battery the
/// host's one battery inventory's), shaped by <see cref="BluetoothSpec"/>.
/// <para>
/// <b>Read-only.</b> Nothing here connects, disconnects or unpairs. A surface that sits under every other window, with
/// no confirmation and no undo, is the wrong place to drop a headset mid-call.
/// </para>
/// <para>
/// Every empty list says which empty list it is ([S-244], [S-7076]): no radio, the radio off, access refused, a failed
/// pass (with Try again), nothing paired. The ring over the content's top end turns while a pass is out ([S-12481]).
/// </para>
/// </summary>
internal sealed partial class BluetoothView : Grid, IWidgetView, IWidgetOpens, IWidgetContextActions, IWidgetSizeAware, IWidgetKeys
{
    /// <inheritdoc />
    public CardOpen? Opens => BluetoothModule.Opens;

    // Segoe Fluent Icons code points this view needs beyond the shared table.
    private static readonly string[] KindGlyphs =
    [
        From(0xE702), // unknown  - Bluetooth
        From(0xE7F6), // audio    - Headphone
        From(0xE962), // mouse    - Mouse
        From(0xE765), // keyboard - KeyboardClassic
        From(0xE8EA), // phone    - CellPhone
        From(0xE7FC), // gamepad  - Game
        From(0xE929), // pen      - PenWorkspace
        From(0xE916), // watch    - Stopwatch
    ];

    private readonly KitBinding _binding;
    private readonly DeviceSource _source;
    private BluetoothConfig _config = new();
    private bool _attached;

    public BluetoothView(IWidgetHost host)
    {
        _source = new DeviceSource(host.Bluetooth, () => _config);
        _binding = new KitBinding(host, BluetoothSpec.Spec, Glyphs.Bluetooth, BluetoothSpec.Actions)
        {
            Look = new KitRichLook { Lead = KindGlyph },
            Decorate = LinkRow,
            Redrawing = ForgetRows,
        };

        Children.Add(_binding.List);
        Children.Add(_binding.State);
        _binding.Status.Place(this);
    }

    /// <summary>[S-1259] Refresh: read the radios again now.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => _binding.CardActions;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as BluetoothConfig ?? new BluetoothConfig();
        _attached = true;
        _links.Join(_surface?.Links);
        _source.Start(ticker, _config.ShowSignal);
        _binding.Attach(ticker, _source, _config, _config.TickInterval);
    }

    /// <summary>[S-13a] The signal switch changes what is read off the radio; every other line redraws what is in hand.</summary>
    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as BluetoothConfig ?? new BluetoothConfig();
        if (_attached)
        {
            _source.Start(null, _config.ShowSignal);
            _binding.Apply(_source, _config, _config.TickInterval, show: false);
        }
    }

    public void ApplySize(WidgetArea area) => _binding.Size(area);

    public bool HandleKey(WidgetKey key) => _binding.HandleKey(key);

    public void Detach()
    {
        _attached = false;
        _binding.Dispose();
        _source.Stop();
        _links.Leave();
    }

    /// <summary>
    /// [S-1257] [S-3961] The kind's glyph, the original's "type" metric: its colour is the state, primary connected,
    /// secondary paired; wrapped, so the row's own lead role does not repaint it.
    /// </summary>
    private static FrameworkElement? KindGlyph(KitItem item)
    {
        if (item.Lead.Value?.Split(':') is not [{ } kind, { } state] || !Enum.TryParse<BluetoothDeviceKind>(kind, out var parsed))
        {
            return null;
        }

        var glyph = Family.Glyph(KindGlyphs[(int)parsed], FamilyShape.Icon, state == "on" ? ColourRole.TextPrimary : ColourRole.TextSecondary);
        return new Grid { VerticalAlignment = VerticalAlignment.Center, Children = { glyph } };
    }

    private static string From(int codePoint) => char.ConvertFromUtf32(codePoint);

    /// <summary>[S-4474] The host's pass over the radios as a kit source: the devices arranged by the widget's rules, every empty state worded.</summary>
    private sealed class DeviceSource(IBluetoothDevices devices, Func<BluetoothConfig> config) : IKitSource
    {
        private IDisposable? _watch;
        private bool _signal;

        /// <summary>[S-11850] [S-1917] A sample's or a preview's devices in place of the radio's.</summary>
        private BluetoothSnapshot? _held;

        public event Action? Changed;

        public bool Busy => _held is null && devices.Busy;

        private BluetoothSnapshot Snapshot => _held ?? (_watch is null ? BluetoothSnapshot.Of(BluetoothStatus.Loading) : devices.Last);

        /// <summary>Watch the radios (again where the signal wanted changed); <paramref name="ticker"/> null keeps the one in use.</summary>
        public void Start(Ticker? ticker, bool signal)
        {
            if (_held is not null || (_watch is not null && signal == _signal))
            {
                return;
            }

            _ticker = ticker ?? _ticker;
            if (_ticker is null)
            {
                return;
            }

            _watch?.Dispose();
            _signal = signal;
            _watch = devices.Watch(_ticker, signal, () => Changed?.Invoke());
        }

        private Ticker? _ticker;

        public void Stop()
        {
            _watch?.Dispose();
            _watch = null;
        }

        public void Show(bool refresh)
        {
            if (refresh && _held is null)
            {
                devices.Refresh();
            }
        }

        public KitRead Read()
        {
            var snapshot = Snapshot;
            var locale = Strings.Current;
            switch (snapshot.Status)
            {
                case BluetoothStatus.Loading:
                    return new KitRead([], [new KitOutcome(null, FetchStatus.Loading)]);

                case BluetoothStatus.Failed:
                    // [S-244] A failed pass is a problem, and asking again may mend it.
                    return new KitRead([], [], Stated: new KitStated(BluetoothRules.StatusMessage(snapshot.Status, locale))
                    {
                        Fix = new KitFix(Strings.Get(locale, Str.Common_Retry), () => Show(refresh: true)),
                    });

                case not BluetoothStatus.Ok:
                    return new KitRead([], [], Stated: new KitStated(BluetoothRules.StatusMessage(snapshot.Status, locale), Problem: snapshot.Status == BluetoothStatus.PermissionDenied));
            }

            var settings = config();
            var arranged = BluetoothRules.Arrange(snapshot.Devices, settings.ConnectedOnly, locale);
            return new KitRead([.. arranged.Select(device => BluetoothSpec.Item(device, settings, locale))], [new KitOutcome(null, FetchStatus.Ok)]);
        }

        public void HoldForPreview(BluetoothSnapshot snapshot)
        {
            Stop();
            _held = snapshot;
            Changed?.Invoke();
        }
    }
}
