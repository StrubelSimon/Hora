using System.Windows.Controls;

namespace Hora.App.Views;

public partial class OverviewView : UserControl
{
    // Die Rückfrage vor dem Abmelden ist mit der Schaltfläche auf die Profilseite gewandert und
    // steht jetzt in ProfileView.xaml.cs: Der Avatar öffnet das Profil, abgemeldet wird dort.
    public OverviewView()
    {
        InitializeComponent();
    }
}
