using Themia.App.Modules;
using Themia.Sdk;

namespace Themia.App.Services;

/// <summary>
/// [S-6902] Installs the generated <see cref="ModuleRegistry"/> into <see cref="ModuleCatalog"/>,
/// first thing in the process, so nothing reads a widget before the registry is there.
/// </summary>
internal static class ModuleHost
{
    public static void Install()
    {
        var widgets = ModuleRegistry.Widgets;
        for (var i = 0; i < widgets.Count; i++)
        {
            // The id the attribute declares is the id the descriptor carries: one id per widget, everywhere.
            if (!string.Equals(widgets[i].Descriptor.Id.Value, ModuleRegistry.WidgetIds[i], StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"module {widgets[i].GetType().Name} is declared as '{ModuleRegistry.WidgetIds[i]}' but its descriptor says '{widgets[i].Descriptor.Id}'");
            }
        }

        ModuleCatalog.Install(widgets.Select(m => m.Descriptor));
    }
}
