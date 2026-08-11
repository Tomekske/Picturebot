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

    [Test]
    public async Task SyncHighlightsAsync_ShouldCopyBlueLabeledPreviewsAndCleanupOthers() {
        // Arrange
        var albumUuid = Guid.NewGuid().ToString();
        var album = new Album { Id = 10, Uuid = albumUuid, Name = "Test Album" };
        var libraryPath = @"C:\Photos";
        var highlightsPath = Path.Combine(libraryPath, albumUuid, "Highlights");
        var jpgsPath = Path.Combine(libraryPath, albumUuid, "JPGs");

        _mockFileSystem.AddDirectory(jpgsPath);
        _mockFileSystem.AddDirectory(highlightsPath);

        // Pre-existing file in Highlights that should be removed because it's not a blue highlight picture
        _mockFileSystem.AddFile(Path.Combine(highlightsPath, "stray.jpg"), new MockFileData("stray"));
        // Pre-existing file in Highlights that belongs to a blue picture (should be overwritten/kept)
        _mockFileSystem.AddFile(Path.Combine(highlightsPath, "blue1.jpg"), new MockFileData("old_blue"));
        // Pre-existing file in Highlights that belongs to a non-blue picture (should be deleted)
        _mockFileSystem.AddFile(Path.Combine(highlightsPath, "red1.jpg"), new MockFileData("old_red"));

        // Source JPG files
        _mockFileSystem.AddFile(Path.Combine(jpgsPath, "blue1.jpg"), new MockFileData("blue1_data"));
        _mockFileSystem.AddFile(Path.Combine(jpgsPath, "blue2.jpg"), new MockFileData("blue2_data"));
        _mockFileSystem.AddFile(Path.Combine(jpgsPath, "red1.jpg"), new MockFileData("red1_data"));

        var pics = new List<Picture> {
            new() { Id = 1, Name = "blue1", ColorLabel = ColorLabel.Blue, Parent = album },
            new() { Id = 2, Name = "blue2", ColorLabel = ColorLabel.Blue, Parent = album },
            new() { Id = 3, Name = "red1", ColorLabel = ColorLabel.Red, Parent = album }
        };

        _mockPictureRepository.Setup(r => r.FindByHierarchyIdAsync(album.Id)).ReturnsAsync(pics);
        _mockPathService.Setup(p => p.GetAlbumHighlightsPath(album)).Returns(highlightsPath);
        _mockPathService.Setup(p => p.PopulatePaths(It.IsAny<IEnumerable<Picture>>()))
            .Callback<IEnumerable<Picture>>(pictures => {
                foreach (var p in pictures) {
                    p.SubFolder = new SubFolder {
                        Preview = Path.Combine(jpgsPath, p.Name + ".jpg")
                    };
                }
            });
        _mockXmpService.Setup(x => x.LoadMetadataAsync(It.IsAny<Picture>())).Returns(Task.CompletedTask);

        // Act
        await _albumService.SyncHighlightsAsync(album);

        // Assert
        Assert.Multiple(() => {
            // Verify blue1.jpg exists and was overwritten with new content
            var blue1Path = Path.Combine(highlightsPath, "blue1.jpg");
            Assert.That(_mockFileSystem.File.Exists(blue1Path), Is.True);
            Assert.That(_mockFileSystem.File.ReadAllText(blue1Path), Is.EqualTo("blue1_data"));

            // Verify blue2.jpg was copied
            var blue2Path = Path.Combine(highlightsPath, "blue2.jpg");
            Assert.That(_mockFileSystem.File.Exists(blue2Path), Is.True);
            Assert.That(_mockFileSystem.File.ReadAllText(blue2Path), Is.EqualTo("blue2_data"));

            // Verify red1.jpg was deleted/not copied
            var red1Path = Path.Combine(highlightsPath, "red1.jpg");
            Assert.That(_mockFileSystem.File.Exists(red1Path), Is.False);

            // Verify stray.jpg was deleted
            var strayPath = Path.Combine(highlightsPath, "stray.jpg");
            Assert.That(_mockFileSystem.File.Exists(strayPath), Is.False);
        });
    }
}
