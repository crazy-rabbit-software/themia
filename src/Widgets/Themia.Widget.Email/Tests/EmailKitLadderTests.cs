using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Email.Tests;

/// <summary>
/// [S-7032]..[S-7034] The kit's lined ladder is Mail's (<see cref="MailLayout"/>), rung for rung, at
/// every size in a grid and for every style and field choice: the proof that lets MailLayout go.
/// </summary>
public sealed class EmailKitLadderTests
{
    private static KitColumn Kit(MailColumn column) => column switch
    {
        MailColumn.From => KitColumn.Title,
        MailColumn.Subject => KitColumn.Subtitle,
        MailColumn.Date => KitColumn.When,
        MailColumn.Preview => KitColumn.Preview,
        _ => KitColumn.Badge,
    };

    public static TheoryData<EmailStyle, bool, bool, int> Configs()
    {
        var data = new TheoryData<EmailStyle, bool, bool, int>();
        foreach (var style in Enum.GetValues<EmailStyle>())
        {
            foreach (var subject in new[] { true, false })
            {
                foreach (var account in new[] { true, false })
                {
                    data.Add(style, subject, account, style == EmailStyle.List ? 3 : 2);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Configs))]
    public void S7032_the_kits_lined_ladder_is_mails(EmailStyle style, bool subject, bool account, int previewLines)
    {
        var fields = new List<MailListField>();
        if (subject)
        {
            fields.Add(MailListField.Subject);
        }

        if (account)
        {
            fields.Add(MailListField.Account);
        }

        var config = new EmailConfig { Style = style, ListFields = fields, PreviewLines = previewLines, RowColumns = [MailColumn.Account, MailColumn.From, MailColumn.Subject, MailColumn.Date] };
        var shows = KitShow.Lead | KitShow.When
            | (subject ? KitShow.Subtitle : KitShow.None)
            | (account ? KitShow.Badges : KitShow.None)
            | (config.ShowsPreview ? KitShow.Preview : KitShow.None);
        var spec = new KitSpec(style == EmailStyle.Table ? KitRowLook.Table : KitRowLook.TwoLine, KitShow.All, () => string.Empty, () => string.Empty, KitTime.Compact);
        var choices = new KitChoices(shows, config.EffectivePreviewLines, [.. config.EffectiveColumns.Select(Kit)]);

        for (var width = 0; width <= 1200; width += 10)
        {
            for (var height = 0; height <= 720; height += 8)
            {
                var area = new WidgetArea(width, height);
                var rows = MailLayout.Rows(config, width, height);
                var plan = KitLadder.Plan(area, spec, choices);
                var at = $"{style} {width}x{height}";

                Assert.True(rows.Envelope == plan.Lead, at);
                Assert.True((height > 0 && height < 200) == plan.Compact, at);

                if (style == EmailStyle.Table)
                {
                    var inner = Math.Max(0, width - (2 * (16 + 1.5)) - (rows.Envelope ? 22 : 0));
                    var columns = MailLayout.Columns(config.EffectiveColumns, inner, 8);
                    Assert.Equal(columns.Select(c => (Kit(c.Column), c.Width, c.Star)), plan.Table!.Select(c => (c.Column, c.Width, c.Star)));

                    var room = new Disclosure(area);
                    room.Reserve(32);
                    Assert.True((height >= 140 && room.Take(FluentType.Caption.LineHeight + 4)) == plan.ColumnHeader, at);
                    continue;
                }

                Assert.True(rows.Inline == (plan.Look == KitRowLook.Line), at);
                Assert.True(rows.Subject == plan.Subtitle, at);
                Assert.True(rows.Account == plan.Badges, at);
                Assert.True(rows.Time == plan.When, at);
                Assert.True(rows.Preview == (plan.PreviewLines > 0), at);
                if (rows.Preview)
                {
                    Assert.True(rows.PreviewLines == plan.PreviewLines, at);
                }

                Assert.True((rows.Inline && area.WidthClass >= SizeClass.Huge) == plan.InlineActions, at);
            }
        }
    }
}
