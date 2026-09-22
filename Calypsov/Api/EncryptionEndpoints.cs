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
            Results.Ok(new EncryptionStatusResponse(settings.IsEncryptionEnabled())));

        group.MapPost("/toggle", () =>
            Results.Ok(new EncryptionStatusResponse(settings.ToggleEncryption())));

        group.MapGet("/targets", () =>
            Results.Ok(settings.GetTargets()));

        group.MapPost("/targets", (AddTargetRequest request) =>
        {
            try
            {
                var target = settings.AddTarget(request.Category, request.Path);
                return Results.Ok(target);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ErrorResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ErrorResponse(ex.Message));
            }
        });

        group.MapDelete("/targets/{id:guid}", (Guid id) =>
            settings.RemoveTarget(id) ? Results.NoContent() : Results.NotFound());
    }
}
