using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Hora.App.Tests.TestDoubles;

/// <summary>
/// Lädt die echten Ressourcen der App — Farbtoken und Theme.xaml — und durchsucht den daraus
/// gebauten visuellen Baum.
/// </summary>
/// <remarks>
/// Vorlagentests hängen daran, dass sie gegen den Bestand prüfen und nicht gegen eine Abschrift:
/// WPF meldet einen gebrochenen Bindungspfad nicht, eine Vorlage rendert dann stumm leere Flächen.
/// Nur der Aufbau der echten Vorlage mit echten Zeilenobjekten fängt das ab.
/// </remarks>
internal static class ThemeResources
{
    public static ResourceDictionary Load()
    {
        var resources = new ResourceDictionary();
        foreach (var part in new[] { "Styles/Tokens.Light.xaml", "Styles/Theme.xaml" })
        {
            resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Hora;component/{part}", UriKind.Absolute),
            });
        }

        return resources;
    }

    /// <summary>
    /// Baut ein Element mit den echten Ressourcen der App auf, misst es und reicht es an
    /// <paramref name="assert"/> weiter. Die Ressourcen müssen dafür an Application.Current hängen:
    /// Die StaticResource-Verweise einer Ansicht werden in deren Konstruktor aufgelöst, da gibt es
    /// noch kein Elternteil, an dem ein eigenes Wörterbuch hinge. Der vorherige Stand wird danach
    /// zurückgesetzt, damit kein Test eine Spur im Prozess hinterlässt.
    /// </summary>
    public static void Render<TElement>(double width, Func<TElement> build, Action<TElement> assert)
        where TElement : FrameworkElement
    {
        StaRunner.Run(() =>
        {
            var application = Application.Current;
            var previousResources = application.Resources;
            application.Resources = Load();

            try
            {
                var element = build();
                element.Width = width;

                // Das umgebende Element ist nicht schmückendes Beiwerk: Ohne es baut WPF gar keinen
                // visuellen Baum auf, und es entsteht keine einzige Zeile.
                var host = new Border { Child = element, Width = width };
                host.Measure(new Size(width, double.PositiveInfinity));
                host.Arrange(new Rect(0, 0, width, host.DesiredSize.Height));
                host.UpdateLayout();

                assert(element);
            }
            finally
            {
                application.Resources = previousResources;
            }
        });
    }

    public static IEnumerable<DependencyObject> Descendants(DependencyObject root)
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
