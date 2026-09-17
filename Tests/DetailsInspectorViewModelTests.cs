using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Database.Domain.Entities;
using Domain.Interfaces;
using Domain.Models;
using Graph.Domain.Interfaces;
using Graph.Infrastructure.Services;
using NUnit.Framework;
using PictureWorker.Domain.Interfaces;
using Picturebot.ViewModels;

namespace Tests;

[TestFixture]
public class DetailsInspectorViewModelTests {
    private class FakeNodeService : INodeService {
        public Task<List<Node>> GetAllNodesAsync() => Task.FromResult(new List<Node>());
        public Task<Node?> GetNodeByIdAsync(Guid id) => Task.FromResult<Node?>(null);
        public Task CreateNodeAsync(Node node) => Task.CompletedTask;
        public Task<bool> IsPictureHashDuplicateAsync(int parentId, ulong hash) => Task.FromResult(false);
        public Task<bool> ExistsAsync(int? parentId, string name, Domain.Enums.NodeType type) => Task.FromResult(false);
        public Task<List<Node>> LoadHydratedTreeAsync() => Task.FromResult(new List<Node>());
        public Task<List<Node>> FindChildrenAsync(int parentId) => Task.FromResult(new List<Node>());
        public Task UpdateNodeAsync(Node node) => Task.CompletedTask;
        public Task DeleteNodeAsync(Node node) => Task.CompletedTask;
        public Task<List<Picture>> SearchPicturesGlobalAsync(string query, CancellationToken cancellationToken = default) => Task.FromResult(new List<Picture>());
    }

    private class FakeCurationQueue : ICurationQueue {
        public int Count => EnqueuedPictures.Count;
        public List<Picture> EnqueuedPictures { get; } = new();
        public void Enqueue(Picture picture) => EnqueuedPictures.Add(picture);
        public void Start() { }
        public void Stop() { }
    }

    private class FakeAlbumService : IAlbumService {
        public Task DeletePictureAsync(Picture picture) => Task.CompletedTask;
        public Task<Album> CreateAsync(int? parentId, string name, string path) => Task.FromResult(new Album());
        public Task DeleteAsync(Album album) => Task.CompletedTask;
        public Task SyncPickedStatusAsync(Album album) => Task.CompletedTask;
        public Task SyncHighlightsAsync(Album album) => Task.CompletedTask;
    }

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

    private FakeSettingsService _settingsService = null!;
    private FakeCurationQueue _curationQueue = null!;
    private TaxonomyService _taxonomyService = null!;
    private DetailsInspectorViewModel _viewModel = null!;

    [SetUp]
    public void SetUp() {
        _settingsService = new FakeSettingsService {
            Current = new SettingsModel {
                MasterTags = new List<Tag> {
                    new() { Id = Guid.NewGuid(), Name = "Car" },
                    new() { Id = Guid.NewGuid(), Name = "Truck" },
                    new() { Id = Guid.NewGuid(), Name = "Hero" },
                    new() { Id = Guid.NewGuid(), Name = "Flagged" }
                },
                HierarchyNodes = new List<HierarchyNode> {
                    new() {
                        NodeId = Guid.NewGuid(),
                        Name = "Vehicles",
                        Children = new ObservableCollection<HierarchyNode> {
                            new() { NodeId = Guid.NewGuid(), Name = "Car" },
                            new() { NodeId = Guid.NewGuid(), Name = "Truck" }
                        }
                    },
                    new() {
                        NodeId = Guid.NewGuid(),
                        Name = "Nature",
                        Children = new ObservableCollection<HierarchyNode> {
                            new() {
                                NodeId = Guid.NewGuid(),
                                Name = "Mountains",
                                Children = new ObservableCollection<HierarchyNode> {
                                    new() { NodeId = Guid.NewGuid(), Name = "Alps" }
                                }
                            }
                        }
                    }
                }
            }
        };

        _curationQueue = new FakeCurationQueue();
        _taxonomyService = new TaxonomyService(_settingsService);

        _viewModel = new DetailsInspectorViewModel(
            new FakeNodeService(),
            _curationQueue,
            _settingsService,
            new FakeAlbumService(),
            taxonomyService: _taxonomyService
        );
    }

