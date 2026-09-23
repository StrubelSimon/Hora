using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

/// <summary>
/// Die Zeilenvorlage der Tagesansicht (WorklogRowTemplate in Styles/Theme.xaml) hatte bis zu diesem
/// Task kein Netz: Sie lag unmittelbar im ItemsControl von DayView.xaml, und ein toter Bindungspfad
/// wäre dort still geblieben — WPF meldet ihn nicht, die Zeile stünde einfach leer da. Seit der
/// Asana-Block dieselbe Vorlage mitbenutzt, steht sie als Ressource in Theme.xaml und lässt sich
/// wie die Ticketvorlage im Abschnitt „Pro Ticket“ aufbauen und zurücklesen.
/// </summary>
public class WorklogRowTemplateTests
{
    private const double ListWidth = 900;

    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    private static readonly WorklogRow[] MixedRows =
    [
        Row(WorklogEntry.Jira(
            "SYN-2712",
            "Angebot schreiben",
            new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.FromHours(2)),
            TimeSpan.FromMinutes(90),
            "Abstimmung mit dem Kunden",
            "https://example.atlassian.net")),
        Row(WorklogEntry.Jira(
            "MAS-143",
            "Ohne Kommentar",
            new DateTimeOffset(2026, 9, 21, 14, 0, 0, TimeSpan.FromHours(2)),
            TimeSpan.FromHours(2),
            string.Empty,
            "https://example.atlassian.net")),
        // Ohne Startzeit, wie jeder Asana-Eintrag.
        Row(new WorklogEntry(
            WorklogSource.Asana,
            "Marketing",
            "Kampagne Herbst",
            new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2)),
            TimeSpan.FromMinutes(45),
            string.Empty,
            "https://app.asana.com/0/1",
            HasStartTime: false)),
    ];

    private static WorklogRow Row(WorklogEntry entry) => WorklogRow.From(entry, Berlin);

    /// <summary>
    /// Nagelt jede Bindung der Vorlage auf das Zeilenobjekt fest. Ginge ein Pfad verloren, bliebe
    /// die betroffene Stelle leer, ohne dass Bau oder Layout etwas meldeten.
    /// </summary>
    [StaFact]
    public void Template_bindings_fill_key_summary_comment_duration_and_url_from_the_row()
    {
        StaRunner.Run(() =>
        {
            var list = BuildList(MixedRows);

            // Je Zeile genau eine Schaltfläche: der Schlüssel, der die Adresse als Befehlswert trägt.
            var keyButtons = ThemeResources.Descendants(list).OfType<Button>().ToList();
            Assert.Equal(MixedRows.Length, keyButtons.Count);
            Assert.Equal(MixedRows.Select(row => row.ItemKey), keyButtons.Select(button => button.Content as string));
            Assert.Equal(MixedRows.Select(row => row.ItemUrl), keyButtons.Select(button => button.CommandParameter as string));

            // Je Zeile vier Textblöcke, in dieser Reihenfolge: Schlüssel (in der Schaltflächenvorlage),
            // Titel, Kommentar und Dauer. Der Kommentar steht auch dann im Baum, wenn er leer und
            // damit ausgeblendet ist.
            foreach (var (row, texts) in MixedRows.Zip(RowTexts(list)))
            {
                Assert.Equal(new[] { row.ItemKey, row.ItemSummary, row.Comment, row.DurationText }, texts);
            }
        });
    }

    /// <summary>
    /// Der Kommentar bekommt eine eigene Unterzeile und die Zeile dafür 14 Bildpunkte mehr Höhe.
    /// Ohne Kommentar bleibt beides aus — sonst klaffte unter jeder kommentarlosen Zeile eine Lücke.
    /// </summary>
    [StaFact]
    public void A_comment_shows_up_as_a_subline_and_makes_the_row_taller()
    {
        StaRunner.Run(() =>
        {
            var list = BuildList(MixedRows);
            var comments = TextBlocksAt(list, index: 2);

            Assert.Equal(Visibility.Visible, comments[0].Visibility);
            Assert.Equal("Abstimmung mit dem Kunden", comments[0].Text);
            Assert.Equal(Visibility.Collapsed, comments[1].Visibility);
            Assert.Equal(Visibility.Collapsed, comments[2].Visibility);

            var heights = RowBorders(list).Select(border => border.ActualHeight).ToList();
            Assert.Equal(58, heights[0]);
            Assert.Equal(44, heights[1]);
            Assert.Equal(44, heights[2]);
        });
    }

    /// <summary>
    /// Keine Zeile zeigt eine Uhrzeit. Die Spalte dafür wurde auf Wunsch des Auftraggebers entfernt,
    /// und für Asana gäbe es ohnehin keine: Solche Einträge tragen nur einen Tag. Stünde dort je
    /// wieder eine Zeitspanne, dürfte sie jedenfalls nicht für Einträge ohne Startzeit erfunden
    /// werden — dieser Test bemerkt beides.
    /// </summary>
    [StaFact]
    public void No_row_shows_a_clock_time()
    {
        StaRunner.Run(() =>
        {
            var list = BuildList(MixedRows);
            var clock = new Regex(@"\d{1,2}:\d{2}");

            var withTime = ThemeResources.Descendants(list)
                .OfType<TextBlock>()
                .Select(text => text.Text)
                .Where(text => clock.IsMatch(text))
                .ToList();

            Assert.True(withTime.Count == 0, $"Uhrzeit in der Zeile: {string.Join(", ", withTime)}.");
        });
    }

    /// <summary>Die Dauer steht rechts und ist bei jeder Zeile gefüllt, auch ohne Startzeit.</summary>
    [StaFact]
    public void Every_row_ends_with_its_duration()
    {
        StaRunner.Run(() =>
        {
            var list = BuildList(MixedRows);
            var durations = TextBlocksAt(list, index: 3);

            Assert.Equal(new[] { "1h 30m", "2h", "45m" }, durations.Select(text => text.Text));
            Assert.All(durations, text => Assert.Equal(2, Grid.GetColumn(text)));
        });
    }

    /// <summary>Die vier Texte jeder Zeile in der Reihenfolge des visuellen Baums.</summary>
    private static IEnumerable<string[]> RowTexts(ItemsControl list) =>
        ThemeResources.Descendants(list)
            .OfType<TextBlock>()
            .Select(text => text.Text)
            .Chunk(4);

    /// <summary>Der <paramref name="index"/>-te Textblock jeder Zeile.</summary>
    private static List<TextBlock> TextBlocksAt(ItemsControl list, int index) =>
        ThemeResources.Descendants(list)
            .OfType<TextBlock>()
            .Chunk(4)
            .Select(row => row[index])
            .ToList();

    /// <summary>Der äußere Rahmen jeder Zeile — er trägt Höhe und Haarlinie.</summary>
    private static List<Border> RowBorders(ItemsControl list) =>
        ThemeResources.Descendants(list)
            .OfType<Border>()
            .Where(border => border.BorderThickness.Bottom > 0)
            .ToList();

    private static ItemsControl BuildList(IReadOnlyList<WorklogRow> rows)
    {
        var resources = ThemeResources.Load();

        var list = new ItemsControl
        {
            Resources = resources,
            ItemTemplate = (DataTemplate)resources["WorklogRowTemplate"],
            ItemsSource = rows,
            Width = ListWidth,
        };

        // Der Rahmen ist nicht schmückendes Beiwerk: Misst man das ItemsControl ohne umgebendes
        // Element, baut WPF gar keinen visuellen Baum auf und es entsteht keine einzige Zeile.
        var host = new Border { Child = list, Width = ListWidth };
        host.Measure(new Size(ListWidth, double.PositiveInfinity));
        host.Arrange(new Rect(0, 0, ListWidth, host.DesiredSize.Height));
        host.UpdateLayout();

        return list;
    }
}
