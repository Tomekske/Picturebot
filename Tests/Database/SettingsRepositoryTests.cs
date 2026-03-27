using Database.Infrastructure.Data;
using Database.Infrastructure.Repositories;
using Domain.Models;
using Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Tests.Database;

[TestFixture]
public class SettingsRepositoryTests {
    private ApplicationDbContext _context;
    private SqliteConnection _connection;
    private SettingsRepository _repository;

    [SetUp]
    public void Setup() {
        // 1. Create a connection to a fresh in-memory SQLite DB
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        // 2. Configure DbContext to use this connection
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated(); // Creates the 'settings' table and seeds it

        // 3. Initialize the repository
        _repository = new SettingsRepository(_context);
    }

    [TearDown]
    public void TearDown() {
        _context.Dispose();
        _connection.Close(); // This deletes the in-memory database
    }

    [Test]
    public async Task LoadAsync_WhenSettingsExist_ShouldReturnExistingSettings() {
        // Arrange
        var existingSettings = _context.Settings.First();
        existingSettings.LibraryPath = "C:/ExistingPath";
        existingSettings.GroupingThreshold = 15;
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.LoadAsync();

        // Assert
        Assert.Multiple(() => {
            Assert.That(result.LibraryPath, Is.EqualTo("C:/ExistingPath"));
            Assert.That(result.GroupingThreshold, Is.EqualTo(15));
            Assert.That(_context.Settings.Count(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task LoadAsync_WhenDatabaseIsEmpty_ShouldCreateAndReturnDefaultSettings() {
        // Arrange - Clear seeded data to test creation logic
        _context.Settings.RemoveRange(_context.Settings);
        await _context.SaveChangesAsync();
        Assert.That(_context.Settings.Count(), Is.EqualTo(0));

        // Act
        var result = await _repository.LoadAsync();

        // Assert
        Assert.Multiple(() => {
            Assert.That(result, Is.Not.Null);
            Assert.That(_context.Settings.Count(), Is.EqualTo(1));
            // Default from Settings.cs is 5.
            Assert.That(result.GroupingThreshold, Is.EqualTo(5)); 
        });
    }

    [Test]
    public async Task UpdateAsync_WhenSettingsExist_ShouldModifyExistingRecord() {
        // Arrange - Ensure a record exists first
        await _repository.LoadAsync();
        var update = new SettingsModel {
            LibraryPath = "C:/NewPath",
            ThemeMode = ThemeMode.Dark,
            GroupingThreshold = 20,
            LaunchMaximized = true
        };

        // Act
        await _repository.UpdateAsync(update);

        // Assert
        var result = await _repository.LoadAsync();
        Assert.Multiple(() => {
            Assert.That(result.LibraryPath, Is.EqualTo("C:/NewPath"));
            Assert.That(result.ThemeMode, Is.EqualTo(ThemeMode.Dark));
            Assert.That(result.GroupingThreshold, Is.EqualTo(20));
            Assert.That(result.LaunchMaximized, Is.True);
            Assert.That(_context.Settings.Count(), Is.EqualTo(1)); // Ensure no duplicate was made
        });
    }

    [Test]
    public async Task UpdateAsync_WhenDatabaseIsEmpty_ShouldCreateNewRecord() {
        // Arrange - Clear seeded data
        _context.Settings.RemoveRange(_context.Settings);
        await _context.SaveChangesAsync();

        var update = new SettingsModel {
            LibraryPath = "C:/NewPath",
            ThemeMode = ThemeMode.Light,
            GroupingThreshold = 12
        };

        // Act
        await _repository.UpdateAsync(update);

        // Assert
        var result = await _repository.LoadAsync();
        Assert.Multiple(() => {
            Assert.That(result.LibraryPath, Is.EqualTo("C:/NewPath"));
            Assert.That(result.ThemeMode, Is.EqualTo(ThemeMode.Light));
            Assert.That(result.GroupingThreshold, Is.EqualTo(12));
            Assert.That(_context.Settings.Count(), Is.EqualTo(1));
        });
    }
}
