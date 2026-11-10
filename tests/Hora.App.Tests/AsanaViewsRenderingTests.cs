using System.Windows;
using System.Windows.Controls;
using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.App.Views;
using Hora.Core.Everhour;
using Hora.Core.Everhour.Models;
using Hora.Core.Jira.Models;
using Hora.Core.Storage;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

/// <summary>
/// Baut das Verbinden-Fenster und den Asana-Abschnitt der Profilseite aus den echten XAML-Dateien
/// auf und liest zurück, was dort steht.
/// </summary>
/// <remarks>
/// Diese beiden Flächen sind von Hand kaum zu erreichen — für das Fenster braucht es ein
/// Everhour-Konto, für den Abschnitt eine Jira-Anmeldung. Ein toter Bindungspfad bliebe dort besonders
/// lange unbemerkt: WPF meldet ihn nicht, die Stelle steht einfach leer da.
/// </remarks>
public class AsanaViewsRenderingTests
{
    private const double WindowWidth = 420;
    private const double ProfileWidth = 1000;

    // ===================== Verbinden-Fenster =====================

    private sealed class StubEverhourClient(EverhourUser user) : IEverhourClient
    {
        public Task<EverhourUser> GetMyselfAsync(CancellationToken cancellationToken) => Task.FromResult(user);

        public Task<IReadOnlyList<EverhourTimeEntry>> GetTimeEntriesAsync(
            long userId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EverhourTimeEntry>>(Array.Empty<EverhourTimeEntry>());

        public Task<EverhourProject> GetProjectAsync(string projectId, CancellationToken cancellationToken) =>
            Task.FromResult(new EverhourProject { Id = projectId, Name = "Projekt" });
    }

    private static EverhourConnectViewModel ConnectViewModel() => new(
        _ => new StubEverhourClient(new EverhourUser { Id = 1431001, Name = "Simon Strubel" }),
        new FakeEverhourCredentialStore());

    /// <summary>
    /// Das Fenster zeigt das Eingabefeld für den Schlüssel, den Weg zum Schlüssel in Everhour und
    /// die Verbinden-Schaltfläche, die ohne Schlüssel grau bleibt.
    /// </summary>
    [StaFact]
    public void The_connect_window_asks_for_the_everhour_key()
    {
        RenderConnectWindow(
            ConnectViewModel(),
            content =>
            {
                Assert.Contains("Asana-Zeiten verbinden", VisibleTexts(content));
                Assert.Contains("Everhour-API-Schlüssel", VisibleTexts(content));
                Assert.Equal(new[] { "Schlüssel in Everhour finden", "Verbinden" }, ButtonLabels(content));

                // Ohne Schlüssel ist „Verbinden“ grau: Der Befehl sagt es, die Schaltfläche folgt ihm.
                Assert.False(Buttons(content).Last().IsEnabled);
                Assert.True(Buttons(content).Last().IsDefault);
            });
    }

    /// <summary>
    /// Der eingetippte Schlüssel landet im Ansichtsmodell, ohne dass das Feld verlassen werden muss —
    /// sonst bliebe „Verbinden“ grau, bis irgendwo hingeklickt wird.
    /// </summary>
    [StaFact]
    public void Typing_a_key_reaches_the_view_model_at_once()
    {
        var viewModel = ConnectViewModel();

        RenderConnectWindow(
            viewModel,
            content =>
            {
                var field = Descendants(content).OfType<TextBox>().Single();
                field.Text = "eh-1234abcd";

                Assert.Equal("eh-1234abcd", viewModel.ApiKey);
                Assert.True(viewModel.ConnectCommand.CanExecute(null));
            });
    }

    /// <summary>Ein abgelehnter Schlüssel steht als Meldung da, statt stumm zu verpuffen.</summary>
    [StaFact]
    public void A_rejected_key_shows_its_message()
    {
        var viewModel = ConnectViewModel();
        viewModel.ErrorMessage = "Everhour hat den Schlüssel abgelehnt. Bitte prüfen und erneut versuchen.";

        RenderConnectWindow(
            viewModel,
            content => Assert.Contains(
                "Everhour hat den Schlüssel abgelehnt. Bitte prüfen und erneut versuchen.",
                VisibleTexts(content)));
    }

    // ===================== Asana-Abschnitt der Profilseite =====================

    private static ProfileViewModel ProfileViewModel(EverhourCredentials? stored) => new(
        new FakeSettingsStore(),
        new JiraUser { AccountId = "1", DisplayName = "Simon Strubel", EmailAddress = "a@b.de" },
        "https://example.atlassian.net",
        WorkSchedule.Default(),
        themeSwitcher: null,
        everhourCredentialStore: new FakeEverhourCredentialStore { Stored = stored });

    /// <summary>Ohne Verbindung steht der Abschnitt da und bietet das Verbinden an.</summary>
    [StaFact]
    public void The_profile_section_offers_connecting_when_nothing_is_stored()
    {
        RenderProfile(
            ProfileViewModel(stored: null),
            view =>
            {
                var texts = VisibleTexts(view);
                Assert.Contains("ASANA", texts);
                Assert.Contains("Verbindung", texts);
                Assert.Contains("Nicht verbunden", texts);

                Assert.Contains("Verbinden", ButtonLabels(view));

                // Ohne Verbindung gäbe es keinen Zeitpunkt zu zeigen; die Zeile bleibt weg.
                Assert.DoesNotContain("Letzte Synchronisation", SectionTexts(view, "ASANA"));
            });
    }

