using Themia.Core.Widgets;

namespace Themia.App.Services.Web;

/// <summary>
/// One answer from a service, with the two things a network widget has to show beside it: how it
/// went, and when.
/// </summary>
/// <param name="Status">The named outcome of the attempt that produced this.</param>
/// <param name="Value">What came back, or null when the attempt failed and nothing was cached.</param>
/// <param name="Detail">One line for the log. Never rendered verbatim: it is a diagnostic, not a sentence.</param>
/// <param name="At">When <paramref name="Value"/> was actually fetched — not when it was handed over.</param>
public sealed record Fetched<T>(FetchStatus Status, T? Value, string? Detail, DateTimeOffset At)
    where T : class
{
    /// <summary>A failure with nothing to show for it; <paramref name="retryAt"/> a throttle's own time ([S-2694]).</summary>
    public static Fetched<T> Failed(FetchStatus status, string? detail, DateTimeOffset at, DateTimeOffset? retryAt = null)
        => new(status, null, detail, at) { RetryAt = retryAt };

    /// <summary>[S-2694] When the service said it may be asked again (a throttle's <c>Retry-After</c>), or null.</summary>
    public DateTimeOffset? RetryAt { get; init; }

    /// <summary>
    /// The cached value, kept on screen, annotated with the failure that has just happened to it.
    /// <para>
    /// This is how a widget stays useful through an outage without lying: the numbers are the ones
    /// it had, <see cref="At"/> still says when they were true, and <see cref="Status"/> says that
    /// the last attempt to improve on them failed.
    /// </para>
    /// </summary>
    public Fetched<T> Stale(FetchStatus status, string? detail, DateTimeOffset? retryAt = null) => this with { Status = status, Detail = detail, RetryAt = retryAt };

    /// <summary>
    /// [S-729] Read back from the store at launch rather than fetched by this run. Drawn with its real
    /// age in the caution colour until a fetch replaces it; fresh for its real age ([S-2662]), so a
    /// restart a minute after the last fetch asks nothing.
    /// </summary>
    public bool Restored { get; init; }
}
