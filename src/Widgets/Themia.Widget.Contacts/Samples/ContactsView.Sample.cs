using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2053] The contacts' sample: the sample day's cast as the address book on the reserved
/// <c>.example</c> domain, never an account's people.
/// </summary>
internal sealed partial class ContactsView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
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
