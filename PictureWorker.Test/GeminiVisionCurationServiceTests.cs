using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Net.Http;
using System.Text;
using Domain.Interfaces;
using Domain.Models;
using Moq;
using Moq.Protected;
using PictureWorker.Infrastructure.Services;

namespace PictureWorker.Test;

[TestFixture]
public class GeminiVisionCurationServiceTests {
    private Mock<ISettingsService> _mockSettingsService = null!;
    private MockFileSystem _mockFileSystem = null!;

    [SetUp]
    public void SetUp() {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockFileSystem = new MockFileSystem();
    }

    [Test]
    public void ParseCurationJson_ValidJson_ExtractsBestPickAndFeedback() {
        // Arrange
        var json = @"{
            ""best_pick"": ""img_002"",
            ""feedback"": {
                ""img_001"": ""Slight subject motion blur on hands; eye focus slightly soft."",
                ""img_002"": ""Critical focus locked on the left eye; excellent subject separation and expression."",
                ""img_003"": ""Caught mid-blink with awkward arm cut at frame border.""
            }
        }";
        var expectedIds = new[] { "img_001", "img_002", "img_003" };

        // Act
        var result = GeminiVisionCurationService.ParseCurationJson(json, expectedIds);

        // Assert
        Assert.That(result.IsError, Is.False);
        Assert.That(result.Value.BestPickId, Is.EqualTo("img_002"));
        Assert.That(result.Value.Feedback, Contains.Key("img_001"));
        Assert.That(result.Value.Feedback["img_001"], Does.Contain("motion blur"));
        Assert.That(result.Value.Feedback["img_002"], Does.Contain("Critical focus"));
        Assert.That(result.Value.Feedback["img_003"], Does.Contain("mid-blink"));
    }

    [Test]
    public void ParseCurationJson_MarkdownFencedJson_ParsesCorrectly() {
        // Arrange
        var markdownJson = @"```json
{
    ""best_pick"": ""pic_10"",
    ""feedback"": {
        ""pic_10"": ""Pin-sharp iris and pupil focus with clean framing."",
        ""pic_11"": ""Missed focus on foreground background element.""
    }
}
```";
        var expectedIds = new[] { "pic_10", "pic_11" };

        // Act
        var result = GeminiVisionCurationService.ParseCurationJson(markdownJson, expectedIds);

        // Assert
        Assert.That(result.IsError, Is.False);
        Assert.That(result.Value.BestPickId, Is.EqualTo("pic_10"));
        Assert.That(result.Value.Feedback["pic_10"], Does.Contain("Pin-sharp"));
    }

    [Test]
    public void ParseCurationJson_InvalidWinner_ReturnsValidationError() {
        // Arrange
        var json = @"{
            ""best_pick"": ""unknown_id"",
            ""feedback"": {
                ""pic_1"": ""Diagnostic note""
            }
        }";
        var expectedIds = new[] { "pic_1", "pic_2" };

        // Act
        var result = GeminiVisionCurationService.ParseCurationJson(json, expectedIds);

        // Assert
        Assert.That(result.IsError, Is.True);
        Assert.That(result.FirstError.Code, Is.EqualTo("AiCuration.InvalidBestPickId"));
    }

    [Test]
    public void ParseGeminiResponse_FullCandidatesResponse_ExtractsCandidateJson() {
        // Arrange
        var apiResponse = @"{
            ""candidates"": [
                {
                    ""content"": {
                        ""parts"": [
                            {
                                ""text"": ""{\n  \""best_pick\"": \""123\"",\n  \""feedback\"": {\n    \""123\"": \""Critical sharpness on primary subject iris.\"",\n    \""124\"": \""Clipped highlights on skin tones.\""\n  }\n}""
                            }
                        ],
                        ""role"": ""model""
                    },
                    ""finishReason"": ""STOP""
                }
            ]
        }";
        var expectedIds = new[] { "123", "124" };

        // Act
        var result = GeminiVisionCurationService.ParseGeminiResponse(apiResponse, expectedIds);

        // Assert
        Assert.That(result.IsError, Is.False);
        Assert.That(result.Value.BestPickId, Is.EqualTo("123"));
        Assert.That(result.Value.Feedback["123"], Does.Contain("Critical sharpness"));
        Assert.That(result.Value.Feedback["124"], Does.Contain("Clipped highlights"));
    }

    [Test]
    public void ParseCurationJson_WithIndexWinnerAndDecoratedFeedback_ResolvesIdsAndFeedback() {
        // Arrange
        var json = @"{
            ""best_pick"": ""2"",
            ""feedback"": {
                ""1"": ""Slight camera shake."",
                ""2"": ""Sharp subject focus and pleasing composition.""
            }
        }";
        var expectedIds = new[] { "pic_001", "pic_002" };

        // Act
        var result = GeminiVisionCurationService.ParseCurationJson(json, expectedIds);

        // Assert
        Assert.That(result.IsError, Is.False);
        Assert.That(result.Value.BestPickId, Is.EqualTo("pic_002"));
        Assert.That(result.Value.Feedback["pic_002"], Does.Contain("Sharp subject focus"));
        Assert.That(result.Value.Feedback["pic_001"], Does.Contain("camera shake"));
    }

    [Test]
    public void ParseCurationJson_WithBracketsPrefix_ResolvesCorrectly() {
        // Arrange
        var json = @"{
            ""best_pick"": ""[Image ID: 1052]"",
            ""feedback"": {
                ""[Image ID: 1052]"": ""Perfect catchlights in eyes."",
                ""[Image ID: 1053]"": ""Alternative shot with subject looking away.""
            }
        }";
        var expectedIds = new[] { "1052", "1053" };

        // Act
        var result = GeminiVisionCurationService.ParseCurationJson(json, expectedIds);

        // Assert
        Assert.That(result.IsError, Is.False);
        Assert.That(result.Value.BestPickId, Is.EqualTo("1052"));
        Assert.That(result.Value.Feedback["1052"], Does.Contain("catchlights"));
        Assert.That(result.Value.Feedback["1053"], Does.Contain("Alternative shot"));
    }

    [Test]
    public async Task EvaluateBurstAsync_EmptyList_ReturnsValidationError() {
        // Arrange
        var service = new GeminiVisionCurationService(_mockSettingsService.Object, _mockFileSystem);

        // Act
        var result = await service.EvaluateBurstAsync(new List<(string Id, string ImagePath)>());

        // Assert
        Assert.That(result.IsError, Is.True);
        Assert.That(result.FirstError.Code, Is.EqualTo("AiCuration.EmptyInput"));
    }

    [Test]
    public async Task EvaluateBurstAsync_MissingApiKey_ReturnsApiKeyMissingError() {
        // Arrange
        _mockSettingsService.Setup(s => s.Current).Returns(new SettingsModel {
            GeminiApiKey = string.Empty
        });
        var service = new GeminiVisionCurationService(_mockSettingsService.Object, _mockFileSystem);

        // Act
        var result = await service.EvaluateBurstAsync(new List<(string Id, string ImagePath)> {
            ("1", "C:\\photos\\1.jpg")
        });

        // Assert
        Assert.That(result.IsError, Is.True);
        Assert.That(result.FirstError.Code, Is.EqualTo("AiCuration.ApiKeyMissing"));
    }

    [Test]
    public async Task EvaluateBurstAsync_SuccessfulApiResponse_ReturnsEvaluationResult() {
        // Arrange
        _mockSettingsService.Setup(s => s.Current).Returns(new SettingsModel {
            GeminiApiKey = "test-api-key",
            AiCurationModel = "gemini-3.6-flash"
        });

        _mockFileSystem.AddFile("C:\\photos\\img1.jpg", new MockFileData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));
        _mockFileSystem.AddFile("C:\\photos\\img2.jpg", new MockFileData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));

        var mockHandler = new Mock<HttpMessageHandler>();
        var geminiResponseJson = @"{
            ""candidates"": [
                {
                    ""content"": {
                        ""parts"": [
                            {
                                ""text"": ""{\n  \""best_pick\"": \""img2\"",\n  \""feedback\"": {\n    \""img1\"": \""Subject motion blur on hands.\"",\n    \""img2\"": \""Critical focus locked on the left eye.\""\n  }\n}""
                            }
                        ]
                    }
                }
            ]
        }";

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(geminiResponseJson, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var service = new GeminiVisionCurationService(_mockSettingsService.Object, _mockFileSystem, httpClient);

        // Act
        var result = await service.EvaluateBurstAsync(new List<(string Id, string ImagePath)> {
            ("img1", "C:\\photos\\img1.jpg"),
            ("img2", "C:\\photos\\img2.jpg")
        });

        // Assert
        Assert.That(result.IsError, Is.False);
        Assert.That(result.Value.BestPickId, Is.EqualTo("img2"));
        Assert.That(result.Value.Feedback["img1"], Does.Contain("motion blur"));
        Assert.That(result.Value.Feedback["img2"], Does.Contain("Critical focus locked"));
    }

    [Test]
    public async Task EvaluateBurstAsync_ApiReturnsError_ExtractsNestedMessage() {
        // Arrange
        _mockSettingsService.Setup(s => s.Current).Returns(new SettingsModel {
            GeminiApiKey = "test-api-key",
            AiCurationModel = "gemini-3.6-flash"
        });

        _mockFileSystem.AddFile("C:\\photos\\img1.jpg", new MockFileData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));

        var mockHandler = new Mock<HttpMessageHandler>();
        var geminiErrorJson = @"{
            ""error"": {
                ""code"": 404,
                ""message"": ""This model is no longer available. Please use gemini-3.6-flash."",
                ""status"": ""NOT_FOUND""
            }
        }";

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent(geminiErrorJson, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var service = new GeminiVisionCurationService(_mockSettingsService.Object, _mockFileSystem, httpClient);

        // Act
        var result = await service.EvaluateBurstAsync(new List<(string Id, string ImagePath)> {
            ("img1", "C:\\photos\\img1.jpg")
        });

        // Assert
        Assert.That(result.IsError, Is.True);
        Assert.That(result.FirstError.Description, Does.Contain("gemini-3.6-flash"));
    }

    [Test]
    public async Task EvaluateBurstAsync_Transient503ThenSuccess_RetriesAndSucceeds() {
        // Arrange
        _mockSettingsService.Setup(s => s.Current).Returns(new SettingsModel {
            GeminiApiKey = "test-api-key",
            AiCurationModel = "gemini-3.6-flash"
        });

        _mockFileSystem.AddFile("C:\\photos\\img1.jpg", new MockFileData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));

        var mockHandler = new Mock<HttpMessageHandler>();
        int callCount = 0;

        var geminiSuccessJson = @"{
            ""candidates"": [
                {
                    ""content"": {
                        ""parts"": [
                            {
                                ""text"": ""{\n  \""best_pick\"": \""img1\"",\n  \""feedback\"": {\n    \""img1\"": \""Sharp focus on eyes.\""\n  }\n}""
                            }
                        ]
                    }
                }
            ]
        }";

        var gemini503Json = @"{
            ""error"": {
                ""code"": 503,
                ""message"": ""This model is currently experiencing high demand."",
                ""status"": ""UNAVAILABLE""
            }
        }";

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => {
                callCount++;
                if (callCount == 1) {
                    return new HttpResponseMessage {
                        StatusCode = HttpStatusCode.ServiceUnavailable,
                        Content = new StringContent(gemini503Json, Encoding.UTF8, "application/json")
                    };
                }
                return new HttpResponseMessage {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(geminiSuccessJson, Encoding.UTF8, "application/json")
                };
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var service = new GeminiVisionCurationService(_mockSettingsService.Object, _mockFileSystem, httpClient);

        // Act
        var result = await service.EvaluateBurstAsync(new List<(string Id, string ImagePath)> {
            ("img1", "C:\\photos\\img1.jpg")
        });

        // Assert
        Assert.That(result.IsError, Is.False);
        Assert.That(result.Value.BestPickId, Is.EqualTo("img1"));
        Assert.That(callCount, Is.EqualTo(2));
    }

    [Test]
    public async Task EvaluateBurstAsync_NonTransient400_DoesNotRetry() {
        // Arrange
        _mockSettingsService.Setup(s => s.Current).Returns(new SettingsModel {
            GeminiApiKey = "test-api-key",
            AiCurationModel = "gemini-3.6-flash"
        });

        _mockFileSystem.AddFile("C:\\photos\\img1.jpg", new MockFileData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));

        var mockHandler = new Mock<HttpMessageHandler>();
        int callCount = 0;

        var gemini400Json = @"{
            ""error"": {
                ""code"": 400,
                ""message"": ""API key not valid."",
                ""status"": ""INVALID_ARGUMENT""
            }
        }";

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => {
                callCount++;
                return new HttpResponseMessage {
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent(gemini400Json, Encoding.UTF8, "application/json")
                };
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var service = new GeminiVisionCurationService(_mockSettingsService.Object, _mockFileSystem, httpClient);

        // Act
        var result = await service.EvaluateBurstAsync(new List<(string Id, string ImagePath)> {
            ("img1", "C:\\photos\\img1.jpg")
        });

        // Assert
        Assert.That(result.IsError, Is.True);
        Assert.That(callCount, Is.EqualTo(1));
    }
}
