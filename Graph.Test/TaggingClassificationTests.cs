using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Database.Domain.Entities;
using Database.Infrastructure.Data;
using Domain.Interfaces;
using Domain.Models;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using PictureWorker.Domain.Interfaces;
using PictureWorker.Infrastructure.Services;

namespace Graph.Test;

[TestFixture]
public class TaggingClassificationTests : IDisposable {
    private string _datasetsPath = string.Empty;
    private ImageEmbeddingService _embeddingService = null!;
    private IFileSystem _fileSystem = null!;
    private ApplicationDbContext _context = null!;
    private SqliteConnection _connection = null!;
    private IServiceScopeFactory _scopeFactory = null!;
    private Mock<IPathService> _mockPathService = null!;
    private Mock<ISettingsService> _mockSettingsService = null!;
    private TaxonomyService _taxonomyService = null!;
    private GlobalExemplarCentroidService _centroidService = null!;
    private XmpService _xmpService = null!;
    private FewShotTagDiscoveryService _discoveryService = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() {
        _datasetsPath = FindDatasetsPath();
        CleanupXmpFiles();
        TestContext.Progress.WriteLine($"Found datasets folder at: {_datasetsPath}");
    }

    [SetUp]
    public void Setup() {
        CleanupXmpFiles();
        _fileSystem = new FileSystem();

        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _mockPathService = new Mock<IPathService>();
        _mockSettingsService = new Mock<ISettingsService>();

        // Set up taxonomy tree: Objects -> Vehicles -> Car, Animals -> Mammals -> Cat / Dog
        var rootVehicles = new HierarchyNode { Name = "Vehicles" };
        rootVehicles.Children.Add(new HierarchyNode { Name = "Car" });

        var rootAnimals = new HierarchyNode { Name = "Animals" };
        var mammals = new HierarchyNode { Name = "Mammals" };
        mammals.Children.Add(new HierarchyNode { Name = "Cat" });
        mammals.Children.Add(new HierarchyNode { Name = "Dog" });
        rootAnimals.Children.Add(mammals);

        var settingsModel = new SettingsModel {
            HierarchyNodes = new List<HierarchyNode> { rootVehicles, rootAnimals }
        };
        _mockSettingsService.Setup(s => s.Current).Returns(settingsModel);

        var services = new ServiceCollection();
        services.AddSingleton(_context);
        services.AddSingleton<IFileSystem>(_fileSystem);
        services.AddSingleton<IPathService>(_mockPathService.Object);
        services.AddSingleton<ISettingsService>(_mockSettingsService.Object);

        var provider = services.BuildServiceProvider();
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        _taxonomyService = new TaxonomyService(_mockSettingsService.Object);
        _embeddingService = new ImageEmbeddingService(_fileSystem, _scopeFactory);
        _centroidService = new GlobalExemplarCentroidService(_scopeFactory);
        _xmpService = new XmpService(_fileSystem, _scopeFactory, _mockPathService.Object);
        _discoveryService = new FewShotTagDiscoveryService(
            _embeddingService,
            _centroidService,
            _taxonomyService,
            _xmpService,
            _scopeFactory
        );
    }

