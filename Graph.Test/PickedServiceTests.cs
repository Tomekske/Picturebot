using System.IO.Abstractions.TestingHelpers;
using Database.Domain.Entities;
using Domain.Enums;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Services;
using Moq;

namespace Graph.Test;

[TestFixture]
public class PickedServiceTests {
    private MockFileSystem _mockFileSystem;
    private Mock<IPathService> _mockPathService;
    private PickedService _pickedService;

    [SetUp]
    public void Setup() {
        _mockFileSystem = new MockFileSystem();
        _mockPathService = new Mock<IPathService>();
        _pickedService = new PickedService(_mockFileSystem, _mockPathService.Object);
    }

    [Test]
    public async Task SyncToPickedAsync_WhenFlagged_ShouldCopyPreviewToPicked() {
        // Arrange
        var previewPath = @"C:\Library\Album1\JPGs\Pic1.jpg";
        var pickedPath = @"C:\Library\Album1\Picked\Pic1.jpg";
        var previewContent = "fake image content";
        
        _mockFileSystem.AddDirectory(@"C:\Library\Album1\JPGs");
        _mockFileSystem.AddDirectory(@"C:\Library\Album1\Picked");
        _mockFileSystem.AddFile(previewPath, new MockFileData(previewContent));

        var picture = new Picture {
            Name = "Pic1",
            CurationStatus = CurationStatus.Flagged,
            SubFolder = new SubFolder {
                Preview = previewPath,
                Picked = pickedPath
            }
        };

        // Act
        await _pickedService.SyncToPickedAsync(picture);

        // Assert
        Assert.That(_mockFileSystem.File.Exists(pickedPath), Is.True);
        Assert.That(_mockFileSystem.GetFile(pickedPath).TextContents, Is.EqualTo(previewContent));
    }

    [Test]
    public async Task SyncToPickedAsync_WhenUnflagged_ShouldDeletePickedFile() {
        // Arrange
        var pickedPath = @"C:\Library\Album1\Picked\Pic1.jpg";
        _mockFileSystem.AddDirectory(@"C:\Library\Album1\Picked");
        _mockFileSystem.AddFile(pickedPath, new MockFileData("existing content"));

        var picture = new Picture {
            Name = "Pic1",
            CurationStatus = CurationStatus.Unflagged,
            SubFolder = new SubFolder {
                Picked = pickedPath
            }
        };

        // Act
        await _pickedService.SyncToPickedAsync(picture);

        // Assert
        Assert.That(_mockFileSystem.File.Exists(pickedPath), Is.False);
    }

    [Test]
    public async Task SyncToPickedAsync_WhenSubFolderNull_ShouldCallPathService() {
        // Arrange
        var picture = new Picture { Name = "Pic1", CurationStatus = CurationStatus.Flagged };
        
        _mockPathService.Setup(s => s.PopulatePaths(picture))
            .Callback<Picture>(p => p.SubFolder = new SubFolder {
                Preview = @"C:\Preview.jpg",
                Picked = @"C:\Picked.jpg"
            });

        // Act
        await _pickedService.SyncToPickedAsync(picture);

        // Assert
        _mockPathService.Verify(s => s.PopulatePaths(picture), Times.Once);
    }
}
