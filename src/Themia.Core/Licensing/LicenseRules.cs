using System.Globalization;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Licensing;

/// <summary>What the running copy may do. [S-1064]</summary>
public enum LicenseTier
{
    /// <summary>The free tier: every widget type, at most <see cref="LicenseRules.FreeWidgetLimit"/> widgets and <see cref="LicenseRules.FreeViewLimit"/> views.</summary>
    Free = 0,

    /// <summary>The Pro add-on is owned: no limits.</summary>
    Pro = 1,

    /// <summary>A developer build (no package identity, so no Store to ask): no limits, and it says so.</summary>
    Unrestricted = 2,
}

/// <summary>Why the tier is what it is — what the License page explains. [S-1064]</summary>
public enum LicenseBasis
{
    /// <summary>Run without package identity (F5, a loose publish folder). The Store is never asked.</summary>
    DeveloperBuild = 0,

    /// <summary>Packaged, the Store has not answered yet, and there is no recent Pro confirmation to lean on.</summary>
    Checking = 1,

    /// <summary>The Store answered: the Pro add-on is not owned.</summary>
    StoreFree = 2,

    /// <summary>The Store answered: the Pro add-on is owned.</summary>
    StorePro = 3,

    /// <summary>The Store cannot be reached, and Pro was confirmed within <see cref="LicenseRules.OfflineGrace"/>.</summary>
    OfflineGrace = 4,

    /// <summary>The Store cannot be reached, and there is no Pro confirmation recent enough to trust.</summary>
    StoreUnavailable = 5,
}

/// <summary>The Store's last answer about the Pro add-on.</summary>
public enum StoreAnswer
{
    /// <summary>Not asked yet in this process.</summary>
    NotAsked = 0,

    /// <summary>The add-on licence is present and active.</summary>
    Owned = 1,

    /// <summary>The Store answered and the add-on is not among the active licences.</summary>
    NotOwned = 2,

    /// <summary>The Store could not be asked or did not answer (offline, Store service broken).</summary>
    Unavailable = 3,
}

/// <param name="Tier">What the copy may do.</param>
/// <param name="Basis">Why.</param>
/// <param name="GraceUntil">With <see cref="LicenseBasis.OfflineGrace"/>: when Pro lapses unless the Store confirms it again.</param>
public sealed record LicenseStatus(LicenseTier Tier, LicenseBasis Basis, DateTimeOffset? GraceUntil = null)
{
    /// <summary>Whether the free limits apply.</summary>
    public bool IsLimited => Tier == LicenseTier.Free;
}

/// <summary>How much of the free allowance is in use: every widget in every view, and the views.</summary>
public readonly record struct LicenseUsage(int Widgets, int Views)
{
    /// <summary>
    /// [S-1065] Counted across ALL views, as the original's <c>totalWidgetCount</c>.
    /// </summary>
    public static LicenseUsage Of(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var widgets = 0;
        foreach (var view in state.Views)
        {
            widgets += view.Widgets.Count;
        }

        return new LicenseUsage(widgets, state.Views.Count);
    }

    /// <summary>
    /// [S-1065] How many widgets a placement (paste, duplicate, move, new widget) adds on balance:
    /// what it adds, less what it only moves (<see cref="PastePlan.NetAdded"/>). A cut-paste or a move
    /// therefore adds nothing and is allowed even at the limit; a copy-paste counts every widget.
    /// </summary>
    public static int AddedByPaste(AppState state, PastePlan plan)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(plan);

        return plan.NetAdded(state);
    }
}

/// <summary>
/// [S-1064..S-1067] The Microsoft Store licensing rules, kept free of the Store API so they can be
/// pinned by tests.
/// <para>
/// The limits are the original's (<c>src/license.ts</c>, <c>layout.ts</c>, <c>views.ts</c>): 10 widgets
/// and 2 views on the free tier, every widget type available. They only ever refuse to CREATE: a copy
/// that is over them (Pro lapsed, an import, a profile from a developer build) keeps everything it
/// has, and moving, editing or deleting is never refused.
/// </para>
/// </summary>
public static class LicenseRules
{
    /// <summary>Widgets the free tier may hold, across all views.</summary>
    public const int FreeWidgetLimit = 20;

    /// <summary>Views the free tier may hold.</summary>
    public const int FreeViewLimit = 2;

    /// <summary>
    /// [S-1066] How long a Pro confirmation carries the copy while the Store cannot be asked — the
    /// original's 7-day offline grace. Windows caches Store licences itself, so this is the fallback
    /// for a broken Store service rather than for every offline start.
    /// </summary>
    public static readonly TimeSpan OfflineGrace = TimeSpan.FromDays(7);

