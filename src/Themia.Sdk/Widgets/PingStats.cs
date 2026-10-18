
using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>[S-1170] What the last probe said about the host.</summary>
public enum PingStatus
{
    /// <summary>No probe has come back yet.</summary>
    Checking = 0,

    /// <summary>The last probe was answered.</summary>
    Online = 1,

    /// <summary>The last probe timed out, was refused or could not be resolved.</summary>
    Offline = 2,
}

/// <summary>
/// [S-1170] [S-1172] The ping meter's status: what the last probe said, in words (the header dot's
/// tooltip) and as a colour.
/// </summary>
public static class PingStats
{
    /// <summary>[S-1170] The status the last probe gives: checking before any, then online or offline.</summary>
    public static PingStatus Status(bool anyProbe, bool lastAnswered)
        => !anyProbe ? PingStatus.Checking : lastAnswered ? PingStatus.Online : PingStatus.Offline;

    /// <summary>[S-1170] The status in words: the header dot's tooltip, and why a reading is dimmed.</summary>
    public static string StatusText(PingStatus status) => StatusText(Strings.Current, status);

    /// <summary><see cref="StatusText(PingStatus)"/> in <paramref name="locale"/>.</summary>
    public static string StatusText(Locale locale, PingStatus status) => Strings.Get(locale, status switch
    {
        PingStatus.Online => Str.WidgetsB_Ping_Online,
        PingStatus.Offline => Str.WidgetsB_Ping_Offline,
        _ => Str.WidgetsB_Ping_Checking,
    });

    /// <summary>[S-1172] What the meter's colour says: success while answering, critical while not.</summary>
    public static MeterPressure Pressure(PingStatus status) => status switch
    {
        PingStatus.Online => MeterPressure.Healthy,
        PingStatus.Offline => MeterPressure.Critical,
        _ => MeterPressure.Normal,
    };
}
