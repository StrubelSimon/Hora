using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.App.Views;
using Hora.Core.Jira.Models;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

/// <summary>
/// Baut die Profilseite wirklich auf. Die vorhandenen Tests prüfen das Ansichtsmodell; ein
/// verschriebener StaticResource-Schlüssel oder eine Bindung auf eine Eigenschaft, die es nicht
/// gibt, fällt dort nicht auf — beides fliegt erst auf, wenn WPF die Seite lädt. Genau das
/// geschieht hier.
/// </summary>
public class ProfileViewRenderTests
{
    private const double PageWidth = 900;

    private static readonly JiraUser User = new()
    {
        AccountId = "me",
        DisplayName = "Simon Strubel",
        EmailAddress = "simon.strubel@advisori.de",
    };

    [StaFact]
    public void The_profile_page_loads_with_all_its_sections()
    {
        StaRunner.Run(() =>
        {
            var page = BuildPage();

            var labels = AppResources.Descendants(page)
                .OfType<TextBlock>()
                .Select(block => block.Text)
                .ToList();

            Assert.Contains("DARSTELLUNG", labels);
            Assert.Contains("ZEITERFASSUNG", labels);
            Assert.Contains("JIRA", labels);
            Assert.Contains("EXPORT", labels);
            Assert.Contains("ÜBER HORA", labels);
        });
    }

    [StaFact]
    public void The_export_section_shows_the_month_and_the_button()
    {
        StaRunner.Run(() =>
        {
            var page = BuildPage();
            var texts = AppResources.Descendants(page).OfType<TextBlock>().Select(block => block.Text).ToList();

            // Der Monatswähler bindet auf ExportMonthLabel, der Knopf auf ExportButtonText.
            Assert.Contains("September 2026", texts);
            Assert.Contains(
                AppResources.Descendants(page).OfType<Button>(),
                button => button.Content as string == "Export");
        });
    }

    [StaFact]
    public void The_forward_arrow_of_the_month_stepper_is_disabled_on_the_current_month()
    {
        StaRunner.Run(() =>
        {
            var page = BuildPage();

            var arrows = AppResources.Descendants(page)
                .OfType<Button>()
                .Where(button => button.ToolTip is string tip && tip.StartsWith("Einen Monat"))
                .ToList();

            Assert.Equal(2, arrows.Count);
            Assert.True(arrows[0].IsEnabled, "Der Pfeil zurück muss bedienbar sein.");
            Assert.False(arrows[1].IsEnabled, "Der Pfeil vorwärts steht am laufenden Monat still.");
        });
    }

    private static ProfileView BuildPage()
    {
        AppResources.Ensure();

        var view = new ProfileView
        {
            DataContext = new ProfileViewModel(
                new FakeSettingsStore(),
                User,
                "https://example.atlassian.net",
                WorkSchedule.Default(),
                themeSwitcher: null,
                worklogs: new FakeWorklogService(),
                zone: TimeZoneInfo.Utc,
                logger: new NullLogger(),
                saveFilePicker: new FakeSaveFilePicker(),
                now: () => new DateTimeOffset(2026, 9, 22, 17, 38, 0, TimeSpan.FromHours(2))),
            Width = PageWidth,
        };

        // Wie in IssueTotalAlignmentTests: Ohne umgebendes Element baut WPF keinen visuellen Baum auf.
        var host = new Border { Child = view, Width = PageWidth };
        host.Measure(new Size(PageWidth, double.PositiveInfinity));
        host.Arrange(new Rect(0, 0, PageWidth, host.DesiredSize.Height));
        host.UpdateLayout();

        return view;
    }
}
