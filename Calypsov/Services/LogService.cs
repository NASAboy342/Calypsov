using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using Calypsov.Models;
using Calypsov.Repositories;

namespace Calypsov.Services;

/// <inheritdoc cref="ILogService"/>
public sealed class LogService : ILogService, IDisposable
{
    private const string LogsFolderName = "Logs";

    /// <summary>The Logs folder only ever holds this many files — the oldest is deleted once a new
    /// one would push the count over.</summary>
    private const int MaxLogFilesToKeep = 4;

    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);

    private readonly ISettingRepository _settingRepository;
    private readonly IEncryptionSettingsService _appSettings;
    private readonly ConcurrentQueue<LogEntry> _queue = new();
    private readonly object _schedulerLock = new();
    private Timer? _scheduler;

    // Cached locally instead of calling _appSettings.GetIsRecordLogEnabled() on every LogInfo/LogError
    // call: that method takes StorageEncryptionSettingsService's own _lock, which ToggleEncryption()
    // also holds for its *entire* zip/unzip run. Logging every API call (including /progress polls)
    // would otherwise block those requests on that lock for as long as a toggle is in flight, stalling
    // live progress updates. SetRecordLogEnabled is the only place this value ever changes, so a plain
    // cache kept in sync there is safe — volatile for cross-thread visibility (set from one request
    // thread, read from any other, with no other synchronization in common).
    private volatile bool _isRecordLogEnabled;

    public LogService(ISettingRepository settingRepository, IEncryptionSettingsService appSettings)
    {
        _settingRepository = settingRepository;
        _appSettings = appSettings;
        _isRecordLogEnabled = _appSettings.GetIsRecordLogEnabled();

        // App-start behavior: only run the background scheduler if logging is actually turned on.
        if (_isRecordLogEnabled)
        {
            StartScheduler();
        }
    }

    public void LogInfo(string message) => Enqueue("Info", message);

    public void LogError(string message) => Enqueue("Error", message);

    private void Enqueue(string tag, string message)
    {
        if (!_isRecordLogEnabled)
            return;

        _queue.Enqueue(new LogEntry(tag, message, DateTime.Now));
    }

    public bool IsRecordLogEnabled() => _isRecordLogEnabled;

    public bool SetRecordLogEnabled(bool enabled)
    {
        var saved = _appSettings.SetIsRecordLogEnabled(enabled);
        _isRecordLogEnabled = saved;

        if (saved)
        {
            StartScheduler();
        }
        else
        {
            StopScheduler();
        }

        return saved;
    }

    private void StartScheduler()
    {
        lock (_schedulerLock)
        {
            _scheduler ??= new Timer(_ => OnSchedulerTick(), null, FlushInterval, FlushInterval);
        }
    }

    private void StopScheduler()
    {
        lock (_schedulerLock)
        {
            _scheduler?.Dispose();
            _scheduler = null;
        }
    }

    private void OnSchedulerTick()
    {
        FlushQueue();
        EnforceRetention();
    }

    private void FlushQueue()
    {
        if (_queue.IsEmpty) return;

        var builder = new StringBuilder();
        while (_queue.TryDequeue(out var entry))
        {
            builder.Append(FormatEntry(entry));
        }
        if (builder.Length == 0) return;

        var fileName = GetLogFileName(DateTime.Now);
        _settingRepository.AppendText(LogsFolderName, fileName, builder.ToString());
    }

    private static string FormatEntry(LogEntry entry) =>
        $"[{entry.Tag}] {entry.Timestamp:yyyy-MM-dd HH:mm:ss} {entry.Message}{Environment.NewLine}";

    private static string GetLogFileName(DateTime date) => $"{date:yyyy-MM-dd}.log";

    /// <summary>Keeps only the <see cref="MaxLogFilesToKeep"/> most-recently-modified log files,
    /// deleting the oldest ones so the Logs folder never holds more than that many days' worth.</summary>
    private void EnforceRetention()
    {
        var files = _settingRepository.ListFiles(LogsFolderName)
            .OrderBy(f => f.LastModifiedUtc)
            .ToList();

        while (files.Count > MaxLogFilesToKeep)
        {
            _settingRepository.DeleteFile(LogsFolderName, files[0].Name);
            files.RemoveAt(0);
        }
    }

    public IReadOnlyList<LogFileSummary> GetLogFiles() =>
        _settingRepository.ListFiles(LogsFolderName)
            .OrderByDescending(f => f.LastModifiedUtc)
            .Select(f => new LogFileSummary(f.Name, f.LastModifiedUtc))
            .ToList();

    public string GetLogFileContent(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
        {
            throw new ArgumentException($"Invalid log file name \"{fileName}\".");
        }

        return _settingRepository.ReadText(LogsFolderName, fileName)
            ?? throw new ArgumentException($"Log file \"{fileName}\" was not found.");
    }

    public void Dispose() => StopScheduler();

    private sealed record LogEntry(string Tag, string Message, DateTime Timestamp);
}