    [Test]
    public void DeduplicateAndFormatKeywords_ShouldMergeHierarchicalAndFlatSegmentsIntoSingleChip() {
        // Raw XMP keywords with hierarchical path and flat segments
        var raw = new List<string> { "Vehicles|Car", "Vehicles", "Car" };

        var chips = DetailsInspectorViewModel.DeduplicateAndFormatKeywords(raw);

        Assert.That(chips.Count, Is.EqualTo(1));
        Assert.That(chips[0].IsHierarchical, Is.True);
        Assert.That(chips[0].DisplayText, Is.EqualTo("Vehicles › Car"));
        Assert.That(chips[0].ParentPath, Is.EqualTo("Vehicles"));
        Assert.That(chips[0].LeafName, Is.EqualTo("Car"));
        Assert.That(chips[0].RawValue, Is.EqualTo("Vehicles|Car"));
    }

    [Test]
    public void DeduplicateAndFormatKeywords_ShouldKeepStandaloneFlatTags() {
        var raw = new List<string> { "Vehicles|Car", "Vehicles", "Car", "Hero", "Flagged" };

        var chips = DetailsInspectorViewModel.DeduplicateAndFormatKeywords(raw);

        Assert.That(chips.Count, Is.EqualTo(3));
        Assert.That(chips.Any(c => c.DisplayText == "Vehicles › Car" && c.IsHierarchical), Is.True);
        Assert.That(chips.Any(c => c.DisplayText == "Hero" && !c.IsHierarchical), Is.True);
        Assert.That(chips.Any(c => c.DisplayText == "Flagged" && !c.IsHierarchical), Is.True);
    }

    [Test]
    public void DeduplicateAndFormatKeywords_ShouldSubsumeShorterPrefixPaths() {
        var raw = new List<string> {
            "Nature|Mountains",
            "Nature|Mountains|Alps",
            "Nature",
            "Mountains",
            "Alps"
        };

        var chips = DetailsInspectorViewModel.DeduplicateAndFormatKeywords(raw);

        Assert.That(chips.Count, Is.EqualTo(1));
        Assert.That(chips[0].DisplayText, Is.EqualTo("Nature › Mountains › Alps"));
        Assert.That(chips[0].ParentPath, Is.EqualTo("Nature › Mountains"));
        Assert.That(chips[0].LeafName, Is.EqualTo("Alps"));
    }

    [Test]
    public void AddKeyword_WithHierarchicalBreadcrumb_ShouldAddPathAndAllFlatSegments() {
        var picture = new Picture { Name = "test.jpg", Keywords = new List<string>() };
        var picVm = new PictureItemViewModel(picture);
        _viewModel.SelectedPicture = picVm;

        _viewModel.AddKeyword("Nature › Mountains › Alps");

        Assert.That(picVm.Keywords, Contains.Item("Nature|Mountains|Alps"));
        Assert.That(picVm.Keywords, Contains.Item("Nature"));
        Assert.That(picVm.Keywords, Contains.Item("Mountains"));
        Assert.That(picVm.Keywords, Contains.Item("Alps"));

        Assert.That(_viewModel.ActiveKeywordChips.Count, Is.EqualTo(1));
        Assert.That(_viewModel.ActiveKeywordChips[0].DisplayText, Is.EqualTo("Nature › Mountains › Alps"));
    }

    [Test]
    public void AddKeyword_WithTaxonomyLeafTag_ShouldResolveAndAddHierarchicalHierarchy() {
        var picture = new Picture { Name = "test.jpg", Keywords = new List<string>() };
        var picVm = new PictureItemViewModel(picture);
        _viewModel.SelectedPicture = picVm;

        _viewModel.AddKeyword("Car");

        Assert.That(picVm.Keywords, Contains.Item("Vehicles|Car"));
        Assert.That(picVm.Keywords, Contains.Item("Vehicles"));
        Assert.That(picVm.Keywords, Contains.Item("Car"));

        Assert.That(_viewModel.ActiveKeywordChips.Count, Is.EqualTo(1));
        Assert.That(_viewModel.ActiveKeywordChips[0].DisplayText, Is.EqualTo("Vehicles › Car"));
    }

