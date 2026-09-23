using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

/// <summary>
/// Im Abschnitt „Pro Ticket“ steht jede Zeile in einem eigenen Grid. Ohne gemeinsame Spaltenbreiten
/// misst jede Zeile ihre Auto-Spalten für sich: Ein längerer Ticketschlüssel oder eine längere Dauer
/// schiebt den Balken dieser Zeile gegenüber der Zeile darüber zur Seite. Die Vorlage vergibt dafür
/// SharedSizeGroups, die drei Ansichten setzen den Geltungsbereich auf ihrem ItemsControl. Diese
/// Tests halten fest, dass Balken und Dauer dadurch bündig untereinander stehen — und dass die
/// Vorlage überhaupt etwas anzeigt: Ein gebrochener Bindungspfad bliebe sonst unbemerkt.
/// </summary>
public class IssueTotalAlignmentTests
{
    private const double ListWidth = 900;

    private static readonly IssueTotalRow[] MixedRows =
    [
        // Bewusst sehr unterschiedlich lang: kurzer gegen langen Schlüssel, „3h“ gegen „12h 45m“.
        new(WorklogSource.Jira, "AB-1", "Kurz", "3h", 1, "https://example.atlassian.net/browse/AB-1", 0.25),
        new(WorklogSource.Jira, "SYNSYN-987654", "Ein deutlich längerer Tickettitel als nebenan", "12h 45m", 4, "https://example.atlassian.net/browse/SYNSYN-987654", 1.0),
        new(WorklogSource.Jira, "MAS-143", "Mittel", "4h", 2, "https://example.atlassian.net/browse/MAS-143", 0.4),
        new(WorklogSource.Jira, "SYN-2712", "Noch ein Titel", "6h 30m", 3, "https://example.atlassian.net/browse/SYN-2712", 0.6),
    ];

    [StaFact]
    public void All_bars_share_one_width_regardless_of_key_and_duration_length()
    {
        StaRunner.Run(() =>
        {
            var list = BuildList(MixedRows);
            var bars = ThemeResources.Descendants(list).OfType<ProgressBar>().ToList();

            Assert.Equal(MixedRows.Length, bars.Count);

            var widths = bars.Select(bar => Math.Round(bar.ActualWidth, 2)).Distinct().ToList();
            Assert.True(
                widths.Count == 1,
                $"Die Balken sind unterschiedlich breit: {string.Join(", ", widths)}.");
            Assert.True(widths[0] > 0, "Die Balken haben keine Breite.");
        });
    }

    [StaFact]
    public void Bars_in_the_same_column_start_at_the_same_position()
    {
        StaRunner.Run(() =>
        {
            var list = BuildList(MixedRows);
            var bars = ThemeResources.Descendants(list).OfType<ProgressBar>().ToList();

            var lefts = bars
                .Select(bar => Math.Round(bar.TransformToAncestor(list).Transform(new Point(0, 0)).X, 2))
                .ToList();

            // Zwei Einträge je Zeile: Eintrag 0 und 2 stehen in der linken Spalte, 1 und 3 rechts.
            Assert.Equal(lefts[0], lefts[2]);
            Assert.Equal(lefts[1], lefts[3]);
        });
    }

    /// <summary>
    /// WPF meldet einen gebrochenen Bindungspfad nicht: Die Vorlage rendert dann stumm leere Spalten.
    /// Die beiden Tests oben blieben davon unberührt — vier leere Spalten sind gleich breit, und die
    /// MinWidth des Balkens hält auch die Breitenprüfung aufrecht. Dieser Test liest deshalb die
    /// gerenderten Werte zurück und nagelt jede Bindung der Vorlage auf das Zeilenobjekt fest.
    /// </summary>
    [StaFact]
    public void Template_bindings_fill_key_summary_bar_total_and_url_from_the_row()
    {
        StaRunner.Run(() =>
        {
            var list = BuildList(MixedRows);

            // Je Zeile genau eine Schaltfläche: der Ticketschlüssel, der die Adresse als Befehlswert trägt.
            var keyButtons = ThemeResources.Descendants(list).OfType<Button>().ToList();
            Assert.Equal(MixedRows.Length, keyButtons.Count);
            Assert.Equal(MixedRows.Select(row => row.ItemKey), keyButtons.Select(button => button.Content as string));
            Assert.Equal(MixedRows.Select(row => row.ItemUrl), keyButtons.Select(button => button.CommandParameter as string));

            // Spalte 1 trägt den Titel, Spalte 3 die Gesamtdauer; die Spaltennummer trennt sie von
            // den Textblöcken, die innerhalb der Schaltflächenvorlage stecken.
            Assert.Equal(MixedRows.Select(row => row.ItemSummary), TextsInColumn(list, 1));
            Assert.Equal(MixedRows.Select(row => row.TotalText), TextsInColumn(list, 3));

            var bars = ThemeResources.Descendants(list).OfType<ProgressBar>().ToList();
            Assert.Equal(MixedRows.Select(row => row.BarRatio), bars.Select(bar => bar.Value));
        });
    }

