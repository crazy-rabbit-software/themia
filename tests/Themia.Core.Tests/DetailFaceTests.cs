using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2404] The detail atoms' list rows: how many lines a row has.</summary>
public sealed class DetailFaceTests
{
    [Fact]
    public void S2404_a_rows_lines_are_its_name_a_second_figure_and_a_detail()
    {
        Assert.Equal(1, DetailFace.ListLines([new DetailRow("a", "a", "1%")]));
        Assert.Equal(2, DetailFace.ListLines([new DetailRow("a", "a", "1%", Second: "2 MB")]));
        Assert.Equal(3, DetailFace.ListLines([new DetailRow("a", "a", "1%"), new DetailRow("b", "b", "1", Second: "2", Detail: "3")]));
    }
}
