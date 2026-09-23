using Hora.App.Services;

namespace Hora.App.Tests.TestDoubles;

/// <summary>
/// Attrappe für den Speichern-Dialog. Sie merkt sich den vorgeschlagenen Dateinamen und gibt den
/// Pfad zurück, den der Test vorgibt; null steht für „abgebrochen“.
/// </summary>
internal sealed class FakeSaveFilePicker : ISaveFilePicker
{
    public string? PathToReturn { get; set; }

    public List<string> SuggestedNames { get; } = new();

    public int Calls => SuggestedNames.Count;

    public string? PickPath(string suggestedFileName)
    {
        SuggestedNames.Add(suggestedFileName);
        return PathToReturn;
    }
}
