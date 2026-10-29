using Microsoft.UI.Xaml.Media;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-6746] The theme brush of a chrome's <see cref="ShellRole"/>: the shared object, recoloured with the
/// theme, as <c>Family.Brush</c> gives a widget's role. The app's shell only, never a widget's ([S-6747]).
/// </summary>
public static class ShellBrush
{
    public static Brush Of(ShellRole role) => ThemeBrush.Of(ShellRoles.Key(role), default);
}
