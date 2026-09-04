using System;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Database.Domain.Entities;
using Database.Infrastructure.Data;
using Domain.Interfaces;
using Domain.Models;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using PictureWorker.Domain.Interfaces;
using PictureWorker.Infrastructure.Services;

namespace Graph.Test;

[TestFixture]
public class FewShotTagDiscoveryTests : IDisposable {
    private MockFileSystem _mockFileSystem;
    private Mock<IPathService> _mockPathService;
    private Mock<ISettingsService> _mockSettingsService;
    private ApplicationDbContext _context;
    private SqliteConnection _connection;
    private IServiceScopeFactory _scopeFactory;

    private TaxonomyService _taxonomyService;
    private GlobalExemplarCentroidService _centroidService;
    private ImageEmbeddingService _embeddingService;
    private XmpService _xmpService;
    private FewShotTagDiscoveryService _discoveryService;

    [SetUp]
    public void Setup() {
        _mockFileSystem = new MockFileSystem();
        _mockPathService = new Mock<IPathService>();
        _mockSettingsService = new Mock<ISettingsService>();

        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddSingleton(_context);
        var provider = services.BuildServiceProvider();
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        // Set up mock taxonomy tree: Animals -> Mammals -> Dog
        var animalNode = new HierarchyNode { Name = "Animals" };
        var mammalNode = new HierarchyNode { Name = "Mammals" };
        var dogNode = new HierarchyNode { Name = "Dog" };

        mammalNode.Children.Add(dogNode);
        animalNode.Children.Add(mammalNode);

        var settingsModel = new SettingsModel {
            HierarchyNodes = new List<HierarchyNode> { animalNode }
        };

        _mockSettingsService.Setup(s => s.Current).Returns(settingsModel);

        _taxonomyService = new TaxonomyService(_mockSettingsService.Object);
        _centroidService = new GlobalExemplarCentroidService(_scopeFactory);
        _embeddingService = new ImageEmbeddingService(_mockFileSystem, _scopeFactory);
        _xmpService = new XmpService(_mockFileSystem, _scopeFactory, _mockPathService.Object);
        _discoveryService = new FewShotTagDiscoveryService(
            _embeddingService,
            _centroidService,
            _taxonomyService,
            _xmpService,
            _scopeFactory
        );
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
    public void TaxonomyService_ResolvesAncestorChainAndHierarchy() {
        var ancestors = _taxonomyService.GetAncestorChain("Dog");
        Assert.That(ancestors, Is.EquivalentTo(new[] { "Animals", "Mammals" }));

        var fullPath = _taxonomyService.GetFullHierarchicalPath("Dog");
        Assert.That(fullPath, Is.EqualTo("Animals|Mammals|Dog"));

        var flatChain = _taxonomyService.ResolveTaxonomySubjectChain("Dog");
        Assert.That(flatChain, Is.EquivalentTo(new[] { "Animals", "Mammals", "Dog" }));
    }

    [Test]
    public async Task GlobalExemplarCentroidService_DeactivatesUnderThreshold_ActivatesAtThreshold() {
        _centroidService.MinimumExemplarThreshold = 3;

        // Seed 2 exemplars for leaf tag "Dog"
        var sameVec = new float[512];
        sameVec[0] = 1.0f;

        for (int i = 1; i <= 2; i++) {
            var pic = new Picture { Id = i, Name = $"DogPic_{i}", KeywordsJson = JsonSerializer.Serialize(new[] { "Dog" }) };
            var metrics = new Metrics { PictureId = i };
            metrics.SetEmbeddingVector(sameVec);
            pic.Metrics = metrics;

            _context.Pictures.Add(pic);
            _context.Metrics.Add(metrics);
        }
        await _context.SaveChangesAsync();

        var centroidsUnder = await _centroidService.GetActiveLeafCentroidsAsync();
        Assert.That(centroidsUnder.ContainsKey("Dog"), Is.False, "Should not be active when |E_T| < 3");

        // Add 3rd exemplar to meet threshold N = 3
        var pic3 = new Picture { Id = 3, Name = "DogPic_3", KeywordsJson = JsonSerializer.Serialize(new[] { "Dog" }) };
        var metrics3 = new Metrics { PictureId = 3 };
        metrics3.SetEmbeddingVector(sameVec);
        pic3.Metrics = metrics3;
        _context.Pictures.Add(pic3);
        _context.Metrics.Add(metrics3);
        await _context.SaveChangesAsync();

        var centroidsActive = await _centroidService.GetActiveLeafCentroidsAsync();
        Assert.That(centroidsActive.ContainsKey("Dog"), Is.True, "Should become active when |E_T| >= 3");
        Assert.That(centroidsActive["Dog"][0], Is.EqualTo(1.0f).Within(1e-5), "Centroid must be unit-normalized");
    }

    [Test]
    public async Task DynamicRecalculation_OnTagAddedAndRemoved_UpdatesCentroid() {
        _centroidService.MinimumExemplarThreshold = 2;
        var vec = new float[512];
        vec[10] = 1.0f;

        _centroidService.OnTagAdded(100, "Cat", vec);
        _centroidService.OnTagAdded(101, "Cat", vec);

        // Manually trigger check after dynamic addition
        var active = await _centroidService.GetActiveLeafCentroidsAsync();
        Assert.That(active.ContainsKey("Cat"), Is.True);

        // Remove tag from 101 -> count drops to 1 (< N = 2)
        _centroidService.OnTagRemoved(101, "Cat", vec);
        var activeAfterEviction = await _centroidService.GetActiveLeafCentroidsAsync();
        Assert.That(activeAfterEviction.ContainsKey("Cat"), Is.False, "Evicting exemplar drops active leaf tag below threshold");
    }

    [Test]
    public async Task FewShotTagDiscovery_MatchesTargetAndPreservesExistingXmpTags() {
        _centroidService.MinimumExemplarThreshold = 2;

        var dogVec = new float[512];
        dogVec[0] = 1.0f; // Unit vector along axis 0

        // Seed 2 dog exemplars in DB
        for (int i = 1; i <= 2; i++) {
            var p = new Picture { Id = i, Name = $"Exemplar_{i}", KeywordsJson = JsonSerializer.Serialize(new[] { "Dog" }) };
            var m = new Metrics { PictureId = i };
            m.SetEmbeddingVector(dogVec);
            p.Metrics = m;
            _context.Pictures.Add(p);
            _context.Metrics.Add(m);
        }
        await _context.SaveChangesAsync();

        // Create untagged target picture with matching embedding vector and existing custom XMP tags
        var targetPic = new Picture {
            Id = 5,
            Name = "UntaggedDog",
            Keywords = new List<string> { "Vacation2026" }
        };
        targetPic.SubFolder = new SubFolder { Raw = @"C:\Photos\Album1\UntaggedDog.JPG" };
        _mockFileSystem.AddFile(@"C:\Photos\Album1\UntaggedDog.xmp", new MockFileData("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:subject><rdf:Bag><rdf:li>Vacation2026</rdf:li></rdf:Bag></dc:subject></rdf:Description></rdf:RDF></x:xmpmeta>"));

        var targetMetrics = new Metrics { PictureId = 5 };
        targetMetrics.SetEmbeddingVector(dogVec);
        targetPic.Metrics = targetMetrics;

        var discoveryResults = await _discoveryService.ScanPicturesAsync(new List<Picture> { targetPic });

        Assert.That(discoveryResults.Count, Is.EqualTo(1));
        var result = discoveryResults[0];

        Assert.That(result.DiscoveredLeafTags, Contains.Item("Dog"));
        Assert.That(result.ResolvedFlatTags, Is.EquivalentTo(new[] { "Animals", "Mammals", "Dog" }));
        Assert.That(result.ResolvedHierarchicalTags, Is.EquivalentTo(new[] { "Animals|Mammals|Dog" }));

        // Non-destructive invariant: original "Vacation2026" tag must still be preserved
        Assert.That(targetPic.Keywords, Contains.Item("Vacation2026"));
        Assert.That(targetPic.Keywords, Contains.Item("Animals"));
        Assert.That(targetPic.Keywords, Contains.Item("Mammals"));
        Assert.That(targetPic.Keywords, Contains.Item("Dog"));
        Assert.That(targetPic.Keywords, Contains.Item("Animals|Mammals|Dog"));
    }

    [Test]
    public void ScanPicturesAsync_HandlesCancellation_Gracefully() {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var pic = new Picture { Id = 10, Name = "TestPic" };

        Assert.ThrowsAsync<OperationCanceledException>(async () => {
            await _discoveryService.ScanPicturesAsync(new List<Picture> { pic }, null, cts.Token);
        });
    }
}
