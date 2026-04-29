using System.IO.Abstractions.TestingHelpers;
using Graph.Infrastructure.Utilities;
// Assumes CachedPictureData and FileGroup are here

namespace Graph.Test;

[TestFixture]
public class FileGrouperTests {
    private MockFileSystem _mockFileSystem;
    private FileGrouper _fileGrouper;

    [SetUp]
    public void Setup() {
        // MockFileSystem automatically handles cross-platform Path operations 
        // without needing to touch the physical disk.
        _mockFileSystem = new MockFileSystem();
        _fileGrouper = new FileGrouper(_mockFileSystem);
    }

    // Helper method to keep test arrangement clean
    private static CachedPictureData CreateCachedData(string path, DateTime time, ulong pHash) {
        return new CachedPictureData {
            FilePath = path,
            PrimaryDate = time,
            PHash = pHash
        };
    }

    [Test]
    public void GroupFiles_ShouldGroupByTimeAndHash_WhenWithinThreshold() {
        // Arrange
        var baseTime = new DateTime(2025, 1, 1, 12, 0, 0);

        // Hashes differ by exactly 1 bit (Hamming distance = 1), well within threshold
        var files = new List<CachedPictureData> {
            CreateCachedData(@"C:\Source\Seq_100.jpg", baseTime, 0b0000),
            CreateCachedData(@"C:\Source\Seq_101.jpg", baseTime.AddSeconds(1), 0b0001),
            CreateCachedData(@"C:\Source\Seq_102.jpg", baseTime.AddSeconds(2), 0b0011)
        };

        // Act
        var result = _fileGrouper.GroupFiles(files);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].FilePaths, Has.Count.EqualTo(3));
        Assert.That(result[0].BaseName, Does.StartWith("Burst_"));
    }

    [Test]
    public void GroupFiles_ShouldGroupExactNames_CaseInsensitive() {
        // Arrange
        var baseTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var files = new List<CachedPictureData> {
            CreateCachedData(@"C:\Source\IMG_001.JPG", baseTime, 0),
            CreateCachedData(@"C:\Source\img_001.cr2", baseTime, 0)
        };

        // Act
        var result = _fileGrouper.GroupFiles(files);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].BaseName, Is.EqualTo("IMG_001").IgnoreCase);
        Assert.That(result[0].FilePaths, Has.Count.EqualTo(2));
    }

    [Test]
    public void GroupFiles_ShouldGroupExplicitBurstPatterns() {
        // Arrange
        var baseTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var files = new List<CachedPictureData> {
            CreateCachedData(@"C:\Source\Photo_BURST1.jpg", baseTime, 0),
            CreateCachedData(@"C:\Source\Photo_BURST2.jpg", baseTime, 0),
            CreateCachedData(@"C:\Source\Event-1.jpg", baseTime, 0),
            CreateCachedData(@"C:\Source\Event-2.jpg", baseTime, 0)
        };

        // Act
        var result = _fileGrouper.GroupFiles(files);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2), "Should create two distinct pattern groups");

        var burstGroup = result.First(g => g.BaseName == "Photo");
        Assert.That(burstGroup.FilePaths, Has.Count.EqualTo(2));

        var eventGroup = result.First(g => g.BaseName == "Event");
        Assert.That(eventGroup.FilePaths, Has.Count.EqualTo(2));
    }

    [Test]
    public void GroupFiles_ShouldHandleEmptyList() {
        // Arrange
        var files = new List<CachedPictureData>();

        // Act
        var result = _fileGrouper.GroupFiles(files);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GroupFiles_ShouldProcessWaterfallCorrectly_PatternThenTime() {
        // Arrange
        var baseTime = new DateTime(2025, 1, 1, 12, 0, 0);

        var files = new List<CachedPictureData> {
            // Pair 1: Should be caught by Pass 1 (Exact Match)
            CreateCachedData(@"C:\Source\Pair1.jpg", baseTime, 100),
            CreateCachedData(@"C:\Source\Pair1.raw", baseTime, 100),

            // Burst: Should be caught by Pass 2 (Time & Hash)
            CreateCachedData(@"C:\Source\Random_01.jpg", baseTime.AddMinutes(5), 0b1010),
            CreateCachedData(@"C:\Source\Random_02.jpg", baseTime.AddMinutes(5).AddSeconds(1), 0b1010)
        };

        // Act
        var result = _fileGrouper.GroupFiles(files);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));

        var patternGroup = result.FirstOrDefault(g => g.BaseName == "Pair1");
        Assert.That(patternGroup, Is.Not.Null);
        Assert.That(patternGroup!.FilePaths, Has.Count.EqualTo(2));

        var burstGroup = result.FirstOrDefault(g => g.BaseName.StartsWith("Burst_"));
        Assert.That(burstGroup, Is.Not.Null);
        Assert.That(burstGroup!.FilePaths, Has.Count.EqualTo(2));
    }

    [Test]
    public void GroupFiles_ShouldSplitBurst_WhenHashThresholdExceeded() {
        // Arrange
        var baseTime = new DateTime(2025, 1, 1, 12, 0, 0);

        // Time gap is within threshold (1 sec), but images are completely different (max hamming distance)
        var files = new List<CachedPictureData> {
            CreateCachedData(@"C:\Source\Seq_100.jpg", baseTime, 0),
            CreateCachedData(@"C:\Source\Seq_101.jpg", baseTime.AddSeconds(1), ulong.MaxValue)
        };

        // Act
        var result = _fileGrouper.GroupFiles(files);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public void GroupFiles_ShouldSplitBurst_WhenTimeThresholdExceeded() {
        // Arrange
        var baseTime = new DateTime(2025, 1, 1, 12, 0, 0);

        // Exact same hash, but time gap is > 2 seconds
        var files = new List<CachedPictureData> {
            CreateCachedData(@"C:\Source\Seq_100.jpg", baseTime, 0),
            CreateCachedData(@"C:\Source\Seq_101.jpg", baseTime.AddSeconds(3), 0)
        };

        // Act
        var result = _fileGrouper.GroupFiles(files);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].FilePaths, Has.Count.EqualTo(1));
        Assert.That(result[1].FilePaths, Has.Count.EqualTo(1));
    }
}
