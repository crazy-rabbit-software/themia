using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11942] [S-11965] [S-11966] Mail's place on its view's links: a row is its message (<see cref="LinkKeys.Message"/>
/// of its account and the provider's id), never its sender. The pointer on a row publishes the message
/// (<see cref="LinkTopics.HoveredMessage"/>) and lights that row alone; while a message is pointed at in another mail
/// widget of the view, the row that is that same message takes the item layer's hover fill, lit in place on the rows
/// the kit's list drew. Two mails from one sender are two things.
/// </summary>
internal sealed partial class EmailView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<MessageLink>? _messages;

    private LinkLights<MessageLink> Messages => _messages ??= new LinkLights<MessageLink>(_links, LinkTopics.HoveredMessage);

    /// <summary>A row the kit's list drew: its message.</summary>
    private void LinkRow(KitItem item, FrameworkElement row)
    {
        if (_source?.Message(item.Id) is { } message)
        {
            Messages.Row(row, LinkKeys.Message(message.AccountId, message.RawId));
        }
    }

    /// <summary>The kit's list draws its rows again: the old ones are forgotten.</summary>
    private void ForgetRows() => Messages.Clear();
}
