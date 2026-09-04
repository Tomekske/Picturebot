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

    [Test]
    public async Task LoadMetadataAsync_WhenXmpDMPropertiesPresent_ShouldMapXmpDMCorrectly() {
        // Arrange
        var rawPath = @"C:\RAWs\PicDM.NEF";
        var xmpPath = @"C:\RAWs\PicDM.xmp";
        _mockFileSystem.AddDirectory(@"C:\RAWs");

        var xmpContent = @"<?xpacket begin='﻿' id='W5M0MpCehiHzreSzNTczkc9d'?>
<x:xmpmeta xmlns:x='adobe:ns:meta/'>
 <rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'>
  <rdf:Description rdf:about=''
    xmlns:xmp='http://ns.adobe.com/xap/1.0/'
    xmlns:xmpDM='http://ns.adobe.com/xmp/1.0/DynamicMedia/'
    xmp:Rating='3'
    xmpDM:pick='1'>
  </rdf:Description>
 </rdf:RDF>
</x:xmpmeta>";
        _mockFileSystem.AddFile(xmpPath, xmpContent);

        var picture = new Picture {
            Name = "PicDM",
            SubFolder = new SubFolder {
                Raw = rawPath
            }
        };

        // Act
        await _xmpService.LoadMetadataAsync(picture);

        // Assert
        Assert.Multiple(() => {
            Assert.That(picture.Rating, Is.EqualTo(3));
            Assert.That(picture.CurationStatus, Is.EqualTo(CurationStatus.Flagged));
        });
    }

    [Test]
    public async Task LoadMetadataAsync_CurationStates_ShouldMapCorrectly() {
        var xmpDM = XNamespace.Get("http://ns.adobe.com/xmp/1.0/DynamicMedia/");
        
        // 1. Picked
        var xmlPicked = $@"<x:xmpmeta xmlns:x='adobe:ns:meta/'><rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'><rdf:Description rdf:about='' xmlns:xmpDM='{xmpDM.NamespaceName}' xmpDM:pick='1'/></rdf:RDF></x:xmpmeta>";
        _mockFileSystem.AddFile(@"C:\RAWs\Picked.xmp", xmlPicked);
        var picPicked = new Picture { SubFolder = new SubFolder { Raw = @"C:\RAWs\Picked.NEF" } };
        await _xmpService.LoadMetadataAsync(picPicked);
        Assert.That(picPicked.CurationStatus, Is.EqualTo(CurationStatus.Flagged));

        // 2. Unflagged
        var xmlUnflagged = $@"<x:xmpmeta xmlns:x='adobe:ns:meta/'><rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'><rdf:Description rdf:about='' xmlns:xmpDM='{xmpDM.NamespaceName}' xmpDM:pick='0'/></rdf:RDF></x:xmpmeta>";
        _mockFileSystem.AddFile(@"C:\RAWs\Unflagged.xmp", xmlUnflagged);
        var picUnflagged = new Picture { SubFolder = new SubFolder { Raw = @"C:\RAWs\Unflagged.NEF" } };
        await _xmpService.LoadMetadataAsync(picUnflagged);
        Assert.That(picUnflagged.CurationStatus, Is.EqualTo(CurationStatus.Unflagged));

        // 3. Rejected
        var xmlRejected = $@"<x:xmpmeta xmlns:x='adobe:ns:meta/'><rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'><rdf:Description rdf:about='' xmlns:xmpDM='{xmpDM.NamespaceName}' xmpDM:pick='-1'/></rdf:RDF></x:xmpmeta>";
        _mockFileSystem.AddFile(@"C:\RAWs\Rejected.xmp", xmlRejected);
        var picRejected = new Picture { SubFolder = new SubFolder { Raw = @"C:\RAWs\Rejected.NEF" } };
        await _xmpService.LoadMetadataAsync(picRejected);
        Assert.That(picRejected.CurationStatus, Is.EqualTo(CurationStatus.Rejected));
    }



    [Test]
    public async Task SaveMetadataAsync_ShouldCleanPhotoshopUrgencyAndMapXmpDMStates() {
        var xmpDM = XNamespace.Get("http://ns.adobe.com/xmp/1.0/DynamicMedia/");
        var photoshop = XNamespace.Get("http://ns.adobe.com/photoshop/1.0/");

        // 1. Save Picked
        var picPicked = new Picture { CurationStatus = CurationStatus.Flagged, SubFolder = new SubFolder { Raw = @"C:\RAWs\SavePicked.NEF" } };
        var xmlPickedInitial = @"<x:xmpmeta xmlns:x='adobe:ns:meta/'><rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'><rdf:Description rdf:about='' xmlns:photoshop='http://ns.adobe.com/photoshop/1.0/' photoshop:Urgency='5'/></rdf:RDF></x:xmpmeta>";
        _mockFileSystem.AddFile(@"C:\RAWs\SavePicked.xmp", xmlPickedInitial);
        await _xmpService.SaveMetadataAsync(picPicked);

        var savedXml1 = _mockFileSystem.File.ReadAllText(@"C:\RAWs\SavePicked.xmp");
        var doc1 = XDocument.Parse(savedXml1);
        var desc1 = doc1.Descendants().First(e => e.Name.LocalName == "Description");
        Assert.That(desc1.Attribute(xmpDM + "pick")?.Value, Is.EqualTo("1"));
        Assert.That(desc1.Attribute(xmpDM + "good")?.Value, Is.EqualTo("true"));
        Assert.That(desc1.Attribute(photoshop + "Urgency"), Is.Null);

        // 2. Save Unflagged
        var picUnflagged = new Picture { CurationStatus = CurationStatus.Unflagged, SubFolder = new SubFolder { Raw = @"C:\RAWs\SaveUnflagged.NEF" } };
        await _xmpService.SaveMetadataAsync(picUnflagged);
        var savedXml2 = _mockFileSystem.File.ReadAllText(@"C:\RAWs\SaveUnflagged.xmp");
        var doc2 = XDocument.Parse(savedXml2);
        var desc2 = doc2.Descendants().First(e => e.Name.LocalName == "Description");
        Assert.That(desc2.Attribute(xmpDM + "pick")?.Value, Is.EqualTo("0"));
        Assert.That(desc2.Attribute(xmpDM + "good"), Is.Null);

        // 3. Save Rejected
        var picRejected = new Picture { CurationStatus = CurationStatus.Rejected, SubFolder = new SubFolder { Raw = @"C:\RAWs\SaveRejected.NEF" } };
        await _xmpService.SaveMetadataAsync(picRejected);
        var savedXml3 = _mockFileSystem.File.ReadAllText(@"C:\RAWs\SaveRejected.xmp");
        var doc3 = XDocument.Parse(savedXml3);
        var desc3 = doc3.Descendants().First(e => e.Name.LocalName == "Description");
        Assert.That(desc3.Attribute(xmpDM + "pick")?.Value, Is.EqualTo("-1"));
        Assert.That(desc3.Attribute(xmpDM + "good")?.Value, Is.EqualTo("false"));
    }

    [Test]
    public async Task SaveMetadataAsync_ShouldRemoveDuplicateElementsAndKeepAttributes() {
        var xmp = XNamespace.Get("http://ns.adobe.com/xap/1.0/");
        var xmpPath = @"C:\RAWs\SaveDuplicateElements.xmp";
        var xml = @"<x:xmpmeta xmlns:x='adobe:ns:meta/'><rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'><rdf:Description rdf:about='' xmlns:xmp='http://ns.adobe.com/xap/1.0/'><xmp:Rating>3</xmp:Rating><xmp:Label>Blue</xmp:Label></rdf:Description></rdf:RDF></x:xmpmeta>";
        _mockFileSystem.AddFile(xmpPath, xml);
        var picture = new Picture { Rating = 5, ColorLabel = ColorLabel.Red, SubFolder = new SubFolder { Raw = @"C:\RAWs\SaveDuplicateElements.NEF" } };

        await _xmpService.SaveMetadataAsync(picture);

        var savedXml = _mockFileSystem.File.ReadAllText(xmpPath);
        var doc = XDocument.Parse(savedXml);
        var desc = doc.Descendants().First(e => e.Name.LocalName == "Description");

        // The child elements should be removed
        Assert.That(desc.Element(xmp + "Rating"), Is.Null);
        Assert.That(desc.Element(xmp + "Label"), Is.Null);

        // The attributes should be set correctly
        Assert.That(desc.Attribute(xmp + "Rating")?.Value, Is.EqualTo("5"));
        Assert.That(desc.Attribute(xmp + "Label")?.Value, Is.EqualTo("Red"));
    }

    [Test]
    public async Task SaveAndLoad_Keywords_ShouldPreserveFlatAndHierarchicalStructure() {
        // Arrange
        var rawPath = @"C:\RAWs\PicKeywords.NEF";
        var xmpPath = @"C:\RAWs\PicKeywords.xmp";
        _mockFileSystem.AddDirectory(@"C:\RAWs");

        var picture = new Picture {
            Name = "PicKeywords",
            Keywords = new List<string> { "vacation", "subject/animal/horse", "subject|nature|forest" },
            SubFolder = new SubFolder {
                Raw = rawPath
            }
        };

        // Act
        await _xmpService.SaveMetadataAsync(picture);

        // Assert XMP contents directly
        var savedXml = _mockFileSystem.File.ReadAllText(xmpPath);
        var doc = XDocument.Parse(savedXml);
        var desc = doc.Descendants().First(e => e.Name.LocalName == "Description");

        var rdf = XNamespace.Get("http://www.w3.org/1999/02/22-rdf-syntax-ns#");
        var dcNamespace = XNamespace.Get("http://purl.org/dc/elements/1.1/");
        var lrNamespace = XNamespace.Get("http://ns.adobe.com/lightroom/1.0/");

        var dcSubject = desc.Element(dcNamespace + "subject");
        Assert.That(dcSubject, Is.Not.Null);
        var dcLis = dcSubject.Element(rdf + "Bag").Elements(rdf + "li").Select(l => l.Value).ToList();
        
        // Flattened segments
        Assert.That(dcLis, Contains.Item("vacation"));
        Assert.That(dcLis, Contains.Item("subject"));
        Assert.That(dcLis, Contains.Item("animal"));
        Assert.That(dcLis, Contains.Item("horse"));
        Assert.That(dcLis, Contains.Item("nature"));
        Assert.That(dcLis, Contains.Item("forest"));

        var lrHierarchical = desc.Element(lrNamespace + "hierarchicalSubject");
        Assert.That(lrHierarchical, Is.Not.Null);
        var lrLis = lrHierarchical.Element(rdf + "Bag").Elements(rdf + "li").Select(l => l.Value).ToList();
        
        // Normalized hierarchical paths
        Assert.That(lrLis, Contains.Item("vacation"));
        Assert.That(lrLis, Contains.Item("subject|animal|horse"));
        Assert.That(lrLis, Contains.Item("subject|nature|forest"));

        // Test loading back
        var pictureLoad = new Picture {
            Name = "PicKeywords",
            SubFolder = new SubFolder {
                Raw = rawPath
            }
        };

        await _xmpService.LoadMetadataAsync(pictureLoad);
        Assert.That(pictureLoad.Keywords, Contains.Item("vacation"));
        Assert.That(pictureLoad.Keywords, Contains.Item("subject|animal|horse"));
        Assert.That(pictureLoad.Keywords, Contains.Item("subject|nature|forest"));
    }

    [Test]
    public void KeywordsFiltering_ShouldMatchCorrectlyBasedOnAnyAndAllOperators() {
        // Arrange
        var pic1 = new Picture { Keywords = new List<string> { "landscape", "summer" } };
        var pic2 = new Picture { Keywords = new List<string> { "portrait", "summer", "black|white" } };
        var pic3 = new Picture { Keywords = new List<string> { "portrait", "winter" } };

        var pictures = new List<Picture> { pic1, pic2, pic3 };

        // Test case 1: OR (ANY) matching "summer" and "portrait" -> should match pic1, pic2, pic3
        var filterTagsAny = new List<string> { "summer", "portrait" };
        var matchedAny = pictures.Where(p => filterTagsAny.Any(tag => p.Keywords.Contains(tag, StringComparer.OrdinalIgnoreCase))).ToList();
        Assert.That(matchedAny, Has.Count.EqualTo(3));

        // Test case 2: AND (ALL) matching "summer" and "portrait" -> should only match pic2
        var filterTagsAll = new List<string> { "summer", "portrait" };
        var matchedAll = pictures.Where(p => filterTagsAll.All(tag => p.Keywords.Contains(tag, StringComparer.OrdinalIgnoreCase))).ToList();
        Assert.Multiple(() => {
            Assert.That(matchedAll, Has.Count.EqualTo(1));
            Assert.That(matchedAll[0].Keywords, Contains.Item("black|white"));
        });
    }
}
