using System.IO.Abstractions.TestingHelpers;
using Database.Domain.Entities;
using Domain.Enums;
using ErrorOr;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Services;
using Graph.Infrastructure.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PictureWorker.Domain.Interfaces;

namespace Graph.Test;

[TestFixture]
public class PictureOrientationResolverTests {
    private const string RawPath = @"C:\RAWs\Pic.NEF";

    private MockFileSystem _mockFileSystem;
    private Mock<IPictureAnalyzer> _mockAnalyzer;
    private Mock<IXmpService> _mockXmpService;

    [SetUp]
    public void Setup() {
        _mockFileSystem = new MockFileSystem();
        _mockFileSystem.AddFile(RawPath, new MockFileData("raw"));
        _mockAnalyzer = new Mock<IPictureAnalyzer>();
        _mockXmpService = new Mock<IXmpService>();
    }

    private static Picture CreatePicture() => new() {
        Name = "Pic",
        SubFolder = new SubFolder { Raw = RawPath }
    };

    [Test]
    public async Task EnsureAsync_WhenAlreadyVerifiedAgainstExif_DoesNotTouchFileOrXmp() {
        var pic = CreatePicture();
        pic.Orientation = Orientation.Portrait;
        pic.RawExifOrientation = 8;
        pic.OrientationFromExif = true;

        var changed = await PictureOrientationResolver.EnsureAsync(pic, _mockAnalyzer.Object, _mockXmpService.Object, _mockFileSystem);

        Assert.That(changed, Is.False);
        _mockAnalyzer.Verify(a => a.ExtractOrientationAsync(It.IsAny<string>()), Times.Never);
        _mockXmpService.Verify(x => x.SaveMetadataAsync(It.IsAny<Picture>()), Times.Never);
    }

    [Test]
    public async Task EnsureAsync_LegacyGuessedCode_IsCorrectedFromExifAndMarked() {
        // XMP written by the first orientation version: "6" guessed from dimensions, no marker
        var pic = CreatePicture();
        pic.Orientation = Orientation.Portrait;
        pic.RawExifOrientation = 6;
        _mockAnalyzer.Setup(a => a.ExtractOrientationAsync(RawPath)).ReturnsAsync(8);

        var changed = await PictureOrientationResolver.EnsureAsync(pic, _mockAnalyzer.Object, _mockXmpService.Object, _mockFileSystem);

        Assert.Multiple(() => {
            Assert.That(changed, Is.True);
            Assert.That(pic.RawExifOrientation, Is.EqualTo(8));
            Assert.That(pic.Orientation, Is.EqualTo(Orientation.Portrait));
            Assert.That(pic.OrientationFromExif, Is.True);
        });
        _mockXmpService.Verify(x => x.SaveMetadataAsync(pic), Times.Once);
    }

    [Test]
    public async Task EnsureAsync_UnmarkedButMatchingCode_IsSavedOnceToAddMarker() {
        var pic = CreatePicture();
        pic.Orientation = Orientation.Landscape;
        pic.RawExifOrientation = 1;
        _mockAnalyzer.Setup(a => a.ExtractOrientationAsync(RawPath)).ReturnsAsync(1);

        await PictureOrientationResolver.EnsureAsync(pic, _mockAnalyzer.Object, _mockXmpService.Object, _mockFileSystem);

        Assert.That(pic.OrientationFromExif, Is.True);
        _mockXmpService.Verify(x => x.SaveMetadataAsync(pic), Times.Once);
    }

    [Test]
    public async Task EnsureAsync_NoOrientationInXmp_IsCalculatedFromExif() {
        // Album never opened since orientation support was added
        var pic = CreatePicture();
        _mockAnalyzer.Setup(a => a.ExtractOrientationAsync(RawPath)).ReturnsAsync(6);

        await PictureOrientationResolver.EnsureAsync(pic, _mockAnalyzer.Object, _mockXmpService.Object, _mockFileSystem);

        Assert.Multiple(() => {
            Assert.That(pic.Orientation, Is.EqualTo(Orientation.Portrait));
            Assert.That(pic.RawExifOrientation, Is.EqualTo(6));
            Assert.That(pic.OrientationFromExif, Is.True);
        });
    }

    [Test]
    public async Task EnsureAsync_ExifReadFails_KeepsKnownOrientationWithoutMarker() {
        var pic = CreatePicture();
        pic.Orientation = Orientation.Portrait;
        pic.RawExifOrientation = 6;
        _mockAnalyzer.Setup(a => a.ExtractOrientationAsync(RawPath)).ReturnsAsync((ErrorOr<int>)Error.Failure());

        var changed = await PictureOrientationResolver.EnsureAsync(pic, _mockAnalyzer.Object, _mockXmpService.Object, _mockFileSystem);

        Assert.Multiple(() => {
            Assert.That(changed, Is.False);
            Assert.That(pic.RawExifOrientation, Is.EqualTo(6), "A failed read must not be treated as EXIF code 1.");
            Assert.That(pic.OrientationFromExif, Is.False);
        });
        _mockXmpService.Verify(x => x.SaveMetadataAsync(It.IsAny<Picture>()), Times.Never);
    }

    [Test]
    public async Task EnsureAsync_ExifReadFailsAndUnknown_FallsBackToDimensionsWithoutMarker() {
        var pic = CreatePicture();
        pic.Width = 4000;
        pic.Height = 6000;
        _mockAnalyzer.Setup(a => a.ExtractOrientationAsync(RawPath)).ReturnsAsync((ErrorOr<int>)Error.Failure());

        var changed = await PictureOrientationResolver.EnsureAsync(pic, _mockAnalyzer.Object, _mockXmpService.Object, _mockFileSystem);

        Assert.Multiple(() => {
            Assert.That(changed, Is.True);
            Assert.That(pic.Orientation, Is.EqualTo(Orientation.Portrait));
            Assert.That(pic.OrientationFromExif, Is.False);
        });
        _mockXmpService.Verify(x => x.SaveMetadataAsync(pic), Times.Once);
    }

    [Test]
    public async Task EnsureAsync_WithRealXmp_ReadsExifOnlyOnFirstLoad() {
        var services = new ServiceCollection().BuildServiceProvider();
        var xmpService = new XmpService(_mockFileSystem, services.GetRequiredService<IServiceScopeFactory>(), new Mock<IPathService>().Object);
        _mockAnalyzer.Setup(a => a.ExtractOrientationAsync(RawPath)).ReturnsAsync(8);

        // First album load
        var first = CreatePicture();
        await xmpService.LoadMetadataAsync(first);
        await PictureOrientationResolver.EnsureAsync(first, _mockAnalyzer.Object, xmpService, _mockFileSystem);

        // Second album load
        var second = CreatePicture();
        await xmpService.LoadMetadataAsync(second);
        var changed = await PictureOrientationResolver.EnsureAsync(second, _mockAnalyzer.Object, xmpService, _mockFileSystem);

        Assert.Multiple(() => {
            Assert.That(changed, Is.False);
            Assert.That(second.Orientation, Is.EqualTo(Orientation.Portrait));
            Assert.That(second.RawExifOrientation, Is.EqualTo(8));
            Assert.That(second.OrientationFromExif, Is.True);
        });
        _mockAnalyzer.Verify(a => a.ExtractOrientationAsync(RawPath), Times.Once);
    }
}
