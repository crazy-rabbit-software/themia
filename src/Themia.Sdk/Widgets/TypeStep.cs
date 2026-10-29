namespace Themia.Core.Widgets;

/// <summary>
/// [S-6700] One step of the family's type (<c>Themia.Sdk.Kit.FamilyType</c>): a size, the line that goes
/// with it and its weight.
/// </summary>
/// <param name="FontSize">Size in device-independent pixels, at the live text size.</param>
/// <param name="LineHeight">The step's line at the live text size.</param>
/// <param name="SemiBold">Whether the step is drawn semibold (600) rather than regular (400).</param>
public readonly record struct TypeStep(double FontSize, double LineHeight, bool SemiBold);
