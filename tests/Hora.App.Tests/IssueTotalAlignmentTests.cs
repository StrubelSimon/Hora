using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;

namespace Hora.App.Tests;

/// <summary>
/// Im Abschnitt „Pro Ticket“ steht jede Zeile in einem eigenen Grid. Ohne gemeinsame Spaltenbreiten
/// misst jede Zeile ihre Auto-Spalten für sich: Ein längerer Ticketschlüssel oder eine längere Dauer
/// schiebt den Balken dieser Zeile gegenüber der Zeile darüber zur Seite. Die Vorlage vergibt dafür
/// SharedSizeGroups, die drei Ansichten setzen den Geltungsbereich auf ihrem ItemsControl. Diese
/// Tests halten fest, dass Balken und Dauer dadurch bündig untereinander stehen.
/// </summary>
public class IssueTotalAlignmentTests
{
    private const double ListWidth = 900;

    private static readonly IssueTotalRow[] MixedRows =
    [
        // Bewusst sehr unterschiedlich lang: kurzer gegen langen Schlüssel, „3h“ gegen „12h 45m“.
        new("AB-1", "Kurz", "3h", 1, "https://example.atlassian.net/browse/AB-1", 0.25),
        new("SYNSYN-987654", "Ein deutlich längerer Tickettitel als nebenan", "12h 45m", 4, "https://example.atlassian.net/browse/SYNSYN-987654", 1.0),
        new("MAS-143", "Mittel", "4h", 2, "https://example.atlassian.net/browse/MAS-143", 0.4),
        new("SYN-2712", "Noch ein Titel", "6h 30m", 3, "https://example.atlassian.net/browse/SYN-2712", 0.6),
    ];

    [StaFact]
    public void All_bars_share_one_width_regardless_of_key_and_duration_length()
    {
        StaRunner.Run(() =>
        {
            var list = BuildList(MixedRows);
            var bars = Descendants(list).OfType<ProgressBar>().ToList();

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
            var bars = Descendants(list).OfType<ProgressBar>().ToList();

            var lefts = bars
                .Select(bar => Math.Round(bar.TransformToAncestor(list).Transform(new Point(0, 0)).X, 2))
                .ToList();

            // Zwei Einträge je Zeile: Eintrag 0 und 2 stehen in der linken Spalte, 1 und 3 rechts.
            Assert.Equal(lefts[0], lefts[2]);
            Assert.Equal(lefts[1], lefts[3]);
        });
    }

    private static ItemsControl BuildList(IReadOnlyList<IssueTotalRow> rows)
    {
        var resources = new ResourceDictionary();
        foreach (var part in new[] { "Styles/Tokens.Light.xaml", "Styles/Theme.xaml" })
        {
            resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Hora;component/{part}", UriKind.Absolute),
            });
        }

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

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;

            foreach (var further in Descendants(child))
            {
                yield return further;
            }
        }
    }
}
