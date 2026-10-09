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
            EndpointHelpers.Try(() => Results.Ok(new EncryptionStatusResponse(settings.IsEncryptionEnabled()))));

        group.MapPost("/toggle", () =>
            EndpointHelpers.Try(() =>Results.Ok(new EncryptionStatusResponse(settings.ToggleEncryption()))));

        group.MapGet("/progress", () =>
            EndpointHelpers.Try(() => Results.Ok(settings.GetProgress())));

        group.MapGet("/targets", () =>
            EndpointHelpers.Try(() => Results.Ok(settings.GetTargets())));

        group.MapPost("/targets", (AddTargetRequest request) =>
            EndpointHelpers.Try(() => Results.Ok(settings.AddTarget(request.Category, request.Path))));

        group.MapDelete("/targets/{id:guid}", (Guid id) =>
            EndpointHelpers.Try(() => settings.RemoveTarget(id) ? Results.NoContent() : Results.NotFound()));

        group.MapGet("/zip-settings", () =>
            EndpointHelpers.Try(() => Results.Ok(settings.GetZipSettings())));

        group.MapPut("/zip-settings", (UpdateZipSettingsRequest request) =>
            EndpointHelpers.Try(() => Results.Ok(settings.SetZipSettings(request.IsUseTurboZip, request.IsEncryptZippedFile))));
    }
}