    [Test]
    public void RemoveKeywordChip_ShouldRemovePathAndCleanUpUnusedSegments() {
        var picture = new Picture {
            Name = "test.jpg",
            Keywords = new List<string> { "Vehicles|Car", "Vehicles", "Car", "Hero" }
        };
        var picVm = new PictureItemViewModel(picture);
        _viewModel.SelectedPicture = picVm;

        Assert.That(_viewModel.ActiveKeywordChips.Count, Is.EqualTo(2));
        var carChip = _viewModel.ActiveKeywordChips.First(c => c.RawValue == "Vehicles|Car");

        _viewModel.RemoveKeywordChip(carChip);

        Assert.That(picVm.Keywords, Does.Not.Contain("Vehicles|Car"));
        Assert.That(picVm.Keywords, Does.Not.Contain("Vehicles"));
        Assert.That(picVm.Keywords, Does.Not.Contain("Car"));
        Assert.That(picVm.Keywords, Contains.Item("Hero"));

        Assert.That(_viewModel.ActiveKeywordChips.Count, Is.EqualTo(1));
        Assert.That(_viewModel.ActiveKeywordChips[0].DisplayText, Is.EqualTo("Hero"));
    }

    [Test]
    public void RemoveKeywordChip_ShouldRetainSegmentsUsedByOtherHierarchies() {
        var picture = new Picture {
            Name = "test.jpg",
            Keywords = new List<string> { "Vehicles|Car", "Vehicles|Truck", "Vehicles", "Car", "Truck" }
        };
        var picVm = new PictureItemViewModel(picture);
        _viewModel.SelectedPicture = picVm;

        Assert.That(_viewModel.ActiveKeywordChips.Count, Is.EqualTo(2));
        var carChip = _viewModel.ActiveKeywordChips.First(c => c.RawValue == "Vehicles|Car");

        _viewModel.RemoveKeywordChip(carChip);

        Assert.That(picVm.Keywords, Does.Not.Contain("Vehicles|Car"));
        Assert.That(picVm.Keywords, Does.Not.Contain("Car"));
        // "Vehicles" must be retained because "Vehicles|Truck" is still present
        Assert.That(picVm.Keywords, Contains.Item("Vehicles"));
        Assert.That(picVm.Keywords, Contains.Item("Vehicles|Truck"));
        Assert.That(picVm.Keywords, Contains.Item("Truck"));

        Assert.That(_viewModel.ActiveKeywordChips.Count, Is.EqualTo(1));
        Assert.That(_viewModel.ActiveKeywordChips[0].DisplayText, Is.EqualTo("Vehicles › Truck"));
    }

    [Test]
    public void ActiveKeywordGroups_GroupsTaxonomyParentsAsTitlesAndChildrenAsPills() {
        var picture = new Picture {
            Name = "test.jpg",
            Keywords = new List<string> {
                "faces|robin", "faces|katsiuska", "faces", "robin", "katsiuska",
                "Vehicles|Car", "Vehicles|Truck", "Vehicles", "Car", "Truck",
                "Hero", "Flagged"
            }
        };
        var picVm = new PictureItemViewModel(picture);
        _viewModel.SelectedPicture = picVm;

        // Verify active keyword groups
        Assert.That(_viewModel.ActiveKeywordGroups.Count, Is.EqualTo(3));

        // Group 1: faces
        var facesGroup = _viewModel.ActiveKeywordGroups.FirstOrDefault(g => g.Title == "faces");
        Assert.That(facesGroup, Is.Not.Null);
        Assert.That(facesGroup!.IsHierarchical, Is.True);
        Assert.That(facesGroup.Chips.Count, Is.EqualTo(2));
        Assert.That(facesGroup.Chips.Select(c => c.LeafName), Is.EqualTo(new[] { "katsiuska", "robin" }));
        Assert.That(facesGroup.IsExpanded, Is.True);

        // Group 2: Vehicles
        var vehiclesGroup = _viewModel.ActiveKeywordGroups.FirstOrDefault(g => g.Title == "Vehicles");
        Assert.That(vehiclesGroup, Is.Not.Null);
        Assert.That(vehiclesGroup!.IsHierarchical, Is.True);
        Assert.That(vehiclesGroup.Chips.Count, Is.EqualTo(2));
        Assert.That(vehiclesGroup.Chips.Select(c => c.LeafName), Is.EqualTo(new[] { "Car", "Truck" }));

        // Group 3: Flat Keywords
        var flatGroup = _viewModel.ActiveKeywordGroups.FirstOrDefault(g => g.Title == "Keywords");
        Assert.That(flatGroup, Is.Not.Null);
        Assert.That(flatGroup!.IsHierarchical, Is.False);
        Assert.That(flatGroup.Chips.Count, Is.EqualTo(2));
        Assert.That(flatGroup.Chips.Select(c => c.LeafName), Is.EqualTo(new[] { "Flagged", "Hero" }));

        // Toggle expand/collapse
        facesGroup.ToggleExpandCommand.Execute(null);
        Assert.That(facesGroup.IsExpanded, Is.False);
        facesGroup.ToggleExpandCommand.Execute(null);
        Assert.That(facesGroup.IsExpanded, Is.True);
    }

