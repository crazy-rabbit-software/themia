using Microsoft.UI.Xaml.Controls;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-9504] [S-9510] What an own icon knows of its target beyond drawing it: whether a local path is
/// still there (looked at once a minute on the thread pool, never a network path) and the Recycle Bin's
/// state while it is the bin. [S-5610] And the tip of both sources, Explorer's kind of tip: a desktop
/// item's type, size and date (asked of the host when the pointer arrives), an own icon's location by
/// its name, and nothing at all where there is nothing to say.
/// </summary>
internal sealed partial class DesktopItemView
{
    /// <summary>How often a local target is looked at again for the broken state ([S-9504]).</summary>
    private static readonly TimeSpan HealthInterval = TimeSpan.FromMinutes(1);

    /// <summary>An own icon's target as classified; default for a desktop item and a layout.</summary>
    private LaunchTarget _target;
    private IDisposable? _healthTick;
    private IDisposable? _binWatch;
    private TargetHealth _health;
    private int _healthGeneration;

    /// <summary>[S-5610] What the host read of a desktop item for its tip, and the newest ask (an answer to an older one is dropped).</summary>
    private DesktopItemFacts? _facts;
    private int _factsRequest;

    private bool IsBin => Own && !_config.IsLayout && OwnIconRules.IsRecycleBin(_config.Target);

    /// <summary>Whether an own icon's target is one the rule refuses (a layout has none; one that opens nothing yet waits for setup instead).</summary>
    private bool Refused => Own && !_config.IsLayout && !_target.IsValid && !string.IsNullOrWhiteSpace(_config.Target);

    // ---- the checks ([S-9504], [S-9510]) -----------------------------------------------------------

    private void UpdateSubscriptions()
    {
        if (!_attached || _ticker is null)
        {
            return;
        }

        _healthTick?.Dispose();
        _healthTick = null;
        _healthGeneration++;

        if (_target.Kind == LaunchKind.LocalPath && LaunchTargets.MayProbe(_target))
        {
            // Fires once now, then every minute: a target deleted while the widget is up shows as broken
            // within the minute, and nothing is looked at per frame.
            _healthTick = _ticker.Subscribe(HealthInterval, CheckHealth);
        }

        WatchBin(IsBin);
    }

    /// <summary>[S-9510] Hold the host's Recycle Bin answer while the shortcut is the bin, and let go of it otherwise.</summary>
    private void WatchBin(bool watch)
    {
        if (watch && _binWatch is null)
        {
            _host.RecycleBin.Changed += OnBinChanged;
            _binWatch = _host.RecycleBin.Watch();
        }
        else if (!watch && _binWatch is not null)
        {
            _host.RecycleBin.Changed -= OnBinChanged;
            _binWatch.Dispose();
            _binWatch = null;
        }
    }

    /// <summary>[S-9510] The bin changed: its tooltip says how much it holds, and its icon is the empty or the full one.</summary>
    private void OnBinChanged()
    {
        if (!_attached || !IsBin)
        {
            return;
        }

        PaintTip();
        if (BinVariant() != _iconVariant)
        {
            _ = LoadIconAsync();
        }
    }

    /// <summary>[S-9504] Look at the local target on the thread pool (an existence check on a sleeping drive blocks), then repaint when the answer changed.</summary>
    private void CheckHealth()
    {
        if (_target.Kind == LaunchKind.LocalPath && LaunchTargets.MayProbe(_target))
        {
            _ = CheckHealthAsync(_target, ++_healthGeneration);
        }
    }

    private async Task CheckHealthAsync(LaunchTarget target, int generation)
    {
        TargetHealth health;

        try
        {
            health = await Task.Run(() => OwnIconRules.Check(target, Probe)).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not check whether a desktop icon's target is still there", ex);
            return;
        }

        if (!_attached || generation != _healthGeneration || health == _health)
        {
            return;
        }

        _health = health;
        PaintHealth();
    }

    /// <summary>Whether a local path is there, and what it is. Never throws; unclear is Unknown.</summary>
    private static TargetHealth Probe(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                return TargetHealth.Folder;
            }

            return File.Exists(path) ? TargetHealth.File : TargetHealth.Missing;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.Debug(Scope, $"could not tell whether a desktop icon's target exists: {ex.Message}");
            return TargetHealth.Unknown;
        }
    }

    /// <summary>[S-9504] A missing local target and a refused one are faded (the desktop's cut look), a missing one's name struck through.</summary>
    private void PaintHealth()
    {
        var missing = _health == TargetHealth.Missing;
        _stack.Opacity = missing || Refused ? SelectionLook.Faded : 1;
        _hang.Opacity = _stack.Opacity;
        _name.Strikethrough = missing;
        PaintTip();
    }

    /// <summary>
    /// [S-5610] The pointer arrived on a desktop item: what the shell says of it is asked of the host
    /// (off the UI thread, once per arrival, never in a preview), and the tip follows the answer.
    /// </summary>
    private async Task LoadFactsAsync()
    {
        if (Own || !Acts || Item is not { } item)
        {
            return;
        }

        var request = ++_factsRequest;
        try
        {
            var facts = await _host.DesktopItems.FactsAsync(item).ConfigureAwait(true);
            if (_attached && request == _factsRequest)
            {
                _facts = facts;
                PaintTip();
            }
        }
        catch (Exception ex)
        {
            // A tip is a courtesy: without the facts it says less, or nothing.
            Log.Debug(Scope, $"a desktop item's facts could not be read for its tip: {ex.Message}");
        }
    }

    /// <summary>
    /// [S-5610] The tip, Explorer's kind (<see cref="DesktopTip"/>): a desktop item its type, size and
    /// date (a shortcut its location); an own icon "Location: " and its target's own name, never the raw
    /// <c>shell:</c> or <c>ms-settings:</c> string; a name cut short leads it in full; and no tip at all
    /// where the name is shown whole and there is nothing more to say. A layout says its apps, the bin
    /// how much it holds ([S-9510]), a missing target that it is missing ([S-9504]), a refused one why.
    /// </summary>
    private void PaintTip()
    {
        var name = ShownName;

        // [S-13974] Icon only draws no name at rest: its tip always leads with it.
        var cut = DesktopCell.TipNames(_layout, _name.IsTrimmed);
        string? tip;

        if (!Own)
        {
            tip = Item is { } item ? DesktopTip.Item(Strings.Current, item, _facts, cut) : null;
        }
        else if (_config.IsLayout)
        {
            tip = cut ? $"{name}\n{LayoutPlan.Summary(_config.Layout)}" : LayoutPlan.Summary(_config.Layout);
        }
        else if (string.IsNullOrWhiteSpace(_config.Target))
        {
            tip = null;
        }
        else if (!_target.IsValid)
        {
            // A refused target says what is wrong rather than disappearing: an icon that changed
            // under the user is the one worth seeing.
            tip = $"{_config.Target}\n{LaunchTargets.RefusalMessage(_target.Refusal)}";
        }
        else if (IsBin)
        {
            tip = RecycleBinState.Tooltip(name, _host.RecycleBin.Current);
        }
        else if (_health == TargetHealth.Missing)
        {
            tip = DesktopItemStr.WidgetsC_Shortcuts_Tooltip_Missing.Format(("name", name), ("target", _config.Target));
        }
        else
        {
            tip = DesktopTip.Own(Strings.Current, name, _target, cut);
        }

        ToolTipService.SetToolTip(this, tip);
    }
}
