using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The add page's tree ([S-3510], [S-3511], [S-3513]): every kind at one home, the folders' shapes,
/// Popular, the homes the search names. The page drawn from it is <c>AddPageTests</c>'. Here, not in
/// Core's tests, because the tree is built from every widget of the build, and only this host has them all.
/// </summary>
public sealed class AddMenuTests
{
    private static readonly WidgetId[] Every = [.. ModuleCatalog.Widgets.Select(w => w.Id)];

    private static IEnumerable<AddNode> Descend(AddShelf shelf)
    {
        foreach (var row in shelf.Rows)
        {
            yield return row;
            if (row is AddShelf inner)
            {
                foreach (var deeper in Descend(inner))
                {
                    yield return deeper;
                }
            }
        }
    }

    private static IEnumerable<AddShelf> Shelves() =>
        AddTree.Top.Concat(AddTree.Top.SelectMany(Descend).OfType<AddShelf>());

    [Fact]
    public void S3511_every_kind_has_exactly_one_home_in_the_tree()
    {
        var homes = AddTree.Top.SelectMany(Descend).OfType<AddLeaf>().Where(leaf => !leaf.Again).Select(leaf => leaf.Kind).ToList();

        var missing = Every.Except(homes).ToList();
        var twice = homes.GroupBy(kind => kind).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

        Assert.True(missing.Count == 0, "kinds with no home in AddTree (give each one in its subject): " + string.Join(", ", missing));
        Assert.True(twice.Count == 0, "kinds at home twice (mark the second Again): " + string.Join(", ", twice));
    }

    [Fact]
    public void S3511_a_kind_shown_again_has_its_home_elsewhere_and_only_popular_shows_kinds_again()
    {
        var homes = AddTree.Top.SelectMany(Descend).OfType<AddLeaf>().Where(leaf => !leaf.Again).Select(leaf => leaf.Kind).ToHashSet();

        foreach (var shelf in Shelves())
        {
            foreach (var leaf in shelf.Rows.OfType<AddLeaf>().Where(leaf => leaf.Again))
            {
                Assert.Equal(AddFolderId.Popular, shelf.Id);
                Assert.Contains(leaf.Kind, homes);
            }
        }

        Assert.All(AddTree.Popular.Rows, row => Assert.True(row is AddLeaf { Again: true }, "Popular holds widgets shown again, nothing else"));
    }

    [Fact]
    public void S3510_subjects_hold_families_families_hold_widgets_and_never_one()
    {
        foreach (var shelf in Shelves())
        {
            Assert.True(shelf.Rows.Count(row => row is not AddLine) >= 2, $"{shelf.Id} is a folder of one");
            Assert.False(shelf.Rows[0] is AddLine || shelf.Rows[^1] is AddLine, $"{shelf.Id} starts or ends with a line");
        }

        Assert.All(AddTree.Browse.Rows, row => Assert.IsType<AddShelf>(row));
        foreach (var subject in AddTree.Browse.Rows.OfType<AddShelf>())
        {
            Assert.Equal(subject.Id, AddTree.SubjectOf(subject.Id));
            foreach (var family in subject.Rows.OfType<AddShelf>())
            {
                Assert.All(family.Rows, row => Assert.IsType<AddLeaf>(row));
                Assert.Equal(subject.Id, AddTree.SubjectOf(family.Id));
            }
        }
    }

    [Fact]
    public void S3510_every_folder_has_its_own_id_and_words()
    {
        var shelves = Shelves().ToList();
        Assert.Equal(shelves.Count, shelves.Select(shelf => shelf.Id).Distinct().Count());
        Assert.All(shelves, shelf => Assert.False(string.IsNullOrWhiteSpace(shelf.Title.Text(Locale.En))));
    }

    [Fact]
    public void S3513_a_member_of_a_family_is_at_home_under_its_subject_and_its_family()
    {
        var homes = AddPage.Of(Every, ReadyMadeGroups.Offered()).Homes;

        var details = homes.Single(home => home.Kind == new WidgetId("cpuFacts"));
        Assert.Equal([AddFolderId.System, AddFolderId.Processor], details.Path.Select(shelf => shelf.Id));

        var clock = homes.Single(home => home.Kind == new WidgetId("clock"));
        Assert.Equal([AddFolderId.Time], clock.Path.Select(shelf => shelf.Id));
    }

    [Fact]
    public void S3511_the_merged_kinds_are_single_tiles_and_popular_lists_the_calendar()
    {
        // [S-3640] [S-3216] [S-4123] The calendar, the visualizer and the folder are one widget each,
        // their former variants styles: a widget of their subject, never a family.
        var homes = AddPage.Of(Every, ReadyMadeGroups.Offered()).Homes;
        Assert.Equal([AddFolderId.CalendarMail], homes.Single(home => home.Kind == new WidgetId("calendar")).Path.Select(shelf => shelf.Id));
        Assert.Equal([AddFolderId.Media], homes.Single(home => home.Kind == new WidgetId("audioVisualizer")).Path.Select(shelf => shelf.Id));
        Assert.Equal([AddFolderId.Files], homes.Single(home => home.Kind == new WidgetId("folder")).Path.Select(shelf => shelf.Id));

        var popular = AddTree.Popular.Rows.OfType<AddLeaf>().Select(leaf => leaf.Kind).ToList();
        Assert.Contains(new WidgetId("calendar"), popular);
        Assert.Equal(popular.Count, popular.Distinct().Count());
    }
}
