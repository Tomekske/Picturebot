using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Domain.Interfaces;
using Domain.Models;
using ErrorOr;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using PictureWorker.Domain.Interfaces;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace PictureWorker.Infrastructure.Services;

public class GeminiVisionCurationService : IAiPictureCurationService {
    private readonly ISettingsService _settingsService;
    private readonly IFileSystem _fileSystem;
    private readonly HttpClient _httpClient;

    public const string SystemPrompt = 
@"EVALUATION CRITERIA (IN STRICT ORDER OF PRIORITY):

Critical Sharpness and Focus:
Check primary subject sharpness, specifically pin-sharp iris and pupils in portraits or leading edges in action and wildlife. Distinguish between actual motion blur or missed focus versus intentional shallow depth of field (bokeh).

Expression and Biometrics:
If people are present, check for open eyes with zero tolerance for accidental blinks or half-open eyelids. Look for natural expressions, flattering micro-expressions, and good head angle.

Composition and Framing:
Check for clean borders without awkward limb cuts at joints. Look for clean subject separation against the background, an absence of distracting background elements, and proper lead room or look room.

Exposure and Technical Quality:
Check highlight retention on skin tones and skies. Avoid clipped highlights and crushed, noisy shadows.

OPERATIONAL RULES:

Exact ID Matching: Every image in the input is labeled with a unique identifier. You must match each feedback entry to its exact provided ID.

Winner Declaration: Pick exactly one best image under best_pick. Never declare multiple winners or ties.

Feedback Style: Write concise, objective, editor-style observations (one to two sentences per image). Avoid generic praise like ""nice image"". Use concrete diagnostic terms like ""Critical focus locked on the left eye"", ""Subject motion blur on hands"", or ""Caught mid-blink"".

Output Strictness: Return valid JSON matching the requested schema. Do not include markdown code blocks, backticks, or any commentary outside the JSON payload.";

    public GeminiVisionCurationService(
        ISettingsService settingsService,
        IFileSystem fileSystem,
        HttpClient? httpClient = null) {
        _settingsService = settingsService;
        _fileSystem = fileSystem;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task<ErrorOr<AiCurationResult>> EvaluateBurstAsync(
        IReadOnlyList<(string Id, string ImagePath)> pictures,
        CancellationToken cancellationToken = default) {
        if (pictures == null || pictures.Count == 0) {
            return Error.Validation("AiCuration.EmptyInput", "No pictures provided for evaluation.");
        }

        var settings = _settingsService.Current;
        var apiKey = settings.GeminiApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey)) {
            return Error.Validation("AiCuration.ApiKeyMissing", "Gemini API key is not configured in Settings.");
        }

        var modelName = !string.IsNullOrWhiteSpace(settings.AiCurationModel) 
            ? settings.AiCurationModel.Trim() 
            : "gemini-3.6-flash";

