#if DEBUG
using Themia.App.Services;
using Themia.App.Services.Kit;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Services;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Services;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-9929] A preview card's kit services (a debug render): the provider's recorded answers replayed
/// through the real rules and the real sync, so every kit widget gets the same preview states for
/// free and has no preview code of its own (docs/WIDGET-KIT.md section 6.7). The answers live once,
/// in the provider project's <c>Fixtures/</c>, copied into Debug builds only beside the app
/// (<c>Fixtures/&lt;Provider&gt;/&lt;state&gt;.json</c>). Nothing reaches the network or an account.
/// <list type="bullet">
/// <item><c>items</c>, <c>many</c>, <c>empty</c>, <c>nomatch</c>, <c>forbidden</c>, <c>ratelimited</c>,
/// <c>problem</c>, <c>hovered</c>, <c>focused</c>: that recording, as one stand-in account (<c>items</c>
/// for the last three when the provider records none).</item>
/// <item><c>accounts</c>: the recording read through two stand-in accounts, merged.</item>
/// <item><c>stale</c>: <c>items</c>, then the next attempt offline.</item>
/// <item><c>signin</c>: the stand-in account needs signing in again. <c>gone</c>: the account the widget
/// names is no longer signed in. <c>loading</c>: the first answer never arrives.</item>
/// </list>
/// </summary>
internal static class FixtureServices
{
    private const string Scope = "fixtures";

    /// <summary>The state the render was asked for, set before its cards are built; null for none.</summary>
    public static string? State { get; set; }

    public static IReadOnlyList<string> States { get; } =
        ["items", "many", "loading", "empty", "nomatch", "stale", "signin", "gone", "forbidden", "ratelimited", "problem", "hovered", "focused", "accounts"];

    public static IServiceSources For(Ticker ticker) => new Sources(ticker);

    private sealed class Sources(Ticker ticker) : IServiceSources
    {
        public IServiceSource Open(ServiceInterest interest)
        {
            var state = State ?? "items";
            var recording = state switch
            {
                "accounts" or "stale" or "signin" or "gone" or "loading" or "hovered" or "focused" => "items",
                _ => state,
            };

            var transport = Load(interest.Provider, recording) ?? Load(interest.Provider, "items");
            var accounts = state switch
            {
                "accounts" => new[] { Stand("sample:work", "Work", interest), Stand("sample:home", "Home", interest) },
                "signin" => [Stand("sample:work", "Work", interest) with { NeedsSignIn = true }],
                "gone" => [],
                _ => [Stand("sample:work", "Work", interest)],
            };

            IServiceTransport replay = state switch
            {
                "loading" => new Never(),
                "stale" when transport is not null => new ThenOffline(transport),
                _ => transport ?? (IServiceTransport)new Never(),
            };

            var named = state == "gone" ? new ServiceInterest(interest.Provider, interest.Query!, ["sample:gone"], interest.Count) : interest;
            var sync = new ServiceSync(ticker, replay, () => accounts);
            var source = sync.Open(named);
            if (state == "stale")
            {
                // The recording answers once; the Refresh after it finds the network gone, and the
                // items stay on screen under the problem.
                void Again()
                {
                    if (source.Read().Items.Count > 0 && !source.Busy)
                    {
                        source.Changed -= Again;
                        source.Show(refresh: true);
                    }
                }

                source.Changed += Again;
            }

            return source;
        }

        private static ServiceAccount Stand(string id, string label, ServiceInterest interest) => new(id, interest.Provider, label);
    }

    private static FixtureReplay? Load(ProviderId provider, string state)
    {
        if (ProviderCatalog.All.FirstOrDefault(p => p.Id == provider) is not { } module)
        {
            return null;
        }

        var name = module.GetType().Assembly.GetName().Name?.Replace("Themia.Provider.", string.Empty, StringComparison.Ordinal) ?? provider.Value;
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name, state + ".json");
        if (!File.Exists(path))
        {
            Log.Info(Scope, $"{name} has no recording for '{state}'");
            return null;
        }

        var replay = FixtureReplay.Parse(File.ReadAllText(path), out var problem);
        if (replay is null)
        {
            Log.Warn(Scope, $"{name}'s recording for '{state}' does not parse ({problem})");
        }

        return replay;
    }

    /// <summary>A first answer that never arrives.</summary>
    private sealed class Never : IServiceTransport
    {
        public Task<ServiceReply> SendAsync(string? accountId, ServiceDescriptor descriptor, ServiceRequest request, string? ifNoneMatch, CancellationToken cancellation)
            => new TaskCompletionSource<ServiceReply>().Task;
    }

    /// <summary>The recording once, then no network.</summary>
    private sealed class ThenOffline(FixtureReplay replay) : IServiceTransport
    {
        private bool _answered;

        public Task<ServiceReply> SendAsync(string? accountId, ServiceDescriptor descriptor, ServiceRequest request, string? ifNoneMatch, CancellationToken cancellation)
        {
            if (_answered)
            {
                throw new AccountException(AccountError.Offline, "no network (a preview)");
            }

            _answered = true;
            return replay.SendAsync(accountId, descriptor, request, ifNoneMatch, cancellation);
        }
    }
}
#endif
