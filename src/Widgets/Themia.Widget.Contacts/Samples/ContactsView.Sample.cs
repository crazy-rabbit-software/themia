using Themia.Core.Office;
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
        _feed.Held = SamplePeople();
        _query = string.Empty;
        Render();
        return Task.CompletedTask;
    }

    /// <summary>[S-2053] The sample cast as contacts.</summary>
    internal static IReadOnlyList<Contact> SamplePeople() => ContactRules.Merge([[
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
}

/// <summary>[S-11850] [S-16973] The Contact's sample: the first of the sample day's cast, never an account's people.</summary>
internal sealed partial class ContactView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        _feed.Held = ContactsView.SamplePeople();
        _sample = true;
        Render();
        return Task.CompletedTask;
    }
}
