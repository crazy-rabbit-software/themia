using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Shell;

/// <summary>
/// [S-881] One notice as drawn: Windows 11's notification shape — a severity mark, a bold title, a
/// line of detail, an optional button, and a close button — on an opaque flyout surface.
/// <para>
/// Opaque rather than acrylic: the surface window is genuinely transparent, so an in-app acrylic
/// brush has nothing behind it to blur and reads as a hole. Every control refuses focus on
/// interaction: the surface never activates for a notice ([S-880]), and a click on its button must
/// not take the keyboard from the widget holding the lease.
/// </para>
/// </summary>
internal sealed partial class NoticeCard : Grid
{
    private const string Scope = "notice";

    /// <summary>Windows' own toast width.</summary>
    public const double CardWidth = ShellMetrics.Notice.Width;

    /// <summary>How far the severity glyph stands down to centre on the title's line.</summary>
    private const double GlyphDrop = (FamilyType.BodyLine - FamilyShape.Icon) / 2;

    public NoticeCard(Notice notice, Action<NoticeCard> dismiss)
    {
        ArgumentNullException.ThrowIfNull(notice);
        ArgumentNullException.ThrowIfNull(dismiss);

        Notice = notice;

        // [S-6706] No success green: a notice that went well is the accent's, as Info is; a warning and
        // an error are the caution and critical roles.
        var (glyph, role) = notice.Kind switch
        {
            NoticeKind.Success => (Glyphs.Success, ColourRole.Accent),
            NoticeKind.Warning => (Glyphs.Warning, ColourRole.Caution),
            NoticeKind.Error => (Glyphs.ErrorBadge, ColourRole.Critical),
            _ => (Glyphs.Info, ColourRole.Accent),
        };

        // The 16 glyph on the title's 20 line: as far down as centres it there.
        var icon = Family.Glyph(glyph, FamilyShape.Icon, role);
        icon.VerticalAlignment = VerticalAlignment.Top;
        icon.Margin = new Thickness(0, GlyphDrop, FamilySpace.S3, 0);

        var title = Family.Text(FamilyType.BodyStrong, ColourRole.TextPrimary, notice.Title);
        title.TextWrapping = TextWrapping.Wrap;

        var text = new StackPanel { Children = { title } };

        if (!string.IsNullOrWhiteSpace(notice.Message))
        {
            var message = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
            message.Text = notice.Message;
            message.TextWrapping = TextWrapping.Wrap;
            message.MaxLines = 4;
            message.TextTrimming = TextTrimming.CharacterEllipsis;
            text.Children.Add(message);
        }

        // A notice that is only a title ("Deleted 3 widgets · Undo") keeps its action on the title's
        // line, so it stays one row; with a detail line the button goes under it, as Windows' toasts
        // do, so the detail keeps the card's width.
        Button? actionButton = null;
        var inline = string.IsNullOrWhiteSpace(notice.Message);
        if (notice.Action is { } action)
        {
            var button = new Button
            {
                Content = action.Label,
                Margin = inline ? new Thickness(FamilySpace.S3, -FamilySpace.S1, 0, 0) : new Thickness(0, FamilySpace.S2, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                AllowFocusOnInteraction = false,
                MinWidth = inline ? 0 : ShellMetrics.Notice.ActionMinWidth,
            };
            button.Click += (_, _) =>
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    Log.Error(Scope, $"the notice action '{action.Label}' threw", ex);
                }

                dismiss(this);
            };
            if (inline)
            {
                actionButton = button;
            }
            else
            {
                text.Children.Add(button);
            }
        }

        var close = new Button
        {
            Content = Family.Glyph(Glyphs.Dismiss, FamilyShape.Icon, ColourRole.TextSecondary),
            Width = FamilyShape.HeaderButton,
            Height = FamilyShape.HeaderButton,
            Padding = new Thickness(0),
            Margin = new Thickness(FamilySpace.S2, -FamilySpace.ButtonOverflow, -FamilySpace.S1, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Background = Family.Brush(ColourRole.Clear),
            BorderThickness = new Thickness(0),
            AllowFocusOnInteraction = false,
        };
        ToolTipService.SetToolTip(close, Str.Shell_Notice_Dismiss.Text());
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(close, Str.Shell_Notice_Dismiss.Text());
        close.Click += (_, _) => dismiss(this);

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        Grid.SetColumn(text, 1);
        Grid.SetColumn(close, 3);
        row.Children.Add(icon);
        row.Children.Add(text);
        if (actionButton is not null)
        {
            Grid.SetColumn(actionButton, 2);
            row.Children.Add(actionButton);
        }

        row.Children.Add(close);

        Children.Add(row);
        Width = CardWidth;
        Padding = new Thickness(FamilySpace.S4, FamilySpace.S3, FamilySpace.S3, FamilySpace.S4);
        CornerRadius = new CornerRadius(ShellMetrics.OverlayRadius);
        BorderThickness = new Thickness(FamilyShape.Hairline);
        BorderBrush = ShellBrush.Of(ShellRole.FlyoutStroke);
        Background = ShellBrush.Of(ShellRole.Raised);
        BackgroundSizing = BackgroundSizing.OuterBorderEdge;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(this, notice.Message is null ? notice.Title : $"{notice.Title}. {notice.Message}");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(this, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
    }

    public Notice Notice { get; }
}
