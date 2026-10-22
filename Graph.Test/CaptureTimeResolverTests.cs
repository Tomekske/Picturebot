using System.IO.Abstractions.TestingHelpers;
using Database.Domain.Entities;
using Domain.TimeZones;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Utilities;
using Moq;
using PictureWorker.Domain.Interfaces;

namespace Graph.Test;

[TestFixture]
public class CaptureTimeResolverTests {
    private const string RawPath = @"C:\RAWs\Pic.ARW";
    private const string JpgPath = @"C:\JPGs\Pic.jpg";

    private MockFileSystem _mockFileSystem;
    private Mock<IPictureAnalyzer> _mockAnalyzer;
    private Mock<IXmpService> _mockXmpService;

    [SetUp]
    public void Setup() {
        _mockFileSystem = new MockFileSystem();
        _mockFileSystem.AddFile(RawPath, new MockFileData("raw"));
        _mockFileSystem.AddFile(JpgPath, new MockFileData("jpg"));
        _mockAnalyzer = new Mock<IPictureAnalyzer>();
        _mockXmpService = new Mock<IXmpService>();
    }

    private static Picture CreatePicture() => new() {
        Name = "Pic",
        SubFolder = new SubFolder { Raw = RawPath, Preview = JpgPath }
    };

    [Test]
    public void ToLocal_UsesTheExifOffset() {
        // Sony A6600 on standard time all year: +01:00 even in May
        var local = CaptureTime.ToLocal(new DateTime(2025, 5, 11, 5, 36, 34), TimeSpan.FromHours(1),
            "Europe/Brussels", "Asia/Taipei");

        Assert.Multiple(() => {
            Assert.That(local.Local, Is.EqualTo(new DateTime(2025, 5, 11, 12, 36, 34)));
            Assert.That(local.ZoneId, Is.EqualTo("Asia/Taipei"));
            Assert.That(local.UtcOffset, Is.EqualTo(TimeSpan.FromHours(8)));
        });
    }

    [TestCase(2025, 1, 15, 21, TestName = "ToLocal_WithoutExif_Winter_Brussels+1")]
    [TestCase(2025, 7, 15, 20, TestName = "ToLocal_WithoutExif_Summer_Brussels+2")]
    public void ToLocal_WithoutExifOffset_ReadsTheCameraClockInItsZoneWithSummerTime(int year, int month, int day,
        int expectedHour) {
        var local = CaptureTime.ToLocal(new DateTime(year, month, day, 14, 0, 0), null, "Europe/Brussels", "Asia/Taipei");

        Assert.That(local.Local, Is.EqualTo(new DateTime(year, month, day, expectedHour, 0, 0)));
    }

    [Test]
    public void ToLocal_AppliesSummerTimeOfTheTargetZoneForThePicturesOwnDate() {
        // Camera at UTC+1; Miami is UTC-4 in October (summer time), UTC-5 in December
        var october = CaptureTime.ToLocal(new DateTime(2022, 10, 22, 0, 51, 23), TimeSpan.FromHours(1),
            "Europe/Brussels", "America/New_York");
        var december = CaptureTime.ToLocal(new DateTime(2022, 12, 22, 0, 51, 23), TimeSpan.FromHours(1),
            "Europe/Brussels", "America/New_York");

        Assert.That(october.Local, Is.EqualTo(new DateTime(2022, 10, 21, 19, 51, 23)));
        Assert.That(december.Local, Is.EqualTo(new DateTime(2022, 12, 21, 18, 51, 23)));
    }

    [Test]
    public void ToLocal_InTheSkippedHourAtTheStartOfSummerTime_ReadsTheClockAnHourLater() {
        // 02:30 doesn't exist in Brussels on 30 March 2025: the camera hadn't switched yet
        var local = CaptureTime.ToLocal(new DateTime(2025, 3, 30, 2, 30, 0), null, "Europe/Brussels", "Etc/UTC");

        Assert.That(local.Local, Is.EqualTo(new DateTime(2025, 3, 30, 1, 30, 0)));
    }

