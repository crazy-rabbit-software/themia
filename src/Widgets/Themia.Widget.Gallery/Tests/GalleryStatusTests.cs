using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Gallery.Tests;

/// <summary>[S-779] The gallery's own Set up sentence, by its source (the Status row's look is the SDK's, tested in Core).</summary>
public sealed class GalleryStatusTests
{
    [Fact]
    public void S779_set_up_names_what_the_source_lacks()
    {
        ISetupRule folder = new GalleryConfig { Source = GallerySource.Folder };
        ISetupRule file = new GalleryConfig { Source = GallerySource.File };
        Assert.Equal(GalleryStr.Gallery_Setup_Folder.Text(), folder.SetupSentence);
        Assert.Equal(GalleryStr.Gallery_Setup_Picture.Text(), file.SetupSentence);
        Assert.NotEqual(folder.SetupSentence, file.SetupSentence);
    }
}