    [TearDown]
    public void TearDown() {
        CleanupXmpFiles();
        _context?.Dispose();
        _connection?.Close();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() {
        CleanupXmpFiles();
    }

    public void Dispose() {
        TearDown();
    }

    private void CleanupXmpFiles() {
        if (string.IsNullOrEmpty(_datasetsPath) || !Directory.Exists(_datasetsPath)) {
            return;
        }

        try {
            var xmpFiles = Directory.GetFiles(_datasetsPath, "*.xmp", SearchOption.AllDirectories);
            foreach (var file in xmpFiles) {
                try {
                    File.Delete(file);
                } catch {
                    // Ignore transient locks
                }
            }
        } catch {
            // Ignore directory scanning errors
        }
    }

    [Test]
    public void DatasetsDirectory_ContainsExpectedStructureAndFiles() {
        Assert.That(Directory.Exists(_datasetsPath), Is.True, "Datasets folder must exist");

        string[] categories = ["Car", "Cat", "Dog"];
        foreach (var category in categories) {
            var catDir = Path.Combine(_datasetsPath, category);
            Assert.That(Directory.Exists(catDir), Is.True, $"Category folder '{category}' must exist");

            var trainDir = Path.Combine(catDir, "Training");
            var testCorrectDir = Path.Combine(catDir, "Testing", "Correct");
            var testIncorrectDir = Path.Combine(catDir, "Testing", "Incorrect");

            Assert.That(Directory.Exists(trainDir), Is.True, $"Training folder for '{category}' must exist");
            Assert.That(Directory.Exists(testCorrectDir), Is.True, $"Testing/Correct folder for '{category}' must exist");
            Assert.That(Directory.Exists(testIncorrectDir), Is.True, $"Testing/Incorrect folder for '{category}' must exist");

            var trainFiles = Directory.GetFiles(trainDir, "*.jpg");
            var correctFiles = Directory.GetFiles(testCorrectDir, "*.jpg");
            var incorrectFiles = Directory.GetFiles(testIncorrectDir, "*.jpg");

            Assert.That(trainFiles.Length, Is.EqualTo(10), $"'{category}' should have 10 training images");
            Assert.That(correctFiles.Length, Is.GreaterThanOrEqualTo(10), $"'{category}' should have >= 10 correct test images");
            Assert.That(incorrectFiles.Length, Is.EqualTo(10), $"'{category}' should have 10 incorrect test images");

            TestContext.Progress.WriteLine($"Category [{category}]: Train={trainFiles.Length}, TestCorrect={correctFiles.Length}, TestIncorrect={incorrectFiles.Length}");
        }
    }

    [Test]
    public async Task FeatureExtraction_ProducesNormalizedNonZeroVectors() {
        var sampleImage = Directory.GetFiles(Path.Combine(_datasetsPath, "Car", "Training"), "*.jpg").First();
        var embedding = await _embeddingService.ComputeEmbeddingAsync(sampleImage);

        Assert.That(embedding, Is.Not.Null);
        Assert.That(embedding.Length, Is.EqualTo(512));

        // Verify L2 norm is approximately 1.0
        double normSq = embedding.Sum(x => x * x);
        Assert.That(normSq, Is.EqualTo(1.0).Within(1e-4), "Embedding vector must be unit-normalized (L2 norm = 1)");

        // Verify it contains meaningful non-zero features across different dimensions
        int nonZeroCount = embedding.Count(x => Math.Abs(x) > 1e-6f);
        Assert.That(nonZeroCount, Is.GreaterThan(200), "Embedding must have rich non-zero feature activations across dimensions");
    }

    [Test]
    public async Task Benchmark_ThresholdSweep_LogsPrecisionRecallCurve() {
        string[] categories = ["Car", "Cat", "Dog"];
        var trainEmbeddings = new Dictionary<string, List<float[]>>();
        var testCorrectEmbeddings = new Dictionary<string, List<float[]>>();
        var testIncorrectEmbeddings = new Dictionary<string, List<float[]>>();

        // 1. Precompute embeddings
        foreach (var category in categories) {
            var trainDir = Path.Combine(_datasetsPath, category, "Training");
            var correctDir = Path.Combine(_datasetsPath, category, "Testing", "Correct");
            var incorrectDir = Path.Combine(_datasetsPath, category, "Testing", "Incorrect");

            trainEmbeddings[category] = await LoadEmbeddingsAsync(trainDir);
            testCorrectEmbeddings[category] = await LoadEmbeddingsAsync(correctDir);
            testIncorrectEmbeddings[category] = await LoadEmbeddingsAsync(incorrectDir);
        }

        // 2. Compute centroids
        var centroids = new Dictionary<string, float[]>();
        foreach (var category in categories) {
            centroids[category] = ComputeCentroid(trainEmbeddings[category]);
        }

        foreach (var category in categories) {
            var simList = testCorrectEmbeddings[category].Select(v => CosineSimilarity(v, centroids[category])).ToList();
            TestContext.Progress.WriteLine($"Sim for {category} Correct test images (N={simList.Count}): [{string.Join(", ", simList.Select(s => s.ToString("F3")))}]");
            var incorrSimList = testIncorrectEmbeddings[category].Select(v => CosineSimilarity(v, centroids[category])).ToList();
            TestContext.Progress.WriteLine($"Sim for {category} Incorrect test images (N={incorrSimList.Count}): [{string.Join(", ", incorrSimList.Select(s => s.ToString("F3")))}]");
        }

        TestContext.Progress.WriteLine("\n--- THRESHOLD SWEEP BENCHMARK (Car, Cat, Dog) ---");
        TestContext.Progress.WriteLine($"{"Threshold",-10} | {"Category",-8} | {"TP",-4} | {"FN",-4} | {"TN",-4} | {"FP",-4} | {"Precision",-10} | {"Recall",-10} | {"Accuracy",-10} | {"F1",-10}");
        TestContext.Progress.WriteLine(new string('-', 95));

        float bestAvgF1 = 0f;
        float bestThreshold = 0f;

        for (float threshold = 0.40f; threshold <= 0.85f; threshold += 0.05f) {
            float totalF1 = 0f;

            foreach (var category in categories) {
                var centroid = centroids[category];
                var correctList = testCorrectEmbeddings[category];
                var incorrectList = testIncorrectEmbeddings[category];

                int tp = correctList.Count(v => CosineSimilarity(v, centroid) >= threshold);
                int fn = correctList.Count - tp;

                int fp = incorrectList.Count(v => CosineSimilarity(v, centroid) >= threshold);
                int tn = incorrectList.Count - fp;

                float precision = (tp + fp) > 0 ? (float)tp / (tp + fp) : 0f;
                float recall = (tp + fn) > 0 ? (float)tp / (tp + fn) : 0f;
                float accuracy = (float)(tp + tn) / (correctList.Count + incorrectList.Count);
                float f1 = (precision + recall) > 0 ? 2 * precision * recall / (precision + recall) : 0f;

                totalF1 += f1;

                TestContext.Progress.WriteLine($"{threshold,10:F2} | {category,-8} | {tp,-4} | {fn,-4} | {tn,-4} | {fp,-4} | {precision,10:P1} | {recall,10:P1} | {accuracy,10:P1} | {f1,10:F3}");
            }

            float avgF1 = totalF1 / categories.Length;
            TestContext.Progress.WriteLine($"--- Avg F1 @ {threshold:F2} = {avgF1:F3} ---\n");

            if (avgF1 > bestAvgF1) {
                bestAvgF1 = avgF1;
                bestThreshold = threshold;
            }
        }

        TestContext.Progress.WriteLine($"\n>>> BEST OPERATING THRESHOLD: {bestThreshold:F2} with Average F1 = {bestAvgF1:F3} <<<\n");
        Assert.That(bestAvgF1, Is.GreaterThan(0.60f), "Tuned visual embedding must achieve strong F1 performance across datasets");
    }

    [Test]
    public async Task EndToEnd_TaggingClassification_VerifiesHighAccuracyOnDatasets() {
        string[] categories = ["Car", "Cat", "Dog"];
        int pictureIdCounter = 1;

        _mockPathService.Setup(ps => ps.PopulatePaths(It.IsAny<Picture>()))
            .Callback<Picture>(p => {
                if (p.SubFolder == null && !string.IsNullOrEmpty(p.Name)) {
                    p.SubFolder = new SubFolder { Raw = p.Name };
                }
            });

        // 1. Seed training exemplars into database for each category
        foreach (var category in categories) {
            var trainDir = Path.Combine(_datasetsPath, category, "Training");
            var files = Directory.GetFiles(trainDir, "*.jpg").OrderBy(f => f).ToList();

            foreach (var file in files) {
                var emb = await _embeddingService.ComputeEmbeddingAsync(file);

                var pic = new Picture {
                    Name = file, // Store full path in Name for test convenience
                    KeywordsJson = JsonSerializer.Serialize(new[] { category }),
                    SubFolder = new SubFolder { Raw = file }
                };

                var metrics = new Metrics();
                metrics.SetEmbeddingVector(emb);
                pic.Metrics = metrics;

                _context.Pictures.Add(pic);
            }
        }
        await _context.SaveChangesAsync();

        var dbPics = await _context.Pictures.Include(p => p.Metrics).ToListAsync();
        TestContext.Progress.WriteLine($"DB Pictures in _context: Count={dbPics.Count}");
        foreach (var p in dbPics.Take(3)) {
            TestContext.Progress.WriteLine($"Pic: ID={p.Id}, Name={p.Name}, KW={p.KeywordsJson}, MetricsNull={p.Metrics == null}, EmbLen={p.Metrics?.Embedding?.Length}");
        }

        // 2. Fetch leaf centroids via GlobalExemplarCentroidService
        _centroidService.MinimumExemplarThreshold = 10;
        var centroids = await _centroidService.GetActiveLeafCentroidsAsync();

        var directCarCentroid = ComputeCentroid(await LoadEmbeddingsAsync(Path.Combine(_datasetsPath, "Car", "Training")));
        var dbCarCentroid = centroids["Car"];
        TestContext.Progress.WriteLine($"Direct Car Centroid [0..4]: {string.Join(", ", directCarCentroid.Take(5).Select(x => x.ToString("F4")))}");
        TestContext.Progress.WriteLine($"DB Car Centroid [0..4]:     {string.Join(", ", dbCarCentroid.Take(5).Select(x => x.ToString("F4")))}");
        TestContext.Progress.WriteLine($"Cosine Sim between DB and Direct Centroid: {CosineSimilarity(dbCarCentroid, directCarCentroid):F4}");

        Assert.That(centroids.ContainsKey("Car"), Is.True, "Car centroid should be active");
        Assert.That(centroids.ContainsKey("Cat"), Is.True, "Cat centroid should be active");
        Assert.That(centroids.ContainsKey("Dog"), Is.True, "Dog centroid should be active");

        // Calibrate discovery threshold
        _discoveryService.SimilarityThreshold = 0.55f;

        TestContext.Progress.WriteLine("\n--- END-TO-END CLASSIFICATION RESULTS ---");

        int totalCorrectEvaluations = 0;
        int totalIncorrectEvaluations = 0;
        int truePositives = 0;
        int trueNegatives = 0;
        int testIdCounter = 1000;

        foreach (var category in categories) {
            var correctFiles = Directory.GetFiles(Path.Combine(_datasetsPath, category, "Testing", "Correct"), "*.jpg");
            var incorrectFiles = Directory.GetFiles(Path.Combine(_datasetsPath, category, "Testing", "Incorrect"), "*.jpg");

            int catTP = 0;
            int catFN = 0;
            int catTN = 0;
            int catFP = 0;

            // Test Positive Cases (Testing/Correct)
            foreach (var file in correctFiles) {
                int pid = testIdCounter++;
                var pic = new Picture {
                    Id = pid,
                    Name = file,
                    SubFolder = new SubFolder { Raw = file }
                };

                var results = await _discoveryService.ScanPicturesAsync(new List<Picture> { pic });
                bool taggedWithCategory = pic.Keywords != null && pic.Keywords.Any(k => k.Equals(category, StringComparison.OrdinalIgnoreCase));

                if (taggedWithCategory) {
                    catTP++;
                    truePositives++;
                } else {
                    catFN++;
                }
                totalCorrectEvaluations++;
            }

            // Test Negative Cases (Testing/Incorrect)
            foreach (var file in incorrectFiles) {
                int pid = testIdCounter++;
                var pic = new Picture {
                    Id = pid,
                    Name = file,
                    SubFolder = new SubFolder { Raw = file }
                };

                var results = await _discoveryService.ScanPicturesAsync(new List<Picture> { pic });
                bool taggedWithCategory = pic.Keywords != null && pic.Keywords.Any(k => k.Equals(category, StringComparison.OrdinalIgnoreCase));

                if (!taggedWithCategory) {
                    catTN++;
                    trueNegatives++;
                } else {
                    catFP++;
                }
                totalIncorrectEvaluations++;
            }

            float precision = (catTP + catFP) > 0 ? (float)catTP / (catTP + catFP) : 0f;
            float recall = (float)catTP / (catTP + catFN);
            float accuracy = (float)(catTP + catTN) / (catTP + catFN + catTN + catFP);
            float f1 = (precision + recall) > 0 ? 2 * precision * recall / (precision + recall) : 0f;

            TestContext.Progress.WriteLine($"[{category,4}] TP={catTP,2}/{correctFiles.Length} | FN={catFN,2} | TN={catTN,2}/{incorrectFiles.Length} | FP={catFP,2} | Accuracy={accuracy,6:P1} | Precision={precision,6:P1} | Recall={recall,6:P1} | F1={f1,5:F3}");

            Assert.That(recall, Is.GreaterThanOrEqualTo(0.50f), $"Recall for {category} should be >= 50%");
            Assert.That(precision, Is.GreaterThanOrEqualTo(0.50f), $"Precision for {category} should be >= 50%");
        }

        float overallAccuracy = (float)(truePositives + trueNegatives) / (totalCorrectEvaluations + totalIncorrectEvaluations);
        TestContext.Progress.WriteLine($"\nOVERALL ACCURACY across all 3 datasets: {overallAccuracy:P1} ({truePositives + trueNegatives}/{totalCorrectEvaluations + totalIncorrectEvaluations})");

        Assert.That(overallAccuracy, Is.GreaterThanOrEqualTo(0.65f), "Overall classification accuracy across datasets should be >= 65%");
    }

    [Test]
    public async Task InterClassSeparation_IntraClassSimilarityIsSignificantlyHigherThanCrossClass() {
        string[] categories = ["Car", "Cat", "Dog"];
        var trainEmbeddings = new Dictionary<string, List<float[]>>();
        var testEmbeddings = new Dictionary<string, List<float[]>>();

        foreach (var category in categories) {
            trainEmbeddings[category] = await LoadEmbeddingsAsync(Path.Combine(_datasetsPath, category, "Training"));
            testEmbeddings[category] = await LoadEmbeddingsAsync(Path.Combine(_datasetsPath, category, "Testing", "Correct"));
        }

        var centroids = categories.ToDictionary(c => c, c => ComputeCentroid(trainEmbeddings[c]));

        TestContext.Progress.WriteLine("\n--- COSINE SIMILARITY MATRIX (Test Set vs Class Centroids) ---");
        TestContext.Progress.WriteLine($"{"Class",-10} | {"Car Centroid",-15} | {"Cat Centroid",-15} | {"Dog Centroid",-15}");
        TestContext.Progress.WriteLine(new string('-', 60));

        foreach (var testClass in categories) {
            var avgSims = categories.ToDictionary(
                c => c,
                c => testEmbeddings[testClass].Average(v => CosineSimilarity(v, centroids[c]))
            );

            TestContext.Progress.WriteLine($"{testClass,-10} | {avgSims["Car"],15:F4} | {avgSims["Cat"],15:F4} | {avgSims["Dog"],15:F4}");

            // Verify that for each class, similarity to its own centroid is highest
            float ownSimilarity = avgSims[testClass];
            foreach (var otherClass in categories.Where(c => c != testClass)) {
                float otherSimilarity = avgSims[otherClass];
                Assert.That(ownSimilarity, Is.GreaterThan(otherSimilarity),
                    $"Intra-class similarity for '{testClass}' ({ownSimilarity:F4}) must exceed cross-class similarity with '{otherClass}' ({otherSimilarity:F4})");
            }
        }
    }

    [Test]
    public void CleanupXmpFiles_RemovesAnyGeneratedXmpFiles() {
        var dummyXmp = Path.Combine(_datasetsPath, "Car", "Testing", "Correct", "temp_test_dummy.xmp");
        File.WriteAllText(dummyXmp, "<xmp></xmp>");
        Assert.That(File.Exists(dummyXmp), Is.True);

        CleanupXmpFiles();

        Assert.That(File.Exists(dummyXmp), Is.False, "XMP file should be cleaned up by CleanupXmpFiles");
    }

    private async Task<List<float[]>> LoadEmbeddingsAsync(string directory) {
        var files = Directory.GetFiles(directory, "*.jpg");
        var list = new List<float[]>();
        foreach (var file in files) {
            var emb = await _embeddingService.ComputeEmbeddingAsync(file);
            list.Add(emb);
        }
        return list;
    }

    private static float[] ComputeCentroid(List<float[]> vectors) {
        var sum = new float[512];
        foreach (var v in vectors) {
            for (int i = 0; i < 512; i++) {
                sum[i] += v[i];
            }
        }

        double sumSq = 0.0;
        for (int i = 0; i < 512; i++) {
            sumSq += sum[i] * sum[i];
        }

        float norm = (float)Math.Sqrt(sumSq);
        if (norm < 1e-9f) norm = 1.0f;

        var normalized = new float[512];
        for (int i = 0; i < 512; i++) {
            normalized[i] = sum[i] / norm;
        }

        return normalized;
    }

    private static float CosineSimilarity(float[] a, float[] b) {
        float dot = 0f;
        for (int i = 0; i < 512; i++) {
            dot += a[i] * b[i];
        }
        return dot;
    }

    private static string FindDatasetsPath() {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir)) {
            var candidate = Path.Combine(dir, "datasets");
            if (Directory.Exists(candidate) && Directory.Exists(Path.Combine(candidate, "Car"))) {
                return candidate;
            }
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        throw new DirectoryNotFoundException("Could not find 'datasets' directory.");
    }
}
