using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Domain.Interfaces;
using Domain.Models;
using NUnit.Framework;
using Picturebot.ViewModels;

namespace Tests;

[TestFixture]
public class SettingsDialogViewModelTests {
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

    private FakeSettingsService _fakeService = null!;

    [SetUp]
    public void SetUp() {
        _fakeService = new FakeSettingsService {
            Current = new SettingsModel {
                MasterTags = new List<Tag> {
                    new() { Id = Guid.NewGuid(), Name = "Landscape" },
                    new() { Id = Guid.NewGuid(), Name = "Portrait" },
                    new() { Id = Guid.NewGuid(), Name = "animals" }
                },
                HierarchyNodes = new List<HierarchyNode> {
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
                },
                TagGroups = new List<TagGroup> {
                    new() { GroupId = Guid.NewGuid(), GroupName = "favorites" }
                }
            }
        };
    }

    [Test]
    public void SubTabNavigation_SwitchesCorrectly() {
        var vm = new SettingsDialogViewModel(_fakeService);

        Assert.That(vm.KeywordsSubTabIndex, Is.EqualTo(0));
        Assert.That(vm.IsTagsSubTabActive, Is.True);
        Assert.That(vm.IsTaxonomySubTabActive, Is.False);

        vm.SelectTaxonomySubTabCommand.Execute(null);
        Assert.That(vm.KeywordsSubTabIndex, Is.EqualTo(1));
        Assert.That(vm.IsTaxonomySubTabActive, Is.True);

        vm.SelectGroupsSubTabCommand.Execute(null);
        Assert.That(vm.KeywordsSubTabIndex, Is.EqualTo(2));
        Assert.That(vm.IsGroupsSubTabActive, Is.True);

        vm.SelectTagsSubTabCommand.Execute(null);
        Assert.That(vm.KeywordsSubTabIndex, Is.EqualTo(0));
        Assert.That(vm.IsTagsSubTabActive, Is.True);
    }

    [Test]
    public void Tags_LoadedSortedAlphabeticallyAndLowercase() {
        var vm = new SettingsDialogViewModel(_fakeService);

        Assert.That(vm.Tags.Count, Is.EqualTo(3));
        Assert.That(vm.Tags[0].Name, Is.EqualTo("animals"));
        Assert.That(vm.Tags[1].Name, Is.EqualTo("landscape"));
        Assert.That(vm.Tags[2].Name, Is.EqualTo("portrait"));
    }

    [Test]
    public void Tags_AddTag_EnforcesLowercaseAndMaintainsAlphabeticalOrder() {
        var vm = new SettingsDialogViewModel(_fakeService);

        vm.NewTagName = " Birds ";
        vm.AddTagCommand.Execute(null);

        Assert.That(vm.Tags.Count, Is.EqualTo(4));
        Assert.That(vm.Tags.Any(t => t.Name == "birds"), Is.True);
        // Alphabetical: animals, birds, landscape, portrait
        Assert.That(vm.Tags[1].Name, Is.EqualTo("birds"));
        Assert.That(string.IsNullOrEmpty(vm.NewTagName), Is.True);

        // Reject duplicate case-insensitive
        vm.NewTagName = "BIRDS";
        vm.AddTagCommand.Execute(null);
        Assert.That(vm.Tags.Count, Is.EqualTo(4));
    }

    [Test]
    public void Tags_InlineRename_EnforcesLowercaseAndMaintainsSorting() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var tagItem = vm.Tags.First(t => t.Name == "portrait");

        tagItem.StartEditCommand.Execute(null);
        tagItem.EditingName = " Action Photography ";
        tagItem.CommitEditCommand.Execute(null);

