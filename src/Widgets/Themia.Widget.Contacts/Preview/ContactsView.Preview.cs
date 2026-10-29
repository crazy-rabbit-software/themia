#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Office;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): a fixed address book on the reserved <c>.example</c> domain, never an
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

        Contact Person(string id, string name, string? title, string? company, string? email, string? phone)
            => new() { Id = id, Name = name, JobTitle = title, Company = company, Email = email, Phone = phone };

        _frozen = true;
        _contacts = ContactRules.Merge([[
            Person("1", "Alex Morgan", "Product designer", "Lumen Labs", "alex.morgan@lumen.example", "+41 44 555 01 10"),
            Person("2", "Bea Lindqvist", "Head of finance", "Northwind", "bea@northwind.example", "+46 8 555 120 40"),
            Person("3", "Carlos Ruiz", null, null, "carlos.ruiz@mail.example", "+34 91 555 23 87"),
            Person("4", "Dana Ruiz", "Engineering lead", "Lumen Labs", "dana@lumen.example", null),
            Person("5", "Emeka Obi", "Architect", "Studio Obi", null, "+234 1 555 0190"),
            Person("6", "Fatima Haddad", "Physician", "City Clinic", "f.haddad@clinic.example", "+961 1 555 314"),
            Person("7", "Grace Chen", null, "Tide Analytics", "grace@tide.example", null),
            Person("8", "Hiro Tanaka", "Translator", null, "hiro.tanaka@mail.example", "+81 3 5550 1123"),
            Person("9", "Ines Moreau", "Teacher", "École du Parc", null, null),
            Person("10", "Jonas Weber", "Plumber", null, null, "+49 30 5550 7788"),
            Person("11", "Kira Novak", "Photographer", "Novak Studio", "kira@novak.example", "+420 2 5550 4411"),
            Person("12", "Liam O'Brien", "Sales", "Northwind", "liam.obrien@northwind.example", "+353 1 555 0199"),
        ]]);
        _screen = Screen.Ready;
        _warningText = null;
        _query = string.Empty;
        Render();
        return Task.CompletedTask;
    }
}
#endif
