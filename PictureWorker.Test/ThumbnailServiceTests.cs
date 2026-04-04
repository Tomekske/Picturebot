using System.IO.Abstractions;
using PictureWorker.Infrastructure.Services;
using SixLabors.ImageSharp;

namespace PictureWorker.Test;

[TestFixture]
public class ThumbnailServiceTests {
    private PictureProcessorService _service;
    private IFileSystem _fileSystem;
    private string _largePicturePath;

    [SetUp]
    public void Setup() {
        // Use the real file system abstraction to interact with bin/Debug/Resources
        _fileSystem = new FileSystem();
        _service = new PictureProcessorService(_fileSystem);

        var baseDir = TestContext.CurrentContext.TestDirectory;

        // Ensure you have a large image (e.g., hash-1.jpg) in your Resources folder
        _largePicturePath = Path.Combine(baseDir, "Resources", "hash-1.jpg");
    }

    [Test]
    public async Task GenerateProcessedImageAsync_ResultShouldBeSmallerInBytesThanOriginal() {
        // Arrange
        var originalFileInfo = _fileSystem.FileInfo.New(_largePicturePath);
        var originalSizeBytes = originalFileInfo.Length;

        // Act
        var result = await _service.GenerateProcessedImageAsync(_largePicturePath, 400, 400);

        // Assert
        Assert.That(result.IsError, Is.False);

        using var image = result.Value;
        using var ms = new MemoryStream();

        // Save to memory to check the "in-memory" size of the processed image
        await image.SaveAsJpegAsync(ms);
        var thumbnailSizeBytes = ms.Length;

        Assert.That(thumbnailSizeBytes, Is.LessThan(originalSizeBytes),
            $"Processed image ({thumbnailSizeBytes} bytes) should be smaller than original ({originalSizeBytes} bytes).");
    }

    [Test]
    public async Task GenerateProcessedImageAsync_WhenFileDoesNotExist_ShouldReturnError() {
        // Arrange
        var missingPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Resources", "missing.jpg");

        // Act
        var result = await _service.GenerateProcessedImageAsync(missingPath, 400, 400);

        // Assert
        Assert.Multiple(() => {
            Assert.That(result.IsError, Is.True);
            Assert.That(result.FirstError.Code, Is.EqualTo("PictureProcessor.GenerationFailed"));
        });
    }

    [Test]
    public async Task GenerateProcessedImageAsync_WhenImageIsLarge_ShouldDownscaleToThreshold() {
        // Arrange
        const int maxDimension = 400;

        // Act
        var result = await _service.GenerateProcessedImageAsync(_largePicturePath, maxDimension, maxDimension);

        // Assert
        Assert.Multiple(() => {
            Assert.That(result.IsError, Is.False, "Image processing should not fail.");

            using var image = result.Value;

            // Check if dimensions are within the 400x400 threshold
            Assert.That(image.Width, Is.LessThanOrEqualTo(maxDimension), "Width should be scaled down.");
            Assert.That(image.Height, Is.LessThanOrEqualTo(maxDimension), "Height should be scaled down.");

            // Check that at least one dimension matches the maximum threshold (Mode = Max)
            // This assumes the source image was actually larger than 400 in at least one direction
            Assert.That(image.Width == maxDimension || image.Height == maxDimension, Is.True,
                "Image should be scaled to the maximum allowed dimension while maintaining aspect ratio.");
        });
    }
}
