// <copyright file="FileToolsTests.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace AgentDemo.Console.Tests;

using AgentDemo.Console.Tools;

/// <summary>
/// Unit tests for <see cref="FileTools"/>.
/// </summary>
public sealed class FileToolsTests : IDisposable
{
    private readonly string testDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileToolsTests"/> class.
    /// </summary>
    public FileToolsTests()
    {
        this.testDir = Path.Combine(Path.GetTempPath(), $"FileToolsTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testDir);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(this.testDir))
        {
            Directory.Delete(this.testDir, recursive: true);
        }
    }

    /// <summary>
    /// Verifies ListFiles returns files in directory.
    /// </summary>
    [Fact]
    public void ListFilesReturnsFilesInDirectory()
    {
        // Arrange
        File.WriteAllText(Path.Combine(this.testDir, "test1.txt"), "content1");
        File.WriteAllText(Path.Combine(this.testDir, "test2.txt"), "content2");

        // Act
        var result = FileTools.ListFiles(this.testDir);

        // Assert
        Assert.Contains("test1.txt", result);
        Assert.Contains("test2.txt", result);
    }

    /// <summary>
    /// Verifies ListFiles returns error for non-existent path.
    /// </summary>
    [Fact]
    public void ListFilesNonExistentPathReturnsError()
    {
        // Act
        var result = FileTools.ListFiles(Path.Combine(this.testDir, "nonexistent"));

        // Assert
        Assert.Single(result);
        Assert.Contains("not found", result[0], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies CountFiles returns correct count.
    /// </summary>
    [Fact]
    public void CountFilesReturnsCorrectCount()
    {
        // Arrange
        File.WriteAllText(Path.Combine(this.testDir, "file1.txt"), "content");
        File.WriteAllText(Path.Combine(this.testDir, "file2.txt"), "content");
        File.WriteAllText(Path.Combine(this.testDir, "file3.txt"), "content");

        // Act
        var result = FileTools.CountFiles(this.testDir);

        // Assert
        Assert.Equal(3, result);
    }

    /// <summary>
    /// Verifies CountFiles returns -1 for non-existent path.
    /// </summary>
    [Fact]
    public void CountFilesNonExistentPathReturnsNegativeOne()
    {
        // Act
        var result = FileTools.CountFiles(Path.Combine(this.testDir, "nonexistent"));

        // Assert
        Assert.Equal(-1, result);
    }

    /// <summary>
    /// Verifies CreateFolder creates a new folder.
    /// </summary>
    [Fact]
    public void CreateFolderCreatesNewFolder()
    {
        // Arrange
        var newFolder = Path.Combine(this.testDir, "newfolder");

        // Act
        var result = FileTools.CreateFolder(newFolder);

        // Assert
        Assert.Contains("Created", result, StringComparison.OrdinalIgnoreCase);
        Assert.True(Directory.Exists(newFolder));
    }

    /// <summary>
    /// Verifies GetFileInfo returns file information.
    /// </summary>
    [Fact]
    public void GetFileInfoReturnsFileInformation()
    {
        // Arrange
        var filePath = Path.Combine(this.testDir, "info.txt");
        File.WriteAllText(filePath, "test content");

        // Act
        var result = FileTools.GetFileInfo(filePath);

        // Assert
        Assert.Contains("info.txt", result, StringComparison.Ordinal);
        Assert.Contains("bytes", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies GetFileInfo returns error for non-existent file.
    /// </summary>
    [Fact]
    public void GetFileInfoNonExistentFileReturnsError()
    {
        // Act
        var result = FileTools.GetFileInfo(Path.Combine(this.testDir, "nonexistent.txt"));

        // Assert
        Assert.Contains("not found", result, StringComparison.OrdinalIgnoreCase);
    }
}