    /// <summary>
    /// Im Abschnitt „Pro Ticket“ stehen Jira-Tickets und Asana-Projekte in einer gemeinsamen, nach
    /// Dauer sortierten Liste. Ohne Herkunftszeichen wären sie nicht auseinanderzuhalten: Ein
    /// Asana-Projekt heißt „Marketing“, nicht „MAS-143“, aber das weiß nur, wer beide Welten kennt.
    /// Das Zeichen steht ausschließlich bei Asana, damit die gewohnte Jira-Ansicht unverändert
    /// bleibt — die Vorlage ist dieselbe in Tages-, Wochen- und Monatsansicht.
    /// </summary>
    [StaFact]
    public void Only_asana_rows_carry_the_source_badge()
    {
        StaRunner.Run(() =>
        {
            IssueTotalRow[] rows =
            [
                new(WorklogSource.Jira, "AB-1", "Ein Ticket", "3h", 1, "https://example.atlassian.net/browse/AB-1", 0.5),
                new(WorklogSource.Asana, "Marketing", "Ein Projekt", "6h", 2, "https://app.asana.com/0/1", 1.0),
            ];

            var list = BuildList(rows);

            // Das Zeichen ist der einzige Rahmen der Vorlage mit einem Textblock darin; der
            // Ticketpunkt davor ist leer.
            var badges = ThemeResources.Descendants(list)
                .OfType<Border>()
                .Where(border => border.Child is TextBlock)
                .ToList();

            Assert.Equal(rows.Length, badges.Count);
            Assert.Equal(Visibility.Collapsed, badges[0].Visibility);
            Assert.Equal(Visibility.Visible, badges[1].Visibility);
            Assert.Equal("Asana", ((TextBlock)badges[1].Child).Text);
        });
    }

    /// <summary>Die Texte aller Textblöcke, die unmittelbar in der genannten Gitterspalte stehen.</summary>
    private static IEnumerable<string> TextsInColumn(DependencyObject root, int column) =>
        ThemeResources.Descendants(root)
            .OfType<TextBlock>()
            .Where(text => Grid.GetColumn(text) == column)
            .Select(text => text.Text)
            .ToList();

    private static ItemsControl BuildList(IReadOnlyList<IssueTotalRow> rows)
    {
        var resources = ThemeResources.Load();

        var panel = new ItemsPanelTemplate();
        panel.VisualTree = new FrameworkElementFactory(typeof(UniformGrid));
        panel.VisualTree.SetValue(UniformGrid.ColumnsProperty, 2);

        var list = new ItemsControl
        {
            Resources = resources,
            ItemTemplate = (DataTemplate)resources["IssueTotalItemTemplate"],
            ItemsPanel = panel,
            ItemsSource = rows,
            Width = ListWidth,
        };
        // Derselbe Geltungsbereich, den DayView, WeekView und MonthView auf ihrem ItemsControl setzen.
        Grid.SetIsSharedSizeScope(list, true);

        // Der Rahmen ist nicht schmückendes Beiwerk: Misst man das ItemsControl ohne umgebendes
        // Element, baut WPF gar keinen visuellen Baum auf und es entsteht keine einzige Zeile.
        var host = new Border { Child = list, Width = ListWidth };
        host.Measure(new Size(ListWidth, double.PositiveInfinity));
        host.Arrange(new Rect(0, 0, ListWidth, host.DesiredSize.Height));
        host.UpdateLayout();

        return list;
    }
}
