using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;

namespace Hora.App.Tests.TestDoubles;

/// <summary>
/// Führt eine Aktion in einem eigens erzeugten STA-Thread aus und reicht eine darin ausgelöste
/// Ausnahme (etwa einen fehlgeschlagenen Assert) an den aufrufenden xUnit-Thread weiter. Nötig,
/// weil der Testläufer keinen STA-Thread stellt, WPF-Typen wie ResourceDictionary das aber brauchen.
/// </summary>
/// <remarks>
/// Das minimale Application-Objekt registriert das pack-Schema bei System.Uri; ohne es scheitert
/// jede „pack://application:,,,/Hora;component/…“-Uri mit „Invalid URI: Invalid port specified“.
/// Es darf im Prozess nur eines geben — die Testläufe dieser Assembly laufen deshalb nacheinander
/// (siehe AssemblyInfo.cs).
/// </remarks>
internal static class StaRunner
{
    public static void Run(Action action)
    {
        ExceptionDispatchInfo? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current is null)
                {
                    _ = new Application();
                }

                action();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        failure?.Throw();
    }
}