    [Test]
    public void RemoveKeywordGroup_RemovesAllPillsInGroup() {
        var picture = new Picture {
            Name = "test.jpg",
            Keywords = new List<string> {
                "faces|robin", "faces|katsiuska", "faces", "robin", "katsiuska",
                "Hero"
            }
        };
        var picVm = new PictureItemViewModel(picture);
        _viewModel.SelectedPicture = picVm;

        Assert.That(_viewModel.ActiveKeywordGroups.Count, Is.EqualTo(2));
        var facesGroup = _viewModel.ActiveKeywordGroups.First(g => g.Title == "faces");

        _viewModel.RemoveKeywordGroupCommand.Execute(facesGroup);

        Assert.That(picVm.Keywords, Does.Not.Contain("faces|robin"));
        Assert.That(picVm.Keywords, Does.Not.Contain("faces|katsiuska"));
        Assert.That(picVm.Keywords, Does.Not.Contain("robin"));
        Assert.That(picVm.Keywords, Does.Not.Contain("katsiuska"));
        Assert.That(picVm.Keywords, Does.Not.Contain("faces"));
        Assert.That(picVm.Keywords, Contains.Item("Hero"));

        Assert.That(_viewModel.ActiveKeywordGroups.Count, Is.EqualTo(1));
        Assert.That(_viewModel.ActiveKeywordGroups[0].Title, Is.EqualTo("Keywords"));
        Assert.That(_viewModel.ActiveKeywordGroups[0].Chips[0].LeafName, Is.EqualTo("Hero"));
    }

    [Test]
    public void AddKeyword_WhenMultiplePicturesSelected_AppliesTagToAllSelectedPictures() {
        var pic1 = new Picture { Name = "pic1.jpg", Keywords = new List<string>() };
        var pic2 = new Picture { Name = "pic2.jpg", Keywords = new List<string> { "Hero" } };
        var vm1 = new PictureItemViewModel(pic1);
        var vm2 = new PictureItemViewModel(pic2);

        _viewModel.SelectedPictures.Add(vm1);
        _viewModel.SelectedPictures.Add(vm2);
        _viewModel.SelectedPicture = vm1;

        _viewModel.AddKeyword("Car");

        Assert.That(vm1.Keywords, Contains.Item("Vehicles|Car"));
        Assert.That(vm1.Keywords, Contains.Item("Vehicles"));
        Assert.That(vm1.Keywords, Contains.Item("Car"));

        Assert.That(vm2.Keywords, Contains.Item("Vehicles|Car"));
        Assert.That(vm2.Keywords, Contains.Item("Vehicles"));
        Assert.That(vm2.Keywords, Contains.Item("Car"));
        Assert.That(vm2.Keywords, Contains.Item("Hero"));

        Assert.That(_curationQueue.EnqueuedPictures, Contains.Item(pic1));
        Assert.That(_curationQueue.EnqueuedPictures, Contains.Item(pic2));
    }

