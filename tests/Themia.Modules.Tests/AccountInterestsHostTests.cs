using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Tasks;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-2608] The background sync's interests over the account widgets' real configs, which are their modules' (moved from Core's OfflineSyncTests, unchanged).</summary>
public sealed class AccountInterestsHostTests
{
    // ---- [S-2608] interests ------------------------------------------------------------------------

    [Fact]
    public void S2608_the_needs_of_every_view_are_collected_and_keyed()
    {
        var state = new AppState
        {
            Views =
            [
                new ViewState
                {
                    Id = "v1",
                    Name = "One",
                    Widgets =
                    [
                        new WidgetInstance { Id = "m", Config = new EmailConfig { Folder = MailFolder.Junk } },
                        new WidgetInstance { Id = "t", Config = new TasksConfig { AccountId = "acc", ListId = "l", ShowCompleted = true } },
                        new WidgetInstance { Id = "local", Config = new TasksConfig { Source = TaskSource.Local } },
                    ],
                },
                new ViewState
                {
                    Id = "v2",
                    Name = "Two",
                    Widgets =
                    [
                        new WidgetInstance { Id = "c", Config = new CalendarWidgetConfig { AccountIds = ["acc"] } },
                        new WidgetInstance { Id = "p", Config = new ContactsConfig() },
                    ],
                },
            ],
        };

        var interests = SyncInterests.From(state);

        Assert.Equal(MailFolder.Junk, Assert.Single(interests.Mail).Folder);
        var task = Assert.Single(interests.Tasks);
        Assert.Equal(("acc", "l", true), (task.AccountId, task.ListId, task.ShowCompleted));
        Assert.Equal(["acc"], Assert.Single(interests.Calendar).AccountIds);
        Assert.Single(interests.Contacts);
        Assert.Equal(interests.Key(), SyncInterests.From(state).Key());

        ((EmailConfig)state.Views[0].Widgets[0].Config).Folder = MailFolder.Sent;
        Assert.NotEqual(interests.Key(), SyncInterests.From(state).Key());
    }
}
