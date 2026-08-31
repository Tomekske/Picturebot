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

        // Select "nature" branch in the ComboBox -> immediately imports branch
        var branchItem = vm.AvailableTaxonomyBranches.First(b => b.Node == natureRoot);
        vm.SelectedTaxonomyBranch = branchItem;

        var createdGroup = vm.TagGroups.FirstOrDefault(g => g.GroupName == "nature");
        Assert.That(createdGroup, Is.Not.Null);
        // Pulled in mountains, alps (excludes parent nature)
        Assert.That(createdGroup!.TagIds.Count, Is.EqualTo(2));
        Assert.That(vm.SelectedTagGroup, Is.EqualTo(createdGroup));
        Assert.That(vm.SelectedTaxonomyBranch, Is.Null);
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
        Assert.That(group!.TagIds.Count, Is.EqualTo(2));
        var groupTreeNode = vm.TagGroupTreeNodes.FirstOrDefault(g => g.Name == "nature");
        Assert.That(groupTreeNode, Is.Not.Null);
        Assert.That(groupTreeNode!.Children.Any(c => c.Name == "nature"), Is.False);
        Assert.That(groupTreeNode.Children.Select(c => c.Name), Is.EquivalentTo(new[] { "alps", "mountains" }));
    }

    [Test]
    public void Groups_AddInlineGroup_CommitAndCancel() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var initialCount = vm.TagGroups.Count;

        // 1. Test AddInlineGroup + Cancel (Escape)
        vm.AddInlineGroupCommand.Execute(null);
        Assert.That(vm.TagGroups.Count, Is.EqualTo(initialCount + 1));
        var tempGroup = vm.SelectedTagGroup;
        Assert.That(tempGroup, Is.Not.Null);
        Assert.That(tempGroup!.IsEditing, Is.True);

        tempGroup.CancelEditCommand.Execute(null);
        Assert.That(vm.TagGroups.Count, Is.EqualTo(initialCount));
        Assert.That(vm.TagGroups.Contains(tempGroup), Is.False);

        // 2. Test AddInlineGroup + Commit (Enter)
        vm.AddInlineGroupCommand.Execute(null);
        var newGroup = vm.SelectedTagGroup;
        Assert.That(newGroup, Is.Not.Null);
        newGroup!.EditingName = " Portraits ";
        newGroup.CommitEditCommand.Execute(null);

        Assert.That(vm.TagGroups.Count, Is.EqualTo(initialCount + 1));
        Assert.That(newGroup.GroupName, Is.EqualTo("portraits"));
        Assert.That(newGroup.IsEditing, Is.False);
        Assert.That(newGroup.TagCountBadge, Is.EqualTo("0 tags"));
        Assert.That(vm.HasGroupTags, Is.False);
    }

    [Test]
    public void Groups_InlineRename_And_InlineChipInput_AddsAndRegisters() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var group = vm.TagGroups.First();

        // 1. Test inline rename
        group.StartEditCommand.Execute(null);
        Assert.That(group.IsEditing, Is.True);
        group.EditingName = " Best Photos ";
        group.CommitEditCommand.Execute(null);
        Assert.That(group.IsEditing, Is.False);
        Assert.That(group.GroupName, Is.EqualTo("best photos"));
        Assert.That(vm.SelectedGroupName, Is.EqualTo("best photos"));

        // 2. Test inline tag chip entry
        vm.SelectedTagGroup = group;
        vm.NewGroupTagInput = " Urban ";
        vm.AddTagToSelectedGroupCommand.Execute(null);

        // Tag added to group
        Assert.That(vm.SelectedGroupTags.Any(t => t.Name == "urban"), Is.True);
        Assert.That(vm.HasGroupTags, Is.True);
        Assert.That(group.TagCountBadge, Is.EqualTo("1 tag"));
        // Automatically registered in global catalog
        Assert.That(vm.Tags.Any(t => t.Name == "urban"), Is.True);
        Assert.That(string.IsNullOrEmpty(vm.NewGroupTagInput), Is.True);

        // 3. Remove tag
        var urbanTag = vm.SelectedGroupTags.First(t => t.Name == "urban");
        vm.RemoveTagFromGroupCommand.Execute(urbanTag);
        Assert.That(vm.SelectedGroupTags.Any(t => t.Name == "urban"), Is.False);
        Assert.That(group.TagCountBadge, Is.EqualTo("0 tags"));
        Assert.That(vm.HasGroupTags, Is.False);
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

    [Test]
    public void Groups_SelectedGroupTagCountBadge_UpdatesCorrectly() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var group = vm.TagGroups.First();
        vm.SelectedTagGroup = group;

        Assert.That(vm.SelectedGroupTagCountBadge, Is.EqualTo("0 tags"));

        vm.NewGroupTagInput = "wildlife";
        vm.AddTagToSelectedGroupCommand.Execute(null);

        Assert.That(vm.SelectedGroupTagCountBadge, Is.EqualTo("1 tag"));

        vm.NewGroupTagInput = "macro";
        vm.AddTagToSelectedGroupCommand.Execute(null);

        Assert.That(vm.SelectedGroupTagCountBadge, Is.EqualTo("2 tags"));

        var tagToRemove = vm.SelectedGroupTags.First(t => t.Name == "wildlife");
        vm.RemoveTagFromGroupCommand.Execute(tagToRemove);

        Assert.That(vm.SelectedGroupTagCountBadge, Is.EqualTo("1 tag"));
    }

    [Test]
    public void Groups_IsAddingGroupTag_StartAndCancel() {
        var vm = new SettingsDialogViewModel(_fakeService);
        Assert.That(vm.IsAddingGroupTag, Is.False);

        vm.StartAddGroupTagCommand.Execute(null);
        Assert.That(vm.IsAddingGroupTag, Is.True);
        Assert.That(string.IsNullOrEmpty(vm.NewGroupTagInput), Is.True);

        vm.NewGroupTagInput = "test";
        vm.CancelAddGroupTagCommand.Execute(null);
        Assert.That(vm.IsAddingGroupTag, Is.False);
        Assert.That(string.IsNullOrEmpty(vm.NewGroupTagInput), Is.True);
    }

    [Test]
    public void Groups_KeyboardCommands_StartEditAndDeleteSelectedGroup() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var group = vm.TagGroups.First();
        vm.SelectedTagGroup = group;

        // F2 StartEdit
        vm.StartEditSelectedGroupCommand.Execute(null);
        Assert.That(group.IsEditing, Is.True);

        group.CancelEditCommand.Execute(null);
        Assert.That(group.IsEditing, Is.False);

        // Delete key
        var countBefore = vm.TagGroups.Count;
        vm.DeleteSelectedGroupCommand.Execute(null);
        Assert.That(vm.TagGroups.Count, Is.EqualTo(countBefore - 1));
    }

    [Test]
    public void Groups_HasTaxonomyBranches_PropertyReflectsTree() {
        var vm = new SettingsDialogViewModel(_fakeService);
        Assert.That(vm.HasTaxonomyBranches, Is.True);
        Assert.That(vm.AvailableTaxonomyBranches.Count, Is.GreaterThan(0));
    }

    [Test]
    public void Groups_TreeStructure_GroupAndTagNodes_EnforcesStrict2LevelHierarchy() {
        var vm = new SettingsDialogViewModel(_fakeService);
        Assert.That(vm.TagGroupTreeNodes.Count, Is.GreaterThan(0));

        var groupNode = vm.TagGroupTreeNodes.First();
        Assert.That(groupNode.IsGroup, Is.True);
        Assert.That(groupNode.IsTag, Is.False);

        // Add a child tag under this group
        vm.SelectedGroupTreeNode = groupNode;
        vm.AddTagToGroupNodeCommand.Execute(groupNode);

        var childTagNode = groupNode.Children.LastOrDefault();
        Assert.That(childTagNode, Is.Not.Null);
        Assert.That(childTagNode!.IsTag, Is.True);
        Assert.That(childTagNode.IsGroup, Is.False);
        Assert.That(childTagNode.ParentGroup, Is.EqualTo(groupNode));

        // Strict flat constraint: child tags have NO children
        Assert.That(childTagNode.Children.Count, Is.EqualTo(0));

        // Commit tag name
        childTagNode.EditingName = "portrait";
        childTagNode.CommitEditCommand.Execute(null);

        Assert.That(childTagNode.Name, Is.EqualTo("portrait"));
        Assert.That(childTagNode.IsEditing, Is.False);
        Assert.That(childTagNode.Children.Count, Is.EqualTo(0));
    }

    [Test]
    public void Groups_BreadcrumbPath_DisplaysCorrectGroupAndTagPaths() {
        var vm = new SettingsDialogViewModel(_fakeService);
        vm.SelectGroupsTabCommand.Execute(null);
        Assert.That(vm.IsGroupsTabActive, Is.True);

        var groupNode = vm.TagGroupTreeNodes.First();
        vm.SelectedGroupTreeNode = groupNode;

        Assert.That(vm.HasSelectedBreadcrumb, Is.True);
        Assert.That(vm.SelectedBreadcrumbLabel, Is.EqualTo("Path:"));
        Assert.That(vm.SelectedBreadcrumbPath, Is.EqualTo(groupNode.Name));

        // Add child tag and select it
        vm.AddTagToGroupNodeCommand.Execute(groupNode);
        var childTag = groupNode.Children.Last();
        childTag.EditingName = "testtag";
        childTag.CommitEditCommand.Execute(null);
        vm.SelectedGroupTreeNode = childTag;

        Assert.That(vm.HasSelectedBreadcrumb, Is.True);
        Assert.That(vm.SelectedBreadcrumbLabel, Is.EqualTo("Path:"));
        Assert.That(vm.SelectedBreadcrumbPath, Is.EqualTo($"{groupNode.Name} > testtag"));
    }

    [Test]
    public void Groups_DeleteGroupTreeNode_RemovesGroupOrTag() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var groupNode = vm.TagGroupTreeNodes.First();
        var initialGroupCount = vm.TagGroupTreeNodes.Count;

        // Add tag under group
        vm.AddTagToGroupNodeCommand.Execute(groupNode);
        var childTag = groupNode.Children.Last();
        childTag.EditingName = "removabletag";
        childTag.CommitEditCommand.Execute(null);

        var childCountBefore = groupNode.Children.Count;
        Assert.That(childCountBefore, Is.GreaterThan(0));

        // Delete tag node
        vm.DeleteGroupTreeNodeCommand.Execute(childTag);
        Assert.That(groupNode.Children.Count, Is.EqualTo(childCountBefore - 1));
        Assert.That(vm.TagGroupTreeNodes.Count, Is.EqualTo(initialGroupCount));

        // Delete group node
        vm.DeleteGroupTreeNodeCommand.Execute(groupNode);
        Assert.That(vm.TagGroupTreeNodes.Count, Is.EqualTo(initialGroupCount - 1));
    }

    [Test]
    public void Groups_AddChildTag_ExpandsGroupAndAddsTag() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var groupNode = vm.TagGroupTreeNodes.First();
        groupNode.IsExpanded = false;

        groupNode.AddChildTagCommand.Execute(null);

        Assert.That(groupNode.IsExpanded, Is.True);
        Assert.That(groupNode.Children.Count, Is.GreaterThan(0));

        var newChild = groupNode.Children.Last();
        Assert.That(newChild.IsNewNode, Is.True);
        Assert.That(newChild.IsEditing, Is.True);

        newChild.EditingName = "forest";
        newChild.CommitEditCommand.Execute(null);

        Assert.That(newChild.IsEditing, Is.False);
        Assert.That(newChild.Name, Is.EqualTo("forest"));
        Assert.That(groupNode.Children.Any(c => c.Name == "forest"), Is.True);
    }

    [Test]
    public void Groups_AddChildTag_RejectsDuplicateTagsInSameGroup() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var groupNode = vm.TagGroupTreeNodes.First();

        vm.AddTagToGroupNodeCommand.Execute(groupNode);
        var tag1 = groupNode.Children.Last();
        tag1.EditingName = "duplicate_test";
        tag1.CommitEditCommand.Execute(null);

        var countBefore = groupNode.Children.Count;

        // Try adding duplicate tag
        vm.AddTagToGroupNodeCommand.Execute(groupNode);
        var tag2 = groupNode.Children.Last();
        tag2.EditingName = "DUPLICATE_TEST";
        tag2.CommitEditCommand.Execute(null);

        // Count should not increase
        Assert.That(groupNode.Children.Count, Is.EqualTo(countBefore));
        Assert.That(groupNode.Children.Count(c => c.Name.Equals("duplicate_test", StringComparison.OrdinalIgnoreCase)), Is.EqualTo(1));
    }

    [Test]
    public void Groups_ImportTaxonomyBranch_CreatesGroupAndImportsTags() {
        var vm = new SettingsDialogViewModel(_fakeService);
        var initialGroupCount = vm.TagGroupTreeNodes.Count;

        Assert.That(vm.HasTaxonomyBranches, Is.True);
        var branch = vm.AvailableTaxonomyBranches.First(b => b.Node.Name.Equals("Nature", StringComparison.OrdinalIgnoreCase));

        vm.ImportTaxonomyBranchCommand.Execute(branch);

        Assert.That(vm.TagGroupTreeNodes.Count, Is.EqualTo(initialGroupCount + 1));
        var importedGroup = vm.TagGroupTreeNodes.FirstOrDefault(g => g.Name.Equals("nature", StringComparison.OrdinalIgnoreCase));
        Assert.That(importedGroup, Is.Not.Null);
        Assert.That(importedGroup!.IsExpanded, Is.True);
        Assert.That(importedGroup.Children.Count, Is.EqualTo(2));
        Assert.That(importedGroup.Children.Any(c => c.Name == "nature"), Is.False);
        Assert.That(importedGroup.Children.Select(c => c.Name), Is.EqualTo(new[] { "alps", "mountains" }));
        Assert.That(vm.SelectedGroupTreeNode, Is.EqualTo(importedGroup));
        Assert.That(vm.SelectedBreadcrumbPath, Is.EqualTo("nature"));
    }

    [Test]
    public void Groups_ImportTaxonomyBranch_Faces_ExcludesParentAndSortsAlphabetically() {
        var settingsService = new FakeSettingsService {
            Current = new SettingsModel {
                MasterTags = new List<Tag>(),
                HierarchyNodes = new List<HierarchyNode> {
                    new() {
                        NodeId = Guid.NewGuid(),
                        Name = "faces",
                        Children = new ObservableCollection<HierarchyNode> {
                            new() { NodeId = Guid.NewGuid(), Name = "robin" },
                            new() { NodeId = Guid.NewGuid(), Name = "annie" },
                            new() { NodeId = Guid.NewGuid(), Name = "katsiuska" }
                        }
                    }
                }
            }
        };

        var vm = new SettingsDialogViewModel(settingsService);
        var branch = vm.AvailableTaxonomyBranches.First(b => b.Node.Name.Equals("faces", StringComparison.OrdinalIgnoreCase));

        vm.ImportTaxonomyBranchCommand.Execute(branch);

        var importedGroup = vm.TagGroupTreeNodes.FirstOrDefault(g => g.Name == "faces");
        Assert.That(importedGroup, Is.Not.Null);
        Assert.That(importedGroup!.IsExpanded, Is.True);
        Assert.That(importedGroup.Children.Count, Is.EqualTo(3));
        Assert.That(importedGroup.Children.Any(c => c.Name == "faces"), Is.False);

        var childNames = importedGroup.Children.Select(c => c.Name).ToList();
        Assert.That(childNames, Is.EqualTo(new[] { "annie", "katsiuska", "robin" }));
        Assert.That(vm.SelectedGroupTreeNode, Is.EqualTo(importedGroup));
        Assert.That(vm.SelectedBreadcrumbPath, Is.EqualTo("faces"));
    }
}



