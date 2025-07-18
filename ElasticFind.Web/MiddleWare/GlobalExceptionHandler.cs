namespace ElasticFind.Web.MiddleWare;

using Microsoft.AspNetCore.Http;
using System.Net;
using System.Text.Json;
using Serilog;
using ElasticFind.Repository.ViewModels;
using Serilog.Context;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var userAgent = context.Request.Headers["User-Agent"].ToString();
        var userName = context.User.Identity?.IsAuthenticated == true
            ? context.User.Identity.Name
            : "Anonymous";

        using (LogContext.PushProperty("UserAgent", userAgent))
        using (LogContext.PushProperty("UserName", userName))
        using (LogContext.PushProperty("IPAddress", context.Connection.RemoteIpAddress?.ToString()))
            try 
            {
                _logger.Information("Handling request: {Method} {Url}", context.Request.Method, context.Request.Path);
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error caught by global middleware.");
                await HandleExceptionAsync(context, ex);
            }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        Error error = new()
        {
            ErrorCode = (int)HttpStatusCode.InternalServerError,
            ErrorMessage = ex.Message,
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(error));
    }
}