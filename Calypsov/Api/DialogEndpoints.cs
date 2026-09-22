using Calypsov.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Photino.NET;

namespace Calypsov.Api;

/// <summary>Minimal-API routes that show native OS file/folder picker dialogs via the app window.</summary>
public static class DialogEndpoints
{
    public static void MapDialogEndpoints(this WebApplication app, PhotinoWindow window)
    {
        var group = app.MapGroup("/api/dialog");

        group.MapPost("/pick-folder", async () =>
        {
            var paths = await window.ShowOpenFolderAsync(
                title: "Choose a folder",
                defaultPath: string.Empty,
                multiSelect: false);
            return Results.Ok(new PickPathResponse(paths.Length > 0 ? paths[0] : null));
        });

        group.MapPost("/pick-file", async () =>
        {
            var paths = await window.ShowOpenFileAsync(
                title: "Choose a file",
                defaultPath: string.Empty,
                multiSelect: false,
                filters: Array.Empty<(string Name, string[] Extensions)>());
            return Results.Ok(new PickPathResponse(paths.Length > 0 ? paths[0] : null));
        });
    }
}
