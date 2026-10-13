using Calypsov.Models;

namespace Calypsov.Services;

/// <summary>Reads the app's own version and checks GitHub Releases for a newer one.</summary>
public interface IAppUpdateService
{
    string GetCurrentVersion();

    Task<UpdateCheckResponse> CheckForUpdateAsync();
}
