using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using Calypsov.Models;

namespace Calypsov.Services;

/// <inheritdoc cref="IAppUpdateService"/>
public sealed class AppUpdateService : IAppUpdateService
{
    private const string RepoOwner = "NASAboy342";
    private const string RepoName = "Calypsov";

    private static readonly HttpClient _httpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Calypsov-App", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public string GetCurrentVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";
    }

    public async Task<UpdateCheckResponse> CheckForUpdateAsync()
    {
        var currentVersion = GetCurrentVersion();

        try
        {
            var response = await _httpClient.GetAsync(
                $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new UpdateCheckResponse(currentVersion, null, false, null,
                    "No releases have been published yet.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResponse(currentVersion, null, false, null,
                    "Couldn't check for updates right now — please try again later.");
            }

            var release = await response.Content.ReadFromJsonAsync<GitHubReleaseDto>();
            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return new UpdateCheckResponse(currentVersion, null, false, null,
                    "Couldn't read the latest release information.");
            }

            var latestVersion = release.TagName.TrimStart('v', 'V');
            var updateAvailable = IsNewer(latestVersion, currentVersion);

            return new UpdateCheckResponse(currentVersion, latestVersion, updateAvailable, release.HtmlUrl, null);
        }
        catch (Exception)
        {
            return new UpdateCheckResponse(currentVersion, null, false, null,
                "Couldn't reach GitHub to check for updates — check your internet connection.");
        }
    }

    private static bool IsNewer(string latest, string current)
    {
        if (!TryParseSemVer(latest, out var latestVersionValue)) return false;
        if (!TryParseSemVer(current, out var currentVersionValue)) return true;

        return latestVersionValue.CompareTo(currentVersionValue) > 0;
    }

    private static bool TryParseSemVer(string value, out Version version)
    {
        // Strip any -prerelease/+build metadata suffix (e.g. "1.2.0-beta1" -> "1.2.0") before parsing.
        var core = value.Split('-', '+')[0];
        return Version.TryParse(core, out version!);
    }

    private sealed record GitHubReleaseDto(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("html_url")] string HtmlUrl);
}
