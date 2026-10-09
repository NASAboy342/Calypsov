using Calypsov.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Calypsov.Api;

/// <summary>
/// Logs every "/api/*" request's method, path, status code, request body and response body via
/// <see cref="ILogService"/>. Registered once from Program.cs, before any endpoint is mapped, so
/// it wraps every request regardless of which group ends up handling it.
/// </summary>
public static class RequestLoggingMiddleware
{
    /// <summary>Bodies longer than this are truncated before being logged, so one oversized
    /// request/response can't bloat a day's log file.</summary>
    private const int MaxLoggedBodyLength = 4000;

    public static void UseApiRequestLogging(this WebApplication app, ILogService logService)
    {
        app.Use(next => async context =>
        {
            if (!context.Request.Path.StartsWithSegments("/api"))
            {
                await next(context);
                return;
            }

            // The "/api/logs" endpoints themselves are excluded from body logging: GET
            // /api/logs/files/{fileName} returns a whole log file's content as its response body,
            // so logging that response would write the file's own (ever-growing) content back
            // into itself on every view. The method/path/status summary is still logged for these
            // like any other call — only the request/response bodies are skipped.
            var logBodies = !context.Request.Path.StartsWithSegments("/api/logs");

            var requestBody = logBodies ? await ReadRequestBodyAsync(context.Request) : string.Empty;

            var originalResponseBody = context.Response.Body;
            await using var responseBuffer = new MemoryStream();
            context.Response.Body = responseBuffer;

            try
            {
                await next(context);

                var responseBody = logBodies ? await ReadResponseBodyAsync(responseBuffer) : string.Empty;

                var message = $"{context.Request.Method} {context.Request.Path}{context.Request.QueryString} -> {context.Response.StatusCode}";
                if (requestBody.Length > 0) message += $" | Request: {Truncate(requestBody)}";
                if (responseBody.Length > 0) message += $" | Response: {Truncate(responseBody)}";
                logService.LogInfo(message);

                responseBuffer.Seek(0, SeekOrigin.Begin);
                await responseBuffer.CopyToAsync(originalResponseBody);
            }
            finally
            {
                context.Response.Body = originalResponseBody;
            }
        });
    }

    /// <summary>Reads the request body without consuming it for the real handler — buffers it so
    /// model binding further down the pipeline can still read the same stream from the start.</summary>
    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        if (!request.ContentLength.HasValue || request.ContentLength == 0)
            return string.Empty;

        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }

    /// <summary>Reads the buffered response body without disturbing it — the caller still needs to
    /// copy this same buffer on to the real response stream afterward.</summary>
    private static async Task<string> ReadResponseBodyAsync(MemoryStream buffer)
    {
        buffer.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(buffer, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static string Truncate(string value) =>
        value.Length <= MaxLoggedBodyLength
            ? value
            : value[..MaxLoggedBodyLength] + $"...(truncated, {value.Length} chars total)";
}
