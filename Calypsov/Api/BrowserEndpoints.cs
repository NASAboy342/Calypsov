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
            Try(() => Results.Ok(browserProfiles.GetProfiles(ParseBrowserType(browser)))));

        group.MapGet("/{browser}/selection", (string browser) =>
            Try(() => Results.Ok(new BrowserProfileSelection(browserProfiles.GetSelectedProfileId(ParseBrowserType(browser))))));

        group.MapPost("/{browser}/selection", (string browser, SetBrowserProfileSelectionRequest request) =>
            Try(() =>
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

    /// <summary>
    /// Runs an endpoint body and turns any exception into a JSON <see cref="ErrorResponse"/> with the
    /// exact exception message, so the UI can show the caller exactly what the backend reported.
    /// </summary>
    private static IResult Try(Func<IResult> body)
    {
        try
        {
            return body();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ErrorResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            return Results.Json(new ErrorResponse(ex.Message), statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
