using Hora.App.Services;

namespace Hora.App.Tests;

/// <summary>
/// Übersetzt zwischen der Wahl im Profil und der Zeichenkette in settings.json. Gespeichert wird
/// die Wahl („System“, „Light“, „Dark“), nicht das wirksame Ergebnis; Unbekanntes fällt auf hell
/// zurück, genau wie bisher.
/// </summary>
public class ThemePreferenceNamesTests
{
    [Theory]
    [InlineData("System", ThemePreference.System)]
    [InlineData("system", ThemePreference.System)]
    [InlineData("Light", ThemePreference.Light)]
    [InlineData("Dark", ThemePreference.Dark)]
    [InlineData("dark", ThemePreference.Dark)]
    public void Parse_reads_the_three_stored_names(string stored, ThemePreference expected)
    {
        Assert.Equal(expected, ThemePreferenceNames.Parse(stored));
    }

    [Theory]
    [InlineData("Blau")]
    [InlineData("")]
    [InlineData(null)]
    public void An_unknown_value_falls_back_to_light(string? stored)
    {
        Assert.Equal(ThemePreference.Light, ThemePreferenceNames.Parse(stored));
    }

    [Theory]
    [InlineData(ThemePreference.System, "System")]
    [InlineData(ThemePreference.Light, "Light")]
    [InlineData(ThemePreference.Dark, "Dark")]
    public void NameOf_writes_the_name_that_goes_into_the_file(ThemePreference choice, string expected)
    {
        Assert.Equal(expected, ThemePreferenceNames.NameOf(choice));
    }

    [Theory]
    [InlineData(ThemePreference.System)]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    public void The_stored_choice_survives_a_restart(ThemePreference choice)
    {
        // Was NameOf in die Datei schreibt, muss Parse beim nächsten Start unverändert zurückgeben.
        Assert.Equal(choice, ThemePreferenceNames.Parse(ThemePreferenceNames.NameOf(choice)));
    }

    [Fact]
    public void The_two_values_written_by_the_previous_version_keep_their_meaning()
    {
        // Ältere Dateien tragen den Namen der Aufzählung AppTheme. Beide bleiben gültig.
        Assert.Equal(ThemePreference.Light, ThemePreferenceNames.Parse(AppTheme.Light.ToString()));
        Assert.Equal(ThemePreference.Dark, ThemePreferenceNames.Parse(AppTheme.Dark.ToString()));
    }
}
