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
                title: "Choose folders",
                defaultPath: string.Empty,
                multiSelect: true);
            return Results.Ok(new PickPathsResponse(paths));
        });

        group.MapPost("/pick-file", async () =>
        {
            var paths = await window.ShowOpenFileAsync(
                title: "Choose files",
                defaultPath: string.Empty,
                multiSelect: true,
                filters: Array.Empty<(string Name, string[] Extensions)>());
            return Results.Ok(new PickPathsResponse(paths));
        });
    }
}
