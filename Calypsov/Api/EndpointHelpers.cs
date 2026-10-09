using Calypsov.Models;
using Calypsov.Services;
using Microsoft.AspNetCore.Http;

namespace Calypsov.Api;

/// <summary>
/// Shared <c>Try(...)</c> error-wrapper used by every endpoint group — turns any exception into a
/// JSON <see cref="ErrorResponse"/> with the exact exception message, and logs it via
/// <see cref="LogService"/> along the way so every endpoint failure ends up in the log file.
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
            LogService?.LogError(ex.ToString());
            return Results.BadRequest(new ErrorResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            LogService?.LogError(ex.ToString());
            return Results.Conflict(new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            LogService?.LogError(ex.ToString());
            return Results.Json(new ErrorResponse(ex.Message), statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
