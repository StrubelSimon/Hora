using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using Hora.App.Controls;
using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;

namespace Hora.App.Tests;

/// <summary>
/// Baut den dreifarbigen Balken wirklich auf. Die Rechnung steckt in <see cref="BarSegments"/> und
/// ist dort geprüft; hier geht es um die Frage, ob die Anteile auch als Breiten auf dem Schirm
/// ankommen — ein verschriebener Vorlagenname oder eine fehlende Bindung fällt sonst nirgends auf.
/// </summary>
public class SegmentedRatioBarTests
{
    private const double BarWidth = 200;

    [StaFact]
    public void Each_segment_takes_its_share_of_the_width()
    {
        StaRunner.Run(() =>
        {
            var bar = Build(new BarSegments(0.5, 0.25, 0.125));

            Assert.Equal(100, WidthOf(bar, "PART_Target"), 3);
            Assert.Equal(50, WidthOf(bar, "PART_Overtime"), 3);
            Assert.Equal(25, WidthOf(bar, "PART_OverLimit"), 3);
        });
    }

    [StaFact]
    public void A_day_on_target_fills_the_bar_green_and_leaves_the_other_segments_out()
    {
        StaRunner.Run(() =>
        {
            var bar = Build(new BarSegments(1.0, 0.0, 0.0));

            Assert.Equal(BarWidth, WidthOf(bar, "PART_Target"), 3);
            Assert.Equal(0, WidthOf(bar, "PART_Overtime"), 3);
            Assert.Equal(0, WidthOf(bar, "PART_OverLimit"), 3);
        });
    }

    [StaFact]
    public void An_empty_bar_draws_nothing_at_all()
    {
        StaRunner.Run(() =>
        {
            var bar = Build(BarSegments.Empty);

            Assert.Equal(0, WidthOf(bar, "PART_Target"), 3);
            Assert.Equal(0, WidthOf(bar, "PART_Overtime"), 3);
            Assert.Equal(0, WidthOf(bar, "PART_OverLimit"), 3);
        });
    }

    [StaFact]
    public void Segments_set_after_the_bar_was_laid_out_still_take_effect()
    {
        StaRunner.Run(() =>
        {
            // Die Zeilen entstehen einmal und bekommen ihre Werte beim nächsten Abruf neu.
            var bar = Build(new BarSegments(0.25, 0.0, 0.0));

            bar.Segments = new BarSegments(0.5, 0.5, 0.0);
            Layout(bar);

            Assert.Equal(100, WidthOf(bar, "PART_Target"), 3);
            Assert.Equal(100, WidthOf(bar, "PART_Overtime"), 3);
        });
    }

    private static SegmentedRatioBar Build(BarSegments segments)
    {
        AppResources.Ensure();

        var bar = new SegmentedRatioBar { Segments = segments, Width = BarWidth };
        Layout(bar);

        return bar;
    }

    /// <summary>Wie in ProfileViewRenderTests: Ohne umgebendes Element baut WPF keinen visuellen Baum auf.</summary>
    private static void Layout(SegmentedRatioBar bar)
    {
        var host = bar.Parent as Border ?? new Border { Child = bar, Width = BarWidth };
        host.Measure(new Size(BarWidth, double.PositiveInfinity));
        host.Arrange(new Rect(0, 0, BarWidth, host.DesiredSize.Height));
        host.UpdateLayout();
    }

    private static double WidthOf(SegmentedRatioBar bar, string partName)
    {
        var part = bar.Template.FindName(partName, bar) as Rectangle;
        Assert.NotNull(part);

        return part!.ActualWidth;
    }
}
