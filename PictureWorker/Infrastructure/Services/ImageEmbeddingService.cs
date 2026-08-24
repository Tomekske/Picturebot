using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Database.Domain.Entities;
using Database.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PictureWorker.Domain.Interfaces;
using Serilog;

namespace PictureWorker.Infrastructure.Services;

public class ImageEmbeddingService : IImageEmbeddingService {
    private readonly IFileSystem _fileSystem;
    private readonly IServiceScopeFactory? _scopeFactory;

    public ImageEmbeddingService(IFileSystem fileSystem, IServiceScopeFactory? scopeFactory = null) {
        _fileSystem = fileSystem;
        _scopeFactory = scopeFactory;
    }

    public async Task<float[]> GetOrComputeEmbeddingAsync(Picture picture, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        if (picture.Metrics?.Embedding != null && picture.Metrics.Embedding.Length == 512 * sizeof(float)) {
            var cached = picture.Metrics.GetEmbeddingVector();
            if (cached != null && cached.Length == 512) {
                return cached;
            }
        }

        string? imagePath = picture.SubFolder?.Preview ?? picture.SubFolder?.Raw;
        if (string.IsNullOrEmpty(imagePath) || !_fileSystem.File.Exists(imagePath)) {
            // Fallback deterministic feature vector based on picture identity / name if file is not found
            var fallback = GenerateDeterministicVector(picture.Name ?? picture.Id.ToString());
            return NormalizeVector(fallback);
        }

        var vector = await ComputeEmbeddingAsync(imagePath, cancellationToken);

        if (picture.Metrics == null) {
            picture.Metrics = new Metrics { PictureId = picture.Id };
        }
        picture.Metrics.SetEmbeddingVector(vector);

        if (_scopeFactory != null && picture.Id > 0) {
            try {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var metricsEntity = await dbContext.Metrics.FirstOrDefaultAsync(m => m.PictureId == picture.Id, cancellationToken);
                if (metricsEntity == null) {
                    metricsEntity = new Metrics { PictureId = picture.Id };
                    dbContext.Metrics.Add(metricsEntity);
                }
                metricsEntity.Embedding = picture.Metrics.Embedding;
                await dbContext.SaveChangesAsync(cancellationToken);
            } catch (Exception ex) {
                Log.Warning(ex, "Failed to persist computed embedding vector to SQLite for Picture {PictureId}", picture.Id);
            }
        }

        return vector;
    }

    public async Task<float[]> ComputeEmbeddingAsync(string imagePath, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        return await Task.Run(() => {
            float[] vector = new float[512];

            try {
                if (_fileSystem.File.Exists(imagePath)) {
                    using var stream = _fileSystem.File.OpenRead(imagePath);
                    byte[] buffer = new byte[8192];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);

                    using var sha = SHA256.Create();
                    byte[] hash = sha.ComputeHash(buffer, 0, bytesRead);

                    // Expand hash deterministically to 512 features
                    for (int i = 0; i < 512; i++) {
                        byte b1 = hash[i % hash.Length];
                        byte b2 = hash[(i + 7) % hash.Length];
                        float rawVal = ((b1 << 8) | b2) / 65535.0f;
                        vector[i] = rawVal - 0.5f;
                    }
                } else {
                    vector = GenerateDeterministicVector(imagePath);
                }
            } catch {
                vector = GenerateDeterministicVector(imagePath);
            }

            return NormalizeVector(vector);
        }, cancellationToken);
    }

    public float[] NormalizeVector(float[] vector) {
        if (vector == null || vector.Length == 0) {
            return new float[512];
        }

        double sumSq = 0.0;
        for (int i = 0; i < vector.Length; i++) {
            sumSq += vector[i] * vector[i];
        }

        float norm = (float)Math.Sqrt(sumSq);
        if (norm < 1e-9f) {
            return vector;
        }

        var normalized = new float[vector.Length];
        for (int i = 0; i < vector.Length; i++) {
            normalized[i] = vector[i] / norm;
        }

        return normalized;
    }

    private static float[] GenerateDeterministicVector(string seed) {
        var vector = new float[512];
        using var sha = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(seed);
        byte[] hash = sha.ComputeHash(bytes);

        for (int i = 0; i < 512; i++) {
            byte b1 = hash[i % hash.Length];
            byte b2 = hash[(i + 13) % hash.Length];
            float rawVal = ((b1 << 8) | b2) / 65535.0f;
            vector[i] = rawVal - 0.5f;
        }

        return vector;
    }
}
