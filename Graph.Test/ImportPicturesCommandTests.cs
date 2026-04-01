using System.IO.Abstractions.TestingHelpers;
using Database.Domain.Entities;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Commands;
using Moq;
using PictureWorker.Domain.Interfaces;
using ErrorOr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Graph.Test;

[TestFixture]
public class ImportPicturesCommandTests {
    private MockFileSystem _mockFileSystem;
    private Mock<IAlbumService> _mockAlbumService;
    private Mock<INodeService> _mockNodeService;
    private Mock<IPictureAnalyzer> _mockPictureAnalyzer;
    private Mock<IPictureProcessor> _mockPictureProcessor;
    private ImportPicturesCommand _command;

    [SetUp]
    public void Setup() {
        _mockFileSystem = new MockFileSystem();
        _mockAlbumService = new Mock<IAlbumService>();
        _mockNodeService = new Mock<INodeService>();
        _mockPictureAnalyzer = new Mock<IPictureAnalyzer>();
        _mockPictureProcessor = new Mock<IPictureProcessor>();

        _command = new ImportPicturesCommand(
            _mockAlbumService.Object,
            _mockNodeService.Object,
            _mockFileSystem,
            _mockPictureAnalyzer.Object,
            _mockPictureProcessor.Object);
    }

    [Test]
    public async Task ExecuteAsync_ShouldImportPicturesAndHandleCollisions() {
        // Arrange
        var parentId = 1;
        var albumName = "New Album";
        var libraryPath = @"C:\Library";
        var sourcePath = @"C:\Source";
        var albumUuid = "album-uuid";
        var albumPath = Path.Combine(libraryPath, albumUuid);

        var album = new Album { Id = 10, Uuid = albumUuid, Name = albumName };
        _mockAlbumService.Setup(s => s.CreateAsync(parentId, albumName, libraryPath))
            .ReturnsAsync(album);

        _mockFileSystem.AddDirectory(sourcePath);
        _mockFileSystem.AddDirectory(albumPath);
        _mockFileSystem.AddDirectory(Path.Combine(albumPath, "RAWs"));
        _mockFileSystem.AddDirectory(Path.Combine(albumPath, "JPGs"));
        _mockFileSystem.AddDirectory(Path.Combine(albumPath, "Thumbnails"));

        var capturedDate = new DateTime(2026, 4, 1, 12, 0, 0);
        var photo1Path = Path.Combine(sourcePath, "photo1.jpg");
        _mockFileSystem.AddFile(photo1Path, new MockFileData("fake jpg content"));

        _mockPictureAnalyzer.Setup(a => a.ExtractTimestamp(photo1Path))
            .ReturnsAsync(capturedDate);
        _mockPictureAnalyzer.Setup(a => a.CalculateHashAsync(photo1Path))
            .ReturnsAsync(12345UL);
        _mockPictureAnalyzer.Setup(a => a.CalculateSharpnessAsync(photo1Path))
            .ReturnsAsync(80);

        var mockImage = new Image<Rgba32>(1, 1);
        _mockPictureProcessor.Setup(p => p.GenerateThumbnailAsync(photo1Path))
            .ReturnsAsync(mockImage);

        // Act
        await _command.ExecuteAsync(parentId, albumName, libraryPath, sourcePath);

        // Assert
        var expectedFileName = capturedDate.ToString("yyyy-MM-dd_HH-mm-ss");
        var expectedJpgPath = Path.Combine(albumPath, "JPGs", expectedFileName + ".jpg");
        var expectedThumbnailPath = Path.Combine(albumPath, "Thumbnails", expectedFileName + ".jpg");

        Assert.Multiple(() => {
            Assert.That(_mockFileSystem.File.Exists(expectedJpgPath), Is.True, "JPG should be copied.");
            Assert.That(_mockFileSystem.File.Exists(expectedThumbnailPath), Is.True, "Thumbnail should be saved.");
            
            _mockNodeService.Verify(s => s.CreateNodeAsync(It.Is<Picture>(p => 
                p.Name == expectedFileName && 
                p.ParentId == album.Id && 
                p.CapturedAt == capturedDate &&
                p.Hash == 12345UL &&
                p.Sharpness == 80)), Times.Once);
        });
    }

    [Test]
    public async Task ExecuteAsync_ShouldHandleNamingCollisions() {
        // Arrange
        var parentId = 1;
        var albumName = "Collision Album";
        var libraryPath = @"C:\Library";
        var sourcePath = @"C:\Source";
        var albumUuid = "album-collision";
        var albumPath = Path.Combine(libraryPath, albumUuid);

        var album = new Album { Id = 11, Uuid = albumUuid, Name = albumName };
        _mockAlbumService.Setup(s => s.CreateAsync(parentId, albumName, libraryPath))
            .ReturnsAsync(album);

        _mockFileSystem.AddDirectory(sourcePath);
        _mockFileSystem.AddDirectory(albumPath);
        var jpgsPath = Path.Combine(albumPath, "JPGs");
        _mockFileSystem.AddDirectory(jpgsPath);
        _mockFileSystem.AddDirectory(Path.Combine(albumPath, "RAWs"));
        _mockFileSystem.AddDirectory(Path.Combine(albumPath, "Thumbnails"));

        var capturedDate = new DateTime(2026, 4, 1, 12, 0, 0);
        var baseFileName = capturedDate.ToString("yyyy-MM-dd_HH-mm-ss");
        
        // Simulate existing file
        _mockFileSystem.AddFile(Path.Combine(jpgsPath, baseFileName + ".jpg"), new MockFileData("existing"));

        var photoPath = Path.Combine(sourcePath, "photo1.jpg");
        _mockFileSystem.AddFile(photoPath, new MockFileData("new photo"));

        _mockPictureAnalyzer.Setup(a => a.ExtractTimestamp(photoPath))
            .ReturnsAsync(capturedDate);
        _mockPictureAnalyzer.Setup(a => a.CalculateHashAsync(photoPath))
            .ReturnsAsync(0UL);
        _mockPictureAnalyzer.Setup(a => a.CalculateSharpnessAsync(photoPath))
            .ReturnsAsync(0);
        _mockPictureProcessor.Setup(p => p.GenerateThumbnailAsync(photoPath))
            .ReturnsAsync(new Image<Rgba32>(1, 1));

        // Act
        await _command.ExecuteAsync(parentId, albumName, libraryPath, sourcePath);

        // Assert
        var expectedFileName = baseFileName + "_1";
        var expectedJpgPath = Path.Combine(jpgsPath, expectedFileName + ".jpg");

        Assert.That(_mockFileSystem.File.Exists(expectedJpgPath), Is.True, "File should be renamed with suffix _1 due to collision.");
        _mockNodeService.Verify(s => s.CreateNodeAsync(It.Is<Picture>(p => p.Name == expectedFileName)), Times.Once);
    }
}
