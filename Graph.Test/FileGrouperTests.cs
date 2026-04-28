using System.IO.Abstractions.TestingHelpers;
using Graph.Infrastructure.Utilities;
using Moq;
using PictureWorker.Domain.Interfaces;
using ErrorOr;

namespace Graph.Test;

[TestFixture]
public class FileGrouperTests {
    private MockFileSystem _mockFileSystem;
    private Mock<IPictureAnalyzer> _mockPictureAnalyzer;
    private FileGrouper _fileGrouper;

    [SetUp]
    public void Setup() {
        _mockFileSystem = new MockFileSystem();
        _mockPictureAnalyzer = new Mock<IPictureAnalyzer>();
        _fileGrouper = new FileGrouper(_mockFileSystem, _mockPictureAnalyzer.Object);
    }

    [Test]
    public async Task GroupFilesAsync_ShouldGroupFilesByBaseName() {
        // Arrange
        var sourcePath = @"C:\Source";
        _mockFileSystem.AddDirectory(sourcePath);
        _mockFileSystem.AddFile(Path.Combine(sourcePath, "photo1.jpg"), new MockFileData(""));
        _mockFileSystem.AddFile(Path.Combine(sourcePath, "photo1.raw"), new MockFileData(""));
        _mockFileSystem.AddFile(Path.Combine(sourcePath, "photo2.jpg"), new MockFileData(""));

        _mockPictureAnalyzer.Setup(a => a.ExtractTimestamp(It.IsAny<string>()))
            .ReturnsAsync(Error.Failure("No metadata"));

        // Act
        var result = await _fileGrouper.GroupFilesAsync(sourcePath);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        var group1 = result.First(g => g.BaseName == "photo1");
        Assert.That(group1.FilePaths, Has.Count.EqualTo(2));
        var group2 = result.First(g => g.BaseName == "photo2");
        Assert.That(group2.FilePaths, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GroupFilesAsync_ShouldPrioritizeMetadataTimestamp() {
        // Arrange
        var sourcePath = @"C:\Source";
        var metadataDate = new DateTime(2025, 1, 1, 12, 0, 0);
        _mockFileSystem.AddDirectory(sourcePath);
        var jpgPath = Path.Combine(sourcePath, "photo1.jpg");
        var rawPath = Path.Combine(sourcePath, "photo1.raw");
        _mockFileSystem.AddFile(jpgPath, new MockFileData(""));
        _mockFileSystem.AddFile(rawPath, new MockFileData(""));

        // Mock jpg having no metadata, but raw having it
        _mockPictureAnalyzer.Setup(a => a.ExtractTimestamp(jpgPath))
            .ReturnsAsync(Error.Failure("No metadata"));
        _mockPictureAnalyzer.Setup(a => a.ExtractTimestamp(rawPath))
            .ReturnsAsync(metadataDate);

        // Act
        var result = await _fileGrouper.GroupFilesAsync(sourcePath);

        // Assert
        var group = result.First(g => g.BaseName == "photo1");
        Assert.That(group.PrimaryDate, Is.EqualTo(metadataDate));
    }

    [Test]
    public async Task GroupFilesAsync_ShouldIgnoreGhostFiles() {
        // Arrange
        var sourcePath = @"C:\Source";
        _mockFileSystem.AddDirectory(sourcePath);
        _mockFileSystem.AddFile(Path.Combine(sourcePath, "photo1.jpg"), new MockFileData(""));
        _mockFileSystem.AddFile(Path.Combine(sourcePath, "._photo1.jpg"), new MockFileData("mac ghost"));
        _mockFileSystem.AddFile(Path.Combine(sourcePath, ".DS_Store"), new MockFileData("ds store"));

        _mockPictureAnalyzer.Setup(a => a.ExtractTimestamp(It.IsAny<string>()))
            .ReturnsAsync(Error.Failure("No metadata"));

        // Act
        var result = await _fileGrouper.GroupFilesAsync(sourcePath);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].BaseName, Is.EqualTo("photo1"));
    }
}
