using System;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Database.Domain.Entities;
using Database.Infrastructure.Data;
using Domain.Enums;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Graph.Test;

[TestFixture]
public class XmpServiceTests : IDisposable {
    private MockFileSystem _mockFileSystem;
    private Mock<IPathService> _mockPathService;
    private ApplicationDbContext _context;
    private SqliteConnection _connection;
    private IServiceScopeFactory _scopeFactory;
    private XmpService _xmpService;

    [SetUp]
    public void Setup() {
        _mockFileSystem = new MockFileSystem();
        _mockPathService = new Mock<IPathService>();

        // Set up in-memory SQLite database
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        // Register in DI to get a real ServiceScopeFactory
        var services = new ServiceCollection();
        services.AddSingleton(_context);
        var provider = services.BuildServiceProvider();
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        _xmpService = new XmpService(_mockFileSystem, _scopeFactory, _mockPathService.Object);
    }

    [TearDown]
    public void TearDown() {
        _context.Dispose();
        _connection.Close();
    }

    public void Dispose() {
        TearDown();
    }

    [Test]
    public async Task LoadMetadataAsync_WhenFileDoesNotExist_ShouldSetDefaults() {
        // Arrange
        var picture = new Picture {
            Name = "Pic1",
            SubFolder = new SubFolder {
                Raw = @"C:\RAWs\Pic1.NEF"
            }
        };

        // Act
        await _xmpService.LoadMetadataAsync(picture);

        // Assert
        Assert.Multiple(() => {
            Assert.That(picture.Rating, Is.EqualTo(0));
            Assert.That(picture.ColorLabel, Is.EqualTo(ColorLabel.None));
            Assert.That(picture.CurationStatus, Is.EqualTo(CurationStatus.Unflagged));
        });
    }

    [Test]
    public async Task SaveAndLoad_ShouldPreserveValues() {
        // Arrange
        var rawPath = @"C:\RAWs\Pic1.NEF";
        var xmpPath = @"C:\RAWs\Pic1.xmp";
        _mockFileSystem.AddDirectory(@"C:\RAWs");

        var picture = new Picture {
            Name = "Pic1",
            CapturedAt = new DateTime(2026, 6, 30, 12, 0, 0),
            Rating = 4,
            ColorLabel = ColorLabel.Red,
            CurationStatus = CurationStatus.Flagged,
            SubFolder = new SubFolder {
                Raw = rawPath
            }
        };

        // Act - Save
        await _xmpService.SaveMetadataAsync(picture);

        // Assert file exists
        Assert.That(_mockFileSystem.File.Exists(xmpPath), Is.True);

        // Create new picture to load into
        var pictureToLoad = new Picture {
            Name = "Pic1",
            SubFolder = new SubFolder {
                Raw = rawPath
            }
        };

        // Act - Load
        await _xmpService.LoadMetadataAsync(pictureToLoad);

        // Assert loaded correctly
        Assert.Multiple(() => {
            Assert.That(pictureToLoad.Rating, Is.EqualTo(4));
            Assert.That(pictureToLoad.ColorLabel, Is.EqualTo(ColorLabel.Red));
            Assert.That(pictureToLoad.CurationStatus, Is.EqualTo(CurationStatus.Flagged));
            Assert.That(pictureToLoad.CapturedAt, Is.EqualTo(picture.CapturedAt));
        });
    }

