using System.Windows;
using System.Windows.Controls;
using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.App.Views;
using Hora.Core.Asana;
using Hora.Core.Asana.Models;
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
/// Asana-Konto, für den Abschnitt eine Jira-Anmeldung. Ein toter Bindungspfad bliebe dort besonders
/// lange unbemerkt: WPF meldet ihn nicht, die Stelle steht einfach leer da.
/// </remarks>
public class AsanaViewsRenderingTests
{
    private const double WindowWidth = 420;
    private const double ProfileWidth = 1000;

    // ===================== Verbinden-Fenster =====================

    private sealed class StubAsanaClient(AsanaUser user) : IAsanaClient
    {
        public Task<AsanaUser> GetMyselfAsync(CancellationToken cancellationToken) => Task.FromResult(user);

        public Task<IReadOnlyList<AsanaTimeEntry>> GetTimeEntriesAsync(
            string userGid, string workspaceGid, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AsanaTimeEntry>>(Array.Empty<AsanaTimeEntry>());
    }

    private static AsanaConnectViewModel ConnectViewModel(params string[] workspaceNames)
    {
        var user = new AsanaUser
        {
            Gid = "111",
            Name = "Simon Strubel",
            Email = "simon.strubel@advisori.de",
            Workspaces = workspaceNames
                .Select((name, index) => new AsanaWorkspace { Gid = $"90{index}", Name = name })
                .ToList(),
        };

        return new AsanaConnectViewModel(_ => new StubAsanaClient(user), new FakeAsanaCredentialStore());
    }

    /// <summary>
    /// Schritt eins zeigt das Eingabefeld für den Token und die Weiter-Schaltfläche; der
    /// Workspace-Schritt bleibt ausgeblendet.
    /// </summary>
    [StaFact]
    public void The_connect_window_opens_on_the_token_step()
    {
        RenderConnectWindow(
            ConnectViewModel("ADVISORI"),
            content =>
            {
                Assert.Contains("Asana verbinden", VisibleTexts(content));
                Assert.Equal("Weiter", ButtonLabels(content).Last());
                Assert.Contains("Token erstellen", ButtonLabels(content));

                // Ohne Token ist „Weiter“ grau: Der Befehl sagt es, die Schaltfläche folgt ihm.
                Assert.False(Buttons(content).Last().IsEnabled);

                // Die Workspace-Auswahl gehört zu Schritt zwei und ist hier ausgeblendet.
                Assert.DoesNotContain(VisibleElements(content), element => element is ComboBox);
            });
    }

    /// <summary>
    /// Der eingetippte Token landet im Ansichtsmodell, ohne dass das Feld verlassen werden muss —
    /// sonst bliebe „Weiter“ grau, bis irgendwo hingeklickt wird.
    /// </summary>
    [StaFact]
    public void Typing_a_token_reaches_the_view_model_at_once()
    {
        var viewModel = ConnectViewModel("ADVISORI");

        RenderConnectWindow(
            viewModel,
            content =>
            {
                var field = Descendants(content).OfType<TextBox>().Single();
                field.Text = "0/1234abcd";

                Assert.Equal("0/1234abcd", viewModel.Token);
                Assert.True(viewModel.ContinueCommand.CanExecute(null));
            });
    }

    /// <summary>Eine abgelehnte Anmeldung steht als Meldung da, statt stumm zu verpuffen.</summary>
    [StaFact]
    public void A_rejected_token_shows_its_message()
    {
        var viewModel = ConnectViewModel("ADVISORI");
        viewModel.ErrorMessage = "Asana hat den Token abgelehnt. Bitte prüfen und erneut versuchen.";

        RenderConnectWindow(
            viewModel,
            content => Assert.Contains(
                "Asana hat den Token abgelehnt. Bitte prüfen und erneut versuchen.",
                VisibleTexts(content)));
    }

    /// <summary>
    /// Schritt zwei zeigt die von Asana bestätigte Kennung und die Workspace-Liste. Die Auswahlliste
    /// trägt eine eigene Vorlage (FieldComboBox) — ohne sie stünde dort im Dunkelmodus ein helles
    /// Windows-Feld; mit ihr muss sie den gewählten Namen auch wirklich anzeigen.
    /// </summary>
    [StaFact]
    public void The_workspace_step_shows_the_verified_identity_and_the_workspace_list()
    {
        var viewModel = OnWorkspaceStep("ADVISORI", "Nebenprojekt");

        RenderConnectWindow(
            viewModel,
            content =>
            {
                var texts = VisibleTexts(content);
                Assert.Contains("Simon Strubel", texts);
                Assert.Contains("simon.strubel@advisori.de", texts);

                var box = Descendants(content).OfType<ComboBox>().Single();
                Assert.Equal(2, box.Items.Count);
                Assert.Same(viewModel.SelectedWorkspace, box.SelectedItem);

                // Die eigene Vorlage muss den gewählten Eintrag zeigen; bliebe sie stumm, stünde im
                // Feld nichts und niemand wüsste, welcher Workspace gemeint ist.
                Assert.Contains("ADVISORI", texts);

                Assert.Equal(new[] { "Zurück", "Verbinden" }, ButtonLabels(content));
            });
    }

