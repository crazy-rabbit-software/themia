using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-7037] [S-7038] The kit's check look (the Tasks widget's ladder, slots and editor rules,
/// [S-780]..[S-788]; their tests are <c>TaskLayoutTests</c>).
/// </summary>
public sealed class KitChecksTests
{
    private static readonly KitSpec Check = new(KitRowLook.Check, KitShow.All, () => string.Empty, () => string.Empty, KitTime.None);

    [Fact]
    public void S7037_a_check_lead_says_whether_it_is_done()
    {
        Assert.True(KitLead.Check(done: true).IsDone);
        Assert.False(KitLead.Check(done: false, KitTone.Critical).IsDone);
        Assert.Equal(KitTone.Critical, KitLead.Check(done: false, KitTone.Critical).Tone);
    }
}
