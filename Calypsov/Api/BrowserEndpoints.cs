using Calypsov.Models;
using Calypsov.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Calypsov.Api;

/// <summary>Minimal-API routes exposing <see cref="IBrowserProfileService"/> to the UI.</summary>
public static class BrowserEndpoints
{
    public static void MapBrowserEndpoints(this WebApplication app, IBrowserProfileService browserProfiles)
    {
        var group = app.MapGroup("/api/browsers");

        group.MapGet("/{browser}/profiles", (string browser) =>
            EndpointHelpers.Try(() => Results.Ok(browserProfiles.GetProfiles(ParseBrowserType(browser)))));

        group.MapGet("/{browser}/selection", (string browser) =>
            EndpointHelpers.Try(() => Results.Ok(new BrowserProfileSelection(browserProfiles.GetSelectedProfileId(ParseBrowserType(browser))))));

        group.MapPost("/{browser}/selection", (string browser, SetBrowserProfileSelectionRequest request) =>
            EndpointHelpers.Try(() =>
            {
                var type = ParseBrowserType(browser);
                browserProfiles.SetSelectedProfileId(type, request.ProfileId);
                return Results.Ok(new BrowserProfileSelection(request.ProfileId));
            }));
    }

    private static BrowserType ParseBrowserType(string browser) =>
        Enum.TryParse<BrowserType>(browser, ignoreCase: true, out var type)
            ? type
            : throw new ArgumentException($"Unknown browser \"{browser}\". Expected \"edge\" or \"chrome\".");
}
