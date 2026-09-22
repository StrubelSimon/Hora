using System.Security.Cryptography;
using System.Text.Json;

namespace Hora.Core.Storage;

/// <summary>
/// Schreibt und liest ein Objekt als DPAPI-verschlüsseltes JSON (CurrentUser). Nur derselbe
/// Windows-Benutzer auf demselben Rechner kann die Datei wieder entschlüsseln.
/// </summary>
/// <remarks>
/// Der gemeinsame Baustein hinter credentials.dat und asana.dat. Das Auffangverhalten von
/// <see cref="Load"/> und <see cref="Delete"/> ist bewusst weit gefasst; die Begründung steht an
/// den beiden Methoden.
/// </remarks>
public sealed class DpapiBlobStore<T>
    where T : class
{
    private readonly string _filePath;

    public DpapiBlobStore(string filePath)
    {
        _filePath = filePath;
    }

    public void Save(T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var plainBytes = JsonSerializer.SerializeToUtf8Bytes(value);
        var protectedBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_filePath, protectedBytes);
    }

    /// <summary>
    /// Liefert null, wenn keine Datei existiert oder sie nicht entschlüsselt werden kann, und
    /// räumt sie in diesem Fall weg.
    /// </summary>
    public T? Load()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(_filePath);
            var plainBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var value = JsonSerializer.Deserialize<T>(plainBytes);

            if (value is null)
            {
                Delete();
                return null;
            }

            return value;
        }
        catch (Exception)
        {
            // Unlesbar bedeutet "nicht vorhanden": Datei beschädigt, von einem anderen Benutzer oder
            // wegen fehlender Zugriffsrechte nicht lesbar. Der Filter nannte früher nur
            // CryptographicException, JsonException und IOException; eine UnauthorizedAccessException
            // entkam und riss den Programmstart mit (Befund F).
            Delete();
            return null;
        }
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch (Exception)
        {
            // Löschen darf nicht werfen: Load() ruft es im eigenen Fehlerzweig auf — eine Ausnahme
            // von hier würde den gerade abgefangenen Fehler durch einen neuen ersetzen. Beim Abmelden
            // gilt dasselbe: Der Nutzer käme sonst nicht zurück auf den Anmeldebildschirm. Bleibt die
            // Datei liegen, ist sie weiterhin DPAPI-verschlüsselt.
        }
    }
}
