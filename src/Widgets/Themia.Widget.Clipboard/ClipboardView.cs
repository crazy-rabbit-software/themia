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
using Themia.Widget.Clipboard;

namespace Themia.App.Widgets;

/// <summary>
/// What has been copied recently, most recent first, as a kit list ([S-7074]) shaped by <see cref="ClipboardSpec"/>. A
/// click on a row puts it back on the clipboard ([S-7075]).
/// <para>
/// This is the most sensitive widget in the app, and the design is mostly a list of things it does
/// not do. It reads <b>text only</b> — a copied file or image is never looked at. It keeps a
/// bounded history <b>in memory for the session</b> and writes nothing to disk, so closing the app
/// is the end of it; there is no persistence setting, because a clipboard history that survives a
/// restart is a credentials file nobody meant to create. Entries are <b>displayed, never
/// executed</b>: no row here is a launch target, whatever it contains — a link is labelled as a
/// link, never opened.
/// </para>
/// <para>
/// [S-1458] The history is the app's one (<see cref="IWidgetHost.ClipboardHistory"/>), kept while any view
/// holds a Clipboard widget, fed by the host's clipboard listener (a message-only window with
/// <c>AddClipboardFormatListener</c>, never a poll). Clear history is the card menu's line ([S-12481]).
/// </para>
/// </summary>
internal sealed partial class ClipboardView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetKeys, IWidgetSizeAware, IWidgetContextActions
{
    private readonly IWidgetHost _host;
    private readonly KitBinding _binding;
    private readonly HistorySource _source;
    private ClipboardConfig _config = new();
    private IWidgetSurface? _surface;
    private bool _attached;

    public ClipboardView(IWidgetHost host)
    {
        _host = host;
        _source = new HistorySource(() => _config);
        _binding = new KitBinding(host, ClipboardSpec.Spec, ClipboardModule.Glyph, item => ClipboardSpec.Actions(_source.TextOf(item.Id)), Run)
        {
            Look = new KitRichLook { Lead = item => Marker(item.Lead.Value) },
        };

        Children.Add(_binding.List);
        Children.Add(_binding.State);
        _binding.Status.Place(this);
    }

    /// <summary>[S-1500] [S-12481] Clear history, on the card menu while there is something to clear.</summary>
    public IReadOnlyList<WidgetAction> ContextActions
        => _attached && _source.Count > 0 ? [new WidgetAction(Glyphs.Delete, ClipboardStr.Clipboard_ClearHistory.Text(), () => _ = ConfirmClearAsync())] : [];

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as ClipboardConfig ?? new ClipboardConfig();
        _attached = true;

        // [S-1458] The app's one history, shared by every Clipboard widget on every view. [S-1443] Never for a preview (a
        // debug render): a render's PNG must not carry whatever happens to be on the user's clipboard.
        _source.Start(_surface?.IsPreview == true ? null : _host.ClipboardHistory, _surface?.IsPreview == true);