        try {
            // 1. Build multimodal parts
            var parts = new List<object>();

            // Add instructions prompt
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine(SystemPrompt);
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Evaluate the following images in this burst group and return the JSON response:");
            promptBuilder.AppendLine("Images to evaluate:");
            foreach (var (id, _) in pictures) {
                promptBuilder.AppendLine($"- Image ID: {id}");
            }

            parts.Add(new { text = promptBuilder.ToString() });

            // Prepare and attach each image
            foreach (var (id, imagePath) in pictures) {
                var imageBase64 = await PrepareImageBase64Async(imagePath, cancellationToken);
                if (string.IsNullOrEmpty(imageBase64)) {
                    Log.Warning("Could not load image bytes for ID {Id} at {Path}", id, imagePath);
                    continue;
                }

                parts.Add(new { text = $"[Image ID: {id}]" });
                parts.Add(new {
                    inline_data = new {
                        mime_type = "image/jpeg",
                        data = imageBase64
                    }
                });
            }

            // 2. Build Gemini API Request Payload
            var requestBody = new {
                contents = new[] {
                    new {
                        role = "user",
                        parts
                    }
                },
                generationConfig = new {
                    response_mime_type = "application/json",
                    temperature = 0.2
                }
            };

            var jsonOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var requestJson = JsonSerializer.Serialize(requestBody, jsonOptions);

            var endpointUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(modelName)}:generateContent?key={Uri.EscapeDataString(apiKey)}";

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpointUrl) {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode) {
                Log.Error("Gemini API error ({StatusCode}) for model {Model}: {Content}", response.StatusCode, modelName, responseContent);

                string errorMessage = $"Gemini API error ({(int)response.StatusCode}): {response.ReasonPhrase}";
                try {
                    using var errorDoc = JsonDocument.Parse(responseContent);
                    if (errorDoc.RootElement.TryGetProperty("error", out var errObj) &&
                        errObj.TryGetProperty("message", out var msgProp)) {
                        var innerMsg = msgProp.GetString();
                        if (!string.IsNullOrWhiteSpace(innerMsg)) {
                            errorMessage = innerMsg;
                        }
                    }
                } catch {
                    // Fallback to default message
                }

                return Error.Failure("AiCuration.ApiError", $"[{modelName}] {errorMessage}");
            }

            // 3. Extract and parse generated JSON text
            var parsedResult = ParseGeminiResponse(responseContent, pictures.Select(p => p.Id).ToList());
            if (parsedResult.IsError) {
                return parsedResult.Errors;
            }

            return parsedResult.Value;
        } catch (OperationCanceledException) {
            return Error.Failure("AiCuration.Cancelled", "AI curation was cancelled.");
        } catch (Exception ex) {
            Log.Error(ex, "Unexpected error during AI vision curation");
            return Error.Failure("AiCuration.ExecutionFailed", $"AI curation failed: {ex.Message}");
        }
    }

    private static readonly HashSet<string> _rawExtensions = new(StringComparer.OrdinalIgnoreCase) {
        ".arw", ".cr2", ".cr3", ".nef", ".nrw", ".dng", ".raf", ".orf", ".rw2", ".pef", ".srw", ".raw"
    };

    private async Task<string?> PrepareImageBase64Async(string imagePath, CancellationToken cancellationToken) {
        if (!_fileSystem.File.Exists(imagePath)) {
            return null;
        }

        try {
            byte[]? imageBytes = null;
            var ext = _fileSystem.Path.GetExtension(imagePath);

            if (!string.IsNullOrEmpty(ext) && _rawExtensions.Contains(ext)) {
                imageBytes = TryExtractRawEmbeddedJpeg(imagePath);
            }

            if (imageBytes != null && imageBytes.Length > 0) {
                using var ms = new MemoryStream(imageBytes);
                using var img = await Image.LoadAsync(ms, cancellationToken);
                return await ProcessAndEncodeJpegAsync(img, cancellationToken);
            }

            await using var inStream = _fileSystem.File.OpenRead(imagePath);
            using var image = await Image.LoadAsync(inStream, cancellationToken);
            return await ProcessAndEncodeJpegAsync(image, cancellationToken);
        } catch (Exception ex) {
            Log.Warning(ex, "Failed to optimize image for vision API at {Path}", imagePath);
            try {
                var rawBytes = TryExtractRawEmbeddedJpeg(imagePath) 
                    ?? await _fileSystem.File.ReadAllBytesAsync(imagePath, cancellationToken);
                return Convert.ToBase64String(rawBytes);
            } catch {
                return null;
            }
        }
    }

    private static async Task<string> ProcessAndEncodeJpegAsync(Image image, CancellationToken cancellationToken) {
        image.Mutate(x => x.AutoOrient());
        const int maxDim = 1280;
        if (image.Width > maxDim || image.Height > maxDim) {
            image.Mutate(x => x.Resize(new ResizeOptions {
                Size = new Size(maxDim, maxDim),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Bicubic
            }));
        }
        using var outStream = new MemoryStream();
        await image.SaveAsJpegAsync(outStream, new JpegEncoder { Quality = 85 }, cancellationToken);
        return Convert.ToBase64String(outStream.ToArray());
    }

    private byte[]? TryExtractRawEmbeddedJpeg(string filePath) {
        try {
            var directories = ImageMetadataReader.ReadMetadata(filePath);
            var thumbDir = directories.OfType<ExifThumbnailDirectory>().FirstOrDefault();
            if (thumbDir != null &&
                thumbDir.TryGetInt32(ExifThumbnailDirectory.TagThumbnailOffset, out var offset) &&
                thumbDir.TryGetInt32(ExifThumbnailDirectory.TagThumbnailLength, out var length) &&
                length > 0 && offset >= 0) {
                
                using var fs = _fileSystem.File.OpenRead(filePath);
                var buffer = new byte[length];
                fs.Position = offset;
                var bytesRead = fs.Read(buffer, 0, length);
                if (bytesRead == length && buffer.Length > 2 && buffer[0] == 0xFF && buffer[1] == 0xD8) {
                    return buffer;
                }
            }
        } catch {
            // Ignored
        }
        return null;
    }

    public static ErrorOr<AiCurationResult> ParseGeminiResponse(string rawApiResponse, IReadOnlyList<string> expectedIds) {
        try {
            using var doc = JsonDocument.Parse(rawApiResponse);
            var root = doc.RootElement;

            // Extract candidate text from Gemini response structure
            string? jsonText = null;
            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0) {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0) {
                    var firstPart = parts[0];
                    if (firstPart.TryGetProperty("text", out var textProp)) {
                        jsonText = textProp.GetString();
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(jsonText)) {
                // If not standard candidates structure, attempt direct parsing (e.g. mock or stripped response)
                jsonText = rawApiResponse;
            }

            return ParseCurationJson(jsonText, expectedIds, rawApiResponse);
        } catch (Exception ex) {
            Log.Error(ex, "Failed to parse Gemini API response JSON: {Raw}", rawApiResponse);
            return Error.Failure("AiCuration.InvalidResponse", $"Failed to parse API response: {ex.Message}");
        }
    }

    public static ErrorOr<AiCurationResult> ParseCurationJson(string jsonText, IReadOnlyList<string> expectedIds, string? rawResponse = null) {
        try {
            var cleanJson = jsonText.Trim();
            if (cleanJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) {
                cleanJson = cleanJson[7..];
            } else if (cleanJson.StartsWith("```", StringComparison.OrdinalIgnoreCase)) {
                cleanJson = cleanJson[3..];
            }
            if (cleanJson.EndsWith("```", StringComparison.OrdinalIgnoreCase)) {
                cleanJson = cleanJson[..^3];
            }
            cleanJson = cleanJson.Trim();

            using var doc = JsonDocument.Parse(cleanJson);
            var root = doc.RootElement;

            string bestPickId = string.Empty;
            if (root.TryGetProperty("best_pick", out var bestPickProp) || root.TryGetProperty("bestPick", out bestPickProp)) {
                bestPickId = bestPickProp.GetString() ?? string.Empty;
            }

            var feedback = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("feedback", out var feedbackProp) && feedbackProp.ValueKind == JsonValueKind.Object) {
                foreach (var prop in feedbackProp.EnumerateObject()) {
                    feedback[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(bestPickId)) {
                return Error.Validation("AiCuration.MissingBestPick", "AI response did not declare a 'best_pick' winner.");
            }

            var matchedWinnerId = expectedIds.FirstOrDefault(id => string.Equals(id, bestPickId, StringComparison.OrdinalIgnoreCase));
            if (matchedWinnerId == null) {
                return Error.Validation("AiCuration.InvalidBestPickId", $"AI declared winner '{bestPickId}' which does not match any image in the burst group.");
            }

            return new AiCurationResult {
                BestPickId = matchedWinnerId,
                Feedback = feedback,
                RawResponse = rawResponse ?? jsonText
            };
        } catch (Exception ex) {
            return Error.Failure("AiCuration.JsonParseError", $"Invalid curation payload JSON: {ex.Message}");
        }
    }
}
