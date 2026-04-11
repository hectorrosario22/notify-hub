using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace NotifyHub.Api.Middleware;

public sealed class ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            await WriteProblemDetailsAsync(context, StatusCodes.Status400BadRequest, "Validation Failed",
                ex.Message, ex.Errors.Select(e => e.ErrorMessage).ToArray());
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Bad request: {Message}", ex.Message);
            await WriteProblemDetailsAsync(context, StatusCodes.Status400BadRequest, "Bad Request", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Unprocessable entity: {Message}", ex.Message);
            await WriteProblemDetailsAsync(context, StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteProblemDetailsAsync(context, StatusCodes.Status500InternalServerError, "Internal Server Error",
                "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemDetailsAsync(
        HttpContext context, int statusCode, string title, string detail, string[]? errors = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };

        if (errors is { Length: > 0 })
            problem.Extensions["errors"] = errors;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
