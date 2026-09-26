namespace Calypsov.Repositories;

/// <summary>
/// Reads and writes small JSON-backed settings files, each under its own file name inside the
/// same per-OS app-data folder — so different features can persist their own settings without
/// colliding, while sharing one place that knows where "the app's data folder" is.
/// </summary>
public interface ISettingRepository
{
    /// <summary>Reads and deserializes <paramref name="fileName"/>, or null if the file doesn't
    /// exist yet, is empty, or fails to parse — callers decide their own default in that case.</summary>
    T? Load<T>(string fileName) where T : class;

    /// <summary>Serializes <paramref name="data"/> and writes it to <paramref name="fileName"/>,
    /// overwriting whatever was there before.</summary>
    void Save<T>(string fileName, T data) where T : class;

    /// <summary>
    /// The shared per-OS app-data folder settings files are stored under — exposed for callers
    /// that need a subfolder alongside those files (e.g. encrypted-target storage), not just a
    /// named settings file. Creates the folder if it doesn't exist yet.
    /// </summary>
    string GetAppDataFolder();
}
