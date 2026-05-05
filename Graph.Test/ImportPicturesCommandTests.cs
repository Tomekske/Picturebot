using System.IO.Abstractions.TestingHelpers;
using Database.Domain.Entities;
using Domain.Enums;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Commands;
using Moq;
using PictureWorker.Domain.Interfaces;

namespace Graph.Test;

[TestFixture]
public class ImportPicturesCommandTests {
    private MockFileSystem _mockFileSystem;
    private Mock<IAlbumService> _mockAlbumService;
    private Mock<INodeService> _mockNodeService;
    private Mock<IPictureAnalyzer> _mockPictureAnalyzer;
    private ImportPicturesCommand _command;

    [SetUp]
    public void Setup() {
        _mockFileSystem = new MockFileSystem();
        _mockAlbumService = new Mock<IAlbumService>();
        _mockNodeService = new Mock<INodeService>();
        _mockPictureAnalyzer = new Mock<IPictureAnalyzer>();

        _command = new ImportPicturesCommand(
            _mockAlbumService.Object,
            _mockNodeService.Object,
            _mockFileSystem,
            _mockPictureAnalyzer.Object);
    }

    [Test]
    public async Task ExecuteAsync_ShouldHandleNamingCollisions() {
        // Arrange
        var parentId = 1;
        var albumName = "Collision Album";
        var libraryPath = @"C:\Library";
        var sourcePath = @"C:\Source";
        var albumUuid = "album-collision";
        var albumPath = _mockFileSystem.Path.Combine(libraryPath, albumUuid);

        var album = new Album { Id = 11, Uuid = albumUuid, Name = albumName };
        _mockAlbumService.Setup(s => s.CreateAsync(parentId, albumName, libraryPath))
            .ReturnsAsync(album);

        _mockFileSystem.AddDirectory(sourcePath);
        _mockFileSystem.AddDirectory(albumPath);
        var jpgsPath = _mockFileSystem.Path.Combine(albumPath, "JPGs");
        _mockFileSystem.AddDirectory(jpgsPath);
        _mockFileSystem.AddDirectory(_mockFileSystem.Path.Combine(albumPath, "RAWs"));
        _mockFileSystem.AddDirectory(_mockFileSystem.Path.Combine(albumPath, "Thumbnails"));

        var capturedDate = new DateTime(2026, 4, 1, 12, 0, 0);
        var baseFileName = capturedDate.ToString("yyyy-MM-dd_HH-mm-ss");

        // Simulate existing file
        _mockFileSystem.AddFile(_mockFileSystem.Path.Combine(jpgsPath, baseFileName + ".jpg"), new MockFileData("existing"));

        var photoPath = _mockFileSystem.Path.Combine(sourcePath, "photo1.jpg");
        _mockFileSystem.AddFile(photoPath, new MockFileData("new photo"));

        _mockPictureAnalyzer.Setup(a => a.ExtractTimestamp(photoPath))
            .ReturnsAsync(capturedDate);

        // Act
        var resultAlbum = await _command.ExecuteAsync(parentId, albumName, libraryPath, sourcePath);

        // Assert
        var expectedFileName = baseFileName + "_1";
        var expectedJpgPath = _mockFileSystem.Path.Combine(jpgsPath, expectedFileName + ".jpg");

        Assert.That(resultAlbum.Children.First().Name, Is.EqualTo(expectedFileName));
        Assert.That(_mockFileSystem.File.Exists(expectedJpgPath), Is.True,
            "File should be renamed with suffix _1 due to collision.");
        _mockNodeService.Verify(s => s.CreateNodeAsync(It.Is<Picture>(p => 
            p.Name == expectedFileName && 
            p.ProcessingState == ProcessingState.Pending)), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_ShouldImportPicturesAsPending() {
        // Arrange
        var parentId = 1;
        var albumName = "New Album";
        var libraryPath = @"C:\Library";
        var sourcePath = @"C:\Source";
        var albumUuid = "album-uuid";
        var albumPath = _mockFileSystem.Path.Combine(libraryPath, albumUuid);

        var album = new Album { Id = 10, Uuid = albumUuid, Name = albumName };
        _mockAlbumService.Setup(s => s.CreateAsync(parentId, albumName, libraryPath))
            .ReturnsAsync(album);

        _mockFileSystem.AddDirectory(sourcePath);
        _mockFileSystem.AddDirectory(albumPath);
        _mockFileSystem.AddDirectory(_mockFileSystem.Path.Combine(albumPath, "RAWs"));
        _mockFileSystem.AddDirectory(_mockFileSystem.Path.Combine(albumPath, "JPGs"));
        _mockFileSystem.AddDirectory(_mockFileSystem.Path.Combine(albumPath, "Thumbnails"));

        var capturedDate = new DateTime(2026, 4, 1, 12, 0, 0);
        var photo1Path = _mockFileSystem.Path.Combine(sourcePath, "photo1.jpg");
        _mockFileSystem.AddFile(photo1Path, new MockFileData("fake jpg content"));

        _mockPictureAnalyzer.Setup(a => a.ExtractTimestamp(photo1Path))
            .ReturnsAsync(capturedDate);

        // Act
        var resultAlbum = await _command.ExecuteAsync(parentId, albumName, libraryPath, sourcePath);

        // Assert
        var expectedFileName = capturedDate.ToString("yyyy-MM-dd_HH-mm-ss");
        var expectedJpgPath = _mockFileSystem.Path.Combine(albumPath, "JPGs", expectedFileName + ".jpg");

        Assert.Multiple(() => {
            Assert.That(resultAlbum, Is.Not.Null);
            Assert.That(resultAlbum.Children.Count, Is.EqualTo(1));
            Assert.That(resultAlbum.Children.First().Name, Is.EqualTo(expectedFileName));
            Assert.That(_mockFileSystem.File.Exists(expectedJpgPath), Is.True, "JPG should be copied.");

            _mockNodeService.Verify(s => s.CreateNodeAsync(It.Is<Picture>(p =>
                p.Name == expectedFileName &&
                p.ProcessingState == ProcessingState.Pending &&
                p.Hash == 0 &&
                p.Sharpness == 0)), Times.Once);
        });
    }

    [Test]
    public async Task ExecuteAsync_ShouldIgnoreHiddenFilesAndSubdirectories() {
        // Arrange
        var parentId = 1;
        var albumName = "Filtered Album";
        var libraryPath = @"C:\Library";
        var sourcePath = @"C:\Source";
        var albumUuid = "album-filtered";
        var albumPath = _mockFileSystem.Path.Combine(libraryPath, albumUuid);

        var album = new Album { Id = 12, Uuid = albumUuid, Name = albumName };
        _mockAlbumService.Setup(s => s.CreateAsync(parentId, albumName, libraryPath))
            .ReturnsAsync(album);

        _mockFileSystem.AddDirectory(sourcePath);
        _mockFileSystem.AddDirectory(albumPath);
        _mockFileSystem.AddDirectory(_mockFileSystem.Path.Combine(albumPath, "RAWs"));
        _mockFileSystem.AddDirectory(_mockFileSystem.Path.Combine(albumPath, "JPGs"));
        _mockFileSystem.AddDirectory(_mockFileSystem.Path.Combine(albumPath, "Thumbnails"));

        var capturedDate = new DateTime(2026, 4, 1, 12, 0, 0);
        
        // 1. Valid image
        var validPhotoPath = _mockFileSystem.Path.Combine(sourcePath, "photo1.jpg");
        _mockFileSystem.AddFile(validPhotoPath, new MockFileData("valid"));
        _mockPictureAnalyzer.Setup(a => a.ExtractTimestamp(validPhotoPath))
            .ReturnsAsync(capturedDate);

        // 2. Hidden macOS file
        var hiddenPath = _mockFileSystem.Path.Combine(sourcePath, "._photo1.jpg");
        _mockFileSystem.AddFile(hiddenPath, new MockFileData("hidden"));

        // 3. File in subdirectory (should be ignored due to TopDirectoryOnly)
        var subDirPath = _mockFileSystem.Path.Combine(sourcePath, "SubDir");
        _mockFileSystem.AddDirectory(subDirPath);
        var subDirPhotoPath = _mockFileSystem.Path.Combine(subDirPath, "photo2.jpg");
        _mockFileSystem.AddFile(subDirPhotoPath, new MockFileData("subdir photo"));

        // 4. Non-image file
        var textFilePath = _mockFileSystem.Path.Combine(sourcePath, "notes.txt");
        _mockFileSystem.AddFile(textFilePath, new MockFileData("notes"));

        // Act
        var resultAlbum = await _command.ExecuteAsync(parentId, albumName, libraryPath, sourcePath);

        // Assert
        Assert.That(resultAlbum.Children.Count, Is.EqualTo(1), "Only the valid top-level photo should be imported.");
        Assert.That(resultAlbum.Children.First().Name, Is.EqualTo(capturedDate.ToString("yyyy-MM-dd_HH-mm-ss")));
    }
}
