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

        vm.SelectTagsCatalogTabCommand.Execute(null);
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
    public void Taxonomy_SelectedTaxonomyPath_VisibilityAndValue() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var root = vm.HierarchyNodes.First();

        // Initially on General tab (SelectedTabIndex = 0)
        vm.SelectedHierarchyNode = root;
        Assert.That(vm.HasSelectedTaxonomyNode, Is.False);

        // Switch to Keywords tab (Index 5), Tags subtab (Index 0)
        vm.SelectedTabIndex = 5;
        vm.KeywordsSubTabIndex = 0;
        Assert.That(vm.HasSelectedTaxonomyNode, Is.False);

        // Switch to Taxonomy subtab (Index 1)
        vm.KeywordsSubTabIndex = 1;
        Assert.That(vm.HasSelectedTaxonomyNode, Is.True);
        Assert.That(vm.SelectedTaxonomyPath, Is.EqualTo("nature"));

        // Deselect node
        vm.SelectedHierarchyNode = null;
        Assert.That(vm.HasSelectedTaxonomyNode, Is.False);
    }

    [Test]
    public void Taxonomy_AddSubNode_ViaTargetNode_ExpandsParentAndInsertsChild() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var root = vm.HierarchyNodes.First();
        root.IsExpanded = false;

        // Trigger AddSubNode directly passing the target node (e.g. from row hover button)
        vm.AddSubNode(root);

        Assert.That(root.IsExpanded, Is.True);
        var newSubNode = vm.SelectedHierarchyNode;
        Assert.That(newSubNode, Is.Not.Null);
        Assert.That(newSubNode!.IsEditing, Is.True);
        Assert.That(newSubNode.IsNewUncommitted, Is.True);
        Assert.That(root.Children.Contains(newSubNode), Is.True);

        // Commit name
        newSubNode.EditingName = " Forest ";
        newSubNode.CommitEditCommand.Execute(null);

        Assert.That(newSubNode.IsEditing, Is.False);
        Assert.That(newSubNode.IsNewUncommitted, Is.False);
        Assert.That(newSubNode.Name, Is.EqualTo("forest"));
        Assert.That(vm.Tags.Any(t => t.Name == "forest"), Is.True);
    }

    [Test]
    public void Taxonomy_AddRootNode_DirectInlineTreeEditing_CancelRemovesNode() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var initialCount = vm.HierarchyNodes.Count;

        vm.AddRootNodeCommand.Execute(null);
        var newRoot = vm.SelectedHierarchyNode;
        Assert.That(newRoot, Is.Not.Null);
        Assert.That(vm.HierarchyNodes.Count, Is.EqualTo(initialCount + 1));

        // User presses Escape
        newRoot!.CancelEditCommand.Execute(null);

        Assert.That(vm.HierarchyNodes.Count, Is.EqualTo(initialCount));
        Assert.That(vm.HierarchyNodes.Contains(newRoot), Is.False);
    }

    [Test]
    public void Taxonomy_DeleteNode_ViaTargetNode_RemovesFromParent() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var root = vm.HierarchyNodes.First();
        var child = root.Children.First();

        vm.DeleteNode(child);

        Assert.That(root.Children.Contains(child), Is.False);
        Assert.That(vm.SelectedHierarchyNode, Is.EqualTo(root));
    }

    [Test]
    public void Groups_AddGroup_PlainCustomGroup_CreatesAndSelects() {
        var vm = new SettingsDialogViewModel(_fakeService);

        vm.NewTagGroupName = " Street Photography ";
        vm.AddTagGroupCommand.Execute(null);

        var createdGroup = vm.TagGroups.FirstOrDefault(g => g.GroupName == "street photography");
        Assert.That(createdGroup, Is.Not.Null);
        Assert.That(createdGroup!.TagIds.Count, Is.EqualTo(0));
        Assert.That(vm.SelectedTagGroup, Is.EqualTo(createdGroup));
        Assert.That(string.IsNullOrEmpty(vm.NewTagGroupName), Is.True);
    }

    [Test]
    public void Groups_AddGroup_WithTaxonomyBranchSelected_PullsInAllBranchTags() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var natureRoot = vm.HierarchyNodes.First();

        // Select "nature" branch in the ComboBox
        var branchItem = vm.AvailableTaxonomyBranches.First(b => b.Node == natureRoot);
        vm.SelectedTaxonomyBranch = branchItem;

        // Verify TextBox auto-populated with branch name
        Assert.That(vm.NewTagGroupName, Is.EqualTo("nature"));

        // User clicks Add
        vm.AddTagGroupCommand.Execute(null);

        var createdGroup = vm.TagGroups.FirstOrDefault(g => g.GroupName == "nature");
        Assert.That(createdGroup, Is.Not.Null);
        // Pulled in nature, mountains, alps
        Assert.That(createdGroup!.TagIds.Count, Is.EqualTo(3));
        Assert.That(vm.SelectedTagGroup, Is.EqualTo(createdGroup));
        Assert.That(vm.SelectedTaxonomyBranch, Is.Null);
        Assert.That(string.IsNullOrEmpty(vm.NewTagGroupName), Is.True);
    }

    [Test]
    public void Groups_AddGroup_DuplicateGroupName_SelectsExistingWithoutDuplicating() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var initialCount = vm.TagGroups.Count;

        vm.NewTagGroupName = "FAVORITES";
        vm.AddTagGroupCommand.Execute(null);

        Assert.That(vm.TagGroups.Count, Is.EqualTo(initialCount));
        Assert.That(vm.SelectedTagGroup?.GroupName, Is.EqualTo("favorites"));
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
    public void Navigation_SidebarExpandableTags_AndSubItemsRouting() {
        var vm = new SettingsDialogViewModel(_fakeService);

        // Initial state: starts on General (index 0) and Tags menu collapsed
        Assert.That(vm.ActiveViewIndex, Is.EqualTo(0));
        Assert.That(vm.IsGeneralTabActive, Is.True);
        Assert.That(vm.IsTagsMenuExpanded, Is.False);

        // Click parent Tags item: expands and routes to Catalog (View 5)
        vm.SelectTagsCategoryCommand.Execute(null);
        Assert.That(vm.IsTagsMenuExpanded, Is.True);
        Assert.That(vm.ActiveViewIndex, Is.EqualTo(5));
        Assert.That(vm.IsTagsCatalogTabActive, Is.True);
        Assert.That(vm.IsTagsCategoryActive, Is.True);

        // Navigate to Taxonomy
        vm.SelectTaxonomyTabCommand.Execute(null);
        Assert.That(vm.IsTagsMenuExpanded, Is.True);
        Assert.That(vm.ActiveViewIndex, Is.EqualTo(6));
        Assert.That(vm.IsTaxonomyTabActive, Is.True);
        Assert.That(vm.IsTagsCategoryActive, Is.True);

        // Navigate to Groups
        vm.SelectGroupsTabCommand.Execute(null);
        Assert.That(vm.IsTagsMenuExpanded, Is.True);
        Assert.That(vm.ActiveViewIndex, Is.EqualTo(7));
        Assert.That(vm.IsGroupsTabActive, Is.True);
        Assert.That(vm.IsTagsCategoryActive, Is.True);

        // Navigate to another setting (e.g. Storage) -> Tags sub options must automatically close
        vm.SelectStorageTabCommand.Execute(null);
        Assert.That(vm.ActiveViewIndex, Is.EqualTo(1));
        Assert.That(vm.IsStorageTabActive, Is.True);
        Assert.That(vm.IsTagsMenuExpanded, Is.False);
        Assert.That(vm.IsTagsCategoryActive, Is.False);

        // Navigate to Culling -> remains closed
        vm.SelectCullingTabCommand.Execute(null);
        Assert.That(vm.ActiveViewIndex, Is.EqualTo(2));
        Assert.That(vm.IsCullingTabActive, Is.True);
        Assert.That(vm.IsTagsMenuExpanded, Is.False);

        // Navigate to Color Labels -> remains closed
        vm.SelectColorLabelsTabCommand.Execute(null);
        Assert.That(vm.ActiveViewIndex, Is.EqualTo(3));
        Assert.That(vm.IsColorLabelsTabActive, Is.True);
        Assert.That(vm.IsTagsMenuExpanded, Is.False);

        // Navigate back to Shortcuts (top level tab) -> remains closed
        vm.SelectShortcutsTabCommand.Execute(null);
        Assert.That(vm.ActiveViewIndex, Is.EqualTo(4));
        Assert.That(vm.IsShortcutsTabActive, Is.True);
        Assert.That(vm.IsTagsMenuExpanded, Is.False);

        // Navigate back to General -> remains closed
        vm.SelectGeneralTabCommand.Execute(null);
        Assert.That(vm.ActiveViewIndex, Is.EqualTo(0));
        Assert.That(vm.IsGeneralTabActive, Is.True);
        Assert.That(vm.IsTagsMenuExpanded, Is.False);
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

