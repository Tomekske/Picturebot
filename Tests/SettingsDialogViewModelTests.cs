using System;
using System.Collections.Generic;
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
                    new() { Id = Guid.NewGuid(), Name = "Portrait" }
                },
                HierarchyNodes = new List<HierarchyNode> {
                    new() {
                        NodeId = Guid.NewGuid(),
                        Name = "Nature",
                        Children = new System.Collections.ObjectModel.ObservableCollection<HierarchyNode> {
                            new() { NodeId = Guid.NewGuid(), Name = "Mountains" }
                        }
                    }
                },
                TagGroups = new List<TagGroup> {
                    new() { GroupId = Guid.NewGuid(), GroupName = "Favorites" }
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

        vm.SelectTagGroupsSubTabCommand.Execute(null);
        Assert.That(vm.KeywordsSubTabIndex, Is.EqualTo(2));
        Assert.That(vm.IsTagGroupsSubTabActive, Is.True);

        vm.SelectTagsSubTabCommand.Execute(null);
        Assert.That(vm.KeywordsSubTabIndex, Is.EqualTo(0));
        Assert.That(vm.IsTagsSubTabActive, Is.True);
    }

    [Test]
    public void Tags_AddTag_AddsUniqueAndIgnoresDuplicates() {
        var vm = new SettingsDialogViewModel(_fakeService);
        Assert.That(vm.Tags.Count, Is.EqualTo(2));

        vm.NewTagName = "Wildlife";
        vm.AddTagCommand.Execute(null);
        Assert.That(vm.Tags.Count, Is.EqualTo(3));
        Assert.That(vm.Tags.Any(t => t.Name == "Wildlife"), Is.True);
        Assert.That(string.IsNullOrEmpty(vm.NewTagName), Is.True);

        // Attempt to add duplicate (case-insensitive)
        vm.NewTagName = "wildlife";
        vm.AddTagCommand.Execute(null);
        Assert.That(vm.Tags.Count, Is.EqualTo(3));
    }

    [Test]
    public void Tags_InlineRename_UpdatesTagAndLinkedHierarchy() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var tagItem = vm.Tags.First(t => t.Name == "Landscape");

        // Link a hierarchy node to this tag
        var natureNode = vm.HierarchyNodes.First();
        var linkedNode = new HierarchyNodeViewModel("Landscape", tagItem.Id, natureNode);
        natureNode.Children.Add(linkedNode);

        tagItem.StartEditCommand.Execute(null);
        Assert.That(tagItem.IsEditing, Is.True);
        tagItem.EditingName = "Scenic Landscape";
        tagItem.CommitEditCommand.Execute(null);

        Assert.That(tagItem.IsEditing, Is.False);
        Assert.That(tagItem.Name, Is.EqualTo("Scenic Landscape"));
        Assert.That(linkedNode.Name, Is.EqualTo("Scenic Landscape"));
    }

    [Test]
    public void Tags_DeleteUnreferenced_DeletesImmediatelyWithoutPrompt() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var tagItem = vm.Tags.First(t => t.Name == "Portrait");

        tagItem.DeleteCommand.Execute(null);

        Assert.That(vm.IsDeleteTagConfirmOpen, Is.False);
        Assert.That(vm.Tags.Any(t => t.Name == "Portrait"), Is.False);
    }

    [Test]
    public void Tags_DeleteReferencedInGroup_ShowsConfirmationAndUnlinksOnConfirm() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var tagItem = vm.Tags.First(t => t.Name == "Landscape");
        var group = vm.TagGroups.First();
        group.TagIds.Add(tagItem.Id);

        tagItem.DeleteCommand.Execute(null);

        Assert.That(vm.IsDeleteTagConfirmOpen, Is.True);
        Assert.That(vm.DeleteTagConfirmMessage, Does.Contain("1 tag group(s)"));

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
    public void Taxonomy_CalculatedBreadcrumbPath_UpdatesOnSelection() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var root = vm.HierarchyNodes.First();
        var child = root.Children.First();

        vm.SelectedHierarchyNode = root;
        Assert.That(vm.CalculatedBreadcrumbPath, Is.EqualTo("Nature"));
        Assert.That(vm.CalculatedXmpPath, Is.EqualTo("Nature"));

        vm.SelectedHierarchyNode = child;
        Assert.That(vm.CalculatedBreadcrumbPath, Is.EqualTo("Nature › Mountains"));
        Assert.That(vm.CalculatedXmpPath, Is.EqualTo("Nature|Mountains"));
    }

    [Test]
    public void Taxonomy_AddSubNode_CreatesChildAndRegistersInTagPool() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var root = vm.HierarchyNodes.First();
        vm.SelectedHierarchyNode = root;

        vm.OpenAddChildNodePromptCommand.Execute(null);
        Assert.That(vm.IsNodeActionPromptOpen, Is.True);

        vm.NodeActionInputName = "Forest";
        vm.ConfirmNodeActionCommand.Execute(null);

        Assert.That(vm.IsNodeActionPromptOpen, Is.False);
        Assert.That(root.Children.Any(c => c.Name == "Forest"), Is.True);
        Assert.That(vm.Tags.Any(t => t.Name == "Forest"), Is.True);
    }

    [Test]
    public void TagGroups_AddAndRemoveTags_UpdatesChips() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var group = vm.TagGroups.First();
        vm.SelectedTagGroup = group;

        vm.NewGroupTagInput = "Landscape";
        vm.AddTagToSelectedGroupCommand.Execute(null);

        Assert.That(vm.SelectedGroupTags.Any(t => t.Name == "Landscape"), Is.True);
        Assert.That(group.TagIds.Count, Is.EqualTo(1));

        // Remove tag
        var tagToRemove = vm.SelectedGroupTags.First();
        vm.RemoveTagFromGroupCommand.Execute(tagToRemove);

        Assert.That(vm.SelectedGroupTags.Count, Is.EqualTo(0));
        Assert.That(group.TagIds.Count, Is.EqualTo(0));
    }

    [Test]
    public void DiscardChanges_RevertsModifications() {
        var vm = new SettingsDialogViewModel(_fakeService);
        vm.NewTagName = "TemporaryTag";
        vm.AddTagCommand.Execute(null);
        Assert.That(vm.Tags.Count, Is.EqualTo(3));

        vm.DiscardChangesCommand.Execute(null);
        Assert.That(vm.Tags.Count, Is.EqualTo(2));
        Assert.That(vm.Tags.Any(t => t.Name == "TemporaryTag"), Is.False);
    }
}