        Assert.That(tagItem.Name, Is.EqualTo("action photography"));
        // Position shifted to 0 due to alphabetical sorting
        Assert.That(vm.Tags[0].Name, Is.EqualTo("action photography"));
    }

    [Test]
    public void Tags_DeleteUnreferenced_DeletesImmediatelyWithoutPrompt() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var tagItem = vm.Tags.First(t => t.Name == "portrait");

        tagItem.DeleteCommand.Execute(null);

        Assert.That(vm.IsDeleteTagConfirmOpen, Is.False);
        Assert.That(vm.Tags.Any(t => t.Name == "portrait"), Is.False);
    }

    [Test]
    public void Tags_DeleteReferencedInGroup_ShowsConfirmationAndUnlinksOnConfirm() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var tagItem = vm.Tags.First(t => t.Name == "landscape");
        var group = vm.TagGroups.First();
        group.TagIds.Add(tagItem.Id);

        tagItem.DeleteCommand.Execute(null);

        Assert.That(vm.IsDeleteTagConfirmOpen, Is.True);
        Assert.That(vm.DeleteTagConfirmMessage, Does.Contain("1 group(s)"));

        // Cancel
        vm.CancelDeleteTagCommand.Execute(null);
        Assert.That(vm.IsDeleteTagConfirmOpen, Is.False);
        Assert.That(vm.Tags.Any(t => t.Id == tagItem.Id), Is.True);

        // Confirm delete
        tagItem.DeleteCommand.Execute(null);
        vm.ConfirmDeleteTagCommand.Execute(null);

        Assert.That(vm.IsDeleteTagConfirmOpen, Is.False);
        Assert.That(vm.Tags.Any(t => t.Id == tagItem.Id), Is.False);
        Assert.That(group.TagIds.Contains(tagItem.Id), Is.False);
    }

    [Test]
    public void Taxonomy_CalculatedBreadcrumbPath_UpdatesOnSelectionAndIsLowercase() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var root = vm.HierarchyNodes.First();
        var child = root.Children.First();

        vm.SelectedHierarchyNode = root;
        Assert.That(vm.CalculatedBreadcrumbPath, Is.EqualTo("nature"));
        Assert.That(vm.CalculatedXmpPath, Is.EqualTo("nature"));

        vm.SelectedHierarchyNode = child;
        Assert.That(vm.CalculatedBreadcrumbPath, Is.EqualTo("nature › mountains"));
        Assert.That(vm.CalculatedXmpPath, Is.EqualTo("nature|mountains"));
    }

    [Test]
    public void Taxonomy_AddSubNode_EnforcesLowercaseAndRegistersInTagCatalogSorted() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var root = vm.HierarchyNodes.First();
        vm.SelectedHierarchyNode = root;

        vm.OpenAddChildNodePromptCommand.Execute(null);
        Assert.That(vm.IsNodeActionPromptOpen, Is.True);

        vm.NodeActionInputName = " Dark Forest ";
        vm.ConfirmNodeActionCommand.Execute(null);

        Assert.That(vm.IsNodeActionPromptOpen, Is.False);
        Assert.That(root.Children.Any(c => c.Name == "dark forest"), Is.True);
        Assert.That(vm.Tags.Any(t => t.Name == "dark forest"), Is.True);
        // Check sorting
        Assert.That(vm.Tags.Select(t => t.Name).ToList(), Is.Ordered);
    }

    [Test]
    public void Groups_CreateFromTaxonomyBranch_CollectsAllBranchTags() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var natureRoot = vm.HierarchyNodes.First();

        vm.OpenCreateGroupFromBranchPromptCommand.Execute(null);
        Assert.That(vm.IsCreateGroupFromBranchOpen, Is.True);
        Assert.That(vm.AvailableTaxonomyBranches.Count, Is.GreaterThanOrEqualTo(3));

        // Select "nature" branch with all sub-branches (nature, mountains, alps)
        vm.SelectedTaxonomyBranch = vm.AvailableTaxonomyBranches.First(b => b.Node == natureRoot);
        vm.NewBranchGroupName = " nature preset ";
        vm.IncludeSubtreeTags = true;
        vm.ConfirmCreateGroupFromBranchCommand.Execute(null);

        Assert.That(vm.IsCreateGroupFromBranchOpen, Is.False);
        var createdGroup = vm.TagGroups.FirstOrDefault(g => g.GroupName == "nature preset");
        Assert.That(createdGroup, Is.Not.Null);

        // Tags should include nature, mountains, alps
        Assert.That(createdGroup!.TagIds.Count, Is.EqualTo(3));
        Assert.That(vm.SelectedTagGroup, Is.EqualTo(createdGroup));
    }

    [Test]
    public void Groups_ContextMenu_CreateFromTaxonomyNodeCommand() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var root = vm.HierarchyNodes.First();

        vm.CreateGroupFromTaxonomyNode(root);

        Assert.That(vm.KeywordsSubTabIndex, Is.EqualTo(2));
        var group = vm.TagGroups.FirstOrDefault(g => g.GroupName == "nature");
        Assert.That(group, Is.Not.Null);
        Assert.That(group!.TagIds.Count, Is.EqualTo(3));
    }

    [Test]
    public void DiscardChanges_RevertsModifications() {
        var vm = new SettingsDialogViewModel(_fakeService);
        vm.NewTagName = "temporarytag";
        vm.AddTagCommand.Execute(null);
        Assert.That(vm.Tags.Count, Is.EqualTo(4));

        vm.DiscardChangesCommand.Execute(null);
        Assert.That(vm.Tags.Count, Is.EqualTo(3));
        Assert.That(vm.Tags.Any(t => t.Name == "temporarytag"), Is.False);
    }
}