    /// <summary>Die Standardschaltfläche wechselt mit dem Schritt — sonst löste die Eingabetaste in
    /// Schritt zwei das „Weiter“ aus, das dort nichts mehr zu suchen hat.</summary>
    [StaFact]
    public void Only_the_button_of_the_current_step_is_the_default_one()
    {
        // Je Schritt ein eigenes Ansichtsmodell: Ein einmal aufgebautes Fenster bleibt an den
        // Befehlen angemeldet, und ein Schrittwechsel danach liefe von diesem Faden aus in die
        // Schaltflächen des vorigen Aufbaus — WPF lässt das über Fadengrenzen hinweg nicht zu.
        RenderConnectWindow(
            OnTokenStep(),
            content => Assert.Equal(new[] { "Weiter" }, DefaultButtonLabels(content)));

        RenderConnectWindow(
            OnWorkspaceStep(),
            content => Assert.Equal(new[] { "Verbinden" }, DefaultButtonLabels(content)));
    }

    private static AsanaConnectViewModel OnTokenStep()
    {
        var viewModel = ConnectViewModel("ADVISORI");
        viewModel.Token = "0/1234abcd";
        return viewModel;
    }

    private static AsanaConnectViewModel OnWorkspaceStep(params string[] workspaceNames)
    {
        var viewModel = ConnectViewModel(workspaceNames.Length == 0 ? ["ADVISORI"] : workspaceNames);
        viewModel.Token = "0/1234abcd";
        viewModel.ContinueCommand.Execute(null);

        Assert.True(viewModel.IsWorkspaceStep);
        return viewModel;
    }

    private static List<string> DefaultButtonLabels(DependencyObject root) =>
        Buttons(root).Where(button => button.IsDefault).Select(button => button.Content as string).OfType<string>().ToList();

    // ===================== Asana-Abschnitt der Profilseite =====================

    private static ProfileViewModel ProfileViewModel(AsanaCredentials? stored) => new(
        new FakeSettingsStore(),
        new JiraUser { AccountId = "1", DisplayName = "Simon Strubel", EmailAddress = "a@b.de" },
        "https://example.atlassian.net",
        WorkSchedule.Default(),
        themeSwitcher: null,
        asanaCredentialStore: new FakeAsanaCredentialStore { Stored = stored });

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
                Assert.DoesNotContain("Letzte Synchronisation", texts.Skip(texts.IndexOf("ASANA")));
            });
    }

    /// <summary>
    /// Mit Verbindung nennt die Zeile Name und Workspace, bietet das Trennen an und zeigt den
    /// Zeitpunkt der letzten Synchronisation.
    /// </summary>
    [StaFact]
    public void The_profile_section_shows_the_stored_connection()
    {
        RenderProfile(
            ProfileViewModel(new AsanaCredentials("t", "111", "Simon Strubel", "900", "ADVISORI")),
            view =>
            {
                var texts = VisibleTexts(view);
                Assert.Contains("Simon Strubel · ADVISORI", texts);
                Assert.Contains("Trennen", ButtonLabels(view));
                Assert.Contains("Letzte Synchronisation", texts.Skip(texts.IndexOf("ASANA")));
            });
    }

    // ===================== Aufbau und Auslesen =====================

    /// <summary>
    /// Der Inhalt des Fensters, aus ihm herausgelöst: Ein Window darf kein Kind eines anderen
    /// Elements sein und ließe sich so nicht messen. Mit dem Lösen fällt die Vererbung des
    /// Datenkontexts weg, er wird deshalb erneut gesetzt.
    /// </summary>
    private static void RenderConnectWindow(AsanaConnectViewModel viewModel, Action<FrameworkElement> assert) =>
        ThemeResources.Render(
            WindowWidth,
            () =>
            {
                var window = new AsanaConnectWindow { DataContext = viewModel };
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

    /// <summary>Alle Schaltflächen, die tatsächlich sichtbar sind — der jeweils andere Schritt zählt nicht.</summary>
    private static List<Button> Buttons(DependencyObject root) =>
        VisibleElements(root).OfType<Button>().ToList();

    private static List<string> ButtonLabels(DependencyObject root) =>
        Buttons(root).Select(button => button.Content as string).OfType<string>().ToList();

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
