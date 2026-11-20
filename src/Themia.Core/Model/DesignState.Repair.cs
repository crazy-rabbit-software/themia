using Themia.Core.Motion;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

public sealed partial class DesignState
{
    /// <summary>
    /// [S-44] Bring a read design back inside its invariants, saying what changed: its numbers held to their ranges, a
    /// colour that is none the theme's own, its fills and look tokens repaired by their own rules, a value of a closed
    /// set that is none its default. Pure and total; part of <see cref="Storage.StateRepair"/>'s walk.
    /// </summary>
    public IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        CornerRadiusPx = Math.Clamp(Sanitise(CornerRadiusPx, DefaultCornerRadiusPx), 0, 32);
        FontScale = Math.Clamp(Sanitise(FontScale, 1), 0.5, 2.0);
        BorderWidthPx = Math.Clamp(Sanitise(BorderWidthPx, 1), 0, 8);
        BorderOpacity = Math.Clamp(Sanitise(BorderOpacity, 1), 0, 1);
        IntroAnimationMs = IntroMotion.ClampMs(Sanitise(IntroAnimationMs, IntroMotion.DefaultMs));

        // A malformed colour becomes empty, which means "use the theme's own" - the one value that
        // is always renderable. Keeping the bad string would leave the widget with no fill at all.
        PrimaryColor = HexColor.Normalise(PrimaryColor);
        BorderColor = HexColor.Normalise(BorderColor);

        // [S-6561] The two fill choices: an unknown kind or a colour that is not one is the default,
        // the opacity 0..1, loudly. A missing one is the default, silently.
        Background ??= new FillChoice();
        repairs.AddRange(Background.Repair("widget background"));
        ItemBackground ??= new FillChoice();
        repairs.AddRange(ItemBackground.Repair("item background"));

        // [S-6543] The item text colour: a colour or empty (the theme's text colours), loudly.
        var (itemText, itemTextRepair) = ItemLook.RepairTextColour(ItemTextColor);
        ItemTextColor = itemText;
        if (itemTextRepair is not null)
        {
            repairs.Add(itemTextRepair);
        }

        if (string.IsNullOrWhiteSpace(FontFamily))
        {
            FontFamily = "system";
        }

        if (!Enum.IsDefined(BorderStyle))
        {
            BorderStyle = WidgetBorderStyle.Solid;
        }

        if (!Enum.IsDefined(Material))
        {
            Material = CardMaterial.Solid;
        }

        // [S-2130] The look tokens: a missing set is a fresh one, silently; a value out of its range, loudly.
        Tokens ??= new LookTokens();
        repairs.AddRange(Tokens.Repair());

        if (!Enum.IsDefined(IntroAnimation))
        {
            IntroAnimation = IntroAnimation.Scale;
        }

        if (!Enum.IsDefined(Theme))
        {
            Theme = AppTheme.System;
        }

        // [S-704] The transition/unlock animations, on the same terms as the rest.
        TransitionAnimationMs = IntroMotion.ClampMs(Sanitise(TransitionAnimationMs, IntroMotion.DefaultMs));
        UnlockAnimationMs = IntroMotion.ClampMs(Sanitise(UnlockAnimationMs, IntroMotion.DefaultMs));

        if (!Enum.IsDefined(TransitionAnimation))
        {
            TransitionAnimation = IntroAnimation.Scale;
        }

        if (!Enum.IsDefined(UnlockAnimation))
        {
            UnlockAnimation = IntroAnimation.Scale;
        }

        return repairs;
    }

    private static double Sanitise(double value, double fallback)
        => double.IsFinite(value) ? value : fallback;
}
