using System.Windows;
using System.Windows.Media;

namespace Hora.App.Controls;

/// <summary>
/// Beschneidet ein Element auf ein Rechteck mit abgerundeten Ecken.
/// </summary>
/// <remarks>
/// WPF bringt dafür nichts mit: ClipToBounds beschneidet auf das RECHTECK eines Elements und weiß
/// nichts vom CornerRadius der umgebenden Border. Inhalt, der bis an die Kante einer abgerundeten
/// Karte läuft, füllt die Ecken deshalb quadratisch aus und steht sichtbar über die Rundung hinaus
/// – in der Wochenansicht die erste und die letzte Tageszeile, sobald sie unter dem Mauszeiger
/// einen Hintergrund bekommen, und der Streifen des heutigen Tages an der unteren linken Ecke.
///
/// Clip ist keine abhaengige Eigenschaft, die sich an ActualWidth binden liesse; die Geometrie muss
/// bei jeder Groessenaenderung neu gesetzt werden. Genau das nimmt diese angehaengte Eigenschaft ab.
///
/// Anzugeben ist der INNEN-Radius: bei einer Karte mit CornerRadius 12 und einem Rahmen von 1
/// Bildpunkt sind das 11. Ein groesserer Wert schnitte in den Rahmen hinein, ein kleinerer liesse
/// eine helle Sichel zwischen Inhalt und Rahmen stehen.
/// </remarks>
public static class RoundedClip
{
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.RegisterAttached(
        "Radius",
        typeof(double),
        typeof(RoundedClip),
        new PropertyMetadata(0d, OnRadiusChanged));

    public static double GetRadius(DependencyObject element) => (double)element.GetValue(RadiusProperty);

    public static void SetRadius(DependencyObject element, double value) => element.SetValue(RadiusProperty, value);

    private static void OnRadiusChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not FrameworkElement target)
        {
            return;
        }

        // Erst abmelden, dann gegebenenfalls neu anmelden: Wird der Radius ein zweites Mal gesetzt,
        // haengt der Handler sonst doppelt am Ereignis.
        target.SizeChanged -= OnSizeChanged;

        if (e.NewValue is double radius && radius > 0)
        {
            target.SizeChanged += OnSizeChanged;
        }

        UpdateClip(target);
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateClip((FrameworkElement)sender);

    private static void UpdateClip(FrameworkElement target)
    {
        var radius = GetRadius(target);

        // Vor dem ersten Anordnen sind ActualWidth und ActualHeight null. Ein Clip der Groesse null
        // wuerde das Element unsichtbar machen, deshalb bleibt es bis dahin ungeschnitten; das
        // SizeChanged-Ereignis holt den Clip nach, sobald die Groesse feststeht.
        if (radius <= 0 || target.ActualWidth <= 0 || target.ActualHeight <= 0)
        {
            target.Clip = null;
            return;
        }

        target.Clip = new RectangleGeometry(
            new Rect(0, 0, target.ActualWidth, target.ActualHeight),
            radius,
            radius);
    }
}
