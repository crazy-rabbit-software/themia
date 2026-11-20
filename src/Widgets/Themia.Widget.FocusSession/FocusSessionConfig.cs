using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-4420] A focus session: a length, and whether it holds Windows' do not disturb while it runs.</summary>
public sealed class FocusSessionConfig : WidgetConfig
{
    /// <summary>[S-11305] Whether the time stands in the ring of the session gone; off for a new one.</summary>
    public bool Ring { get; set; }

    /// <summary>[S-4421] How long a session started here runs, in minutes (Windows' own default is 30).</summary>
    public int Minutes { get; set; } = FocusSessionRules.DefaultMinutes;

    /// <summary>[S-4423] Turn on Windows' do not disturb while the session runs, as Windows' Focus does.</summary>
    public bool DoNotDisturb { get; set; } = true;

    public override WidgetConfig Clone() => new FocusSessionConfig { Minutes = Minutes, DoNotDisturb = DoNotDisturb, Ring = Ring };

    /// <summary>A hand-edited length outside the range is pulled back into it, and said so.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();
        var clamped = FocusSessionRules.ClampMinutes(Minutes);
        if (clamped != Minutes)
        {
            repairs.Add($"focus session length {Minutes} min was outside {FocusSessionRules.MinMinutes}..{FocusSessionRules.MaxMinutes}; now {clamped}");
            Minutes = clamped;
        }

        return repairs;
    }
}
