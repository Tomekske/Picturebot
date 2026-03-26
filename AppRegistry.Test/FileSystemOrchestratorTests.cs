using System.IO.Abstractions.TestingHelpers;
using AppRegistry.Infrastructure;
using Domain.Enums;

namespace AppRegistry.Test;

[TestFixture]
public class FileSystemOrchestratorTests
{
    private MockFileSystem _mockFileSystem;

    [SetUp]
    public void Setup()
    {
        _mockFileSystem = new MockFileSystem();
    }

    [Test] 
    public async Task ExecuteAsync_WhenFolderMissing_ShouldCreateFolderAndReturnSuccess()
    {
        // 1. Arrange: Utilizing the new Primary Constructor
        var expectedPath = @"C:\Local\Picturebot\Development";
        var logsPath = Path.Combine(expectedPath, "Logs");
        
        var orchestrator = new FileSystemOrchestrator(_mockFileSystem)
        {
            Location = @"C:\Local",
            Name = "Picturebot",
            Environment = AppEnvironment.Development
        };

        // 2. Act
        var result = await orchestrator.ExecuteAsync(null);

        // 3. Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.False, "The result should not contain errors.");
            Assert.That(result.Value, Is.EqualTo(expectedPath), "Service should return a Success signal.");
            
            // Verify the physical side effect
            Assert.That(_mockFileSystem.Directory.Exists(expectedPath), Is.True, "The main directory should be created.");
            Assert.That(_mockFileSystem.Directory.Exists(logsPath), Is.True, "The Logs directory should be created.");
        });
    }
    
    [Test] 
    public async Task ExecuteAsync_WhenFolderAlreadyExists_ShouldStillReturnSuccess()
    {
        // 1. Arrange
        var testPath = @"C:\Local\Picturebot\Development";
        var logsPath = Path.Combine(testPath, "Logs");
        _mockFileSystem.AddDirectory(testPath); 
        _mockFileSystem.AddDirectory(logsPath);
    
        var orchestrator = new FileSystemOrchestrator(_mockFileSystem)
        {
            Location = @"C:\Local",
            Name = "Picturebot",
            Environment = AppEnvironment.Development
        };

        // 2. Act
        var result = await orchestrator.ExecuteAsync(null);

        // 3. Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.False, "The result should not contain errors.");
            Assert.That(result.Value, Is.EqualTo(testPath), "Service should return a Success signal.");
            
            // Verify the physical side effect
            Assert.That(_mockFileSystem.Directory.Exists(testPath), Is.True, "The main directory should exist.");
            Assert.That(_mockFileSystem.Directory.Exists(logsPath), Is.True, "The Logs directory should exist.");
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenEnvironmentIsNew_ShouldNotAffectExistingEnvironments()
    {
        // 1. Arrange
        var baseLocation = @"C:\Local";
        var appName = "Picturebot";
        var existingEnvPath = Path.Combine(baseLocation, appName, "Development");
        var existingLogsPath = Path.Combine(existingEnvPath, "Logs");
        var existingFilePath = Path.Combine(existingLogsPath, "keep_me.txt");

        _mockFileSystem.AddDirectory(existingEnvPath);
        _mockFileSystem.AddDirectory(existingLogsPath);
        _mockFileSystem.AddFile(existingFilePath, new MockFileData("original content"));

        var orchestrator = new FileSystemOrchestrator(_mockFileSystem)
        {
            Location = baseLocation,
            Name = appName,
            Environment = AppEnvironment.Production
        };
    
        var expectedProductionPath = Path.Combine(baseLocation, appName, "Production");

        // 2. Act
        var result = await orchestrator.ExecuteAsync(null);

        // 3. Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.False);
            Assert.That(_mockFileSystem.Directory.Exists(expectedProductionPath), Is.True);
            Assert.That(_mockFileSystem.Directory.Exists(Path.Combine(expectedProductionPath, "Logs")), Is.True);

            Assert.That(_mockFileSystem.File.Exists(existingFilePath), Is.True);
        });
    }
}