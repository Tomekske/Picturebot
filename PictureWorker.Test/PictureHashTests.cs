using ErrorOr;
using System.IO.Abstractions;
using PictureWorker.Infrastructure.Services;

namespace PictureWorker.Test;

[TestFixture]
public class PictureHashTests {
    private PictureAnalyzerService _analyzer;
    private string _pictureHashPath1;
    private string _pictureHashPath2;
    private string _invalidPicturesPath;
    private string _missingPicturesPath;

    [SetUp]
    public void Setup() {
        _analyzer = new PictureAnalyzerService(new FileSystem());


        // NUnit provides the exact path to the bin/Debug folder where the DLL and resources live
        var baseDir = TestContext.CurrentContext.TestDirectory;

        // Point to the files that were copied to the output directory
        _pictureHashPath1 = Path.Combine(baseDir, "Resources", "hash-1.jpg");
        _pictureHashPath2 = Path.Combine(baseDir, "Resources", "hash-2.jpg");
        _invalidPicturesPath = Path.Combine(baseDir, "Resources", "non_picture.txt");

        // Point to a file that definitively does not exist
        _missingPicturesPath = Path.Combine(baseDir, "Resources", "ghost_pictures.jpg");
    }

    [Test]
    public async Task CalculateHashAsync_WhenFileDoesNotExist_ShouldReturnNotFoundError() {
        var result = await _analyzer.CalculateHashAsync(_missingPicturesPath);

        Assert.Multiple(() => {
            Assert.That(result.IsError, Is.True);
            Assert.That(result.FirstError.Type, Is.EqualTo(ErrorType.NotFound));
            Assert.That(result.FirstError.Code, Is.EqualTo("Picture.NotFound"));
        });
    }

    [Test]
    public async Task CalculateHashAsync_WhenFileIsNotAPictures_ShouldReturnValidationError() {
        var result = await _analyzer.CalculateHashAsync(_invalidPicturesPath);

        // 2. Act
        Assert.Multiple(() => {
            Assert.That(result.IsError, Is.True);
            Assert.That(result.FirstError.Type, Is.EqualTo(ErrorType.Validation));
            Assert.That(result.FirstError.Code, Is.EqualTo("Picture.InvalidFormat"));
        });
    }

    [Test]
    public async Task CalculateHashAsync_WhenFileIsValidPictures_ShouldReturnHash() {
        // 1. Act
        var result = await _analyzer.CalculateHashAsync(_pictureHashPath1);

        // 2. Assert
        Assert.Multiple(() => {
            Assert.That(result.IsError, Is.False, "The result should be successful.");
            Assert.That(result.Value, Is.GreaterThan(0ul), "The perceptual hash should be calculated.");
        });
    }

    [Test]
    public async Task CalculateHashAsync_WhenPicturesAreDifferent_ShouldReturnDifferentHashes() {
        // 1. Act
        var result1 = await _analyzer.CalculateSharpnessAsync(_pictureHashPath1);
        var result2 = await _analyzer.CalculateSharpnessAsync(_pictureHashPath2);

        // 2. Assert
        Assert.Multiple(() => {
            Assert.That(result1.IsError, Is.False, "First picture sharpness should succeed.");
            Assert.That(result2.IsError, Is.False, "Second picture sharpness should succeed.");
            Assert.That(result1.Value, Is.Not.EqualTo(result2.Value), "The sharpness values should be different.");
        });
    }

    [Test]
    public async Task CalculateHashAsync_WhenPicturesAreIdentical_ShouldReturnSameHashes() {
        // 1. Act
        var result1 = await _analyzer.CalculateSharpnessAsync(_pictureHashPath1);
        var result2 = await _analyzer.CalculateSharpnessAsync(_pictureHashPath1);

        // 2. Assert
        Assert.Multiple(() => {
            Assert.That(result1.IsError, Is.False, "First picture hash should succeed.");
            Assert.That(result2.IsError, Is.False, "Second picture hash should succeed.");

            // The core comparison
            Assert.That(result1.Value, Is.EqualTo(result2.Value),
                "Identical pictures should produce the exact same hash value.");
        });
    }
}