    /// <summary>
    /// Mit Verbindung nennt die Zeile Name und Herkunft, bietet das Trennen an und zeigt den
    /// Zeitpunkt der letzten Synchronisation.
    /// </summary>
    [StaFact]
    public void The_profile_section_shows_the_stored_connection()
    {
        RenderProfile(
            ProfileViewModel(new EverhourCredentials("k", 1431001, "Simon Strubel")),
            view =>
            {
                var texts = VisibleTexts(view);
                Assert.Contains("Simon Strubel · über Everhour", texts);
                Assert.Contains("Trennen", ButtonLabels(view));
                Assert.Contains("Letzte Synchronisation", SectionTexts(view, "ASANA"));
            });
    }

    // ===================== Aufbau und Auslesen =====================

    /// <summary>
    /// Der Inhalt des Fensters, aus ihm herausgelöst: Ein Window darf kein Kind eines anderen
    /// Elements sein und ließe sich so nicht messen. Mit dem Lösen fällt die Vererbung des
    /// Datenkontexts weg, er wird deshalb erneut gesetzt.
    /// </summary>
    private static void RenderConnectWindow(EverhourConnectViewModel viewModel, Action<FrameworkElement> assert) =>
        ThemeResources.Render(
            WindowWidth,
            () =>
            {
                var window = new EverhourConnectWindow { DataContext = viewModel };
                var content = (FrameworkElement)window.Content;
                window.Content = null;
                content.DataContext = viewModel;
                return content;
            },
            assert);

    private static void RenderProfile(ProfileViewModel viewModel, Action<ProfileView> assert) =>
        ThemeResources.Render(ProfileWidth, () => new ProfileView { DataContext = viewModel }, assert);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root) =>
        ThemeResources.Descendants(root);

    /// <summary>Alle Schaltflächen, die tatsächlich sichtbar sind.</summary>
    private static List<Button> Buttons(DependencyObject root) =>
        VisibleElements(root).OfType<Button>().ToList();

    private static List<string> ButtonLabels(DependencyObject root) =>
        Buttons(root).Select(button => button.Content as string).OfType<string>().ToList();

    /// <summary>Die Überschriften der Profilabschnitte, in der Reihenfolge der Seite.</summary>
    private static readonly string[] SectionLabels =
        { "DARSTELLUNG", "ZEITERFASSUNG", "ASANA", "JIRA", "EXPORT", "ÜBER HORA" };

    /// <summary>
    /// Die sichtbaren Texte eines Profilabschnitts, von seiner Überschrift bis zur nächsten.
    /// </summary>
    /// <remarks>
    /// „Alles ab der Überschrift“ genügt nicht: Die Profilseite hat zwei Spalten, und der visuelle
    /// Baum läuft am Ende der einen in die andere. Seit ASANA links unter ZEITERFASSUNG steht,
    /// folgt darauf der JIRA-Abschnitt — der eine eigene Zeile „Letzte Synchronisation“ trägt und
    /// eine Zusicherung über die Asana-Zeile sonst still beantwortet.
    /// </remarks>
    private static List<string> SectionTexts(ProfileView view, string section)
    {
        var texts = VisibleTexts(view);
        var start = texts.IndexOf(section);
        Assert.True(start >= 0, $"Abschnitt „{section}“ steht nicht auf der Profilseite.");

        var next = texts.FindIndex(start + 1, text => SectionLabels.Contains(text));
        return next < 0
            ? texts.Skip(start).ToList()
            : texts.Skip(start).Take(next - start).ToList();
    }

    /// <summary>Die Texte aller sichtbaren Textblöcke, in der Reihenfolge des visuellen Baums.</summary>
    private static List<string> VisibleTexts(DependencyObject root) =>
        VisibleElements(root)
            .OfType<TextBlock>()
            .Select(text => text.Text)
            .Where(text => !string.IsNullOrEmpty(text))
            .ToList();

    /// <summary>
    /// Wie ThemeResources.Descendants, überspringt aber ausgeblendete Elemente samt allem, was
    /// darunter hängt. UIElement.IsVisible taugt dafür nicht: Es verlangt ein tatsächlich
    /// dargestelltes Fenster und ist im Testaufbau immer falsch.
    /// </summary>
    private static IEnumerable<DependencyObject> VisibleElements(DependencyObject root)
    {
        foreach (var child in Descendants(root))
        {
            if (child is UIElement { Visibility: not Visibility.Visible })
            {
                continue;
            }

            if (Ancestors(child, root).Any(ancestor => ancestor is UIElement { Visibility: not Visibility.Visible }))
            {
                continue;
            }

            yield return child;
        }
    }

    private static IEnumerable<DependencyObject> Ancestors(DependencyObject element, DependencyObject root)
    {
        var current = System.Windows.Media.VisualTreeHelper.GetParent(element);
        while (current is not null && current != root)
        {
            yield return current;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
    }
}
