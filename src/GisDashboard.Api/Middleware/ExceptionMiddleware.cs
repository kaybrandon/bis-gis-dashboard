using System.Net;
using System.Text.Json;
using GisDashboard.Application.Exceptions;

namespace GisDashboard.Api.Middleware;

public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (status, message) = ex switch
            {
                NotFoundException => (HttpStatusCode.NotFound, ex.Message),
                ForbiddenException => (HttpStatusCode.Forbidden, ex.Message),
                ValidationException => (HttpStatusCode.BadRequest, ex.Message),
                ServiceUnavailableException => (HttpStatusCode.ServiceUnavailable, ex.Message),
                ConflictException => (HttpStatusCode.Conflict, ex.Message),
                BadHttpRequestException => (HttpStatusCode.BadRequest, "This file is too large for the server request limit."),
                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
            };

            if (status == HttpStatusCode.InternalServerError)
            {
                _logger.LogError(ex, "Unhandled API exception");
                if (context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
                {
                    message = ex.ToString();
                }
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)status;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message }));
        }
    }
}
