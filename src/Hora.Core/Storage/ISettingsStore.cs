namespace Hora.Core.Storage;

public interface ISettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}
