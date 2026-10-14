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

    [Test]
    public async Task UpdateAsync_WhenPictureWithMetricsIsUpdated_ShouldNotThrowUniqueConstraintExceptionAndShouldUpdateProperties() {
        // Arrange: create picture and metrics in DB
        var picture = new Picture {
            Name = "test_pic.jpg",
            Type = NodeType.Picture,
            CapturedAt = DateTime.UtcNow
        };
        await _repository.CreateAsync(picture);

        var metrics = new Metrics {
            PictureId = picture.Id,
            Sharpness = 100,
            PHash = 123456789UL,
            Embedding = new byte[512 * sizeof(float)]
        };
        _context.Metrics.Add(metrics);
        await _context.SaveChangesAsync();

        // Detached picture instance with Metrics loaded (like from FindAllAsync or UI thread)
        var detachedPic = await _repository.FindByIdAsync(picture.Id) as Picture;
        Assert.That(detachedPic, Is.Not.Null);
        Assert.That(detachedPic!.Metrics, Is.Not.Null);

        // Act: modify detached picture and update via repository
        detachedPic.Rating = 5;
        detachedPic.ColorLabel = ColorLabel.Green;
        detachedPic.Metrics!.Sharpness = 200;

        Assert.DoesNotThrowAsync(async () => await _repository.UpdateAsync(detachedPic));

        // Assert: verify picture and metrics were updated in DB
        var updated = await _context.Pictures
            .Include(p => p.Metrics)
            .FirstOrDefaultAsync(p => p.Id == picture.Id);

        Assert.Multiple(() => {
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated!.Rating, Is.EqualTo(5));
            Assert.That(updated.ColorLabel, Is.EqualTo(ColorLabel.Green));
            Assert.That(updated.Metrics, Is.Not.Null);
            Assert.That(updated.Metrics!.Sharpness, Is.EqualTo(200));
        });
    }

    [Test]
    public async Task DeleteAsync_WhenPictureWithMetricsIsDeleted_ShouldRemovePictureAndMetricsWithoutError() {
        // Arrange
        var picture = new Picture {
            Name = "delete_pic.jpg",
            Type = NodeType.Picture,
            CapturedAt = DateTime.UtcNow
        };
        await _repository.CreateAsync(picture);

        var metrics = new Metrics {
            PictureId = picture.Id,
            Sharpness = 50,
            PHash = 987654321UL
        };
        _context.Metrics.Add(metrics);
        await _context.SaveChangesAsync();

        var detachedPic = await _repository.FindByIdAsync(picture.Id);
        Assert.That(detachedPic, Is.Not.Null);

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await _repository.DeleteAsync(detachedPic!));

        var deletedPic = await _context.Pictures.FirstOrDefaultAsync(p => p.Id == picture.Id);
        var deletedMetrics = await _context.Metrics.FirstOrDefaultAsync(m => m.PictureId == picture.Id);

        Assert.Multiple(() => {
            Assert.That(deletedPic, Is.Null);
            Assert.That(deletedMetrics, Is.Null);
        });
    }

    [Test]
    public async Task DeleteAsync_WhenAlbumWithPicturesAndMetricsIsDeleted_ShouldRemoveAlbumAndAllPicturesAndMetrics() {
        // Arrange
        var album = new Album {
            Name = "Summer Vacation",
            Type = NodeType.Album,
            Uuid = Guid.NewGuid().ToString()
        };
        await _repository.CreateAsync(album);

        var pic1 = new Picture {
            Name = "pic1.jpg",
            Type = NodeType.Picture,
            ParentId = album.Id,
            CapturedAt = DateTime.UtcNow
        };
        await _repository.CreateAsync(pic1);

        var metrics1 = new Metrics {
            PictureId = pic1.Id,
            Sharpness = 80,
            PHash = 111111UL
        };
        _context.Metrics.Add(metrics1);

        var pic2 = new Picture {
            Name = "pic2.jpg",
            Type = NodeType.Picture,
            ParentId = album.Id,
            CapturedAt = DateTime.UtcNow
        };
        await _repository.CreateAsync(pic2);

        var metrics2 = new Metrics {
            PictureId = pic2.Id,
            Sharpness = 90,
            PHash = 222222UL
        };
        _context.Metrics.Add(metrics2);
        await _context.SaveChangesAsync();

        // Also simulate stray Added metrics in change tracker (the exact bug encountered by user)
        _context.ChangeTracker.Clear();
        var stray = new Metrics {
            PictureId = pic1.Id,
            Sharpness = 99
        };
        _context.Metrics.Add(stray);

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await _repository.DeleteAsync(album));

        var deletedAlbum = await _context.Albums.FirstOrDefaultAsync(a => a.Id == album.Id);
        var deletedAlbumNode = await _context.Nodes.FirstOrDefaultAsync(n => n.Id == album.Id);
        var deletedPics = await _context.Pictures.Where(p => p.ParentId == album.Id).ToListAsync();
        var deletedM1 = await _context.Metrics.FirstOrDefaultAsync(m => m.PictureId == pic1.Id);
        var deletedM2 = await _context.Metrics.FirstOrDefaultAsync(m => m.PictureId == pic2.Id);

        Assert.Multiple(() => {
            Assert.That(deletedAlbum, Is.Null);
            Assert.That(deletedAlbumNode, Is.Null);
            Assert.That(deletedPics, Is.Empty);
            Assert.That(deletedM1, Is.Null);
            Assert.That(deletedM2, Is.Null);
        });
    }

    [Test]
    public async Task UpdateAsync_WithStrayAddedMetrics_DoesNotThrowUniqueConstraintViolation() {
        // Arrange: Create a folder and a picture with existing metrics
        var folder = new Folder {
            Name = "Original Folder",
            Type = NodeType.Folder
        };
        await _repository.CreateAsync(folder);

        var pic = new Picture {
            Name = "pic.jpg",
            Type = NodeType.Picture,
            ParentId = folder.Id,
            CapturedAt = DateTime.UtcNow
        };
        await _repository.CreateAsync(pic);

        var metrics = new Metrics {
            PictureId = pic.Id,
            Sharpness = 50,
            PHash = 12345UL
        };
        _context.Metrics.Add(metrics);
        await _context.SaveChangesAsync();

        // Simulate stray Added metrics in change tracker for the same PictureId (the exact bug encountered when editing folders)
        _context.ChangeTracker.Clear();
        var stray = new Metrics {
            PictureId = pic.Id,
            Sharpness = 99
        };
        _context.Metrics.Add(stray);

        // Act: Rename the folder and update
        folder.Name = "Renamed Folder";
        Assert.DoesNotThrowAsync(async () => await _repository.UpdateAsync(folder));

        // Assert: Folder was updated successfully
        var updated = await _context.Folders.FirstOrDefaultAsync(f => f.Id == folder.Id);
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Name, Is.EqualTo("Renamed Folder"));
    }

    public void Dispose() {
    }
}
