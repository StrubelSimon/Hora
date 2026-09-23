using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.App.Views;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

/// <summary>
/// Baut die echte DayView auf und liest zurück, was sie zeigt. Die Tests darüber prüfen die
/// Zeilenvorlage für sich; hier geht es um die Ansicht drumherum: dass beide Listen die Vorlage
/// überhaupt finden und dass der Asana-Block mit seiner Zwischenüberschrift genau dann steht, wenn
/// es Asana-Zeiten gibt. Ein vertippter Ressourcenname oder eine ins Leere laufende
/// Sichtbarkeitsbindung bliebe sonst bis zum ersten Blick auf die laufende App verborgen.
/// </summary>
public class DayViewRenderingTests
{
    private const double ViewWidth = 900;

    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    private static WorklogEntry Jira(string key, int hour) => WorklogEntry.Jira(
        key, $"Titel {key}", new DateTimeOffset(2026, 9, 21, hour, 0, 0, TimeSpan.FromHours(2)),
        TimeSpan.FromHours(1), string.Empty, "https://example.atlassian.net");

    private static WorklogEntry Asana(string project) => new(
        WorklogSource.Asana, project, $"Titel {project}",
        new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(2)),
        TimeSpan.FromHours(2), string.Empty, "https://app.asana.com/0/1", HasStartTime: false);

    /// <summary>
    /// Beide Listen tragen ihre Zeilen, und die Asana-Zeilen stehen unter den Jira-Zeilen — nicht
    /// zwischen ihnen, wo ihre fehlende Uhrzeit eine Ordnung vortäuschte.
    /// </summary>
    [StaFact]
    public void Asana_entries_stand_in_their_own_block_below_the_jira_bookings()
    {
        Render(
            new[] { Jira("A-1", 9), Asana("Marketing"), Jira("A-2", 14) },
            view =>
            {
                Assert.Equal(
                    new[] { "A-1", "A-2", "ASANA", "Marketing" },
                    KeysAndHeadings(view));

                Assert.Equal(Visibility.Visible, AsanaBlock(view).Visibility);
            });
    }

    /// <summary>Ohne Asana-Zeiten bleibt der Block samt Zwischenüberschrift ausgeblendet.</summary>
    [StaFact]
    public void Without_asana_entries_the_block_and_its_heading_stay_hidden()
    {
        Render(
            new[] { Jira("A-1", 9) },
            view =>
            {
                Assert.Equal(new[] { "A-1" }, KeysAndHeadings(view));
                Assert.Equal(Visibility.Collapsed, AsanaBlock(view).Visibility);
            });
    }

    /// <summary>
    /// Ein Tag mit ausschließlich Asana-Zeiten zeigt sie und nicht den Hinweis „Keine Buchungen“.
    /// </summary>
    [StaFact]
    public void A_day_with_only_asana_entries_shows_them_instead_of_the_empty_hint()
    {
        Render(
            new[] { Asana("Marketing") },
            view =>
            {
                Assert.Equal(new[] { "ASANA", "Marketing" }, KeysAndHeadings(view));
                Assert.Equal(Visibility.Collapsed, EmptyHint(view).Visibility);
            });
    }

    /// <summary>Ohne jede Buchung steht der Hinweis da und sonst nichts.</summary>
    [StaFact]
    public void An_empty_day_shows_the_hint()
    {
        Render(
            Array.Empty<WorklogEntry>(),
            view =>
            {
                Assert.Empty(KeysAndHeadings(view));
                Assert.Equal(Visibility.Visible, EmptyHint(view).Visibility);
            });
    }

    /// <summary>
    /// Die Schlüssel beider Listen und die Zwischenüberschrift dazwischen, in der Reihenfolge, in
    /// der sie untereinander stehen. Die Schlüssel sind Schaltflächen, die Überschrift ist der
    /// einzige sichtbare Textblock im Stil SectionLabel oberhalb des Abschnitts „Pro Ticket“.
    /// </summary>
    private static List<string> KeysAndHeadings(DayView view) =>
        VisibleDescendants(EntryCard(view))
            .Select(element => element switch
            {
                Button button => button.Content as string,
                TextBlock { Text: "ASANA" } heading => heading.Text,
                _ => null,
            })
            .OfType<string>()
            .ToList();

    /// <summary>
    /// Wie ThemeResources.Descendants, überspringt aber ausgeblendete Elemente samt allem, was
    /// darunter hängt. UIElement.IsVisible taugt hier nicht: Es verlangt ein tatsächlich
    /// dargestelltes Fenster und ist im Testaufbau immer falsch.
    /// </summary>
    private static IEnumerable<DependencyObject> VisibleDescendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is UIElement { Visibility: not Visibility.Visible })
            {
                continue;
            }

            yield return child;

            foreach (var further in VisibleDescendants(child))
            {
                yield return further;
            }
        }
    }

    /// <summary>Die Karte mit den Buchungen, also alles oberhalb des Abschnitts „Pro Ticket“.</summary>
    private static Border EntryCard(DayView view) =>
        ThemeResources.Descendants(view).OfType<Border>().First(border => border.CornerRadius.TopLeft > 0);

    private static StackPanel AsanaBlock(DayView view) =>
        ThemeResources.Descendants(EntryCard(view))
            .OfType<StackPanel>()
            .First(panel => panel.Children.Count == 2 && panel.Children[0] is TextBlock { Text: "ASANA" });

    private static TextBlock EmptyHint(DayView view) =>
        ThemeResources.Descendants(EntryCard(view))
            .OfType<TextBlock>()
            .First(text => text.Text.StartsWith("Keine Buchungen", StringComparison.Ordinal));

    private static void Render(IReadOnlyList<WorklogEntry> entries, Action<DayView> assert) =>
        ThemeResources.Render(
            ViewWidth,
            () =>
            {
                var viewModel = new DayViewModel(Berlin);
                var summary = WorklogAggregator.BuildDaySummary(entries, new DateOnly(2026, 9, 21), Berlin);
                viewModel.Show(summary, WorklogAggregator.BuildIssueTotals(summary.Entries));

                return new DayView { DataContext = viewModel };
            },
            assert);
}
