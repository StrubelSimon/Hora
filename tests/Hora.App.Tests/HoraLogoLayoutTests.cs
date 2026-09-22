using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hora.App.Tests.TestDoubles;
using Hora.App.Views;

namespace Hora.App.Tests;

/// <summary>
/// Das Hora-Zeichen wird mit einer Höhenangabe eingesetzt (Fensterleiste 16, Anmeldekarte 24); die
/// Breite ergibt sich aus dem Seitenverhältnis 15:16 der Vorlage. Diese Tests halten fest, dass es
/// dabei neben seinem Nachbarn Platz belegt und vollständig innerhalb dieses Platzes gezeichnet
/// wird – ein Zeichen, das über seine Nachbarn hinweg läuft, fällt in der 40 Punkt hohen
/// Fensterleiste sonst erst im fertigen Bild auf.
/// </summary>
public class HoraLogoLayoutTests
{
    [StaFact]
    public void The_logo_takes_up_room_next_to_the_word_mark()
    {
        StaRunner.Run(() =>
        {
            var logo = new HoraLogo { Height = 16, VerticalAlignment = VerticalAlignment.Center };
            var wordMark = new TextBlock { Text = "Hora", Margin = new Thickness(8, 0, 0, 0) };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(logo);
            row.Children.Add(wordMark);

            row.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            row.Arrange(new Rect(row.DesiredSize));

            Assert.Equal(15d, logo.DesiredSize.Width, 3);
            Assert.Equal(16d, logo.DesiredSize.Height, 3);

            // Die Wortmarke beginnt hinter dem Zeichen, nicht darüber.
            var wordMarkLeft = wordMark.TranslatePoint(new Point(0, 0), row).X;
            Assert.True(wordMarkLeft >= 15d, $"Die Wortmarke beginnt bei {wordMarkLeft} statt hinter dem Zeichen.");
        });
    }

    [StaFact]
    public void Both_bars_of_the_h_lie_inside_the_reserved_width()
    {
        StaRunner.Run(() =>
        {
            var logo = new HoraLogo { Height = 16 };

            logo.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            logo.Arrange(new Rect(logo.DesiredSize));
            logo.UpdateLayout();

            // Beide Pfade zusammen ergeben das vollständige Zeichen: die zwei senkrechten Balken
            // und der Querbalken. Liegt etwas davon links von null oder rechts der belegten
            // Breite, ragt das Zeichen aus seinem Platz heraus.
            var drawn = Rect.Empty;
            foreach (var path in Descendants(logo).OfType<System.Windows.Shapes.Path>())
            {
                drawn.Union(path.TransformToAncestor(logo).TransformBounds(path.Data.Bounds));
            }

            Assert.False(drawn.IsEmpty, "Im Zeichen wurde kein einziger Pfad gefunden.");
            Assert.True(drawn.Left >= -0.5, $"Das Zeichen beginnt bei {drawn.Left} und ragt nach links heraus.");
            Assert.True(drawn.Right <= 15.5, $"Das Zeichen endet bei {drawn.Right} und ragt über seine Breite hinaus.");
        });
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
