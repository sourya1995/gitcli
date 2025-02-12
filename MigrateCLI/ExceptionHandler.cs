using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public static class GlobalExceptionHandlerExtensions
{
    public static void UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(appError =>
        {
            appError.Run(async context =>
            {
                var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
                var exception = exceptionHandlerFeature?.Error;
                var logger = context.RequestServices.GetService<ILogger<Startup>>();

                if (exception == null) return;

                logger?.LogError(exception, "Unhandled exception occurred");

                context.Response.ContentType = "application/json";
                
                // Default values
                var statusCode = (int)HttpStatusCode.InternalServerError;
                var exceptionType = exception.GetType().Name;
                object responseData;

                // Custom exception handling
                switch (exception)
                {
                    case HttpRequestException httpEx when httpEx.StatusCode.HasValue:
                        statusCode = (int)httpEx.StatusCode.Value;
                        responseData = CreateResponse(exceptionType, statusCode, exception.Message);
                        break;
                        
                    case ApiException apiEx:
                        statusCode = (int)apiEx.StatusCode;
                        responseData = CreateResponse(exceptionType, statusCode, exception.Message);
                        break;
                        
                    case KeyNotFoundException:
                        statusCode = (int)HttpStatusCode.NotFound;
                        responseData = CreateResponse(exceptionType, statusCode, "Resource not found");
                        break;
                        
                    case UnauthorizedAccessException:
                        statusCode = (int)HttpStatusCode.Unauthorized;
                        responseData = CreateResponse(exceptionType, statusCode, "Access denied");
                        break;
                        
                    case ValidationException validationEx:
                        statusCode = (int)HttpStatusCode.BadRequest;
                        responseData = new
                        {
                            ExceptionType = exceptionType,
                            Code = statusCode,
                            Error = "Validation failed",
                            Errors = validationEx.Errors
                        };
                        break;
                        
                    case ArgumentException:
                    case ArgumentNullException:
                        statusCode = (int)HttpStatusCode.BadRequest;
                        responseData = CreateResponse(exceptionType, statusCode, "Invalid arguments");
                        break;
                        
                    case NotSupportedException:
                        statusCode = (int)HttpStatusCode.NotImplemented;
                        responseData = CreateResponse(exceptionType, statusCode, "Feature not implemented");
                        break;
                        
                    case InvalidOperationException:
                        statusCode = (int)HttpStatusCode.Conflict;
                        responseData = CreateResponse(exceptionType, statusCode, "Conflict in operation");
                        break;
                        
                    case TimeoutException:
                        statusCode = (int)HttpStatusCode.RequestTimeout;
                        responseData = CreateResponse(exceptionType, statusCode, "Request timed out");
                        break;
                        
                    default:
                        responseData = CreateResponse(exceptionType, statusCode, exception.Message);
                        break;
                }

                context.Response.StatusCode = statusCode;
                await context.Response.WriteAsync(JsonSerializer.Serialize(responseData));
            });
        });
    }

    private static object CreateResponse(string exceptionType, int code, string error) => new
    {
        ExceptionType = exceptionType,
        Code = code,
        Error = error
    };
}

// Custom exception classes
public class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public ApiException(HttpStatusCode statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class ValidationException : Exception
{
    public Dictionary<string, string[]> Errors { get; }

    public ValidationException(Dictionary<string, string[]> errors) : base("Validation failed")
    {
        Errors = errors;
    }
}