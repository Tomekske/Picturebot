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
        var expectedPath = @"C:\Local\Picturebot\Development\Logs";
        
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
            bool folderExists = _mockFileSystem.Directory.Exists(expectedPath);
            Assert.That(folderExists, Is.True, "The directory should be created in the file system.");
        });
    }
    
    [Test] 
    public async Task ExecuteAsync_WhenFolderAlreadyExists_ShouldStillReturnSuccess()
    {
        // 1. Arrange
        var testPath = @"C:\Local\Picturebot\Development\Logs";
        _mockFileSystem.AddDirectory(testPath); 
    
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
            bool folderExists = _mockFileSystem.Directory.Exists(testPath);
            Assert.That(folderExists, Is.True, "The directory should be created in the file system.");
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenEnvironmentIsNew_ShouldNotAffectExistingEnvironments()
    {
        // 1. Arrange
        var baseLocation = @"C:\Local";
        var appName = "Picturebot";
        var existingEnvPath = Path.Combine(baseLocation, appName, "Development", "Logs");
        var existingFilePath = Path.Combine(existingEnvPath, "keep_me.txt");

        _mockFileSystem.AddDirectory(existingEnvPath);
        _mockFileSystem.AddFile(existingFilePath, new MockFileData("original content"));

        var orchestrator = new FileSystemOrchestrator(_mockFileSystem)
        {
            Location = baseLocation,
            Name = appName,
            Environment = AppEnvironment.Production
        };
    
        var expectedProductionPath = Path.Combine(baseLocation, appName, "Production", "Logs");

        // 2. Act
        var result = await orchestrator.ExecuteAsync(null);

        // 3. Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.False);
            Assert.That(_mockFileSystem.Directory.Exists(expectedProductionPath), Is.True);

            Assert.That(_mockFileSystem.File.Exists(existingFilePath), Is.True);
        });
    }
}