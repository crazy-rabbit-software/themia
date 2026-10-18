using Themia.Core.Model;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-582] The contacts config's columns, now its module's (moved from Core's ContactsAndBirthdaysTests, whose rules half stays there under the root tag).</summary>
public sealed class ContactsHostTests
{
    [Fact]
    public void S582_the_configs_columns_stay_canonical_and_never_empty()
    {
        var config = new ContactsConfig { Columns = [ContactColumn.Title, ContactColumn.Name] };
        Assert.Equal([ContactColumn.Name, ContactColumn.Title], config.EffectiveColumns);
        Assert.Equal(3, new ContactsConfig { Columns = [] }.EffectiveColumns.Count);
    }
}
