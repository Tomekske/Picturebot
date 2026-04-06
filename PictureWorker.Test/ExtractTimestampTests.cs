using ErrorOr;
using PictureWorker.Infrastructure.Services;

namespace PictureWorker.Test;

[TestFixture]
public class ExtractTimestampTests {
    private PictureAnalyzerService _analyzer;
    private string _pictureWithMetadataPath;
    private string _pictureWithoutMetadataPath;
    private string _invalidPicturesPath;
    private string _missingPicturesPath;

    [SetUp]
    public void Setup() {
        _analyzer = new PictureAnalyzerService();

        var baseDir = TestContext.CurrentContext.TestDirectory;

        // Assumes these files exist in your Resources folder
        _pictureWithMetadataPath = Path.Combine(baseDir, "Resources", "exif-has-metadata.ARW");
        _pictureWithoutMetadataPath = Path.Combine(baseDir, "Resources", "exif-has-no-metadata.jpg");
        _invalidPicturesPath = Path.Combine(baseDir, "Resources", "non_picture.txt");
        _missingPicturesPath = Path.Combine(baseDir, "Resources", "ghost_pictures.jpg");
    }

    [Test]
    public async Task ExtractTimestamp_WhenFileDoesNotExist_ShouldReturnNotFoundError() {
        // Act
        var result = await _analyzer.ExtractTimestamp(_missingPicturesPath);

        // Assert
        Assert.Multiple(() => {
            Assert.That(result.IsError, Is.True);
            Assert.That(result.FirstError.Type, Is.EqualTo(ErrorType.NotFound));
            Assert.That(result.FirstError.Code, Is.EqualTo("Picture.NotFound"));
        });
    }

    [Test]
    public async Task ExtractTimestamp_WhenFileIsNotAnImage_ShouldReturnFailureError() {
        // Act
        var result = await _analyzer.ExtractTimestamp(_invalidPicturesPath);

        // Assert
        Assert.Multiple(() => {
            Assert.That(result.IsError, Is.True);
            // MetadataExtractor usually throws an exception for non-images, 
            // which triggers the catch block in our service.
            // which triggers the catch block in our service.
            Assert.That(result.FirstError.Code, Is.EqualTo("Picture.MetadataExtractionFailed"));
        });
    }

    [Test]
    public async Task ExtractTimestamp_WhenMetadataExists_ShouldReturnCorrectDateTime() {
        // Act
        var result = await _analyzer.ExtractTimestamp(_pictureWithMetadataPath);

        // Assert
        Assert.Multiple(() => {
            Assert.That(result.IsError, Is.False, "The result should be successful.");
            Assert.That(result.Value, Is.Not.EqualTo(default(DateTime)), "Should return a valid date.");

            Assert.That(result.Value.Year, Is.EqualTo(2025));
            Assert.That(result.Value.Month, Is.EqualTo(9));
            Assert.That(result.Value.Day, Is.EqualTo(11));
            Assert.That(result.Value.Hour, Is.EqualTo(22));
            Assert.That(result.Value.Minute, Is.EqualTo(10));
            Assert.That(result.Value.Second, Is.EqualTo(17));
        });
    }

    [Test]
    public async Task ExtractTimestamp_WhenMetadataIsMissing_ShouldReturnMetadataNotFoundError() {
        // Act
        var result = await _analyzer.ExtractTimestamp(_pictureWithoutMetadataPath);

        // Assert
        Assert.Multiple(() => {
            Assert.That(result.IsError, Is.True);
            Assert.That(result.FirstError.Type, Is.EqualTo(ErrorType.NotFound));
            Assert.That(result.FirstError.Code, Is.EqualTo("Picture.MetadataNotFound"));
        });
    }
}
