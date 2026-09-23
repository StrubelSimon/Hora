using System.Windows;
using System.Windows.Controls;
using Hora.App.ViewModels;

namespace Hora.App.Controls;

/// <summary>
/// Balken aus drei aneinandergesetzten Abschnitten: grün bis zum Tagessoll, orange für die Mehrzeit
/// darüber, rot jenseits der gesetzlichen Höchstarbeitszeit. Was wie lang ist, rechnet
/// <see cref="BarSegments"/> aus; dieses Steuerelement setzt die Anteile nur in Spaltenbreiten um.
/// </summary>
/// <remarks>
/// Eine ProgressBar kann das nicht: Sie kennt genau einen Balken und eine Spur. Die Vorlage steht
/// als schlüsselloser Stil in Theme.xaml und greift damit über den Typ; die Farben holt sie als
/// DynamicResource, damit ein Wechsel des Erscheinungsbilds sie mitnimmt.
///
/// Die Breiten sind Sternmaße, keine Bildpunkte: Das Gitter verteilt die verfügbare Breite im
/// Verhältnis der drei Anteile plus des unausgefüllten Rests, und zwar bei jeder Größenänderung neu.
/// </remarks>
public sealed class SegmentedRatioBar : Control
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments),
        typeof(BarSegments),
        typeof(SegmentedRatioBar),
        new PropertyMetadata(BarSegments.Empty, OnSegmentsChanged));

    private static readonly DependencyPropertyKey TargetWidthKey = DependencyProperty.RegisterReadOnly(
        nameof(TargetWidth), typeof(GridLength), typeof(SegmentedRatioBar), new PropertyMetadata(Star(0)));

    private static readonly DependencyPropertyKey OvertimeWidthKey = DependencyProperty.RegisterReadOnly(
        nameof(OvertimeWidth), typeof(GridLength), typeof(SegmentedRatioBar), new PropertyMetadata(Star(0)));

    private static readonly DependencyPropertyKey OverLimitWidthKey = DependencyProperty.RegisterReadOnly(
        nameof(OverLimitWidth), typeof(GridLength), typeof(SegmentedRatioBar), new PropertyMetadata(Star(0)));

    private static readonly DependencyPropertyKey RestWidthKey = DependencyProperty.RegisterReadOnly(
        nameof(RestWidth), typeof(GridLength), typeof(SegmentedRatioBar), new PropertyMetadata(Star(1)));

    private static readonly DependencyPropertyKey IsEmptyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsEmpty), typeof(bool), typeof(SegmentedRatioBar), new PropertyMetadata(true));

    public static readonly DependencyProperty TargetWidthProperty = TargetWidthKey.DependencyProperty;

    public static readonly DependencyProperty OvertimeWidthProperty = OvertimeWidthKey.DependencyProperty;

    public static readonly DependencyProperty OverLimitWidthProperty = OverLimitWidthKey.DependencyProperty;

    public static readonly DependencyProperty RestWidthProperty = RestWidthKey.DependencyProperty;

    public static readonly DependencyProperty IsEmptyProperty = IsEmptyKey.DependencyProperty;

    public BarSegments Segments
    {
        get => (BarSegments)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public GridLength TargetWidth => (GridLength)GetValue(TargetWidthProperty);

    public GridLength OvertimeWidth => (GridLength)GetValue(OvertimeWidthProperty);

    public GridLength OverLimitWidth => (GridLength)GetValue(OverLimitWidthProperty);

    /// <summary>Der unausgefüllte Rest bis zur vollen Breite; er bleibt leere Spur.</summary>
    public GridLength RestWidth => (GridLength)GetValue(RestWidthProperty);

    /// <summary>Wahr, solange nichts erfasst ist: Die Spur steht dann blasser da.</summary>
    public bool IsEmpty => (bool)GetValue(IsEmptyProperty);

    private static void OnSegmentsChanged(DependencyObject element, DependencyPropertyChangedEventArgs e) =>
        ((SegmentedRatioBar)element).ApplySegments(e.NewValue as BarSegments ?? BarSegments.Empty);

    private void ApplySegments(BarSegments segments)
    {
        var target = Math.Max(segments.TargetShare, 0.0);
        var overtime = Math.Max(segments.OvertimeShare, 0.0);
        var overLimit = Math.Max(segments.OverLimitShare, 0.0);
        var rest = Math.Max(1.0 - target - overtime - overLimit, 0.0);

        SetValue(TargetWidthKey, Star(target));
        SetValue(OvertimeWidthKey, Star(overtime));
        SetValue(OverLimitWidthKey, Star(overLimit));
        SetValue(RestWidthKey, Star(rest));
        SetValue(IsEmptyKey, target + overtime + overLimit <= 0.0);
    }

    /// <summary>
    /// Ein Sternmaß von genau null wäre keines: WPF liest GridLength(0, Star) als „so wenig wie
    /// möglich“ und verteilt den Rest auf die übrigen Sternspalten – das ergibt hier dasselbe
    /// Ergebnis, nämlich Breite null. Der Umweg über Math.Max hält den Wert nur von negativen
    /// Anteilen frei, die GridLength mit einer Ausnahme quittieren würde.
    /// </summary>
    private static GridLength Star(double weight) => new(Math.Max(weight, 0.0), GridUnitType.Star);
}