    [Test]
    public async Task SaveMetadataAsync_WhenXmpExists_ShouldPreserveOtherXmlElements() {
        // Arrange
        var rawPath = @"C:\RAWs\Pic1.NEF";
        var xmpPath = @"C:\RAWs\Pic1.xmp";
        _mockFileSystem.AddDirectory(@"C:\RAWs");

        var initialXml = @"<x:xmpmeta xmlns:x=""adobe:ns:meta/"">
  <rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#"">
    <rdf:Description rdf:about="""" xmlns:other=""http://other.com/"" other:CustomField=""custom_value"">
    </rdf:Description>
  </rdf:RDF>
</x:xmpmeta>";

        _mockFileSystem.AddFile(xmpPath, new MockFileData(initialXml));

        var picture = new Picture {
            Name = "Pic1",
            Rating = 3,
            SubFolder = new SubFolder {
                Raw = rawPath
            }
        };

        // Act
        await _xmpService.SaveMetadataAsync(picture);

        // Assert custom field is preserved
        var savedXmlContent = _mockFileSystem.File.ReadAllText(xmpPath);
        var doc = XDocument.Parse(savedXmlContent);
        var desc = doc.Descendants().FirstOrDefault(el => el.Name.LocalName == "Description");
        
        Assert.That(desc, Is.Not.Null);
        Assert.That(desc.Attribute(XNamespace.Get("http://other.com/") + "CustomField")?.Value, Is.EqualTo("custom_value"));
        Assert.That(desc.Attribute(XNamespace.Get("http://ns.adobe.com/xap/1.0/") + "Rating")?.Value, Is.EqualTo("3"));
    }

    [Test]
    public async Task CreateXmpForAlbumAsync_ShouldUseLegacyDatabaseData() {
        // Arrange
        var album = new Album { Id = 100, Name = "TestAlbum", Uuid = "test-uuid" };
        _context.Nodes.Add(album);
        await _context.SaveChangesAsync();

        var pic1 = new Picture {
            Id = 200,
            Name = "Pic1",
            ParentId = album.Id,
            Type = NodeType.Picture,
            CapturedAt = new DateTime(2026, 6, 30),
            Extension = ".NEF"
        };
        _context.Nodes.Add(pic1);
        await _context.SaveChangesAsync();

        // Write directly to SQLite legacy columns since EF Core [NotMapped] ignores them now
        var connection = _context.Database.GetDbConnection();
        var originalState = connection.State;
        if (originalState != System.Data.ConnectionState.Open) {
            await connection.OpenAsync();
        }
        using (var command = connection.CreateCommand()) {
            command.CommandText = @"
                ALTER TABLE pictures ADD COLUMN CurationStatus TEXT;
                ALTER TABLE pictures ADD COLUMN ColorLabel TEXT;
                ALTER TABLE pictures ADD COLUMN Rating INTEGER;
                
                UPDATE pictures 
                SET CurationStatus = 'Flagged', ColorLabel = 'Blue', Rating = 5 
                WHERE Id = 200";
            await command.ExecuteNonQueryAsync();
        }
        if (originalState != System.Data.ConnectionState.Open) {
            await connection.CloseAsync();
        }

        var rawPath = @"C:\Library\test-uuid\RAWs\Pic1.NEF";
        var xmpPath = @"C:\Library\test-uuid\RAWs\Pic1.xmp";
        _mockFileSystem.AddDirectory(@"C:\Library\test-uuid\RAWs");

        _mockPathService.Setup(s => s.PopulatePaths(It.IsAny<IEnumerable<Picture>>()))
            .Callback<IEnumerable<Picture>>(pics => {
                foreach (var pic in pics) {
                    pic.SubFolder = new SubFolder {
                        Raw = rawPath
                    };
                }
            });

        // Act
        await _xmpService.CreateXmpForAlbumAsync(album.Id);

        // Assert XMP file was created
        Assert.That(_mockFileSystem.File.Exists(xmpPath), Is.True);

        // Load metadata from created file
        var testPic = new Picture {
            Name = "Pic1",
            SubFolder = new SubFolder {
                Raw = rawPath
            }
        };
        await _xmpService.LoadMetadataAsync(testPic);

        Assert.Multiple(() => {
            Assert.That(testPic.Rating, Is.EqualTo(5));
            Assert.That(testPic.ColorLabel, Is.EqualTo(ColorLabel.Blue));
            Assert.That(testPic.CurationStatus, Is.EqualTo(CurationStatus.Flagged));
        });
    }
}
