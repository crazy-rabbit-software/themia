using Themia.Core.Model;

namespace Themia.Core.SystemData;

/// <summary>[S-2513] The host's half of <see cref="SystemInterests"/>: every view's system widgets, collected from the state.</summary>
public static class StateSystemInterests
{
    extension(SystemInterests)
    {
        /// <summary>
        /// Every view's widgets, deduplicated and in read order, so two states that want the same thing
        /// compare equal (<see cref="SystemInterests.SameAs"/>) and the hub hears only of real changes.
        /// </summary>
        /// <param name="systemRoot">The drive Windows runs from (<c>C:\</c>): a disk meter with no drive chosen reads it.</param>
        public static SystemInterests From(AppState state, string systemRoot)
        {
            ArgumentNullException.ThrowIfNull(state);

            var feeds = new HashSet<FeedKey>();
            var series = new HashSet<SeriesInterest>();
            var coreSeconds = 0;

            foreach (var widget in state.Views.SelectMany(v => v.Widgets))
            {
                if (widget.Config is { } config)
                {
                    var wants = SystemInterests.For(config, systemRoot);
                    feeds.UnionWith(wants.Feeds);
                    series.UnionWith(wants.Series);
                    coreSeconds = Math.Max(coreSeconds, wants.CoreHistorySeconds);
                }
            }

            return new SystemInterests(
                [.. feeds.OrderBy(k => k.Source).ThenBy(k => k.Target, StringComparer.Ordinal)],
                [.. series.OrderBy(s => s.Key.Source).ThenBy(s => s.Key.Target, StringComparer.Ordinal).ThenBy(s => s.Instance, StringComparer.Ordinal)])
            {
                CoreHistorySeconds = coreSeconds,
            };
        }
    }
}
