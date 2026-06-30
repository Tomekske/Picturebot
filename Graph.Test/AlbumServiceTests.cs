using Database.Domain.Interfaces;
using System.IO.Abstractions.TestingHelpers;
using Database.Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Models;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Services;
using Moq;

namespace Graph.Test;

[TestFixture]
public class AlbumServiceTests {
    private MockFileSystem _mockFileSystem;
    private Mock<INodeService> _mockNodeService;
    private Mock<ISettingsService> _mockSettingsService;
    private Mock<IPictureRepository> _mockPictureRepository;
    private Mock<IPathService> _mockPathService;
    private Mock<IXmpService> _mockXmpService;
    private AlbumService _albumService;

    [SetUp]
    public void Setup() {
        _mockFileSystem = new MockFileSystem();
        _mockNodeService = new Mock<INodeService>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockPictureRepository = new Mock<IPictureRepository>();
        _mockPathService = new Mock<IPathService>();
        _mockXmpService = new Mock<IXmpService>();
        
        _mockSettingsService.Setup(s => s.Current).Returns(new SettingsModel {
            LibraryPath = @"C:\Photos"
        });

        _albumService = new AlbumService(_mockNodeService.Object, _mockFileSystem, _mockSettingsService.Object, _mockPictureRepository.Object, _mockPathService.Object, _mockXmpService.Object);

        // Configure mock NodeService to simulate the strategy's Prepare behavior
        _mockNodeService
            .Setup(s => s.CreateNodeAsync(It.IsAny<Node>()))
            .Callback<Node>(node => {
                if (node is Album album) {
                    album.Uuid = Guid.CreateVersion7().ToString();
                }
            })
            .Returns(Task.CompletedTask);
    }

    [Test]
    public async Task CreateAsync_ShouldGenerateUuidAndCreateDirectory() {
        // Arrange
        var parentId = 1;
        var albumName = "My Vacation";
        var basePath = @"C:\Photos";
        _mockFileSystem.AddDirectory(basePath);

        // Act
        var result = await _albumService.CreateAsync(parentId, albumName, basePath);

        // Assert
        Assert.Multiple(() => {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Name, Is.EqualTo(albumName));
            Assert.That(result.ParentId, Is.EqualTo(parentId));
            Assert.That(result.Type, Is.EqualTo(NodeType.Album));
            Assert.That(result.Uuid, Is.Not.Null.And.Not.Empty);
            
            // Check if UUID is a valid Guid
            Assert.That(Guid.TryParse(result.Uuid, out _), Is.True);

            // Verify physical directory creation
            var expectedPath = Path.Combine(basePath, result.Uuid!);
            Assert.That(_mockFileSystem.Directory.Exists(expectedPath), Is.True, "The album directory should be created with the UUID as name.");
            
            // Verify standard subdirectories
            Assert.Multiple(() => {
                Assert.That(_mockFileSystem.Directory.Exists(Path.Combine(expectedPath, "RAWs")), Is.True, "The RAWs directory should be created.");
                Assert.That(_mockFileSystem.Directory.Exists(Path.Combine(expectedPath, "JPGs")), Is.True, "The JPGs directory should be created.");
                Assert.That(_mockFileSystem.Directory.Exists(Path.Combine(expectedPath, "Thumbnails")), Is.True, "The Thumbnails directory should be created.");
                Assert.That(_mockFileSystem.Directory.Exists(Path.Combine(expectedPath, "Picked")), Is.True, "The Picked directory should be created.");
            });

            // Verify node service call
            _mockNodeService.Verify(s => s.CreateNodeAsync(It.Is<Album>(a => 
                a.Name == albumName && 
                a.ParentId == parentId && 
                a.Uuid == result.Uuid)), Times.Once);
        });
    }

    [Test]
    public async Task CreateAsync_WhenRoot_ShouldSetParentIdToNull() {
        // Arrange
        var albumName = "Root Album";
        var basePath = @"C:\Photos";
        _mockFileSystem.AddDirectory(basePath);

        // Act
        var result = await _albumService.CreateAsync(null, albumName, basePath);

        // Assert
        Assert.Multiple(() => {
            Assert.That(result.ParentId, Is.Null);
            _mockNodeService.Verify(s => s.CreateNodeAsync(It.Is<Album>(a => a.ParentId == null)), Times.Once);
        });
    }

    [Test]
    public async Task DeleteAsync_ShouldMoveDirectoryToDeletedFolderAndCallDeleteNode() {
        // Arrange
        var libraryPath = @"C:\Photos";
        var albumUuid = Guid.NewGuid().ToString();
        var album = new Album { Id = 10, Uuid = albumUuid, Name = "Deleted Album" };
        var albumPath = Path.Combine(libraryPath, albumUuid);
        
        _mockFileSystem.AddDirectory(albumPath);
        _mockFileSystem.AddFile(Path.Combine(albumPath, "test.txt"), new MockFileData("test"));

        // Act
        await _albumService.DeleteAsync(album);

        // Assert
        var expectedDeletedPath = Path.Combine(libraryPath, "deleted", albumUuid);
        Assert.Multiple(() => {
            Assert.That(_mockFileSystem.Directory.Exists(albumPath), Is.False, "Original album directory should be moved.");
            Assert.That(_mockFileSystem.Directory.Exists(expectedDeletedPath), Is.True, "Album directory should exist in the 'deleted' folder.");
            Assert.That(_mockFileSystem.File.Exists(Path.Combine(expectedDeletedPath, "test.txt")), Is.True, "Contents should be preserved.");
            
            _mockNodeService.Verify(s => s.DeleteNodeAsync(album), Times.Once);
        });
    }
}
