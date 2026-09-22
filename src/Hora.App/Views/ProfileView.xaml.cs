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

        var owner = Window.GetWindow(this);
        var answer = owner is null
            ? MessageBox.Show(
                LogoutQuestionText,
                LogoutQuestionCaption,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No)
            : MessageBox.Show(
                owner,
                LogoutQuestionText,
                LogoutQuestionCaption,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

        if (answer == MessageBoxResult.Yes)
        {
            viewModel.LogoutCommand.Execute(null);
        }
    }
}
