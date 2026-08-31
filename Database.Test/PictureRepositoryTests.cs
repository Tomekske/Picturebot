using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Database.Domain.Entities;
using Database.Infrastructure.Data;
using Database.Infrastructure.Repositories;
using Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Database.Test;

[TestFixture]
public class PictureRepositoryTests : IDisposable {
    private ApplicationDbContext _context = null!;
    private SqliteConnection _connection = null!;
    private PictureRepository _repository = null!;

    [SetUp]
    public void Setup() {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new PictureRepository(_context);
    }

    [TearDown]
    public void TearDown() {
        _context.Dispose();
        _connection.Close();
    }

    [Test]
    public async Task SearchGlobalAsync_ReturnsMatchingPicturesAcrossMultipleAlbums() {
        // Arrange
        var album1 = new Album { Name = "Vacation 2026", Type = NodeType.Album, Uuid = Guid.NewGuid().ToString() };
        var album2 = new Album { Name = "Studio Session", Type = NodeType.Album, Uuid = Guid.NewGuid().ToString() };
        _context.Nodes.AddRange(album1, album2);
        await _context.SaveChangesAsync();

        var pic1 = new Picture {
            Name = "DSC_001.JPG",
            ParentId = album1.Id,
            Type = NodeType.Picture,
            KeywordsJson = "[\"faces|robin\", \"faces\", \"robin\"]"
        };
        var pic2 = new Picture {
            Name = "DSC_002.JPG",
            ParentId = album1.Id,
            Type = NodeType.Picture,
            KeywordsJson = "[\"faces|katsiuska\", \"faces\", \"katsiuska\"]"
        };
        var pic3 = new Picture {
            Name = "DSC_003.JPG",
            ParentId = album2.Id,
            Type = NodeType.Picture,
            KeywordsJson = "[\"faces|robin\", \"faces\", \"robin\", \"Hero\"]"
        };
        var pic4 = new Picture {
            Name = "Landscape_001.JPG",
            ParentId = album2.Id,
            Type = NodeType.Picture,
            KeywordsJson = "[\"Nature|Mountains\", \"Nature\"]"
        };

        _context.Nodes.AddRange(pic1, pic2, pic3, pic4);
        await _context.SaveChangesAsync();

        // Act 1: Search by specific leaf tag "robin" across all albums
        var robinResults = await _repository.SearchGlobalAsync("robin");
        Assert.That(robinResults.Count, Is.EqualTo(2));
        Assert.That(robinResults.Select(p => p.Name), Does.Contain("DSC_001.JPG"));
        Assert.That(robinResults.Select(p => p.Name), Does.Contain("DSC_003.JPG"));

        // Act 2: Search by parent taxonomy branch "faces" across all albums
        var facesResults = await _repository.SearchGlobalAsync("faces");
        Assert.That(facesResults.Count, Is.EqualTo(3));

        // Act 3: Search by flat tag "Hero"
        var heroResults = await _repository.SearchGlobalAsync("Hero");
        Assert.That(heroResults.Count, Is.EqualTo(1));
        Assert.That(heroResults[0].Name, Is.EqualTo("DSC_003.JPG"));

        // Act 4: Search by Album Name "Studio"
        var studioResults = await _repository.SearchGlobalAsync("Studio");
        Assert.That(studioResults.Count, Is.EqualTo(2));
    }

    public void Dispose() {
        _context?.Dispose();
        _connection?.Dispose();
    }
}
