using System.Windows;
using System.Windows.Controls;
using Hora.App.ViewModels;

namespace Hora.App.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    // PasswordBox.Password ist aus Sicherheitsgründen keine Dependency Property und lässt sich nicht binden.
    private void OnTokenChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel)
        {
            viewModel.ApiToken = ((PasswordBox)sender).Password;
        }
    }
}
