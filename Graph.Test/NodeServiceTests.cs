using Database.Domain.Entities;
using Database.Domain.Interfaces;
using Domain.Enums;
using Graph.Domain.Strategies;
using Graph.Infrastructure.Services;
using Moq;

namespace Graph.Test;

[TestFixture]
public class NodeServiceTests {
    [SetUp]
    public void Setup() {
        _nodeRepositoryMock = new Mock<INodeRepository>();

        var folderStrategy = new FolderCreationStrategy();
        var albumStrategy = new AlbumCreationStrategy();
        _strategyFactory = new NodeStrategyFactory(folderStrategy, albumStrategy);

        _nodeService = new NodeService(_nodeRepositoryMock.Object, _strategyFactory);
    }

    private Mock<INodeRepository> _nodeRepositoryMock;
    private NodeService _nodeService;
    private NodeStrategyFactory _strategyFactory;

    [Test]
    public async Task CreateNodeAsync_RootMustBeFolder() {
        // Arrange
        var album = new Album { Name = "Root Album", Type = NodeType.Album };

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await _nodeService.CreateNodeAsync(album));
        Assert.That(ex.Message, Is.EqualTo("Root Level: The entry point of any tree must be a Folder."));
    }

    [Test]
    public async Task CreateNodeAsync_Branching_OnlyFolderCanHaveChildren() {
        // Arrange
        var parentAlbum = new Album { Id = 1, Name = "Parent Album", Type = NodeType.Album };
        _nodeRepositoryMock.Setup(r => r.FindByIdAsync(1)).ReturnsAsync(parentAlbum);

        var childFolder = new Folder { Name = "Child Folder", Type = NodeType.Folder, ParentId = 1 };

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _nodeService.CreateNodeAsync(childFolder));
        Assert.That(ex.Message, Is.EqualTo("Branching: Only Folder nodes can have children."));
    }

    [Test]
    public async Task CreateNodeAsync_TypeHomogeneity_FolderCannotMixTypes_AddingAlbumToFolderWithFolders() {
        // Arrange
        var parentFolder = new Folder {
            Id = 1,
            Name = "Parent Folder",
            Type = NodeType.Folder,
            Children = new List<Node> { new Folder { Name = "Existing Folder", Type = NodeType.Folder } }
        };
        _nodeRepositoryMock.Setup(r => r.FindByIdAsync(1)).ReturnsAsync(parentFolder);

        var newAlbum = new Album { Name = "New Album", Type = NodeType.Album, ParentId = 1 };

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _nodeService.CreateNodeAsync(newAlbum));
        Assert.That(ex.Message, Is.EqualTo("Type Homogeneity: This folder already contains non-album nodes."));
    }

    [Test]
    public async Task CreateNodeAsync_TypeHomogeneity_FolderCannotMixTypes_AddingFolderToFolderWithAlbums() {
        // Arrange
        var parentFolder = new Folder {
            Id = 1,
            Name = "Parent Folder",
            Type = NodeType.Folder,
            Children = new List<Node> { new Album { Name = "Existing Album", Type = NodeType.Album } }
        };
        _nodeRepositoryMock.Setup(r => r.FindByIdAsync(1)).ReturnsAsync(parentFolder);

        var newFolder = new Folder { Name = "New Folder", Type = NodeType.Folder, ParentId = 1 };

        // Act & Assert
        var ex =
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _nodeService.CreateNodeAsync(newFolder));
        Assert.That(ex.Message, Is.EqualTo("Type Homogeneity: This folder already contains non-folder nodes."));
    }

    [Test]
    public async Task CreateNodeAsync_AlbumUuid_ShouldBeGeneratedOnCreation() {
        // Arrange
        var parentFolder = new Folder { Id = 1, Name = "Parent Folder", Type = NodeType.Folder };
        _nodeRepositoryMock.Setup(r => r.FindByIdAsync(1)).ReturnsAsync(parentFolder);

        var newAlbum = new Album { Name = "New Album", Type = NodeType.Album, ParentId = 1 };

        // Act
        await _nodeService.CreateNodeAsync(newAlbum);

        // Assert
        Assert.That(newAlbum.Uuid, Is.Not.Null.And.Not.Empty);
        Guid uuid;
        Assert.That(Guid.TryParse(newAlbum.Uuid, out uuid), Is.True);
        _nodeRepositoryMock.Verify(r => r.CreateAsync(newAlbum), Times.Once);
    }

    [Test]
    public async Task CreateNodeAsync_ValidFolderCreation_ShouldSucceed() {
        // Arrange
        var parentFolder = new Folder { Id = 1, Name = "Parent", Type = NodeType.Folder };
        _nodeRepositoryMock.Setup(r => r.FindByIdAsync(1)).ReturnsAsync(parentFolder);

        var childFolder = new Folder { Name = "Child", Type = NodeType.Folder, ParentId = 1 };

        // Act & Assert
        await _nodeService.CreateNodeAsync(childFolder);
        _nodeRepositoryMock.Verify(r => r.CreateAsync(childFolder), Times.Once);
    }
}
