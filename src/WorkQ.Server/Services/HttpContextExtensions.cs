using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace WorkQ.Server.Services;

public static class HttpContextExtensions
{
    public static async Task WriteApiErrorAsync(this HttpContext context, ApiException exception)
    {
        var problem = new ProblemDetails
        {
            Status = exception.StatusCode,
            Title = exception.ErrorCode,
            Detail = exception.Message
        };

        context.Response.StatusCode = exception.StatusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
