using Calypsov.Models;
using Calypsov.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Calypsov.Api;

/// <summary>Minimal-API routes exposing <see cref="ILogService"/> to the UI.</summary>
public static class LogEndpoints
{
    public static void MapLogEndpoints(this WebApplication app, ILogService logService)
    {
        var group = app.MapGroup("/api/logs");

        group.MapGet("/setting", () =>
            EndpointHelpers.Try(() => Results.Ok(new LogSettingResponse(logService.IsRecordLogEnabled()))));

        group.MapPut("/setting", (UpdateLogSettingRequest request) =>
            EndpointHelpers.Try(() => Results.Ok(new LogSettingResponse(logService.SetRecordLogEnabled(request.IsRecordLog)))));

        group.MapGet("/files", () =>
            EndpointHelpers.Try(() => Results.Ok(logService.GetLogFiles())));

        group.MapGet("/files/{fileName}", (string fileName) =>
            EndpointHelpers.Try(() => Results.Ok(new LogFileContentResponse(fileName, logService.GetLogFileContent(fileName)))));
    }
}
