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
    public async Task ExecuteAsync_WhenFolderMissing_ShouldActuallyCreateFolder()
    {
        // 1. Arrange: Start with a completely empty in-memory drive
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
        Assert.That(result, Is.EqualTo(State.Ready));
        
        // This checks if the folder actually exists in the "fake" disk
        bool folderExists = _mockFileSystem.Directory.Exists(expectedPath);
        Assert.That(folderExists, Is.True, "The directory has been created in the file system.");
    }
    
    [Test] 
    public async Task ExecuteAsync_WhenFolderAlreadyExists_ShouldReturnVerified()
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
        Assert.That(result, Is.EqualTo(State.Verified), "The orchestrator should return Verified when the path already exists.");
    }
    
    [Test]
    public async Task ExecuteAsync_WhenNewEnvironmentCreated_ShouldNotTouchExistingEnvironment()
    {
        // 1. Arrange: Create a "Development" environment with a dummy file
        var baseLocation = @"C:\Local";
        var appName = "Picturebot";
        var existingEnvPath = Path.Combine(baseLocation, appName, "Development", "Logs");
        var existingFilePath = Path.Combine(existingEnvPath, "keep_me.txt");

        _mockFileSystem.AddDirectory(existingEnvPath);
        _mockFileSystem.AddFile(existingFilePath, new MockFileData("original content"));

        // Set up an orchestrator for a DIFFERENT environment (Release)
        var orchestrator = new FileSystemOrchestrator(_mockFileSystem)
        {
            Location = baseLocation,
            Name = appName,
            Environment = AppEnvironment.Production
        };
    
        var expectedReleasePath = Path.Combine(baseLocation, appName, "Production", "Logs");

        // 2. Act
        var result = await orchestrator.ExecuteAsync(null);

        // 3. Assert
        // Check that the new environment was created
        Assert.That(result, Is.EqualTo(State.Ready));
        Assert.That(_mockFileSystem.Directory.Exists(expectedReleasePath), Is.True);

        // Check that the existing environment and its files are still there and unchanged
        Assert.That(_mockFileSystem.File.Exists(existingFilePath), Is.True, "Existing file should still exist.");
        var content = _mockFileSystem.File.ReadAllText(existingFilePath);
        Assert.That(content, Is.EqualTo("original content"), "Existing file content should remain untouched.");
    }
}