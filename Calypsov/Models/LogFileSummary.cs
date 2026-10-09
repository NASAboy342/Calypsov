namespace Calypsov.Models;

/// <summary>One log file's name and last-modified time, as returned by
/// <see cref="Services.ILogService.GetLogFiles"/> — newest-modified first.</summary>
public sealed record LogFileSummary(string FileName, DateTime ModifiedAtUtc);
