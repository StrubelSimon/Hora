using System.Windows;
using System.Windows.Controls;

namespace Hora.App.Tests.TestDoubles;

/// <summary>
/// Hängt die Wörterbücher der Anwendung an, bevor eine Ansicht aufgebaut wird.
/// </summary>
/// <remarks>
/// Die Wörterbücher gehören an die Anwendung und nicht an die fertige Ansicht: InitializeComponent
/// löst die StaticResource-Verweise schon beim Konstruieren auf, ein danach gesetztes
/// Resources-Wörterbuch käme zu spät. Im laufenden Programm besorgt das App.xaml.
/// </remarks>
internal static class AppResources
{
    public static void Ensure()
    {
        var application = Application.Current;
        if (application.Resources.MergedDictionaries.Count > 0)
        {
            return; // Ein Anwendungsobjekt je Prozess; die Tests dieser Assembly teilen es sich.
        }

        foreach (var part in new[] { "Styles/Tokens.Light.xaml", "Styles/Theme.xaml" })
        {
            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Hora;component/{part}", UriKind.Absolute),
            });
        }

        // Steht im laufenden Programm unmittelbar in App.xaml, nicht in einem der Wörterbücher.
        application.Resources["BoolToVisibility"] = new BooleanToVisibilityConverter();
    }

    /// <summary>Sucht den visuellen Baum ab; ohne Aufbau durch Measure/Arrange ist er leer.</summary>
    public static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            yield return child;

            foreach (var further in Descendants(child))
            {
                yield return further;
            }
        }
    }
}
