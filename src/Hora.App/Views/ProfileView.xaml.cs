using System.Windows;
using System.Windows.Controls;
using Hora.App.ViewModels;

namespace Hora.App.Views;

public partial class ProfileView : UserControl
{
    // Nutzertexte der Rückfrage vor dem Abmelden. Sie sind mit der Schaltfläche vom Avatar der
    // Übersicht hierher gewandert: Der Avatar öffnet jetzt das Profil, abgemeldet wird hier.
    private const string LogoutQuestionCaption = "Abmelden";

    private const string LogoutQuestionText =
        "Möchten Sie sich wirklich abmelden?\n\n" +
        "Die gespeicherte Anmeldung wird dabei gelöscht. Beim nächsten Start müssen Sie Ihren " +
        "API-Token erneut eingeben.";

    // Nutzertexte der Rückfrage vor dem Trennen der Asana-Verbindung.
    private const string AsanaDisconnectCaption = "Asana trennen";

    private const string AsanaDisconnectText =
        "Verbindung zu Asana trennen?\n\n" +
        "Der gespeicherte Asana-Token wird gelöscht und die Asana-Zeiten verschwinden aus der " +
        "Übersicht. Die Jira-Anmeldung bleibt bestehen.";

    public ProfileView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Rückfrage vor dem Abmelden. Sie sitzt bewusst hier in der Ansicht und nicht im
    /// Ansichtsmodell: Dessen Testbarkeit hängt daran, dass es keine WPF-Typen kennt. Standardwahl
    /// ist „Nein“, damit ein versehentliches Bestätigen mit der Eingabetaste nichts löscht.
    /// </summary>
    private void OnLogoutClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProfileViewModel viewModel)
        {
            return;
        }

        if (AskYesNo(LogoutQuestionText, LogoutQuestionCaption) == MessageBoxResult.Yes)
        {
            viewModel.LogoutCommand.Execute(null);
        }
    }

    /// <summary>
    /// Eine Schaltfläche für beide Richtungen. Verbinden geht ohne Rückfrage — es entsteht nur
    /// etwas. Trennen löscht einen gespeicherten Zugang und fragt deshalb zurück, mit „Nein“
    /// vorausgewählt, damit ein versehentliches Bestätigen mit der Eingabetaste nichts löscht.
    /// </summary>
    /// <remarks>
    /// Die Rückfrage liegt wie die vor dem Abmelden hier in der Ansicht und nicht im
    /// Ansichtsmodell: Dessen Prüfbarkeit hängt daran, dass es keine WPF-Typen kennt.
    /// </remarks>
    private void OnAsanaAction(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProfileViewModel viewModel)
        {
            return;
        }

        if (!viewModel.IsAsanaConnected)
        {
            viewModel.ConnectAsanaCommand.Execute(null);
            return;
        }

        if (AskYesNo(AsanaDisconnectText, AsanaDisconnectCaption) == MessageBoxResult.Yes)
        {
            viewModel.DisconnectAsanaCommand.Execute(null);
        }
    }

    /// <summary>
    /// Ja/Nein-Rückfrage mit „Nein“ als Standardwahl, an das umgebende Fenster geheftet. Ohne
    /// Besitzer erscheint das Meldungsfenster unabhängig und kann hinter Hora verschwinden; im
    /// Entwurfsmodus gibt es keines, deshalb der zweite Zweig.
    /// </summary>
    private MessageBoxResult AskYesNo(string text, string caption)
    {
        var owner = Window.GetWindow(this);

        return owner is null
            ? MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            : MessageBox.Show(owner, text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
    }
}
