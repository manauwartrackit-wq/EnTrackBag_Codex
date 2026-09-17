using EnTrackBag.Api.Data;
using EnTrackBag.Api.Data.Entities;
namespace EnTrackBag.Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, BltsmftDbContext db, ILogger<ExceptionMiddleware> logger)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? context.TraceIdentifier;
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);
            try
            {
                db.EnTrackBagExceptions.Add(new EnTrackBagExceptionEntity
                {
                    OccurredAt = DateTime.Now,
                    CorrelationId = correlationId,
                    HttpMethod = context.Request.Method,
                    RequestPath = context.Request.Path,
                    QueryString = context.Request.QueryString.Value,
                    StatusCode = 500,
                    ExceptionType = ex.GetType().FullName,
                    Message = ex.Message,
                    StackTrace = ex.StackTrace,
                    InnerException = ex.InnerException?.ToString(),
                    UserName = context.User?.Identity?.Name,
                    RemoteIp = context.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = context.Request.Headers["User-Agent"].ToString(),
                    Source = "EnTrackBag.Api",
                    IsHandled = true
                });

                await db.SaveChangesAsync();
            }
            catch (Exception persistEx)
            {
                logger.LogError(persistEx, "Failed to persist exception. CorrelationId: {CorrelationId}", correlationId);
            }
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                title = "An unexpected error occurred.",
                status = 500,
                correlationId
            });
        }
    }
}
