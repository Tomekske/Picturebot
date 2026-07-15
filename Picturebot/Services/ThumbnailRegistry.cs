using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using Avalonia.Media.Imaging;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Serilog;

namespace Picturebot.Services;

public class ThumbnailRequest {
    public string FilePath { get; }
    public int TargetHeight { get; }
    public CancellationToken CancellationToken { get; }
    public TaskCompletionSource<Bitmap?> Tcs { get; }
    public int Priority { get; }
    public string CacheKey { get; }

    public ThumbnailRequest(string filePath, int targetHeight, int priority, CancellationToken cancellationToken) {
        FilePath = filePath;
        TargetHeight = targetHeight;
        Priority = priority;
        CancellationToken = cancellationToken;
        Tcs = new TaskCompletionSource<Bitmap?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CacheKey = CalculateCacheKey(filePath);
    }

    private static string CalculateCacheKey(string filePath) {
        try {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists) {
                return string.Empty;
            }
            var lastWrite = fileInfo.LastWriteTimeUtc.Ticks;
            var input = $"{filePath}_{lastWrite}";
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLower();
        } catch {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(filePath));
            return Convert.ToHexString(bytes).ToLower();
        }
    }
}

public class ThumbnailLruCache {
    private readonly long _maxMemoryBytes;
    private readonly Dictionary<string, LinkedListNode<CacheItem>> _map = new();
    private readonly LinkedList<CacheItem> _list = new();
    private long _currentMemoryBytes = 0;
    private readonly object _lock = new();

    private class CacheItem {
        public string Key { get; set; } = string.Empty;
        public Bitmap Bitmap { get; set; } = null!;
        public long SizeBytes { get; set; }
    }

    public ThumbnailLruCache(long maxMemoryBytes) {
        _maxMemoryBytes = maxMemoryBytes;
    }

    public bool TryGet(string key, out Bitmap? bitmap) {
        lock (_lock) {
            if (_map.TryGetValue(key, out var node)) {
                _list.Remove(node);
                _list.AddFirst(node);
                bitmap = node.Value.Bitmap;
                return true;
            }
            bitmap = null;
            return false;
        }
    }

    public void Add(string key, Bitmap bitmap) {
        lock (_lock) {
            if (_map.TryGetValue(key, out var existingNode)) {
                _list.Remove(existingNode);
                _list.AddFirst(existingNode);
                return;
            }

            var size = (long)bitmap.Size.Width * (long)bitmap.Size.Height * 4;
            var item = new CacheItem {
                Key = key,
                Bitmap = bitmap,
                SizeBytes = size
            };

            var node = new LinkedListNode<CacheItem>(item);
            _list.AddFirst(node);
            _map[key] = node;
            _currentMemoryBytes += size;

            EvictIfNecessary();
        }
    }

    private void EvictIfNecessary() {
        while (_currentMemoryBytes > _maxMemoryBytes && _list.Count > 0) {
            var last = _list.Last;
            if (last == null) break;

            _list.RemoveLast();
            _map.Remove(last.Value.Key);
            _currentMemoryBytes -= last.Value.SizeBytes;
        }
    }

    public void Clear() {
        lock (_lock) {
            _list.Clear();
            _map.Clear();
            _currentMemoryBytes = 0;
        }
    }
}

public class ThumbnailRegistry : IDisposable {
    private static readonly Lazy<ThumbnailRegistry> _instance = new(() => new ThumbnailRegistry());
    public static ThumbnailRegistry Instance => _instance.Value;

    private readonly ThumbnailLruCache _lruCache;
    private readonly PriorityQueue<ThumbnailRequest, int> _queue = new();
    private readonly object _queueLock = new();
    private readonly Channel<bool> _wakeupChannel = Channel.CreateUnbounded<bool>();
    private readonly CancellationTokenSource _cts = new();
    private readonly string _cacheDir;
    private readonly Task _workerTask;

    private static readonly HashSet<string> RawExtensions = new(StringComparer.OrdinalIgnoreCase) {
        ".cr2", ".nef", ".arw", ".dng", ".crw", ".orf", ".mrw", ".pef", ".raf", ".raw", ".rw2"
    };

    private ThumbnailRegistry() {
        _lruCache = new ThumbnailLruCache(200 * 1024 * 1024);

        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Picturebot",
            "Thumbnails"
        );

        try {
            if (!System.IO.Directory.Exists(_cacheDir)) {
                System.IO.Directory.CreateDirectory(_cacheDir);
            }
        } catch (Exception ex) {
            Log.Error(ex, "Failed to create cache directory: {Path}", _cacheDir);
        }

