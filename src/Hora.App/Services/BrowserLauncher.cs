using System.ComponentModel;
using System.Diagnostics;

namespace Hora.App.Services;

/// <summary>Öffnet eine URL im Standardbrowser des Nutzers.</summary>
public static class BrowserLauncher
{
    public static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            // Kein Browser verfügbar oder Start verweigert: Es gibt nichts Sinnvolles zu tun.
        }
    }
}
