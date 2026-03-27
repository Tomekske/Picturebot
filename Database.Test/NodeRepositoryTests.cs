using Database.Domain.Entities;
using Database.Infrastructure.Data;
using Database.Infrastructure.Repositories;
using Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Database.Test;

[TestFixture]
public class NodeRepositoryTests : IDisposable {
    [SetUp]
    public void Setup() {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new NodeRepository(_context);
    }

    [TearDown]
    public void TearDown() {
        _context.Dispose();
        _connection.Close();
    }

    private ApplicationDbContext _context;
    private SqliteConnection _connection;
    private NodeRepository _repository;

    [Test]
    public async Task CreateAsync_WhenFolderIsCreated_ShouldPersistInDatabase() {
        // Arrange
        var folder = new Folder {
            Name = "Root Folder",
            Type = NodeType.Folder
        };

        // Act
        await _repository.CreateAsync(folder);

        // Assert
        var result = await _context.Folders.FirstOrDefaultAsync(f => f.Name == "Root Folder");
        Assert.Multiple(() => {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Name, Is.EqualTo("Root Folder"));

            // ParentId should be null for root folders
            Assert.That(result.ParentId, Is.Null);
            Assert.That(result.Type, Is.EqualTo(NodeType.Folder));
            Assert.That(result.Id, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task CreateAsync_WhenFolderWithParentIsCreated_ShouldMaintainHierarchy() {
        // Arrange
        var parentFolder = new Folder {
            Name = "Parent Folder",
            Type = NodeType.Folder
        };
        await _repository.CreateAsync(parentFolder);

        var childFolder = new Folder {
            Name = "Child Folder",
            Type = NodeType.Folder,
            ParentId = parentFolder.Id
        };

        // Act
        await _repository.CreateAsync(childFolder);

        // Assert
        var result = await _context.Folders
            .Include(f => f.Parent)
            .FirstOrDefaultAsync(f => f.Name == "Child Folder");

        Assert.Multiple(() => {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ParentId, Is.EqualTo(parentFolder.Id));
            Assert.That(result.Parent, Is.Not.Null);
            Assert.That(result.Parent!.Name, Is.EqualTo("Parent Folder"));
        });
    }

    [Test]
    public async Task CreateAsync_WhenDuplicateFolderUnderSameParentIsCreated_ShouldThrowException() {
        // Arrange
        var parent = new Folder { Name = "Parent", Type = NodeType.Folder };
        await _repository.CreateAsync(parent);

        var folder1 = new Folder {
            Name = "Duplicate Folder",
            Type = NodeType.Folder,
            ParentId = parent.Id
        };
        await _repository.CreateAsync(folder1);

        var folder2 = new Folder {
            Name = "Duplicate Folder",
            Type = NodeType.Folder,
            ParentId = parent.Id
        };

        // Act & Assert
        Assert.ThrowsAsync<DbUpdateException>(async () => await _repository.CreateAsync(folder2));
    }

    [Test]
    public async Task CreateAsync_WhenMultipleFoldersAtSameLevel_ShouldPersistAll() {
        // Arrange
        var folders = new List<Folder> {
            new() { Name = "Folder 1", Type = NodeType.Folder },
            new() { Name = "Folder 2", Type = NodeType.Folder },
            new() { Name = "Folder 3", Type = NodeType.Folder }
        };

        // Act
        foreach (var folder in folders) {
            await _repository.CreateAsync(folder);
        }

        // Assert
        var count = await _context.Folders.CountAsync();
        Assert.That(count, Is.EqualTo(3));
    }

    public void Dispose() {
    }
}