    /// <summary>How far in the future a stored confirmation may lie before it is treated as tampered.</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(10);

    /// <summary>
    /// [S-1064] The tier from what is known. Anything that is not a clear, current "owned" (or a
    /// confirmation inside the grace window) is Free: an unknown state never unlocks Pro.
    /// </summary>
    /// <param name="packaged">Whether the process has package identity. Without it there is no Store and nothing is limited.</param>
    /// <param name="answer">The Store's last answer.</param>
    /// <param name="lastConfirmedPro">When the Store last said "owned" on this PC, from the protected cache; null when absent or unreadable.</param>
    /// <param name="now">The current time.</param>
    public static LicenseStatus Decide(bool packaged, StoreAnswer answer, DateTimeOffset? lastConfirmedPro, DateTimeOffset now)
    {
        if (!packaged)
        {
            return new LicenseStatus(LicenseTier.Unrestricted, LicenseBasis.DeveloperBuild);
        }

        switch (answer)
        {
            case StoreAnswer.Owned:
                return new LicenseStatus(LicenseTier.Pro, LicenseBasis.StorePro);
            case StoreAnswer.NotOwned:
                return new LicenseStatus(LicenseTier.Free, LicenseBasis.StoreFree);
        }

        if (GraceUntil(lastConfirmedPro, now) is { } until)
        {
            return new LicenseStatus(LicenseTier.Pro, LicenseBasis.OfflineGrace, until);
        }

        return new LicenseStatus(
            LicenseTier.Free,
            answer == StoreAnswer.NotAsked ? LicenseBasis.Checking : LicenseBasis.StoreUnavailable);
    }

    /// <summary>
    /// [S-1066] The end of the offline grace a confirmation gives, or null when it gives none: absent,
    /// expired, or dated in the future (a set-back clock or an edited cache).
    /// </summary>
    public static DateTimeOffset? GraceUntil(DateTimeOffset? lastConfirmedPro, DateTimeOffset now)
    {
        if (lastConfirmedPro is not { } confirmed || confirmed > now + ClockSkew)
        {
            return null;
        }

        var until = confirmed + OfflineGrace;
        return now <= until ? until : null;
    }

    /// <summary>
    /// [S-1065] Whether creating <paramref name="addWidgets"/> widgets and <paramref name="addViews"/>
    /// views would take a limited copy past the free limits. Parameterised because a paste brings
    /// several widgets at once, and a half-pasted selection is worse than a refusal. Adding nothing never
    /// exceeds — so a copy already over the limit can still move, edit and delete.
    /// </summary>
    public static bool WouldExceed(LicenseTier tier, LicenseUsage usage, int addWidgets, int addViews = 0)
    {
        if (tier != LicenseTier.Free)
        {
            return false;
        }

        return (addWidgets > 0 && usage.Widgets + addWidgets > FreeWidgetLimit)
            || (addViews > 0 && usage.Views + addViews > FreeViewLimit);
    }

