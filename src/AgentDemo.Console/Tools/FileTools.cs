// <copyright file="FileTools.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace AgentDemo.Console.Tools;

using System.ComponentModel;
using System.IO;

/// <summary>
/// Provides file system tools for the AI agent.
/// </summary>
internal static class FileTools
{
    /// <summary>
    /// Lists files in the specified directory (non-recursive).
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>An array of file names.</returns>
    [Description("Lists files in a directory (non-recursive, top-level only)")]
    public static string[] ListFiles(string path)
    {
        System.Console.WriteLine($"[Tool] ListFiles: {path}");

        return !Directory.Exists(path)
            ? [$"Directory not found: {path}"]
            : [.. Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly)
                .Take(100)
                .Select(Path.GetFileName)
                .Where(f => f is not null)
                .Cast<string>()];
    }

    /// <summary>
    /// Counts files in the specified directory (non-recursive).
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>The file count, or -1 if not found.</returns>
    [Description("Counts files in a directory (non-recursive, top-level only)")]
    public static int CountFiles(string path)
    {
        System.Console.WriteLine($"[Tool] CountFiles: {path}");

        return !Directory.Exists(path)
            ? -1
            : Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly).Count();
    }

    /// <summary>
    /// Creates a folder at the specified path.
    /// </summary>
    /// <param name="path">The folder path to create.</param>
    /// <returns>Result message.</returns>
    [Description("Creates a new folder at the specified path")]
    public static string CreateFolder(string path)
    {
        System.Console.WriteLine($"[Tool] CreateFolder: {path}");

        try
        {
            Directory.CreateDirectory(path);
            return $"Created: {path}";
        }
        catch (UnauthorizedAccessException)
        {
            return $"Access denied: {path}";
        }
        catch (IOException ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Gets basic file information.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>File info string.</returns>
    [Description("Gets file information (name, size, modified date)")]
    public static string GetFileInfo(string filePath)
    {
        System.Console.WriteLine($"[Tool] GetFileInfo: {filePath}");

        if (!File.Exists(filePath))
        {
            return $"Not found: {filePath}";
        }

        var info = new FileInfo(filePath);
        return $"{info.Name} | {info.Length:N0} bytes | {info.LastWriteTime:g}";
    }
}
