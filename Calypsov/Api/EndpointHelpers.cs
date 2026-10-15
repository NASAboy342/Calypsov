using Calypsov.Models;
using Calypsov.Services;
using Microsoft.AspNetCore.Http;

namespace Calypsov.Api;

/// <summary>
/// Shared <c>Try(...)</c> error-wrapper used by every endpoint group — turns any exception into a
/// JSON <see cref="ErrorResponse"/> with the exact exception message. Only a genuinely unexpected
/// failure (the final <c>catch (Exception ex)</c>, mapped to 500) gets logged here with its full
/// stack trace via <see cref="LogService"/> — an <see cref="ArgumentException"/>/
/// <see cref="InvalidOperationException"/> (400/409) is an expected, correct validation outcome, not
/// a server fault, and its message is already captured by the per-request logging middleware
/// (method/path/status/response body) for every call regardless of status code, so logging it a
/// second time here at <c>[Error]</c> severity with a stack trace would just be misleading noise.
/// </summary>
public static class EndpointHelpers
{
    /// <summary>Set once from Program.cs right after the DI container is built, so this static
    /// helper can log without every endpoint file taking its own ILogService parameter.</summary>
    public static ILogService? LogService { get; set; }

    public static IResult Try(Func<IResult> body)
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
            LogService?.LogError(ex.ToString());
            return Results.Json(new ErrorResponse(ex.Message), statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public static async Task<IResult> Try(Func<Task<IResult>> body)
    {
        try
        {
            return await body();
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
            LogService?.LogError(ex.ToString());
            return Results.Json(new ErrorResponse(ex.Message), statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
