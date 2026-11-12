#if DEBUG
namespace Themia.Sdk.Samples;

/// <summary>One invented person of the sample day: no real person, every address on a reserved <c>.example</c> domain.</summary>
public sealed record SamplePerson(string Name, string Email, string? JobTitle = null, string? Company = null, string? Phone = null)
{
    /// <summary>The first name, as a chat or a meeting title says it.</summary>
    public string First => Name.Split(' ')[0];
}

/// <summary>
/// Debug renders only ([S-2053]): the invented people of the sample day. Robin Keller, whose desktop
/// it is, works on the product team of Lumen Labs in Zürich; the same colleagues, client and friends
/// write the mail, organise the meetings, fill the address book and send the notifications.
/// </summary>
public static class SampleCast
{
    public const string WorkDomain = "lumen.example";

    /// <summary>Whose desktop it is.</summary>
    public static SamplePerson Owner { get; } = new("Robin Keller", "robin.keller@lumen.example", "Product manager", "Lumen Labs", "+41 44 555 01 22");

    /// <summary>Robin's own address at home.</summary>
    public const string OwnerHomeEmail = "robin@mail.example";

    public static SamplePerson Ana { get; } = new("Ana Silva", "ana.silva@lumen.example", "Design lead", "Lumen Labs", "+41 44 555 01 14");

    public static SamplePerson Dana { get; } = new("Dana Ruiz", "dana.ruiz@lumen.example", "Engineering lead", "Lumen Labs", "+41 44 555 01 18");

    public static SamplePerson Priya { get; } = new("Priya Natarajan", "priya@lumen.example", "Marketing", "Lumen Labs", "+41 44 555 01 31");

    public static SamplePerson Alex { get; } = new("Alex Morgan", "alex.morgan@lumen.example", "Product designer", "Lumen Labs", "+41 44 555 01 10");

    public static SamplePerson Sam { get; } = new("Sam Okafor", "sam.okafor@lumen.example", "Software engineer", "Lumen Labs", null);

    public static SamplePerson Bea { get; } = new("Bea Lindqvist", "bea@northwind.example", "Head of finance", "Northwind", "+46 8 555 120 40");

    public static SamplePerson Liam { get; } = new("Liam O'Brien", "liam.obrien@northwind.example", "Sales", "Northwind", "+353 1 555 0199");

    public static SamplePerson Lena { get; } = new("Lena Fischer", "lena.fischer@mail.example", null, null, "+41 79 555 42 10");

    public static SamplePerson Mia { get; } = new("Mia Chen", "mia.chen@mail.example", "Architect", "Studio Chen", "+41 78 555 13 07");

    public static SamplePerson Jonas { get; } = new("Jonas Weber", "jonas@mail.example", "Teacher", null, "+41 76 555 77 88");

    public static SamplePerson Fatima { get; } = new("Fatima Haddad", "f.haddad@clinic.example", "Dentist", "Lake Dental Clinic", "+41 44 555 31 40");

    public static SamplePerson Kira { get; } = new("Kira Novak", "kira@novak.example", "Photographer", "Novak Studio", "+41 44 555 44 11");

    /// <summary>The address book, in no particular order (the widget sorts it).</summary>
    public static IReadOnlyList<SamplePerson> Contacts { get; } = [Alex, Ana, Bea, Dana, Fatima, Jonas, Kira, Lena, Liam, Mia, Priya, Sam];
}
#endif
