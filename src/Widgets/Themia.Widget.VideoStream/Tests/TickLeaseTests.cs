using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.VideoStream.Tests;

/// <summary>[S-1313] The frame age's tick, held once however the ticker's first, synchronous tick asks again.</summary>
public sealed class TickLeaseTests
{
    private sealed class Counted(Action disposed) : IDisposable
    {
        public void Dispose() => disposed();
    }

    [Fact]
    public void S1313_a_tick_that_fires_inside_subscribe_does_not_subscribe_again()
    {
        var lease = new TickLease();
        var subscribed = 0;
        IDisposable Subscribe()
        {
            subscribed++;
            Assert.True(subscribed < 5, "subscribed again from its own first tick");

            // The ticker fires a new subscription at once: the view asks again whether to tick.
            lease.Want(true, Subscribe);
            return new Counted(() => { });
        }

        lease.Want(true, Subscribe);
        Assert.Equal(1, subscribed);
        Assert.True(lease.Held);
    }

    [Fact]
    public void S1313_let_go_during_its_first_tick_the_subscription_is_dropped_when_it_arrives()
    {
        var lease = new TickLease();
        var disposed = 0;
        lease.Want(true, () =>
        {
            lease.Want(false, () => throw new InvalidOperationException());
            return new Counted(() => disposed++);
        });

        Assert.False(lease.Held);
        Assert.Equal(1, disposed);
    }

    [Fact]
    public void S1313_letting_go_disposes_the_one_subscription()
    {
        var lease = new TickLease();
        var disposed = 0;
        lease.Want(true, () => new Counted(() => disposed++));
        lease.Want(true, () => throw new InvalidOperationException("held already"));
        lease.Want(false, () => throw new InvalidOperationException());
        lease.Dispose();
        Assert.Equal(1, disposed);
    }
}
