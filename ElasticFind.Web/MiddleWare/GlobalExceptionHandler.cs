namespace ElasticFind.Web.MiddleWare;

using Microsoft.AspNetCore.Http;
using System.Net;
using System.Text.Json;
using Serilog;
using ElasticFind.Repository.ViewModels;
using Serilog.Context;
using ElasticFind.Service.Exceptions;


public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public GlobalExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        var userAgent = context.Request.Headers["User-Agent"].ToString();
        var userName = context.User.Identity?.Name ?? "anonymous";
        var filePath = context.Request.Path;
        var raiseDate = DateTime.Now;
        var machineName = Environment.MachineName;
        var threadId = Environment.CurrentManagedThreadId;
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "unknown";
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var processInfo = $"{process.ProcessName}:{process.Id}";
        var methodName = context.Request.Method; // Assuming HTTP method here
        var propsTest = "test_value";

        // You can also add any custom dynamic properties in a dictionary as needed.
        using (LogContext.PushProperty("ip_address", ipAddress))
        using (LogContext.PushProperty("user_agent", userAgent))
        using (LogContext.PushProperty("user_name", userName))
        using (LogContext.PushProperty("file_path", filePath))
        using (LogContext.PushProperty("raise_date", raiseDate))
        using (LogContext.PushProperty("props_test", propsTest))
        using (LogContext.PushProperty("machine_name", machineName))
        using (LogContext.PushProperty("thread_id", threadId))
        using (LogContext.PushProperty("environment_name", environmentName))
        using (LogContext.PushProperty("process_info", processInfo))
        using (LogContext.PushProperty("method_name", methodName))
        {
            try
            {
                await _next(context); // pass to next middleware
            }
            catch (ElasticSearchException ex)
            {
                var stackTrace = new System.Diagnostics.StackTrace(ex, true);
                var lineNumber = stackTrace.GetFrame(0)?.GetFileLineNumber() ?? 0;
                Console.WriteLine("Line number: " + lineNumber);
                using (LogContext.PushProperty("line_number", lineNumber))
                {
                    Log.Error(ex, ex.Message); 
                }
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Unhandled Exception ocuured: " + ex.Message);
            }
            catch (Exception ex)
            {
                var stackTrace = new System.Diagnostics.StackTrace(ex, true);
                var lineNumber = stackTrace.GetFrame(0)?.GetFileLineNumber() ?? 0;
                Console.WriteLine("Line number: " + lineNumber);
                using (LogContext.PushProperty("line_number", lineNumber))
                {
                    Log.Error(ex, "Unhandled exception for user {user_name} from IP {ip_address} on path {file_path}. User-Agent: {user_agent}", userName, ipAddress, filePath, userAgent);
                }
                
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("An unexpected error occurred.");
            }
        }
    }

    // private static Task HandleExceptionAsync(HttpContext context, Exception ex)
    // {
    //     context.Response.ContentType = "application/json";
    //     context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

    //     Error error = new()
    //     {
    //         ErrorCode = (int)HttpStatusCode.InternalServerError,
    //         ErrorMessage = ex.Message,
    //     };
    //     return context.Response.WriteAsync(JsonSerializer.Serialize(error));
    // }
}