using System.Text.Json;

namespace Jira.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Jira.Api.Collaboration.CollaborationForbiddenException)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
        }
        catch (Exception exception) when (exception is Jira.Application.Services.StaleWriteException or Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            context.Response.StatusCode = StatusCodes.Status412PreconditionFailed;
            await context.Response.WriteAsJsonAsync(new { code = "STALE_VERSION", message = "This resource has changed. Reload it before saving again." });
        }
        catch (Jira.Application.Services.UserDirectoryUnavailableException exception)
        {
            await WriteErrorAsync(context, StatusCodes.Status503ServiceUnavailable, exception.Message);
        }
        catch (Jira.Application.Services.ProjectConflictException exception)
        {
            await WriteErrorAsync(context, StatusCodes.Status409Conflict, exception.Message);
        }
        catch (Jira.Application.Services.ResourceNotFoundException)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "The requested resource was not found.");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                ex,
                "Validation error while processing request.");

            await WriteErrorAsync(
                context,
                StatusCodes.Status400BadRequest,
                ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Invalid operation while processing request.");

            await WriteErrorAsync(
                context,
                StatusCodes.Status400BadRequest,
                ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception while processing request.");

            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.");
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = statusCode,
            error = message,
            traceId = context.TraceIdentifier
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response));
    }
}

