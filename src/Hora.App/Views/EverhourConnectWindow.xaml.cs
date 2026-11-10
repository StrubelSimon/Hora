using System.Windows;

namespace Hora.App.Views;

/// <summary>
/// Reine Hülle für das EverhourConnectViewModel. Die gesamte Logik steht dort; hier bleibt
/// nichts, was sich prüfen ließe — und genau so ist es gewollt.
/// </summary>
public partial class EverhourConnectWindow : Window
{
    public EverhourConnectWindow()
    {
        InitializeComponent();
    }
}
