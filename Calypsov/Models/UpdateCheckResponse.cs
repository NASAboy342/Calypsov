namespace Calypsov.Models;

public sealed record UpdateCheckResponse(
    string CurrentVersion,
    string? LatestVersion,
    bool UpdateAvailable,
    string? ReleaseUrl,
    string? Error);
