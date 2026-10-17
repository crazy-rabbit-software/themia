namespace Themia.Sdk;

/// <summary>
/// [S-6905] Facts about a widget the shell asks without building it: what its card offers. A
/// descriptor states all of them (the member is required), so a new widget cannot inherit another's
/// by omission.
/// </summary>
[Flags]
public enum WidgetTraits
{
    None = 0,

    /// <summary>[S-6570] Its view paints items (rows, tiles, a meter's bars), so the item background and text apply.</summary>
    HasItems = 1,
}
