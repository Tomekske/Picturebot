using ErrorOr;
using PictureWorker.Infrastructure.Services;

namespace PictureWorker.Test;

[TestFixture]
public class PictureSharpnessTests {
    private PictureAnalyzerService _analyzer;
    private string _pictureHighResPath;
    private string _pictureLowResPath;
    private string _invalidPicturePath;
    private string _missingPicturePath;

    [SetUp]
    public void Setup() {
        _analyzer = new PictureAnalyzerService();

        // NUnit provides the exact path to the bin/Debug folder where the DLL and resources live
        var baseDir = TestContext.CurrentContext.TestDirectory;

        // Point to the files that were copied to the output directory
        _pictureHighResPath = Path.Combine(baseDir, "Resources", "sharpness-high.jpg");
        _pictureLowResPath = Path.Combine(baseDir, "Resources", "sharpness-low.jpg");
        _invalidPicturePath = Path.Combine(baseDir, "Resources", "non_picture.txt");

        // Point to a file that definitively does not exist
        _missingPicturePath = Path.Combine(baseDir, "Resources", "ghost_picture.jpg");
    }

    [Test]
    public async Task CalculateSharpnessAsync_WhenFileDoesNotExist_ShouldReturnNotFoundError() {
        var result = await _analyzer.CalculateSharpnessAsync(_missingPicturePath);

        Assert.Multiple(() => {
            Assert.That(result.IsError, Is.True);
            Assert.That(result.FirstError.Type, Is.EqualTo(ErrorType.NotFound));
            Assert.That(result.FirstError.Code, Is.EqualTo("Picture.NotFound"));
        });
    }

    [Test]
    public async Task CalculateSharpnessAsync_WhenFileIsNotAnPicture_ShouldReturnValidationError() {
        var result = await _analyzer.CalculateSharpnessAsync(_invalidPicturePath);

        Assert.Multiple(() => {
            Assert.That(result.IsError, Is.True);
            Assert.That(result.FirstError.Type, Is.EqualTo(ErrorType.Validation));
            Assert.That(result.FirstError.Code, Is.EqualTo("Picture.InvalidFormat"));
        });
    }

    [Test]
    public async Task CalculateSharpnessAsync_WhenFileIsValidPicture_ShouldReturnSharpnessScore() {
        // 1. Act
        var result = await _analyzer.CalculateSharpnessAsync(_pictureHighResPath);

        // 2. Assert
        Assert.Multiple(() => {
            Assert.That(result.IsError, Is.False, "The result should be successful.");
            Assert.That(result.Value, Is.GreaterThan(0),
                "The sharpness score should be calculated and greater than zero.");
        });
    }

    [Test]
    public async Task CalculateSharpnessAsync_WhenPicturesAreDifferent_ShouldReturnDifferentValues() {
        // 1. Act
        var result1 = await _analyzer.CalculateSharpnessAsync(_pictureHighResPath);
        var result2 = await _analyzer.CalculateSharpnessAsync(_pictureLowResPath);

        // 2. Assert
        Assert.Multiple(() => {
            Assert.That(result1.IsError, Is.False, "First picture sharpness should succeed.");
            Assert.That(result2.IsError, Is.False, "Second picture sharpness should succeed.");
            Assert.That(result1.Value, Is.GreaterThan(result2.Value), "The sharpness values should be different.");
        });
    }

    [Test]
    public async Task CalculateSharpnessAsync_WhenPicturesAreIdentical_ShouldReturnSameHValue() {
        // 1. Act
        var result1 = await _analyzer.CalculateSharpnessAsync(_pictureHighResPath);
        var result2 = await _analyzer.CalculateSharpnessAsync(_pictureHighResPath);

        // 2. Assert
        Assert.Multiple(() => {
            Assert.That(result1.IsError, Is.False, "First picture sharpness should succeed.");
            Assert.That(result2.IsError, Is.False, "Second picture sharpness should succeed.");

            // The core comparison
            Assert.That(result1.Value, Is.EqualTo(result2.Value),
                "Identical pictures should produce the exact same sharpness value.");
        });
    }
}
