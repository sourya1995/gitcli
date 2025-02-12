using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Data;
using System.Net.Sockets;

public static class EnhancedExceptionHandlerExtensions
{
    public static void UseEnhancedExceptionHandler(this IApplicationBuilder app, bool includeDevelopmentDetails = false)
    {
        app.UseExceptionHandler(appError =>
        {
            appError.Run(async context =>
            {
                var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
                var exception = exceptionHandlerFeature?.Error;
                var logger = context.RequestServices.GetService<ILogger<Startup>>();

                if (exception == null) return;

                // Log the full exception details
                logger?.LogError(exception, "Unhandled exception occurred: {ExceptionMessage}", exception.Message);

                context.Response.ContentType = "application/json";
                context.Response.Headers.Add("X-Error-Handler-Version", "2.0");

                var (statusCode, responseData) = HandleException(exception, includeDevelopmentDetails);
                context.Response.StatusCode = statusCode;

                await context.Response.WriteAsync(JsonSerializer.Serialize(responseData, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
            });
        });
    }

    private static (int statusCode, object response) HandleException(Exception exception, bool includeDevelopmentDetails)
    {
        var statusCode = (int)HttpStatusCode.InternalServerError;
        var exceptionType = exception.GetType().Name;
        object responseData;

        // Enhanced exception handling with more specific cases
        switch (exception)
        {
            // HTTP and API Related Exceptions
            case HttpRequestException httpEx when httpEx.StatusCode.HasValue:
                statusCode = (int)httpEx.StatusCode.Value;
                responseData = CreateErrorResponse(exceptionType, statusCode, "HTTP request failed", httpEx.Message);
                break;

            case ApiException apiEx:
                statusCode = (int)apiEx.StatusCode;
                responseData = CreateErrorResponse(exceptionType, statusCode, apiEx.Message, apiEx.Details);
                break;

            // Authentication & Authorization Exceptions
            case UnauthorizedAccessException:
                statusCode = (int)HttpStatusCode.Unauthorized;
                responseData = CreateErrorResponse(exceptionType, statusCode, "Authentication required", "Access denied");
                break;

            case SecurityException:
                statusCode = (int)HttpStatusCode.Forbidden;
                responseData = CreateErrorResponse(exceptionType, statusCode, "Security violation", "Insufficient permissions");
                break;

            // Data Related Exceptions
            case KeyNotFoundException:
                statusCode = (int)HttpStatusCode.NotFound;
                responseData = CreateErrorResponse(exceptionType, statusCode, "Resource not found", "The requested resource does not exist");
                break;

            case ValidationException validationEx:
                statusCode = (int)HttpStatusCode.BadRequest;
                responseData = new
                {
                    ExceptionType = exceptionType,
                    Code = statusCode,
                    Message = "Validation failed",
                    ValidationErrors = validationEx.Errors,
                    Timestamp = DateTime.UtcNow
                };
                break;

            case DbException dbEx:
                statusCode = (int)HttpStatusCode.ServiceUnavailable;
                responseData = CreateErrorResponse(exceptionType, statusCode, "Database error", "A database error occurred");
                break;

            case DuplicateNameException:
                statusCode = (int)HttpStatusCode.Conflict;
                responseData = CreateErrorResponse(exceptionType, statusCode, "Duplicate entry", "Resource already exists");
                break;

            // Input Related Exceptions
            case ArgumentException or ArgumentNullException:
                statusCode = (int)HttpStatusCode.BadRequest;
                responseData = CreateErrorResponse(exceptionType, statusCode, "Invalid input", "The provided arguments are invalid");
                break;

            case FormatException:
                statusCode = (int)HttpStatusCode.BadRequest;
                responseData = CreateErrorResponse(exceptionType, statusCode, "Invalid format", "The input format is incorrect");
                break;

            // System Related Exceptions
            case IOException ioEx:
                statusCode = (int)HttpStatusCode.InternalServerError;
                responseData = CreateErrorResponse(exceptionType, statusCode, "I/O error", "File system operation failed");
                break;

            case OutOfMemoryException:
                statusCode = (int)HttpStatusCode.ServiceUnavailable;
                responseData = CreateErrorResponse(exceptionType, statusCode, "System resource error", "Insufficient memory");
                break;

            case SocketException sockEx:
                statusCode = (int)HttpStatusCode.BadGateway;
                responseData = CreateErrorResponse(exceptionType, statusCode, "Network error", "Communication error occurred");
                break;

            // Operation Related Exceptions
            case NotSupportedException:
                statusCode = (int)HttpStatusCode.NotImplemented;
                responseData = CreateErrorResponse(exceptionType, statusCode, "Not implemented", "This feature is not available");
                break;

            case InvalidOperationException:
                statusCode = (int)HttpStatusCode.Conflict;
                responseData = CreateErrorResponse(exceptionType, statusCode, "Invalid operation", "The requested operation is invalid");
                break;

            case TimeoutException:
                statusCode = (int)HttpStatusCode.RequestTimeout;
                responseData = CreateErrorResponse(exceptionType, statusCode, "Timeout", "The operation timed out");
                break;

            case OperationCanceledException:
                statusCode = (int)HttpStatusCode.BadRequest;
                responseData = CreateErrorResponse(exceptionType, statusCode, "Operation cancelled", "The operation was cancelled");
                break;

            // Default case for unknown exceptions
            default:
                responseData = CreateErrorResponse(
                    exceptionType,
                    statusCode,
                    "An unexpected error occurred",
                    includeDevelopmentDetails ? exception.Message : "Internal server error"
                );
                break;
        }

        return (statusCode, responseData);
    }

    private static object CreateErrorResponse(string exceptionType, int code, string message, string details) => new
    {
        ExceptionType = exceptionType,
        Code = code,
        Message = message,
        Details = details,
        Timestamp = DateTime.UtcNow,
        TraceId = Guid.NewGuid().ToString()
    };
}

// Enhanced custom exception classes
public class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string Details { get; }

    public ApiException(HttpStatusCode statusCode, string message, string details = null) 
        : base(message)
    {
        StatusCode = statusCode;
        Details = details ?? message;
    }
}

public class ValidationException : Exception
{
    public Dictionary<string, string[]> Errors { get; }

    public ValidationException(Dictionary<string, string[]> errors, string message = "Validation failed") 
        : base(message)
    {
        Errors = errors;
    }
}

public class DbException : Exception
{
    public string SqlState { get; }

    public DbException(string message, string sqlState = null) 
        : base(message)
    {
        SqlState = sqlState;
    }
}