using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public static class CliErrorHandler
{
    // Standard exit codes following Unix/Linux conventions
    public enum ExitCode
    {
        Success = 0,
        GeneralError = 1,
        InvalidArgument = 2,
        FileNotFound = 3,
        FilePermissionError = 4,
        ConfigError = 5,
        ValidationError = 6,
        RuntimeError = 7,
        NetworkError = 8,
        DatabaseError = 9,
        UnknownError = 127
    }

    // ANSI color codes for console output
    private static class ConsoleColor
    {
        public const string Red = "\u001b[31m";
        public const string Yellow = "\u001b[33m";
        public const string Blue = "\u001b[34m";
        public const string Reset = "\u001b[0m";
        public const string Bold = "\u001b[1m";
    }

    public static void HandleException(Exception ex, bool showStackTrace = false)
    {
        var (exitCode, errorMessage) = GetErrorDetails(ex);
        PrintError(errorMessage, ex, showStackTrace);
        Environment.Exit((int)exitCode);
    }

    private static (ExitCode code, string message) GetErrorDetails(Exception ex) => ex switch
    {
        // File operations
        FileNotFoundException => (ExitCode.FileNotFound, 
            $"File not found: {ex.Message}"),
        UnauthorizedAccessException => (ExitCode.FilePermissionError, 
            $"Access denied: {ex.Message}"),
        
        // Command line arguments
        ArgumentException => (ExitCode.InvalidArgument, 
            $"Invalid argument: {ex.Message}"),
        ArgumentNullException => (ExitCode.InvalidArgument, 
            $"Missing required argument: {ex.Message}"),
        
        // Configuration
        CliConfigException configEx => (ExitCode.ConfigError, 
            $"Configuration error: {configEx.Message}\nFile: {configEx.ConfigFile}"),
        
        // Validation
        CliValidationException validEx => (ExitCode.ValidationError, 
            FormatValidationErrors(validEx.Errors)),
        
        // Runtime errors
        CliRuntimeException runEx => (ExitCode.RuntimeError, 
            $"Runtime error: {runEx.Message} (Code: {runEx.ErrorCode})"),
        
        // Database
        CliDatabaseException dbEx => (ExitCode.DatabaseError, 
            $"Database error: {dbEx.Message}\nDetails: {dbEx.Details}"),
        
        // Default case
        _ => (ExitCode.UnknownError, $"An unexpected error occurred: {ex.Message}")
    };

    private static void PrintError(string message, Exception ex, bool showStackTrace)
    {
        Console.Error.WriteLine($"\n{ConsoleColor.Red}{ConsoleColor.Bold}ERROR{ConsoleColor.Reset}");
        Console.Error.WriteLine($"{ConsoleColor.Red}{message}{ConsoleColor.Reset}");

        if (showStackTrace && ex.StackTrace != null)
        {
            Console.Error.WriteLine($"\n{ConsoleColor.Yellow}Stack trace:{ConsoleColor.Reset}");
            Console.Error.WriteLine(ex.StackTrace);
        }

        // Print help hint if it's an argument error
        if (ex is ArgumentException)
        {
            Console.Error.WriteLine($"\n{ConsoleColor.Blue}Tip: Use --help to see usage instructions{ConsoleColor.Reset}");
        }
    }

    private static string FormatValidationErrors(Dictionary<string, string[]> errors)
    {
        var lines = new List<string> { "Validation failed:" };
        foreach (var (field, messages) in errors)
        {
            lines.Add($"  {field}:");
            foreach (var message in messages)
            {
                lines.Add($"    - {message}");
            }
        }
        return string.Join('\n', lines);
    }

    // Helper method for logging (optional)
    public static void LogError(Exception ex, string logFile, 
        [CallerMemberName] string caller = "", 
        [CallerFilePath] string file = "")
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logEntry = $"[{timestamp}] {caller} ({file})\n{ex}\n\n";
            System.IO.File.AppendAllText(logFile, logEntry);
        }
        catch
        {
            Console.Error.WriteLine("Failed to write to log file");
        }
    }
}

// Custom CLI-specific exceptions
public class CliConfigException : Exception
{
    public string ConfigFile { get; }

    public CliConfigException(string message, string configFile) 
        : base(message)
    {
        ConfigFile = configFile;
    }
}

public class CliValidationException : Exception
{
    public Dictionary<string, string[]> Errors { get; }

    public CliValidationException(Dictionary<string, string[]> errors) 
        : base("Validation failed")
    {
        Errors = errors;
    }
}

public class CliRuntimeException : Exception
{
    public string ErrorCode { get; }

    public CliRuntimeException(string message, string errorCode) 
        : base(message)
    {
        ErrorCode = errorCode;
    }
}

public class CliDatabaseException : Exception
{
    public string Details { get; }

    public CliDatabaseException(string message, string details) 
        : base(message)
    {
        Details = details;
    }
}

// Example usage
public class Program
{
    public static void Main(string[] args)
    {
        try
        {
            // Your CLI app code here
            RunApp(args);
        }
        catch (Exception ex)
        {
            // Handle any unhandled exceptions
            CliErrorHandler.HandleException(ex, showStackTrace: IsDebugMode());
        }
    }

    private static void RunApp(string[] args)
    {
        // Example error handling scenarios
        if (args.Length == 0)
        {
            throw new ArgumentException("No arguments provided");
        }

        if (!System.IO.File.Exists("config.json"))
        {
            throw new CliConfigException("Missing configuration", "config.json");
        }

        // Example validation error
        if (args[0].Length < 3)
        {
            var errors = new Dictionary<string, string[]>
            {
                ["command"] = new[] { "Command must be at least 3 characters long" }
            };
            throw new CliValidationException(errors);
        }
    }

    private static bool IsDebugMode() => 
        Environment.GetEnvironmentVariable("DEBUG") == "1";
}