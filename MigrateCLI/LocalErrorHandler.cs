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
using System.Text.RegularExpressions;

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

                // Enhanced logging with stack trace and inner exception details
                logger?.LogError(exception, 
                    "Unhandled exception occurred: {ExceptionMessage}. Stack Trace: {StackTrace}", 
                    exception.Message, 
                    exception.StackTrace);

                context.Response.ContentType = "application/json";
                context.Response.Headers.Add("X-Error-Handler-Version", "3.0");

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

        switch (exception)
        {
            // Runtime and Execution Errors
            case RuntimeError runtimeEx:
                statusCode = (int)HttpStatusCode.InternalServerError;
                responseData = CreateErrorResponse(exceptionType, statusCode, 
                    "Runtime error occurred", 
                    runtimeEx.ErrorDetails,
                    runtimeEx.ErrorCode);
                break;

            case ExecutionEngineException:
                statusCode = (int)HttpStatusCode.InternalServerError;
                responseData = CreateErrorResponse(exceptionType, statusCode,
                    "Critical execution error",
                    "The program encountered a critical execution error",
                    "EE001");
                break;

            case StackOverflowException:
                statusCode = (int)HttpStatusCode.InternalServerError;
                responseData = CreateErrorResponse(exceptionType, statusCode,
                    "Stack overflow error",
                    "Program exceeded stack limit",
                    "SO001");
                break;

            // String and Text Processing Errors
            case StringMatchException stringEx:
                statusCode = (int)HttpStatusCode.BadRequest;
                responseData = CreateErrorResponse(exceptionType, statusCode,
                    "String matching error",
                    stringEx.Message,
                    stringEx.ErrorCode);
                break;

            case RegexMatchTimeoutException:
                statusCode = (int)HttpStatusCode.RequestTimeout;
                responseData = CreateErrorResponse(exceptionType, statusCode,
                    "Regex matching timeout",
                    "Regular expression matching operation timed out",
                    "RE001");
                break;

            case EncodingException encodingEx:
                statusCode = (int)HttpStatusCode.BadRequest;
                responseData = CreateErrorResponse(exceptionType, statusCode,
                    "Text encoding error",
                    encodingEx.Message,
                    encodingEx.ErrorCode);
                break;

            // Resource Management Errors
            case ResourceNotFoundException notFoundEx:
                statusCode = (int)HttpStatusCode.NotFound;
                responseData = CreateErrorResponse(exceptionType, statusCode,
                    "Resource not found",
                    notFoundEx.ResourceDetails,
                    notFoundEx.ErrorCode);
                break;

            case ResourceBusyException busyEx:
                statusCode = (int)HttpStatusCode.Conflict;
                responseData = CreateErrorResponse(exceptionType, statusCode,
                    "Resource busy",
                    busyEx.Message,
                    busyEx.ErrorCode);
                break;

            // Program State Errors
            case StateException stateEx:
                statusCode = (int)HttpStatusCode.Conflict;
                responseData = CreateErrorResponse(exceptionType, statusCode,
                    "Invalid program state",
                    stateEx.Message,
                    stateEx.ErrorCode);
                break;

            case InitializationException initEx:
                statusCode = (int)HttpStatusCode.ServiceUnavailable;
                responseData = CreateErrorResponse(exceptionType, statusCode,
                    "Initialization failed",
                    initEx.Message,
                    initEx.ErrorCode);
                break;

            // Memory and Resource Management
            case MemoryException memEx:
                statusCode = (int)HttpStatusCode.ServiceUnavailable;
                responseData = CreateErrorResponse(exceptionType, statusCode,
                    "Memory error",
                    memEx.Message,
                    memEx.ErrorCode);
                break;

            case ResourceLeakException leakEx:
                statusCode = (int)HttpStatusCode.InternalServerError;
                responseData = CreateErrorResponse(exceptionType, statusCode,
                    "Resource leak detected",
                    leakEx.Message,
                    leakEx.ErrorCode);
                break;

            // Configuration and Environment Errors
            case ConfigurationException configEx:
                statusCode = (int)HttpStatusCode.InternalServerError;
                responseData = CreateErrorResponse(exceptionType, statusCode,
                    "Configuration error",
                    configEx.Message,
                    configEx.ErrorCode);
                break;

            case EnvironmentException envEx:
                statusCode = (int)HttpStatusCode.InternalServerError;
                responseData = CreateErrorResponse(exceptionType, statusCode,
                    "Environment error",
                    envEx.Message,
                    envEx.ErrorCode);
                break;

            // Include original exception types...
            [Previous cases from the original handler...]

            default:
                responseData = CreateErrorResponse(
                    exceptionType,
                    statusCode,
                    "An unexpected error occurred",
                    includeDevelopmentDetails ? exception.Message : "Internal server error",
                    "UNK001"
                );
                break;
        }

        return (statusCode, responseData);
    }

    private static object CreateErrorResponse(string exceptionType, int code, string message, string details, string errorCode) => new
    {
        ExceptionType = exceptionType,
        Code = code,
        Message = message,
        Details = details,
        ErrorCode = errorCode,
        Timestamp = DateTime.UtcNow,
        TraceId = Guid.NewGuid().ToString()
    };
}