        // The tick fetches nothing: it keeps the ages ("4m", "2h") honest, the one thing here that goes stale on its own.
        _binding.Attach(ticker, _source, _config, _config.TickInterval);
    }

    /// <summary>
    /// A lower capacity shows fewer entries at once; the shared history drops what no Clipboard widget keeps any more
    /// when the state saves the setting ([S-1458]): a shorter history is the one setting that actually reduces exposure.
    /// </summary>
    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ClipboardConfig ?? new ClipboardConfig();
        if (_attached)
        {
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
    }

    /// <summary>[S-1444] Remove: the history raises the change, and every Clipboard widget redraws from it.</summary>
    private void Run(KitItem item, KitAction action)
    {
        if (action.Id == ClipboardSpec.RemoveId && _source.TextOf(item.Id) is { } text)
        {
            _source.Remove(text);
        }
    }

    /// <summary>
    /// [S-1442] What the entry looks like it is: the tick for what is on the clipboard now, a dot of a colour's own
    /// colour (a data colour, [S-6705]), else its kind's glyph, all 16.
    /// </summary>
    private static FrameworkElement Marker(string? key)
    {
        if (key == ClipboardSpec.Current)
        {
            return Family.Glyph(Glyphs.Done);
        }

        if (key?.StartsWith(ClipboardSpec.ColourLead, StringComparison.Ordinal) == true)
        {
            return new Border
            {
                Width = FamilyShape.Icon,
                Height = FamilyShape.Icon,
                CornerRadius = Family.Round(FamilyShape.Icon, FamilyShape.Icon),

                // The user's own colour, which is the content here, not a design choice.
                Background = Family.Data(key[ClipboardSpec.ColourLead.Length..]),
            };
        }

        return Family.Glyph(Enum.TryParse<ClipboardContent>(key, out var kind) ? kind switch
        {
            ClipboardContent.Link => Glyphs.Link,
            ClipboardContent.Email => Glyphs.Mail,
            ClipboardContent.Number => Glyphs.Calculator,
            ClipboardContent.Lines => Glyphs.Note,
            _ => Glyphs.Copy,
        } : Glyphs.Copy);
    }

    private async Task ConfirmClearAsync()
    {
        if (!_attached || _source.Count == 0)
        {
            return;
        }

        if (await AskToClearAsync().ConfigureAwait(true) && _attached)
        {
            // Every Clipboard widget shows the one history, so all of them are cleared.
            _source.Clear();
        }
    }

    /// <summary>[S-1500] The widgets' one confirmation, beside the widget.</summary>
    private Task<bool> AskToClearAsync() => ConfirmFlyout.AskAsync(
        this,
        ClipboardStr.WidgetsA_Clipboard_ClearConfirm_Title.Text(),
        Str.Common_Clear.Text(),
        ClipboardStr.WidgetsA_Clipboard_ClearConfirm_Body.Text());

    /// <summary>
    /// [S-1458] The shared history as a kit source: its newest entries, as many as this widget keeps; nothing copied yet
    /// is the spec's empty sentence, a listener that never started the state's problem ([S-6714]: they need very
    /// different things).
    /// </summary>
    private sealed class HistorySource(Func<ClipboardConfig> config) : IKitSource
    {
        private IClipboardHistory? _history;
        private bool _preview;

        /// <summary>[S-11850] The sample's fixed entries in place of the history ([S-1917]); never the user's clipboard.</summary>
        private IReadOnlyList<ClipboardEntry>? _sample;

        public event Action? Changed;

        public bool Busy => false;

        public int Count => Entries.Count;

        private IReadOnlyList<ClipboardEntry> Entries
        {
            get
            {
                var all = _sample ?? _history?.Entries ?? [];
                var kept = config().EffectiveCapacity;
                return all.Count <= kept ? all : [.. all.Take(kept)];
            }
        }

        public void Start(IClipboardHistory? history, bool preview)
        {
            _preview = preview;
            if (_history is null && history is not null)
            {
                _history = history;
                _history.Changed += OnChanged;
            }
        }

        public void Stop()
        {
            if (_history is not null)
            {
                _history.Changed -= OnChanged;
                _history = null;
            }
        }

        public void Show(bool refresh)
        {
            // ignore: the history follows the listener; there is nothing to ask again.
        }

        public KitRead Read()
        {
            var entries = Entries;
            var locale = Strings.Current;
            if (entries.Count == 0 && !_preview && _sample is null && _history?.Listening != true)
            {
                return new KitRead([], [], Stated: new KitStated(ClipboardStr.WidgetsA_Clipboard_NoListener.Text(locale)));
            }

            return new KitRead([.. entries.Select((entry, i) => ClipboardSpec.Item(entry, i == 0, locale))], [new KitOutcome(null, FetchStatus.Ok)]);
        }

        /// <summary>The text of the entry shown under <paramref name="id"/>, or null for one no longer held.</summary>
        public string? TextOf(string id)
        {
            foreach (var entry in Entries)
            {
                if (ClipboardSpec.Id(entry.Text) == id)
                {
                    return entry.Text;
                }
            }

            return null;
        }

        public void Remove(string text)
        {
            if (_sample is null)
            {
                _history?.Remove(text);
            }
        }

        public void Clear()
        {
            if (_sample is null)
            {
                _history?.Clear();
            }
        }

        public void HoldForPreview(IReadOnlyList<ClipboardEntry> sample)
        {
            _sample = sample;
            Changed?.Invoke();
        }

        private void OnChanged() => Changed?.Invoke();
    }
}
