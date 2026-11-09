using System.Data.Common;
using System.Text.Json;

namespace Dbcc.Api.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The caller closed the connection; there is no response to write.
        }
        catch (Exception exception)
        {
            var databaseFailure = IsDatabaseFailure(exception);
            if (databaseFailure)
                logger.LogError(exception, "Database request failed for {Method} {Path}.", context.Request.Method, context.Request.Path);
            else
                logger.LogError(exception, "Unhandled request failure for {Method} {Path}.", context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            context.Response.StatusCode = databaseFailure ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            var message = databaseFailure
                ? "The database is unavailable. Check the SQL Server connection and try again."
                : "Internal server error.";
            await JsonSerializer.SerializeAsync(context.Response.Body, new { error = message }, cancellationToken: context.RequestAborted);
        }
    }

    public static bool IsDatabaseFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException) return true;
            if (current is TimeoutException) return true;
        }
        return false;
    }
}