    [Test]
    public void ToLocal_WithoutATargetZone_ShowsTheCameraTime() {
        var cameraTime = new DateTime(2025, 5, 11, 5, 36, 34);

        var local = CaptureTime.ToLocal(cameraTime, TimeSpan.FromHours(1), "Europe/Brussels", null);

        Assert.That(local.Local, Is.EqualTo(cameraTime));
        Assert.That(local.ZoneId, Is.Null);
    }

    [TestCase("+01:00", 60)]
    [TestCase("-04:30", -270)]
    [TestCase("+00:00", 0)]
    public void ParseOffset_ReadsExifOffsets(string text, int minutes) {
        Assert.That(CaptureTime.ParseOffset(text), Is.EqualTo(TimeSpan.FromMinutes(minutes)));
        Assert.That(CaptureTime.FormatOffset(TimeSpan.FromMinutes(minutes)), Is.EqualTo(text));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("none")]
    [TestCase("01:00")]
    [TestCase("+25:00")]
    public void ParseOffset_RejectsAnythingElse(string? text) {
        Assert.That(CaptureTime.ParseOffset(text), Is.Null);
    }

    [Test]
    public void Apply_UsesTheAlbumZone_OrTheDefaultZoneWithoutOne() {
        var pic = CreatePicture();
        pic.CapturedAt = new DateTime(2022, 10, 22, 0, 51, 23);
        pic.CameraUtcOffset = TimeSpan.FromHours(1);

        CaptureTimeResolver.Apply(pic, "America/New_York", "Europe/Brussels");
        Assert.That(pic.LocalCapturedAt, Is.EqualTo(new DateTime(2022, 10, 21, 19, 51, 23)));
        Assert.That(pic.LocalTimeZoneId, Is.EqualTo("America/New_York"));

        CaptureTimeResolver.Apply(pic, null, "Europe/Brussels");
        Assert.That(pic.LocalCapturedAt, Is.EqualTo(new DateTime(2022, 10, 22, 1, 51, 23)));
        Assert.That(pic.LocalTimeZoneId, Is.EqualTo("Europe/Brussels"));
    }

    [Test]
    public async Task EnsureCameraOffsetAsync_ReadsTheJpgOnceAndSavesToXmp() {
        var pic = CreatePicture();
        _mockAnalyzer.Setup(a => a.ExtractUtcOffsetAsync(JpgPath)).ReturnsAsync(TimeSpan.FromHours(1));

        var changed = await CaptureTimeResolver.EnsureCameraOffsetAsync(pic, _mockAnalyzer.Object, _mockXmpService.Object, _mockFileSystem);

        Assert.Multiple(() => {
            Assert.That(changed, Is.True);
            Assert.That(pic.CameraUtcOffset, Is.EqualTo(TimeSpan.FromHours(1)));
            Assert.That(pic.CameraUtcOffsetChecked, Is.True);
        });
        _mockXmpService.Verify(x => x.SaveMetadataAsync(pic), Times.Once);
    }

    [Test]
    public async Task EnsureCameraOffsetAsync_WhenAlreadyChecked_DoesNotTouchTheFile() {
        var pic = CreatePicture();
        pic.CameraUtcOffsetChecked = true;

        var changed = await CaptureTimeResolver.EnsureCameraOffsetAsync(pic, _mockAnalyzer.Object, _mockXmpService.Object, _mockFileSystem);

        Assert.That(changed, Is.False);
        _mockAnalyzer.Verify(a => a.ExtractUtcOffsetAsync(It.IsAny<string>()), Times.Never);
        _mockXmpService.Verify(x => x.SaveMetadataAsync(It.IsAny<Picture>()), Times.Never);
    }

    [Test]
    public async Task EnsureCameraOffsetAsync_WithoutOffsetInExif_RemembersItWasChecked() {
        var pic = CreatePicture();
        _mockAnalyzer.Setup(a => a.ExtractUtcOffsetAsync(JpgPath)).ReturnsAsync((TimeSpan?)null);

        await CaptureTimeResolver.EnsureCameraOffsetAsync(pic, _mockAnalyzer.Object, _mockXmpService.Object, _mockFileSystem);

        Assert.That(pic.CameraUtcOffset, Is.Null);
        Assert.That(pic.CameraUtcOffsetChecked, Is.True);
    }
}
