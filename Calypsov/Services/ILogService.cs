using Calypsov.Models;

namespace Calypsov.Services;

/// <summary>
/// Queues "[Info]"/"[Error]" log lines and writes them to a dated file (one per day, under the
/// app-data folder's "Logs" subfolder) on a background schedule — <see cref="LogInfo"/> and
/// <see cref="LogError"/> never touch the file system directly. Logging only happens while the
/// IsRecordLog setting is on; toggling it via <see cref="SetRecordLogEnabled"/> also starts or
/// stops the background scheduler, so nothing runs in the background when logging is off.
/// </summary>
public interface ILogService
{
    void LogInfo(string message);
    void LogError(string message);

    bool IsRecordLogEnabled();

    /// <summary>Persists the setting and starts (true) or stops (false) the background scheduler to match.</summary>
    bool SetRecordLogEnabled(bool enabled);

    /// <summary>Every log file currently on disk, newest-modified first.</summary>
    IReadOnlyList<LogFileSummary> GetLogFiles();

    /// <summary>Throws <see cref="ArgumentException"/> if <paramref name="fileName"/> is invalid or doesn't exist.</summary>
    string GetLogFileContent(string fileName);
}
