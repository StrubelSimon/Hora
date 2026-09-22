using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using Hora.App.Services;

namespace Hora.App.Tests;

public class ThemeManagerTests
{
    private static ThemeManager Create(out Collection<ResourceDictionary> merged)
    {
        merged = new Collection<ResourceDictionary>();
        return new ThemeManager(merged);
    }

    /// <summary>
    /// Führt <paramref name="action"/> in einem eigens erzeugten STA-Thread aus und reicht eine
    /// darin ausgelöste Ausnahme (etwa einen fehlgeschlagenen Assert) an den aufrufenden
    /// xUnit-Thread weiter. Nötig, weil der Testläufer selbst keinen STA-Thread stellt, WPF-Typen
    /// wie ResourceDictionary das aber brauchen.
    /// </summary>
    private static void RunOnSta(Action action)
    {
        ExceptionDispatchInfo? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                // Ohne ein Application-Objekt kennt WPF keine Einstiegsassembly für die relativen
                // Ressourcen-URIs ("Styles/Tokens.Light.xaml") – im Testprozess ist die Einstiegsassembly
                // testhost, nicht Hora.App. Application.ResourceAssembly lässt sich nur einmal setzen und
                // ist beim Erreichen dieser Zeile teils schon (falsch) belegt; ein echtes, minimales
                // Application-Objekt löst die Auflösung stattdessen korrekt wie im echten Programmstart.
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

    [StaFact]
    public void Apply_inserts_exactly_one_token_dictionary()
    {
        RunOnSta(() =>
        {
            var manager = Create(out var merged);

            manager.Apply(AppTheme.Light);

            Assert.Single(merged);
            Assert.Equal(AppTheme.Light, manager.Current);
        });
    }

    [StaFact]
    public void Apply_replaces_the_previous_dictionary_instead_of_stacking()
    {
        RunOnSta(() =>
        {
            var manager = Create(out var merged);

            manager.Apply(AppTheme.Light);
            manager.Apply(AppTheme.Dark);

            Assert.Single(merged);
            Assert.Equal(AppTheme.Dark, manager.Current);
        });
    }

    [StaFact]
    public void Toggle_switches_back_and_forth()
    {
        RunOnSta(() =>
        {
            var manager = Create(out _);

            manager.Apply(AppTheme.Light);
            manager.Toggle();
            Assert.Equal(AppTheme.Dark, manager.Current);

            manager.Toggle();
            Assert.Equal(AppTheme.Light, manager.Current);
        });
    }

    [StaFact]
    public void Changed_reports_the_new_theme()
    {
        RunOnSta(() =>
        {
            var manager = Create(out _);
            AppTheme? reported = null;
            manager.Changed += theme => reported = theme;

            manager.Apply(AppTheme.Dark);

            Assert.Equal(AppTheme.Dark, reported);
        });
    }

    [StaFact]
    public void Both_token_dictionaries_define_the_same_keys()
    {
        // Ein Schlüssel, den nur eine Seite kennt, lässt die App beim Umschalten abstürzen.
        //
        // Hinweis zur Abweichung vom Brief: Eine absolute "pack://application:,,,/Hora;component/…"-Uri
        // scheitert ohne jedes Application-Objekt mit "Invalid URI: Invalid port specified", weil das
        // pack-Schema dann nie bei System.Uri registriert ist. RunOnSta legt deshalb bei Bedarf ein
        // minimales Application-Objekt an – das registriert das Schema. Die assembly-qualifizierte Form
        // mit explizitem "Hora;component/…" ist danach nötig, weil Application.ResourceAssembly im
        // Testprozess schon (falsch) auf testhost gesetzt ist, bevor dieser Code überhaupt läuft, und sich
        // kein zweites Mal setzen lässt; die explizite Form ist von dieser Standard-Assembly unabhängig.
        RunOnSta(() =>
        {
            var light = new ResourceDictionary { Source = new Uri("pack://application:,,,/Hora;component/Styles/Tokens.Light.xaml") };
            var dark = new ResourceDictionary { Source = new Uri("pack://application:,,,/Hora;component/Styles/Tokens.Dark.xaml") };

            var lightKeys = light.Keys.Cast<object>().Select(k => k.ToString()!).OrderBy(k => k).ToList();
            var darkKeys = dark.Keys.Cast<object>().Select(k => k.ToString()!).OrderBy(k => k).ToList();

            Assert.Equal(lightKeys, darkKeys);
        });
    }
}
