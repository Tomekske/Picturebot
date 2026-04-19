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
        var rootPath = @"C:\Batch Import";
        _fileSystem.AddDirectory(rootPath);
        
        // Structure:
        // C:\Batch Import (Root)
        //   ├── Album 1 (with image)
        //   ├── Album 2 (with image)
        //   ├── Album 3 (with image)
        //   ├── Folder 1
        //   │   ├── Album 1 (with image)
        //   │   └── Album 2 (with image)
        //   └── Folder 2
        //       ├── Album 1 (with image)
        //       ├── Album 2 (with image)
        //       └── Folder 1 (No images)
        //           ├── Album 1 (with image)
        //           └── Album 2 (with image)

        // Root children
        AddAlbumWithImage(_fileSystem.Path.Combine(rootPath, "Album 1"));
        AddAlbumWithImage(_fileSystem.Path.Combine(rootPath, "Album 2"));
        AddAlbumWithImage(_fileSystem.Path.Combine(rootPath, "Album 3"));

        // Folder 1
        var folder1Path = _fileSystem.Path.Combine(rootPath, "Folder 1");
        _fileSystem.AddDirectory(folder1Path);
        AddAlbumWithImage(_fileSystem.Path.Combine(folder1Path, "Album 1"));
        AddAlbumWithImage(_fileSystem.Path.Combine(folder1Path, "Album 2"));

        // Folder 2
        var folder2Path = _fileSystem.Path.Combine(rootPath, "Folder 2");
        _fileSystem.AddDirectory(folder2Path);
        AddAlbumWithImage(_fileSystem.Path.Combine(folder2Path, "Album 1"));
        AddAlbumWithImage(_fileSystem.Path.Combine(folder2Path, "Album 2"));

        // Folder 2 -> Folder 1 (Nested same name as top level)
        var nestedFolder1Path = _fileSystem.Path.Combine(folder2Path, "Folder 1");
        _fileSystem.AddDirectory(nestedFolder1Path);
        AddAlbumWithImage(_fileSystem.Path.Combine(nestedFolder1Path, "Album 1"));
        AddAlbumWithImage(_fileSystem.Path.Combine(nestedFolder1Path, "Album 2"));

        var libraryPath = @"C:\Library";

        int idCounter = 1;
        _folderServiceMock.Setup(s => s.CreateAsync(It.IsAny<int?>(), It.IsAny<string>()))
            .ReturnsAsync((int? parentId, string name) => new Folder { Id = idCounter++, Name = name, ParentId = parentId });

        _importPicturesCommandMock.Setup(s => s.ExecuteAsync(It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<Graph.Domain.DTOs.ImportProgress>>()))
            .ReturnsAsync((int? parentId, string name, string libPath, string srcPath, IProgress<Graph.Domain.DTOs.ImportProgress> p) => new Album { Id = idCounter++, Name = name, ParentId = parentId });

        // Act
        await _service.ImportRecursiveAsync(null, rootPath, libraryPath);

        // Assert
        // Verify Root Folder creation (since C:\Batch Import has no images itself)
        _folderServiceMock.Verify(s => s.CreateAsync(null, "Batch Import"), Times.Once);
        
        // We can't easily check exact IDs in Verify without tracking them, 
        // but we can check the calls were made for all paths.
        
        // Root children albums
        VerifyAlbumImported("Album 1", rootPath);
        VerifyAlbumImported("Album 2", rootPath);
        VerifyAlbumImported("Album 3", rootPath);

        // Folder 1 creation
        _folderServiceMock.Verify(s => s.CreateAsync(It.IsAny<int?>(), "Folder 1"), Times.Exactly(2)); // One under root, one under Folder 2
        
        // Folder 1 children albums
        VerifyAlbumImported("Album 1", folder1Path);
        VerifyAlbumImported("Album 2", folder1Path);

        // Folder 2 creation
        _folderServiceMock.Verify(s => s.CreateAsync(It.IsAny<int?>(), "Folder 2"), Times.Once);
        
        // Folder 2 children albums
        VerifyAlbumImported("Album 1", folder2Path);
        VerifyAlbumImported("Album 2", folder2Path);
        
        // Nested Folder 1 children albums
        VerifyAlbumImported("Album 1", nestedFolder1Path);
        VerifyAlbumImported("Album 2", nestedFolder1Path);
    }

    private void AddAlbumWithImage(string path) {
        _fileSystem.AddDirectory(path);
        _fileSystem.AddFile(_fileSystem.Path.Combine(path, "photo.jpg"), new MockFileData("image data"));
    }

    private void VerifyAlbumImported(string name, string parentPath) {
        var fullPath = _fileSystem.Path.Combine(parentPath, name);
        _importPicturesCommandMock.Verify(s => s.ExecuteAsync(
            It.IsAny<int?>(), 
            name, 
            @"C:\Library", 
            fullPath, 
            It.IsAny<IProgress<Graph.Domain.DTOs.ImportProgress>>()), 
            Times.Once);
    }
}
