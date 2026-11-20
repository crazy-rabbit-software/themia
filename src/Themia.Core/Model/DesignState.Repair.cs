using Themia.Core.Widgets;

namespace Themia.Core.Model;

public sealed partial class DesignState
{
    /// <summary>
    /// [S-44] Bring a read design back inside its invariants, saying what changed: its numbers held to their ranges
    /// (<see cref="DesignNumbers"/>) and a value of a closed set that is none its default, one line each; a colour
    /// that is none the theme's own; its fills and look tokens repaired by their own rules. Pure and total; part of
    /// <see cref="Storage.StateRepair"/>'s walk.
    /// </summary>
    public IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        CornerRadiusPx = Held(DesignNumbers.CornerRadius, CornerRadiusPx, "cornerRadiusPx");
        FontScale = Held(DesignNumbers.FontScale, FontScale, "fontScale");
        BorderWidthPx = Held(DesignNumbers.BorderWidth, BorderWidthPx, "borderWidthPx");
        BorderOpacity = Held(DesignNumbers.BorderOpacity, BorderOpacity, "borderOpacity");
        IntroAnimationMs = Held(DesignNumbers.AnimationMs, IntroAnimationMs, "introAnimationMs");
        TransitionAnimationMs = Held(DesignNumbers.AnimationMs, TransitionAnimationMs, "transitionAnimationMs");
        UnlockAnimationMs = Held(DesignNumbers.AnimationMs, UnlockAnimationMs, "unlockAnimationMs");

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

        // [S-2130] The look tokens: a missing set is a fresh one, silently; a value out of its range, loudly.
        Tokens ??= new LookTokens();
        repairs.AddRange(Tokens.Repair());

        BorderStyle = Member(BorderStyle, WidgetBorderStyle.Solid, "borderStyle");
        Material = Member(Material, CardMaterial.Solid, "material");
        Theme = Member(Theme, AppTheme.System, "theme");

        // [S-704] The intro, transition and unlock animations, on the same terms.
        IntroAnimation = Member(IntroAnimation, IntroAnimation.Scale, "introAnimation");
        TransitionAnimation = Member(TransitionAnimation, IntroAnimation.Scale, "transitionAnimation");
        UnlockAnimation = Member(UnlockAnimation, IntroAnimation.Scale, "unlockAnimation");

        return repairs;

        // A number held to its range; not finite, its default.
        double Held(DesignNumber number, double was, string key)
        {
            var now = number.Clamp(was);
            if (!now.Equals(was))
            {
                repairs.Add($"design '{key}' {was} is out of {number.Min}..{number.Max}; using {now}");
            }

            return now;
        }

        // A value of a closed set, or its default where it names no member (a number in the file).
        T Member<T>(T was, T fallback, string key)
            where T : struct, Enum
        {
            if (Enum.IsDefined(was))
            {
                return was;
            }

            repairs.Add($"design '{key}' {was} is not one; using {fallback}");
            return fallback;
        }
    }
}