        _workerTask = Task.Run(WorkerLoopAsync);
    }

    public bool TryGetCached(string filePath, out Bitmap? bitmap) {
        return _lruCache.TryGet(filePath, out bitmap);
    }

    public async Task<Bitmap?> QueueRequestAsync(string filePath, int targetHeight, int priority, CancellationToken cancellationToken) {
        if (_lruCache.TryGet(filePath, out var cached)) {
            return cached;
        }

        var request = new ThumbnailRequest(filePath, targetHeight, priority, cancellationToken);

        lock (_queueLock) {
            _queue.Enqueue(request, request.Priority);
            EvictCancelledTasks();
        }

        _wakeupChannel.Writer.TryWrite(true);

        return await request.Tcs.Task;
    }

    private void EvictCancelledTasks() {
        if (_queue.Count < 20) return;

        var active = new List<(ThumbnailRequest Request, int Priority)>();
        while (_queue.TryDequeue(out var req, out var priority)) {
            if (!req.CancellationToken.IsCancellationRequested) {
                active.Add((req, priority));
            }
        }

        foreach (var (req, priority) in active) {
            _queue.Enqueue(req, priority);
        }
    }

    private async Task WorkerLoopAsync() {
        while (!_cts.Token.IsCancellationRequested) {
            try {
                await _wakeupChannel.Reader.ReadAsync(_cts.Token).ConfigureAwait(false);

                ThumbnailRequest? request = null;
                lock (_queueLock) {
                    if (_queue.Count > 0) {
                        request = _queue.Dequeue();
                    }
                }

                if (request == null || request.CancellationToken.IsCancellationRequested) {
                    continue;
                }

                try {
                    var bitmap = await ProcessRequestAsync(request).ConfigureAwait(false);
                    if (!request.CancellationToken.IsCancellationRequested) {
                        request.Tcs.TrySetResult(bitmap);
                    } else {
                        request.Tcs.TrySetCanceled();
                    }
                } catch (OperationCanceledException) {
                    request.Tcs.TrySetCanceled();
                } catch (Exception ex) {
                    request.Tcs.TrySetException(ex);
                }
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                Log.Error(ex, "Error in thumbnail registry worker loop");
            }
        }
    }

    private async Task<Bitmap?> ProcessRequestAsync(ThumbnailRequest request) {
        request.CancellationToken.ThrowIfCancellationRequested();

        if (_lruCache.TryGet(request.FilePath, out var cached)) {
            return cached;
        }

        // Fast-pass: if the file is already a pre-generated thumbnail, decode it directly
        if (IsPreGeneratedThumbnail(request.FilePath) && File.Exists(request.FilePath)) {
            try {
                using var fs = new FileStream(request.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                var bmp = Bitmap.DecodeToHeight(fs, request.TargetHeight);
                _lruCache.Add(request.FilePath, bmp);
                return bmp;
            } catch (Exception ex) {
                Log.Warning(ex, "Failed to load pre-generated thumbnail directly for {Path}", request.FilePath);
            }
        }

        var cachePath = Path.Combine(_cacheDir, request.CacheKey + ".jpg");

        if (File.Exists(cachePath)) {
            try {
                using var fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                var bmp = Bitmap.DecodeToHeight(fs, request.TargetHeight);
                _lruCache.Add(request.FilePath, bmp);
                return bmp;
            } catch (Exception ex) {
                Log.Warning(ex, "Failed to load thumbnail from disk cache for {Path}", request.FilePath);
            }
        }

        request.CancellationToken.ThrowIfCancellationRequested();

        if (IsRawFile(request.FilePath)) {
            var rawBytes = await ExtractExifThumbnailBytesAsync(request.FilePath, request.CancellationToken).ConfigureAwait(false);
            request.CancellationToken.ThrowIfCancellationRequested();

            if (rawBytes != null && rawBytes.Length > 0) {
                try {
                    await File.WriteAllBytesAsync(cachePath, rawBytes, request.CancellationToken).ConfigureAwait(false);
                    request.CancellationToken.ThrowIfCancellationRequested();

                    using var ms = new MemoryStream(rawBytes);
                    var bmp = Bitmap.DecodeToHeight(ms, request.TargetHeight);
                    _lruCache.Add(request.FilePath, bmp);
                    return bmp;
                } catch (Exception ex) {
                    Log.Error(ex, "Failed decoding or saving extracted RAW thumbnail for {Path}", request.FilePath);
                }
            }
        }

        request.CancellationToken.ThrowIfCancellationRequested();

        try {
            using var image = await Task.Run(() => {
                var img = Image.Load(request.FilePath);
                img.Mutate(x => x.AutoOrient());
                return img;
            }, request.CancellationToken).ConfigureAwait(false);

            request.CancellationToken.ThrowIfCancellationRequested();

            image.Mutate(x => x.Resize(new ResizeOptions {
                Size = new Size(0, request.TargetHeight),
                Mode = ResizeMode.Max
            }));

            request.CancellationToken.ThrowIfCancellationRequested();

            await Task.Run(() => image.SaveAsJpeg(cachePath, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 85 }), request.CancellationToken).ConfigureAwait(false);
            request.CancellationToken.ThrowIfCancellationRequested();

            using var fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            var bmp = Bitmap.DecodeToHeight(fs, request.TargetHeight);
            _lruCache.Add(request.FilePath, bmp);
            return bmp;
        } catch (Exception ex) {
            Log.Error(ex, "Failed full decode and downscale for {Path}", request.FilePath);
            return null;
        }
    }

    private async Task<byte[]?> ExtractExifThumbnailBytesAsync(string filePath, CancellationToken cancellationToken) {
        return await Task.Run(() => {
            try {
                var directories = ImageMetadataReader.ReadMetadata(filePath);
                var thumbDir = directories.OfType<ExifThumbnailDirectory>().FirstOrDefault();
                if (thumbDir == null) return null;

                if (thumbDir.TryGetInt32(ExifThumbnailDirectory.TagThumbnailOffset, out var offset) &&
                    thumbDir.TryGetInt32(ExifThumbnailDirectory.TagThumbnailLength, out var length)) {
                    
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var buffer = new byte[length];
                    fs.Position = offset;
                    var bytesRead = fs.Read(buffer, 0, length);
                    
                    if (bytesRead == length && buffer[0] == 0xFF && buffer[1] == 0xD8) {
                        return buffer;
                    }
                }
            } catch (Exception ex) {
                Log.Warning(ex, "Failed to extract EXIF thumbnail from {Path}", filePath);
            }
            return null;
        }, cancellationToken);
    }

    private bool IsRawFile(string path) {
        var ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) && RawExtensions.Contains(ext);
    }

    private bool IsPreGeneratedThumbnail(string path) {
        return path.Contains("Thumbnails", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() {
        _cts.Cancel();
        _cts.Dispose();
        _lruCache.Clear();
    }
}
