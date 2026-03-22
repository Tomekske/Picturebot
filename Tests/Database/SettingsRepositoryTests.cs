using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Database.Infrastructure.Data;
using Database.Infrastructure.Repositories;
using Domain.Models;
using Domain.Enums;

namespace Tests.Database;

[TestFixture]
public class SettingsRepositoryTests
{
    private ApplicationDbContext _context;
    private SqliteConnection _connection;
    private SettingsRepository _repository;

    [SetUp]
    public void Setup()
    {
        // 1. Create a connection to a fresh in-memory SQLite DB
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        // 2. Configure DbContext to use this connection
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated(); // Creates the 'settings' table

        // 3. Initialize the repository
        _repository = new SettingsRepository(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Close(); // This deletes the in-memory database
    }

    [Test]
    public async Task LoadAsync_WhenDatabaseIsEmpty_ShouldCreateAndReturnDefaultSettings()
    {
        // Act
        var result = await _repository.LoadAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(_context.Settings.Count(), Is.EqualTo(1));
            Assert.That(result.GroupingThreshold, Is.EqualTo(5)); // Default from Settings.cs
        });
    }

    [Test]
    public async Task UpdateAsync_ShouldModifyExistingRecord()
    {
        // Arrange - Ensure a record exists first
        await _repository.LoadAsync();
        var update = new SettingsModel 
        { 
            LibraryPath = "C:/NewPath", 
            ThemeMode = ThemeMode.Dark 
        };

        // Act
        await _repository.UpdateAsync(update);

        // Assert
        var result = await _repository.LoadAsync();
        Assert.Multiple(() =>
        {
            Assert.That(result.LibraryPath, Is.EqualTo("C:/NewPath"));
            Assert.That(result.ThemeMode, Is.EqualTo(ThemeMode.Dark));
            Assert.That(_context.Settings.Count(), Is.EqualTo(1)); // Ensure no duplicate was made
        });
    }
}