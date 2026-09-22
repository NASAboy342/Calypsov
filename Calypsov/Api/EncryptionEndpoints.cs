using Calypsov.Models;
using Calypsov.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Calypsov.Api;

/// <summary>Minimal-API routes exposing <see cref="IEncryptionSettingsService"/> to the UI.</summary>
public static class EncryptionEndpoints
{
    public static void MapEncryptionEndpoints(this WebApplication app, IEncryptionSettingsService settings)
    {
        var group = app.MapGroup("/api/encryption");

        group.MapGet("/status", () =>
            Try(() => Results.Ok(new EncryptionStatusResponse(settings.IsEncryptionEnabled()))));

        group.MapPost("/toggle", () =>
            Try(() =>Results.Ok(new EncryptionStatusResponse(settings.ToggleEncryption()))));

        group.MapGet("/targets", () =>
            Try(() => Results.Ok(settings.GetTargets())));

        group.MapPost("/targets", (AddTargetRequest request) =>
            Try(() => Results.Ok(settings.AddTarget(request.Category, request.Path))));

        group.MapDelete("/targets/{id:guid}", (Guid id) =>
            Try(() => settings.RemoveTarget(id) ? Results.NoContent() : Results.NotFound()));
    }

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
