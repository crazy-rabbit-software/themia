using Themia.App.Services.Accounts;
using Themia.Core.Accounts;
using Themia.Core.Office;
using Themia.Sdk;

namespace Themia.App.Services.Office;

/// <summary>Which <see cref="IOfficeSource"/> widgets use right now.</summary>
internal static class OfficeSources
{
    /// <summary>
    /// The host's source, or null when there is none: the host has not started (or is shutting
    /// down). A widget treats that as "no account". Set by the host, which owns its lifetime.
    /// </summary>
    public static IOfficeSource? Current { get; internal set; }
}
