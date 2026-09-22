using System.Security.AccessControl;
using System.Security.Principal;

namespace Hora.Core.Tests.Storage;

/// <summary>
/// Entzieht dem aktuellen Windows-Benutzer für die Lebensdauer des Objekts das Leserecht auf eine
/// Datei und gibt es beim Verwerfen wieder frei. Damit lässt sich ohne Administratorrechte genau
/// die UnauthorizedAccessException nachstellen, die bei einer Ordnerumleitung auf eine Netzfreigabe
/// oder bei restriktiven Zugriffsrechten auftritt.
///
/// Verweigert wird bewusst nur ReadData: Das Lesen und Ändern der Rechte selbst (der Benutzer ist
/// Eigentümer) und das Löschen der Datei bleiben möglich, sodass der Test hinterher aufräumen kann.
/// </summary>
internal sealed class FileReadDenial : IDisposable
{
    private readonly FileInfo _file;
    private readonly FileSystemAccessRule _rule;

    private FileReadDenial(FileInfo file, FileSystemAccessRule rule)
    {
        _file = file;
        _rule = rule;
    }

    public static FileReadDenial For(string path)
    {
        var file = new FileInfo(path);
        var rule = new FileSystemAccessRule(
            WindowsIdentity.GetCurrent().User!,
            FileSystemRights.ReadData,
            AccessControlType.Deny);

        var security = file.GetAccessControl();
        security.AddAccessRule(rule);
        file.SetAccessControl(security);

        return new FileReadDenial(file, rule);
    }

    public void Dispose()
    {
        _file.Refresh();
        if (!_file.Exists)
        {
            // Die geprüfte Klasse darf die Datei im Fehlerzweig löschen (so macht es der
            // Zugangsdatenspeicher); dann gibt es keine Rechte mehr zurückzunehmen.
            return;
        }

        var security = _file.GetAccessControl();
        security.RemoveAccessRule(_rule);
        _file.SetAccessControl(security);
    }
}
