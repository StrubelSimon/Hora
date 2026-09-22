namespace Hora.App.Services;

/// <summary>Liest die Version aus der Assembly, gepflegt über &lt;Version&gt; in Hora.App.csproj.</summary>
public static class AppVersion
{
    public static string Current
    {
        get
        {
            var version = typeof(AppVersion).Assembly.GetName().Version;
            return version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}
