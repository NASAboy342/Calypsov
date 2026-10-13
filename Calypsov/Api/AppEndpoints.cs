using Calypsov.Models;
using Calypsov.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Calypsov.Api;

/// <summary>Minimal-API routes exposing <see cref="IAppUpdateService"/> to the UI.</summary>
public static class AppEndpoints
{
    public static void MapAppEndpoints(this WebApplication app, IAppUpdateService appUpdateService)
    {
        var group = app.MapGroup("/api/app");

        group.MapGet("/version", () =>
            EndpointHelpers.Try(() => Results.Ok(new AppVersionResponse(appUpdateService.GetCurrentVersion()))));

        group.MapGet("/update-check", () =>
            EndpointHelpers.Try(async () => Results.Ok(await appUpdateService.CheckForUpdateAsync())));

        group.MapPost("/open-release-page", (OpenUrlRequest request) =>
            EndpointHelpers.Try(() =>
            {
                if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw new ArgumentException("A valid http/https URL is required.");
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.ToString())
                {
                    UseShellExecute = true,
                });

                return Results.NoContent();
            }));
    }
}
