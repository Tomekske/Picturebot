using Database.Domain.Entities;
using Domain.Enums;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Services;
using Moq;

namespace Graph.Test;

[TestFixture]
public class FolderServiceTests {
    private Mock<INodeService> _nodeServiceMock;
    private FolderService _folderService;

    [SetUp]
    public void Setup() {
        _nodeServiceMock = new Mock<INodeService>();
        _folderService = new FolderService(_nodeServiceMock.Object);
    }

    [Test]
    public async Task FindAllAsync_ShouldOnlyReturnFoldersWithFolderType() {
        // Arrange
        var nodes = new List<Node> {
            new Folder { Id = 1, Name = "Real Folder", Type = NodeType.Folder },
            new Album { Id = 2, Name = "Album", Type = NodeType.Album },
            new Folder { Id = 3, Name = "Misclassed Album", Type = NodeType.Album } // Misclassed but is a Folder object
        };

        _nodeServiceMock.Setup(s => s.GetAllNodesAsync()).ReturnsAsync(nodes);

        // Act
        var result = await _folderService.FindAllAsync();

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("Real Folder"));
        Assert.That(result[0].Type, Is.EqualTo(NodeType.Folder));
    }

    [Test]
    public async Task DeleteAsync_WhenFolderIsEmpty_ShouldCallDeleteNode() {
        // Arrange
        var folder = new Folder { Id = 1, Name = "Empty Folder", Type = NodeType.Folder };
        _nodeServiceMock.Setup(s => s.LoadHydratedTreeAsync()).ReturnsAsync(new List<Node> { folder });

        // Act
        await _folderService.DeleteAsync(folder);

        // Assert
        _nodeServiceMock.Verify(s => s.DeleteNodeAsync(folder), Times.Once);
    }

    [Test]
    public void DeleteAsync_WhenFolderIsNotEmpty_ShouldThrowInvalidOperationException() {
        // Arrange
        var folder = new Folder { 
            Id = 1, 
            Name = "Not Empty", 
            Type = NodeType.Folder,
            Children = new List<Node> { new Album { Id = 2, Name = "Child" } }
        };
        _nodeServiceMock.Setup(s => s.LoadHydratedTreeAsync()).ReturnsAsync(new List<Node> { folder });

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () => await _folderService.DeleteAsync(folder));
        _nodeServiceMock.Verify(s => s.DeleteNodeAsync(It.IsAny<Folder>()), Times.Never);
    }
}
