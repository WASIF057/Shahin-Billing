using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace ShahinBilling.Api.Infrastructure;

/// <summary>Turns exceptions into ProblemDetails JSON the Angular app can show.
/// Validation errors come back as { errors: { field: [messages] } }.</summary>
public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            var errors = ex.Errors
                .GroupBy(e => ToCamel(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            var problem = new ValidationProblemDetails(errors)
            {
                Status = 400,
                Title = errors.Values.SelectMany(v => v).FirstOrDefault() ?? "Please correct the highlighted fields."
            };
            await Write(context, 400, problem);
        }
        catch (AppException ex)
        {
            await Write(context, ex.StatusCode, new ProblemDetails { Status = ex.StatusCode, Title = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error");
            await Write(context, 500, new ProblemDetails
            {
                Status = 500,
                Title = "Something went wrong on the server. Check the API logs for details."
            });
        }
    }

    private static Task Write(HttpContext ctx, int status, ProblemDetails problem)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        return ctx.Response.WriteAsJsonAsync<object>(problem);
    }

    private static string ToCamel(string path) =>
        string.Join(".", path.Split('.').Select(p => p.Length > 0 ? char.ToLowerInvariant(p[0]) + p[1..] : p));
}
