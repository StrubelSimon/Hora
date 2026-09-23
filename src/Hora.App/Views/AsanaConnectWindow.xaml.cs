using System.Windows;

namespace Hora.App.Views;

/// <summary>
/// Reine Hülle für das AsanaConnectViewModel. Die gesamte Logik der beiden Schritte steht dort;
/// hier bleibt nichts, was sich prüfen ließe — und genau so ist es gewollt.
/// </summary>
public partial class AsanaConnectWindow : Window
{
    public AsanaConnectWindow()
    {
        InitializeComponent();
    }
}
