using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6733] The family's two wallpaper roles: a name over the wallpaper and its shadow.</summary>
public sealed class WallpaperRolesTests
{
    [Fact]
    public void S6733_the_wallpaper_roles_have_their_own_resources_and_the_shadow_falls_down_right()
    {
        var text = ColourRoles.Key(ColourRole.OnWallpaper);
        var shadow = ColourRoles.Key(ColourRole.OnWallpaperShadow);

        Assert.NotEqual(text, shadow);
        Assert.Equal(ColourRole.OnWallpaper, ColourRoles.Of(text));
        Assert.Equal(ColourRole.OnWallpaperShadow, ColourRoles.Of(shadow));

        // Never a card's text role: a name over the wallpaper is light in either theme.
        Assert.NotEqual(ColourRoles.Key(ColourRole.TextPrimary), text);
        Assert.True(FamilyShape.TextShadowOffset > 0);
    }
}
