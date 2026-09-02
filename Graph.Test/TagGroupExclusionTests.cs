using System.Collections.ObjectModel;
using System.Text.Json;
using Database.Domain.Entities;
using Database.Infrastructure.Data;
using Domain.Interfaces;
using Domain.Models;
using Graph.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Graph.Test;

[TestFixture]
public class TagGroupExclusionTests : IDisposable {
    private ApplicationDbContext _context = null!;
    private SqliteConnection _connection = null!;
    private IServiceScopeFactory _scopeFactory = null!;
    private Mock<ISettingsService> _mockSettingsService = null!;

    [SetUp]
    public void Setup() {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _mockSettingsService = new Mock<ISettingsService>();

        var services = new ServiceCollection();
        services.AddSingleton(_context);
        services.AddSingleton(_mockSettingsService.Object);

        var provider = services.BuildServiceProvider();
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
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
    public void ContainsExcludedTag_IdentifiesExcludedWorkflowTagsAccurately() {
        var excludedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "To Print",
            "Needs Retouching"
        };

        // Standard tags only -> Not excluded
        var pic1Keywords = new List<string> { "Nature", "Mountain", "Landscape" };
        Assert.That(GlobalExemplarCentroidService.ContainsExcludedTag(pic1Keywords, excludedTags), Is.False);

        // Mixed with exact excluded tag -> Excluded
        var pic2Keywords = new List<string> { "Nature", "To Print" };
        Assert.That(GlobalExemplarCentroidService.ContainsExcludedTag(pic2Keywords, excludedTags), Is.True);

        // Mixed with hierarchical excluded tag -> Excluded
        var pic3Keywords = new List<string> { "Workflow|Needs Retouching", "Portrait" };
        Assert.That(GlobalExemplarCentroidService.ContainsExcludedTag(pic3Keywords, excludedTags), Is.True);

        // Formatted hierarchy with › delimiter -> Excluded
        var pic4Keywords = new List<string> { "Workflow › To Print" };
        Assert.That(GlobalExemplarCentroidService.ContainsExcludedTag(pic4Keywords, excludedTags), Is.True);

        // Empty keywords -> Not excluded
        Assert.That(GlobalExemplarCentroidService.ContainsExcludedTag(new List<string>(), excludedTags), Is.False);
    }

    [Test]
    public async Task GetActiveLeafCentroidsAsync_ExcludesPicturesWithWorkflowTagsFromTraining() {
        // Arrange
        var dogTagId = Guid.NewGuid();
        var printTagId = Guid.NewGuid();

        var masterTags = new List<Tag> {
            new() { Id = dogTagId, Name = "Dog" },
            new() { Id = printTagId, Name = "To Print" }
        };

        var tagGroups = new List<TagGroup> {
            new() {
                GroupId = Guid.NewGuid(),
                GroupName = "Workflow",
                ExcludeFromTraining = true,
                TagIds = new ObservableCollection<Guid> { printTagId }
            },
            new() {
                GroupId = Guid.NewGuid(),
                GroupName = "Animals",
                ExcludeFromTraining = false,
                TagIds = new ObservableCollection<Guid> { dogTagId }
            }
        };

        var settings = new SettingsModel {
            MasterTags = masterTags,
            TagGroups = tagGroups
        };

        _mockSettingsService.Setup(s => s.Current).Returns(settings);

        // Seed 10 pictures with "Dog" tag (valid exemplars)
        // Seed 5 pictures with "Dog" + "To Print" tag (should be excluded)
        var dummyEmbedding = new float[512];
        dummyEmbedding[0] = 1.0f; // Unit vector on dim 0
        var embeddingBytes = new byte[512 * sizeof(float)];
        Buffer.BlockCopy(dummyEmbedding, 0, embeddingBytes, 0, embeddingBytes.Length);

        for (var i = 1; i <= 10; i++) {
            var pic = new Picture {
                Id = i,
                Name = $"ValidDog_{i}",
                KeywordsJson = JsonSerializer.Serialize(new[] { "Dog" }),
                Metrics = new Metrics { Embedding = embeddingBytes }
            };
            _context.Pictures.Add(pic);
        }

        for (var i = 11; i <= 15; i++) {
            var pic = new Picture {
                Id = i,
                Name = $"ContaminatedDog_{i}",
                KeywordsJson = JsonSerializer.Serialize(new[] { "Dog", "To Print" }),
                Metrics = new Metrics { Embedding = embeddingBytes }
            };
            _context.Pictures.Add(pic);
        }

        await _context.SaveChangesAsync();

        var centroidService = new GlobalExemplarCentroidService(_scopeFactory) {
            MinimumExemplarThreshold = 10
        };

        // Act
        var centroids = await centroidService.GetActiveLeafCentroidsAsync();

        // Assert: 10 valid exemplars reached threshold, 5 contaminated pictures were excluded
        Assert.That(centroids, Does.ContainKey("Dog"));

        // If we bump threshold to 11, "Dog" won't be active because 5 contaminated images were filtered out
        centroidService.MinimumExemplarThreshold = 11;
        var centroidsHigherThreshold = await centroidService.GetActiveLeafCentroidsAsync();
        Assert.That(centroidsHigherThreshold, Does.Not.ContainKey("Dog"));
    }

    [Test]
    public void TagGroup_DefaultExcludeFromTraining_IsFalse() {
        var group = new TagGroup { GroupName = "Default" };
        Assert.That(group.ExcludeFromTraining, Is.False);
    }

    [Test]
    public void TagGroup_Serialization_RoundTrips_ExcludeFromTrainingProperty() {
        // Arrange
        var groups = new List<TagGroup> {
            new() {
                GroupId = Guid.NewGuid(),
                GroupName = "Standard Recognition",
                ExcludeFromTraining = false,
                TagIds = new ObservableCollection<Guid> { Guid.NewGuid(), Guid.NewGuid() }
            },
            new() {
                GroupId = Guid.NewGuid(),
                GroupName = "Workflow Excluded",
                ExcludeFromTraining = true,
                TagIds = new ObservableCollection<Guid> { Guid.NewGuid() }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(groups);
        var deserialized = JsonSerializer.Deserialize<List<TagGroup>>(json);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.Count, Is.EqualTo(2));

        var group1 = deserialized.FirstOrDefault(g => g.GroupName == "Standard Recognition");
        Assert.That(group1, Is.Not.Null);
        Assert.That(group1!.ExcludeFromTraining, Is.False);
        Assert.That(group1.TagIds.Count, Is.EqualTo(2));

        var group2 = deserialized.FirstOrDefault(g => g.GroupName == "Workflow Excluded");
        Assert.That(group2, Is.Not.Null);
        Assert.That(group2!.ExcludeFromTraining, Is.True);
        Assert.That(group2.TagIds.Count, Is.EqualTo(1));
    }
}
