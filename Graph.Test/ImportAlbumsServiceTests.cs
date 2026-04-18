using System.IO.Abstractions.TestingHelpers;
using Database.Domain.Entities;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Services;
using Moq;

namespace Graph.Test;

[TestFixture]
public class ImportAlbumsServiceTests {
    private Mock<IFolderService> _folderServiceMock;
    private Mock<IImportPicturesCommand> _importPicturesCommandMock;
    private MockFileSystem _fileSystem;
    private ImportAlbumsService _service;

    [SetUp]
    public void SetUp() {
        _fileSystem = new MockFileSystem();
        _folderServiceMock = new Mock<IFolderService>();
        _importPicturesCommandMock = new Mock<IImportPicturesCommand>();
        
        _service = new ImportAlbumsService(
            _fileSystem,
            _folderServiceMock.Object,
            _importPicturesCommandMock.Object
        );
    }

    [Test]
    public async Task ImportRecursiveAsync_CreatesFoldersAndAlbumsCorrectly() {
        // Arrange
        var rootPath = @"C:\Source";
        _fileSystem.AddDirectory(rootPath);
        
        // Folder 1 (No images)
        var folder1Path = _fileSystem.Path.Combine(rootPath, "Folder1");
        _fileSystem.AddDirectory(folder1Path);
        
        // Album 1 (Has images)
        var album1Path = _fileSystem.Path.Combine(folder1Path, "Album1");
        _fileSystem.AddDirectory(album1Path);
        _fileSystem.AddFile(_fileSystem.Path.Combine(album1Path, "photo.jpg"), new MockFileData("image data"));
        
        // Album 2 (Has images, direct child of root)
        var album2Path = _fileSystem.Path.Combine(rootPath, "Album2");
        _fileSystem.AddDirectory(album2Path);
        _fileSystem.AddFile(_fileSystem.Path.Combine(album2Path, "photo.cr2"), new MockFileData("raw data"));

        var libraryPath = @"C:\Library";

        _folderServiceMock.Setup(s => s.CreateAsync(It.IsAny<int?>(), It.IsAny<string>()))
            .ReturnsAsync((int? parentId, string name) => new Folder { Id = name == "Source" ? 1 : (name == "Folder1" ? 2 : 0), Name = name, ParentId = parentId });

        _importPicturesCommandMock.Setup(s => s.ExecuteAsync(It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<Graph.Domain.DTOs.ImportProgress>>()))
            .ReturnsAsync((int? parentId, string name, string libPath, string srcPath, IProgress<Graph.Domain.DTOs.ImportProgress> p) => new Album { Id = name == "Album1" ? 3 : 4, Name = name, ParentId = parentId });

        // Act
        await _service.ImportRecursiveAsync(null, rootPath, libraryPath);

        // Assert
        // Verify Source Folder
        _folderServiceMock.Verify(s => s.CreateAsync(null, "Source"), Times.Once);
        
        // Verify Folder1
        _folderServiceMock.Verify(s => s.CreateAsync(1, "Folder1"), Times.Once);
        
        // Verify Album1
        _importPicturesCommandMock.Verify(s => s.ExecuteAsync(2, "Album1", libraryPath, album1Path, It.IsAny<IProgress<Graph.Domain.DTOs.ImportProgress>>()), Times.Once);
        
        // Verify Album2
        _importPicturesCommandMock.Verify(s => s.ExecuteAsync(1, "Album2", libraryPath, album2Path, It.IsAny<IProgress<Graph.Domain.DTOs.ImportProgress>>()), Times.Once);
    }
}
