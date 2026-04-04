using Database.Domain.Entities;
using Database.Domain.Interfaces;
using Database.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Database.Test;

[TestFixture]
public class PictureGroupingServiceTests {
    private Mock<IPictureRepository> _repositoryMock;
    private Mock<ILogger<PictureGroupingService>> _loggerMock;
    private PictureGroupingService _service;

    [SetUp]
    public void SetUp() {
        _repositoryMock = new Mock<IPictureRepository>();
        _loggerMock = new Mock<ILogger<PictureGroupingService>>();
        _service = new PictureGroupingService(_repositoryMock.Object, _loggerMock.Object);
    }

    [Test]
    public async Task EmptySet_ReturnsEmptyList() {
        // Arrange
        _repositoryMock.Setup(r => r.FindByHierarchyIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<Picture>());

        // Act
        var result = await _service.GroupSimilarPicturesAsync(1, 5);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task NoPHash_Skipped() {
        // Arrange
        var pictures = new List<Picture> {
            new() { Id = 1, Name = "NoMetrics", Metrics = null },
            new() { Id = 2, Name = "NullHash", Metrics = new Metrics { PHash = null } }
        };
        _repositoryMock.Setup(r => r.FindByHierarchyIdAsync(1))
            .ReturnsAsync(pictures);

        // Act
        var result = await _service.GroupSimilarPicturesAsync(1, 5);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task IdenticalHashes_SingleGroup() {
        // Arrange
        var hash = 0xAL;
        var pictures = new List<Picture> {
            new() { Id = 1, Metrics = new Metrics { PHash = 0xAL } },
            new() { Id = 2, Metrics = new Metrics { PHash = 0xAL } },
            new() { Id = 3, Metrics = new Metrics { PHash = 0xAL } }
        };
        _repositoryMock.Setup(r => r.FindByHierarchyIdAsync(1))
            .ReturnsAsync(pictures);

        // Act
        var result = await _service.GroupSimilarPicturesAsync(1, 0);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Has.Count.EqualTo(3));
    }

    [Test]
    public async Task DistinctHashes_MultipleGroups() {
        // Arrange
        var pictures = new List<Picture> {
            new() { Id = 1, Metrics = new Metrics { PHash = 0x0000UL } },
            new() { Id = 2, Metrics = new Metrics { PHash = 0xFFFFUL } }
        };
        _repositoryMock.Setup(r => r.FindByHierarchyIdAsync(1))
            .ReturnsAsync(pictures);

        // Act
        var result = await _service.GroupSimilarPicturesAsync(1, 5);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0], Has.Count.EqualTo(1));
        Assert.That(result[1], Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ThresholdSensitivity_BelowThreshold_GroupsTogether() {
        // Arrange
        // 0x1111 = 0001 0001 0001 0001
        // 0x1113 = 0001 0001 0001 0011
        // Distance is 1 (only the second to last bit differs)
        var pictures = new List<Picture> {
            new() { Id = 1, Metrics = new Metrics { PHash = 0x1111UL } },
            new() { Id = 2, Metrics = new Metrics { PHash = 0x1113UL } }
        };
        _repositoryMock.Setup(r => r.FindByHierarchyIdAsync(1))
            .ReturnsAsync(pictures);

        // Act
        var result = await _service.GroupSimilarPicturesAsync(1, 2);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ThresholdSensitivity_AboveThreshold_SeparateGroups() {
        // Arrange
        // 0x1111 = 0001 0001 0001 0001
        // 0x111F = 0001 0001 0001 1111
        // Bits in F are 1111, in 1 is 0001. Diff is 3 bits.
        var pictures = new List<Picture> {
            new() { Id = 1, Metrics = new Metrics { PHash = 0x1111UL } },
            new() { Id = 2, Metrics = new Metrics { PHash = 0x111FUL } }
        };
        _repositoryMock.Setup(r => r.FindByHierarchyIdAsync(1))
            .ReturnsAsync(pictures);

        // Act
        var result = await _service.GroupSimilarPicturesAsync(1, 2);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ComplexGrouping_VerifiesMathematicalCorrectness() {
        // Arrange
        var pictures = new List<Picture> {
            new() { Id = 1, Metrics = new Metrics { PHash = 0b0001UL } },
            new() { Id = 2, Metrics = new Metrics { PHash = 0b0011UL } }, // Dist to 1 is 1
            new() { Id = 3, Metrics = new Metrics { PHash = 0b0111UL } }, // Dist to 2 is 1, Dist to 1 is 2
            new() { Id = 4, Metrics = new Metrics { PHash = 0b1000UL } }  // Dist to all is high
        };
        _repositoryMock.Setup(r => r.FindByHierarchyIdAsync(1))
            .ReturnsAsync(pictures);

        // Threshold 1: 
        // 1 added to G1
        // 2 added to G1 (Dist 1 to 1)
        // 3: Dist to 1 is 2 (>1), Dist to 2 is 1 (<=1). BUT service logic says MUST be similar to ALL members.
        // Wait, the prompt says: "A picture joins a group only if its Hamming Distance is <= threshold compared to all members of that group."
        // So 3 vs {1, 2}: 3 vs 1 is 2. 2 > 1. So 3 cannot join G1. Starts G2.
        // 4 starts G3.

        // Act
        var result = await _service.GroupSimilarPicturesAsync(1, 1);

        // Assert
        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result[0].Select(p => p.Id), Is.EquivalentTo(new[] { 1, 2 }));
        Assert.That(result[1].Select(p => p.Id), Is.EquivalentTo(new[] { 3 }));
        Assert.That(result[2].Select(p => p.Id), Is.EquivalentTo(new[] { 4 }));
    }
}
