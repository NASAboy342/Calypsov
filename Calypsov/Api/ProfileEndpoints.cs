using Calypsov.Models;
using Calypsov.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Calypsov.Api;

/// <summary>Minimal-API routes exposing protection-profile management. Backed by the same
/// <see cref="IEncryptionSettingsService"/>/settings.json store encryption targets already use —
/// profiles are named, switchable partitions of that same data, not a separate store.</summary>
public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this WebApplication app, IEncryptionSettingsService settings)
    {
        var group = app.MapGroup("/api/profiles");

        group.MapGet("/", () =>
            EndpointHelpers.Try(() => Results.Ok(new ProfilesResponse(settings.GetProfiles(), settings.GetCurrentProfileId()))));

        group.MapPost("/", (CreateProfileRequest request) =>
            EndpointHelpers.Try(() => Results.Ok(settings.CreateProfile(request.Name, request.Color, request.Icon))));

        group.MapPut("/{id:guid}", (Guid id, UpdateProfileRequest request) =>
            EndpointHelpers.Try(() => Results.Ok(settings.UpdateProfile(id, request.Name, request.Color, request.Icon))));

        group.MapPost("/{id:guid}/switch", (Guid id) =>
            EndpointHelpers.Try(() => Results.Ok(settings.SwitchProfile(id))));

        group.MapDelete("/{id:guid}", (Guid id) =>
            EndpointHelpers.Try(() =>
            {
                settings.DeleteProfile(id);
                return Results.NoContent();
            }));
    }
}
