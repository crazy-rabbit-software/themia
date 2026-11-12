#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2053]): the sample day's cast as the address book on the reserved <c>.example</c> domain, never an
/// account's people, so a render can be matched with its mockup.
/// </summary>
internal sealed partial class ContactsView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state != "sample")
        {
            return Task.CompletedTask;
        }

        _frozen = true;
        _contacts = ContactRules.Merge([[
            .. SampleCast.Contacts.Select((p, i) => new Contact
            {
                Id = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Name = p.Name,
                JobTitle = p.JobTitle,
                Company = p.Company,
                Email = p.Email,
                Phone = p.Phone,
            }),
        ]]);
        _screen = Screen.Ready;
        _warningText = null;
        _query = string.Empty;
        Render();
        return Task.CompletedTask;
    }
}
#endif