    [Test]
    public void RemoveKeywordChip_WhenMultiplePicturesSelected_RemovesTagFromAllSelectedPictures() {
        var pic1 = new Picture { Name = "pic1.jpg", Keywords = new List<string> { "Vehicles|Car", "Vehicles", "Car", "Hero" } };
        var pic2 = new Picture { Name = "pic2.jpg", Keywords = new List<string> { "Vehicles|Car", "Vehicles", "Car" } };
        var vm1 = new PictureItemViewModel(pic1);
        var vm2 = new PictureItemViewModel(pic2);

        _viewModel.SelectedPictures.Add(vm1);
        _viewModel.SelectedPictures.Add(vm2);
        _viewModel.SelectedPicture = vm1;

        var carChip = _viewModel.ActiveKeywordChips.First(c => c.RawValue == "Vehicles|Car");
        _viewModel.RemoveKeywordChip(carChip);

        Assert.That(vm1.Keywords, Does.Not.Contain("Vehicles|Car"));
        Assert.That(vm1.Keywords, Does.Not.Contain("Vehicles"));
        Assert.That(vm1.Keywords, Does.Not.Contain("Car"));
        Assert.That(vm1.Keywords, Contains.Item("Hero"));

        Assert.That(vm2.Keywords, Does.Not.Contain("Vehicles|Car"));
        Assert.That(vm2.Keywords, Does.Not.Contain("Vehicles"));
        Assert.That(vm2.Keywords, Does.Not.Contain("Car"));
    }

    [Test]
    public void ToggleQuickTag_WhenMultiplePicturesSelected_AddsTagToAllIfAnyMissing_AndRemovesIfAllPresent() {
        var carTag = _settingsService.Current.MasterTags.First(t => t.Name == "Car");
        _settingsService.Current.TagGroups = new List<TagGroup> {
            new() { GroupId = Guid.NewGuid(), GroupName = "Quick", TagIds = new ObservableCollection<Guid> { carTag.Id } }
        };
        _settingsService.Current.ActiveTagGroupId = _settingsService.Current.TagGroups[0].GroupId;

        // Force rebuild quick tags
        _viewModel.ActiveTagGroup = _settingsService.Current.TagGroups[0];

        var pic1 = new Picture { Name = "pic1.jpg", Keywords = new List<string> { "Vehicles|Car", "Vehicles", "Car" } };
        var pic2 = new Picture { Name = "pic2.jpg", Keywords = new List<string>() };
        var vm1 = new PictureItemViewModel(pic1);
        var vm2 = new PictureItemViewModel(pic2);

        _viewModel.SelectedPictures.Add(vm1);
        _viewModel.SelectedPictures.Add(vm2);
        _viewModel.SelectedPicture = vm1;

        var quickBtn = _viewModel.QuickTagButtons.First(b => b.Tag.Id == carTag.Id);

        // State: pic1 has tag, pic2 does not (not all have it) -> toggling should add to all (pic2 gets it)
        _viewModel.ToggleQuickTagCommand.Execute(quickBtn);

        Assert.That(vm1.Keywords, Contains.Item("Vehicles|Car"));
        Assert.That(vm2.Keywords, Contains.Item("Vehicles|Car"));
        Assert.That(vm2.Keywords, Contains.Item("Vehicles"));
        Assert.That(vm2.Keywords, Contains.Item("Car"));

        // State: both have tag -> toggling should remove from all
        _viewModel.ToggleQuickTagCommand.Execute(quickBtn);

        Assert.That(vm1.Keywords, Does.Not.Contain("Vehicles|Car"));
        Assert.That(vm2.Keywords, Does.Not.Contain("Vehicles|Car"));
    }

    [Test]
    public void GetDxoExecutablePath_ReturnsValidExecutableString() {
        var exePath = DetailsInspectorViewModel.GetDxoExecutablePath();
        Assert.That(exePath, Is.Not.Null.And.Not.Empty);
        Assert.That(exePath, Does.EndWith("DxO.PhotoLab.exe"));
    }