    /// <summary>
    /// [S-1067] Whether the Store's add-on licences include an active licence for the product. The
    /// keys are SKU Store ids (<c>9ABCDEFGHIJK/0010</c>), so the product id is matched as the prefix
    /// before the slash, as Microsoft's own samples do.
    /// </summary>
    public static bool OwnsAddOn(IEnumerable<(string SkuStoreId, bool IsActive)> licences, string productStoreId)
    {
        ArgumentNullException.ThrowIfNull(licences);

        if (!IsStoreId(productStoreId))
        {
            return false;
        }

        foreach (var (sku, active) in licences)
        {
            if (active && sku is not null
                && sku.StartsWith(productStoreId, StringComparison.OrdinalIgnoreCase)
                && (sku.Length == productStoreId.Length || sku[productStoreId.Length] == '/'))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// [S-1067] Whether a string is a real Store product id (12 letters and digits, as Partner Center
    /// issues them) rather than the placeholder the source carries until the add-on exists.
    /// </summary>
    public static bool IsStoreId(string? productStoreId)
        => productStoreId is { Length: 12 } id && id.All(c => c is (>= '0' and <= '9') or (>= 'A' and <= 'Z'));
}

/// <summary>How a Store purchase ended, as the License page reports it. [S-1068]</summary>
public enum PurchaseOutcome
{
    Purchased,
    AlreadyOwned,
    Cancelled,
    NetworkError,
    ServerError,

    /// <summary>The add-on's Store id is still the placeholder.</summary>
    NotConfigured,

    /// <summary>No Store here (developer build) or the Store could not start the purchase.</summary>
    Unavailable,
}

/// <summary>
/// [S-1069] The protected on-disk record of the last Store "owned" answer, for the offline grace.
/// <para>
/// The payload is <c>themia-license:v1:&lt;unix seconds&gt;</c>, sealed with DPAPI by the app before it
/// touches the disk. Anything else — another version, a damaged number, a future date — reads as
/// <see cref="LicenseCacheState.Corrupt"/> and gives no grace: a broken cache never unlocks Pro.
/// </para>
/// </summary>
public static class LicenseCache
{
    public const string Prefix = "themia-license:v1:";

    public static string Format(DateTimeOffset confirmedPro)
        => Prefix + confirmedPro.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

    public static (LicenseCacheState State, DateTimeOffset? ConfirmedPro) Parse(string? payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return (LicenseCacheState.Absent, null);
        }

        if (!payload.StartsWith(Prefix, StringComparison.Ordinal)
            || !long.TryParse(payload.AsSpan(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || seconds <= 0
            || seconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
        {
            return (LicenseCacheState.Corrupt, null);
        }

        return (LicenseCacheState.Valid, DateTimeOffset.FromUnixTimeSeconds(seconds));
    }
}

/// <summary>What reading the licence cache found.</summary>
public enum LicenseCacheState
{
    Absent,
    Valid,
    Corrupt,
}

/// <summary>
/// [S-1068] The License page's words, kept here so every state has one and the tests can see it.
/// Each takes the language explicitly; the overloads without one use <see cref="Strings.Current"/>.
/// </summary>
public static class LicenseText
{
    public static string Title(LicenseStatus status) => Title(Strings.Current, status);

    public static string Title(Locale locale, LicenseStatus status) => status.Basis switch
    {
        LicenseBasis.DeveloperBuild => Strings.Get(locale, Str.Shell_License_DeveloperBuild),
        LicenseBasis.StorePro or LicenseBasis.OfflineGrace => "Themia Pro",
        LicenseBasis.Checking => Strings.Get(locale, Str.Shell_License_Checking),
        _ => "Themia Free",
    };

    public static string Detail(LicenseStatus status) => Detail(Strings.Current, status);

    public static string Detail(Locale locale, LicenseStatus status) => status.Basis switch
    {
        LicenseBasis.DeveloperBuild => Strings.Get(locale, Str.Shell_License_DeveloperDetail),
        LicenseBasis.StorePro => Strings.Get(locale, Str.Shell_License_ProDetail),
        LicenseBasis.OfflineGrace => status.GraceUntil is { } until
            ? Strings.Format(locale, Str.Shell_License_GraceUntil, ("date", LocaleFormat.Date(locale, until.ToLocalTime().DateTime, DateStyle.Medium)))
            : Strings.Get(locale, Str.Shell_License_GraceOpen),
        LicenseBasis.Checking => Strings.Get(locale, Str.Shell_License_CheckingDetail),
        LicenseBasis.StoreUnavailable => Strings.Format(locale, Str.Shell_License_StoreUnavailable,
            ("widgets", LicenseRules.FreeWidgetLimit), ("views", LicenseRules.FreeViewLimit)),
        _ => Strings.Format(locale, Str.Shell_License_FreeDetail,
            ("widgets", LicenseRules.FreeWidgetLimit), ("views", LicenseRules.FreeViewLimit)),
    };

    public static string Purchase(PurchaseOutcome outcome) => Purchase(Strings.Current, outcome);

    public static string Purchase(Locale locale, PurchaseOutcome outcome) => Strings.Get(locale, outcome switch
    {
        PurchaseOutcome.Purchased => Str.Shell_License_Purchased,
        PurchaseOutcome.AlreadyOwned => Str.Shell_License_AlreadyOwned,
        PurchaseOutcome.Cancelled => Str.Shell_License_Cancelled,
        PurchaseOutcome.NetworkError => Str.Shell_License_NetworkError,
        PurchaseOutcome.ServerError => Str.Shell_License_ServerError,
        PurchaseOutcome.NotConfigured => Str.Shell_License_NotOnSale,
        _ => Str.Shell_License_StoreMissing,
    });

    /// <summary>Why a create was refused, for the page the refusal opens.</summary>
    public static string Refusal(LicenseUsage usage, int addWidgets, int addViews) => Refusal(Strings.Current, usage, addWidgets, addViews);

    public static string Refusal(Locale locale, LicenseUsage usage, int addWidgets, int addViews)
        => addViews > 0 && usage.Views + addViews > LicenseRules.FreeViewLimit
            ? Strings.Format(locale, Str.Shell_License_ViewLimit, ("count", LicenseRules.FreeViewLimit))
            : Strings.Format(locale, Str.Shell_License_WidgetLimit, ("count", LicenseRules.FreeWidgetLimit));
}