// New Custom Exception Classes for Runtime Errors
public class RuntimeError : Exception
{
    public string ErrorCode { get; }
    public string ErrorDetails { get; }

    public RuntimeError(string message, string errorCode, string details = null) 
        : base(message)
    {
        ErrorCode = errorCode;
        ErrorDetails = details ?? message;
    }
}

public class StringMatchException : Exception
{
    public string ErrorCode { get; }
    public string ExpectedValue { get; }
    public string ActualValue { get; }

    public StringMatchException(string message, string expected, string actual, string errorCode = "STR001") 
        : base(message)
    {
        ErrorCode = errorCode;
        ExpectedValue = expected;
        ActualValue = actual;
    }
}

public class ResourceNotFoundException : Exception
{
    public string ErrorCode { get; }
    public string ResourceDetails { get; }
    public string ResourceType { get; }

    public ResourceNotFoundException(string message, string resourceType, string details, string errorCode = "RNF001") 
        : base(message)
    {
        ErrorCode = errorCode;
        ResourceType = resourceType;
        ResourceDetails = details;
    }
}

public class StateException : Exception
{
    public string ErrorCode { get; }
    public string ExpectedState { get; }
    public string ActualState { get; }

    public StateException(string message, string expected, string actual, string errorCode = "STE001") 
        : base(message)
    {
        ErrorCode = errorCode;
        ExpectedState = expected;
        ActualState = actual;
    }
}

public class EncodingException : Exception
{
    public string ErrorCode { get; }
    public string EncodingType { get; }

    public EncodingException(string message, string encodingType, string errorCode = "ENC001") 
        : base(message)
    {
        ErrorCode = errorCode;
        EncodingType = encodingType;
    }
}

public class ResourceBusyException : Exception
{
    public string ErrorCode { get; }
    public string ResourceId { get; }

    public ResourceBusyException(string message, string resourceId, string errorCode = "RB001") 
        : base(message)
    {
        ErrorCode = errorCode;
        ResourceId = resourceId;
    }
}

public class InitializationException : Exception
{
    public string ErrorCode { get; }
    public string Component { get; }

    public InitializationException(string message, string component, string errorCode = "INIT001") 
        : base(message)
    {
        ErrorCode = errorCode;
        Component = component;
    }
}

public class MemoryException : Exception
{
    public string ErrorCode { get; }
    public long RequestedBytes { get; }

    public MemoryException(string message, long requestedBytes, string errorCode = "MEM001") 
        : base(message)
    {
        ErrorCode = errorCode;
        RequestedBytes = requestedBytes;
    }
}

public class ResourceLeakException : Exception
{
    public string ErrorCode { get; }
    public string ResourceType { get; }

    public ResourceLeakException(string message, string resourceType, string errorCode = "RL001") 
        : base(message)
    {
        ErrorCode = errorCode;
        ResourceType = resourceType;
    }
}

public class ConfigurationException : Exception
{
    public string ErrorCode { get; }
    public string ConfigSection { get; }

    public ConfigurationException(string message, string configSection, string errorCode = "CFG001") 
        : base(message)
    {
        ErrorCode = errorCode;
        ConfigSection = configSection;
    }
}

public class EnvironmentException : Exception
{
    public string ErrorCode { get; }
    public string EnvironmentVariable { get; }

    public EnvironmentException(string message, string envVariable, string errorCode = "ENV001") 
        : base(message)
    {
        ErrorCode = errorCode;
        EnvironmentVariable = envVariable;
    }
}