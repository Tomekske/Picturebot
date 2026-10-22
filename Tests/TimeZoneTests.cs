using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Database.Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Models;
using Domain.TimeZones;
using NUnit.Framework;
using Picturebot.Services;
using Picturebot.ViewModels;

namespace Tests;

[TestFixture]
public class TimeZoneTests {
    private class FakeSettingsService : ISettingsService {
        public event PropertyChangedEventHandler? PropertyChanged;
        public SettingsModel Current { get; set; } = new();
        public Task InitializeAsync() => Task.CompletedTask;
        public Task<SettingsModel> GetAsync() => Task.FromResult(Current);

        public Task UpdateAsync(SettingsModel model) {
            Current = model;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
            return Task.CompletedTask;
        }
    }

    private class FakeRecentLocations : IRecentLocationsService {
        public IReadOnlyList<int> GetRecent() => [];
        public void Remember(int folderId) { }
        public string? LastSourceFolder { get; set; }
    }

    [Test]
    public void Catalog_ListsSingleZoneCountryWithoutComment() {
        var taipei = TimeZoneCatalog.Find("Asia/Taipei");

        Assert.That(taipei, Is.Not.Null);
        Assert.That(taipei!.CountryName, Is.EqualTo("Taiwan"));
        Assert.That(taipei.Comment, Is.Null);
        Assert.That(taipei.OffsetText, Is.EqualTo("UTC+8"));
    }

    [Test]
    public void Catalog_ShowsBothOffsetsForZonesWithSummerTime() {
        Assert.That(TimeZoneCatalog.Find("Europe/Brussels")!.OffsetText, Is.EqualTo("UTC+1 / +2"));
        Assert.That(TimeZoneCatalog.Find("America/Denver")!.OffsetText, Is.EqualTo("UTC−7 / −6"));
    }

    [Test]
    public void Catalog_GivesEachZoneOfAMultiZoneCountryItsOwnEntry() {
        var us = TimeZoneCatalog.All.Where(o => o.CountryCode == "US").ToList();

        Assert.That(us.Count, Is.GreaterThan(10));
        Assert.That(us.All(o => o.Comment != null), Is.True);
        Assert.That(us.Select(o => o.Id), Does.Contain("Pacific/Honolulu"));
    }

    [Test]
    public void Search_IgnoresAccentsAndCase() {
        var results = TimeZoneCatalog.Search("REUNION").ToList();

        Assert.That(results.Select(o => o.Id), Does.Contain("Indian/Reunion"));
    }

    [Test]
    public void Search_PutsCityStartMatchesFirst() {
        Assert.That(TimeZoneCatalog.Search("denver").First().Id, Is.EqualTo("America/Denver"));
        Assert.That(TimeZoneCatalog.Search("taiw").First().Id, Is.EqualTo("Asia/Taipei"));
    }

    [Test]
    public void DefaultZoneId_PrefersTheConfiguredZone() {
        Assert.That(TimeZoneCatalog.DefaultZoneId("Asia/Tokyo"), Is.EqualTo("Asia/Tokyo"));
        Assert.That(TimeZoneCatalog.DefaultZoneId(""), Is.EqualTo(TimeZoneCatalog.SystemZoneId()));
    }

    [Test]
    public void NewNodeDialog_InheritsTheZoneOfTheNearestFolderUpThePath() {
        // Japan (Asia/Tokyo) › Kyoto (no zone)
        var kyoto = new Folder { Id = 2, Name = "Kyoto", Type = NodeType.Folder, Children = new List<Node>() };
        var japan = new Folder {
            Id = 1, Name = "Japan", Type = NodeType.Folder, TimeZoneId = "Asia/Tokyo",
            Children = new List<Node> { kyoto }
        };

        var vm = CreateNewNodeDialog([japan], startFolderId: 2);

        // Nothing is copied: the picker shows Japan's zone greyed out and the new node follows Japan
        Assert.That(vm.TimeZoneId, Is.Null);
        Assert.That(vm.InheritedTimeZoneId, Is.EqualTo("Asia/Tokyo"));
        Assert.That(vm.TimeZoneHint, Is.EqualTo("Inherited from Japan."));
    }

    [Test]
    public void TimeZoneHint_StoresAZoneEqualToTheInheritedOneAsNotSet() {
        var miami = new TimeZoneSource("America/New_York", 1, "Miami");

        Assert.That(TimeZoneHint.Normalize("America/New_York", miami, "Europe/Brussels"), Is.Null);
        Assert.That(TimeZoneHint.Normalize("America/Chicago", miami, "Europe/Brussels"), Is.EqualTo("America/Chicago"));
        // Without a folder zone, the default zone is what's inherited
        Assert.That(TimeZoneHint.Normalize("Europe/Brussels", null, "Europe/Brussels"), Is.Null);
    }

    [Test]
    public void TimeZoneHint_SaysWhereTheZoneComesFrom() {
        var miami = new TimeZoneSource("America/New_York", 1, "Miami");

        Assert.That(TimeZoneHint.Describe(null, miami, "Europe/Brussels", isFolder: false),
            Is.EqualTo("Inherited from Miami."));
        Assert.That(TimeZoneHint.Describe(null, null, "Europe/Brussels", isFolder: false),
            Is.EqualTo("Not set: uses the default time zone from Settings."));
        Assert.That(TimeZoneHint.Describe("America/Chicago", miami, "Europe/Brussels", isFolder: false),
            Does.StartWith("Set here. Clear it to use Miami's time zone, United States — New York"));
    }

    [Test]
    public void NewNodeDialog_KeepsAChosenZoneWhenTheLocationChanges() {
        var japan = new Folder {
            Id = 1, Name = "Japan", Type = NodeType.Folder, TimeZoneId = "Asia/Tokyo", Children = new List<Node>()
        };
        var trips = new Folder { Id = 3, Name = "Trips", Type = NodeType.Folder, Children = new List<Node>() };
        var vm = CreateNewNodeDialog([japan, trips], startFolderId: 3);
        Assert.That(vm.TimeZoneId, Is.Null);

        vm.TimeZoneId = "Asia/Taipei";
        vm.Picker.CreateHereCommand.Execute("Japan");

        Assert.That(vm.TimeZoneId, Is.EqualTo("Asia/Taipei"));
    }

    [Test]
    public async Task SettingsDialog_SavesTheDefaultZone() {
        var settings = new FakeSettingsService { Current = new SettingsModel { DefaultTimeZoneId = "Asia/Taipei" } };
        var vm = new SettingsDialogViewModel(settings);
        Assert.That(vm.DefaultTimeZoneId, Is.EqualTo("Asia/Taipei"));

        vm.DefaultTimeZoneId = "America/Denver";
        await vm.SaveSettingsCommand.ExecuteAsync(null);

        Assert.That(settings.Current.DefaultTimeZoneId, Is.EqualTo("America/Denver"));
    }

    private static NewNodeDialogViewModel CreateNewNodeDialog(IEnumerable<Node> roots, int startFolderId) {
        return new NewNodeDialogViewModel(null!, new FakeSettingsService(), null!, new FakeRecentLocations(),
            roots, startFolderId, startAsAlbum: false, _ => { });
    }
}