    [Test]
    public void ResolvePicturesForDxo_WhenPicturesExplicitlySelected_ReturnsSelected() {
        var pic1 = new PictureItemViewModel(new Picture { Name = "Pic1" }) { IsSelected = false };
        var pic2 = new PictureItemViewModel(new Picture { Name = "Pic2" }) { IsSelected = true };
        var pic3 = new PictureItemViewModel(new Picture { Name = "Pic3", CurationStatus = Domain.Enums.CurationStatus.Flagged }) { IsSelected = false };

        var list = new List<PictureItemViewModel> { pic1, pic2, pic3 };
        var resolved = GalleryViewModel.ResolvePicturesForDxo(list, list, new List<PictureItemViewModel> { pic2 }, pic2);

        Assert.That(resolved.Count, Is.EqualTo(1));
        Assert.That(resolved[0].Name, Is.EqualTo("Pic2"));
    }

    [Test]
    public void ResolvePicturesForDxo_WhenNoSelectionAndHasPicked_ReturnsFirstPicked() {
        var pic1 = new PictureItemViewModel(new Picture { Name = "Pic1", CurationStatus = Domain.Enums.CurationStatus.Unflagged });
        var pic2 = new PictureItemViewModel(new Picture { Name = "Pic2", CurationStatus = Domain.Enums.CurationStatus.Flagged });
        var pic3 = new PictureItemViewModel(new Picture { Name = "Pic3", CurationStatus = Domain.Enums.CurationStatus.Flagged });

        var list = new List<PictureItemViewModel> { pic1, pic2, pic3 };
        var resolved = GalleryViewModel.ResolvePicturesForDxo(list, list, new List<PictureItemViewModel>(), null);

        Assert.That(resolved.Count, Is.EqualTo(1));
        Assert.That(resolved[0].Name, Is.EqualTo("Pic2"));
    }

    [Test]
    public void ResolvePicturesForDxo_WhenNoSelectionAndNoPicked_ReturnsFirstPicture() {
        var pic1 = new PictureItemViewModel(new Picture { Name = "Pic1", CurationStatus = Domain.Enums.CurationStatus.Unflagged });
        var pic2 = new PictureItemViewModel(new Picture { Name = "Pic2", CurationStatus = Domain.Enums.CurationStatus.Unflagged });

        var list = new List<PictureItemViewModel> { pic1, pic2 };
        var resolved = GalleryViewModel.ResolvePicturesForDxo(list, list, new List<PictureItemViewModel>(), null);

        Assert.That(resolved.Count, Is.EqualTo(1));
        Assert.That(resolved[0].Name, Is.EqualTo("Pic1"));
    }

    [Test]
    public void ResolvePicturesForDxo_WhenEmptyAlbum_ReturnsEmpty() {
        var list = new List<PictureItemViewModel>();
        var resolved = GalleryViewModel.ResolvePicturesForDxo(list, list, new List<PictureItemViewModel>(), null);

        Assert.That(resolved, Is.Empty);
    }

    [Test]
    public void Receive_WhenNullPictureSelectedMessageReceived_ClearsSelectedPicturesAndMode() {
        var pic1 = new PictureItemViewModel(new Picture { Name = "Pic1", Keywords = new List<string> { "Tag1" } });
        _viewModel.SelectedPictures.Add(pic1);
        _viewModel.SelectedPicture = pic1;

        Assert.That(_viewModel.SelectedPictures.Count, Is.EqualTo(1));
        Assert.That(_viewModel.SelectedPicture, Is.Not.Null);

        _viewModel.Receive(new Picturebot.Messages.PictureSelectedMessage(null));

        Assert.That(_viewModel.SelectedPicture, Is.Null);
        Assert.That(_viewModel.SelectedPictures, Is.Empty);
        Assert.That(_viewModel.ActiveKeywordChips, Is.Empty);
    }

    [Test]
    public void ResolveRawOrImagePath_WhenSubFolderRawIsSet_ReturnsRawPath() {
        var pic = new Picture {
            Name = "RAW_001",
            Extension = ".ARW",
            SubFolder = new SubFolder {
                Raw = @"D:\Photos\RAW_001.ARW",
                Preview = @"D:\Photos\RAW_001.jpg"
            }
        };

        var resolved = DetailsInspectorViewModel.ResolveRawOrImagePath(pic);
        Assert.That(resolved, Is.EqualTo(@"D:\Photos\RAW_001.ARW"));
    }
}


