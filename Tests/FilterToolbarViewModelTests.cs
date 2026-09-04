using Database.Domain.Entities;
using Picturebot.ViewModels;

namespace Tests;

[TestFixture]
public class FilterToolbarViewModelTests {
    private List<PictureItemViewModel> CreateSamplePictures() {
        var list = new List<PictureItemViewModel>();

        // 17 pictures with faces > katsiuska
        for (var i = 1; i <= 17; i++) {
            var pic = new Picture {
                Id = i,
                Name = $"Pic_Kat_{i}.jpg",
                Keywords = new List<string> { "faces|katsiuska", "faces", "katsiuska" }
            };
            list.Add(new PictureItemViewModel(pic));
        }

        // 19 pictures with faces > robin
        for (var i = 18; i <= 36; i++) {
            var pic = new Picture {
                Id = i,
                Name = $"Pic_Rob_{i}.jpg",
                Keywords = new List<string> { "faces › robin", "faces", "robin" }
            };
            list.Add(new PictureItemViewModel(pic));
        }

        // 5 pictures with a standalone flat tag: landscape
        for (var i = 37; i <= 41; i++) {
            var pic = new Picture {
                Id = i,
                Name = $"Pic_Land_{i}.jpg",
                Keywords = new List<string> { "landscape" }
            };
            list.Add(new PictureItemViewModel(pic));
        }

        return list;
    }

    [Test]
    public void ClearAll_ResetsStatusesStarsColorsAndTags() {
        var vm = new FilterToolbarViewModel();
        vm.UpdateAvailableTags(CreateSamplePictures());

        vm.IsFlaggedActive = true;
        vm.IsStar4Active = true;
        vm.IsGreenActive = true;
        var facesNode = vm.RootNodes.First(n => n.Name == "faces");
        facesNode.IsChecked = true;

        Assert.That(vm.IsAnyFilterActive, Is.True);

        vm.ClearAllCommand.Execute(null);

        Assert.That(vm.IsFlaggedActive, Is.False);
        Assert.That(vm.IsStar4Active, Is.False);
        Assert.That(vm.IsGreenActive, Is.False);
        Assert.That(vm.IsTagFilterActive, Is.False);
        Assert.That(vm.IsAnyFilterActive, Is.False);
        Assert.That(facesNode.IsChecked, Is.EqualTo(false));
    }

    [Test]
    public void ClearTagFilters_ResetsTreeAndToolbar() {
        var vm = new FilterToolbarViewModel();
        vm.UpdateAvailableTags(CreateSamplePictures());

        var facesNode = vm.RootNodes.First(n => n.Name == "faces");
        facesNode.IsChecked = true;

        Assert.That(vm.IsTagFilterActive, Is.True);

        vm.ClearTagFiltersCommand.Execute(null);

        Assert.That(vm.IsTagFilterActive, Is.False);
        Assert.That(facesNode.IsChecked, Is.EqualTo(false));
        Assert.That(facesNode.Children.All(c => c.IsChecked == false), Is.True);
    }

    [Test]
    public void ParentSelection_TogglingParentPropagatesToAllChildren() {
        var vm = new FilterToolbarViewModel();
        vm.UpdateAvailableTags(CreateSamplePictures());

        var facesNode = vm.RootNodes.First(n => n.Name == "faces");
        var katNode = facesNode.Children.First(c => c.Name == "katsiuska");
        var robNode = facesNode.Children.First(c => c.Name == "robin");

        // User checks faces parent checkbox
        facesNode.IsChecked = true;

        Assert.That(katNode.IsChecked, Is.True);
        Assert.That(robNode.IsChecked, Is.True);
        Assert.That(vm.IsTagFilterActive, Is.True);
        Assert.That(vm.ActiveTagFiltersCountText, Is.EqualTo(" (2)"));

        // User unchecks faces parent checkbox
        facesNode.IsChecked = false;

        Assert.That(katNode.IsChecked, Is.False);
        Assert.That(robNode.IsChecked, Is.False);
        Assert.That(vm.IsTagFilterActive, Is.False);
    }

    [Test]
    public void SearchFiltering_FiltersVisibleHierarchyCorrectly() {
        var vm = new FilterToolbarViewModel();
        vm.UpdateAvailableTags(CreateSamplePictures());

        // Search for "rob"
        vm.TagSearchText = "rob";

        Assert.That(vm.VisibleRootNodes.Count, Is.EqualTo(1));
        var facesNode = vm.VisibleRootNodes[0];
        Assert.That(facesNode.Name, Is.EqualTo("faces"));
        Assert.That(facesNode.IsExpanded, Is.True);
        Assert.That(facesNode.VisibleChildren.Count, Is.EqualTo(1));
        Assert.That(facesNode.VisibleChildren[0].Name, Is.EqualTo("robin"));

        // Clear search
        vm.TagSearchText = string.Empty;
        Assert.That(vm.VisibleRootNodes.Count, Is.EqualTo(2));
        Assert.That(vm.VisibleRootNodes.First(n => n.Name == "faces").VisibleChildren.Count, Is.EqualTo(2));
    }

    [Test]
    public void TriStateSelection_CheckingChildSetsParentToIndeterminate_AndCheckingAllSetsParentToTrue() {
        var vm = new FilterToolbarViewModel();
        vm.UpdateAvailableTags(CreateSamplePictures());

        var facesNode = vm.RootNodes.First(n => n.Name == "faces");
        var katNode = facesNode.Children.First(c => c.Name == "katsiuska");
        var robNode = facesNode.Children.First(c => c.Name == "robin");

        Assert.That(facesNode.IsChecked, Is.EqualTo(false));
        Assert.That(vm.IsTagFilterActive, Is.False);

        // 1. Check katsiuska
        katNode.IsChecked = true;

        Assert.That(facesNode.IsChecked, Is.Null, "Parent must be indeterminate when some children are checked.");
        Assert.That(vm.IsTagFilterActive, Is.True);
        Assert.That(vm.ActiveTagFiltersCountText, Is.EqualTo(" (1)"));
        Assert.That(vm.GetSelectedFilterPaths(), Contains.Item("faces|katsiuska"));
        Assert.That(vm.GetSelectedFilterPaths(), Does.Not.Contain("faces|robin"));

        // 2. Check robin
        robNode.IsChecked = true;

        Assert.That(facesNode.IsChecked, Is.True, "Parent must be true when all children are checked.");
        Assert.That(vm.ActiveTagFiltersCountText, Is.EqualTo(" (2)"));
        Assert.That(vm.GetSelectedFilterPaths(), Contains.Item("faces|katsiuska"));
        Assert.That(vm.GetSelectedFilterPaths(), Contains.Item("faces|robin"));

        // 3. Uncheck katsiuska
        katNode.IsChecked = false;

        Assert.That(facesNode.IsChecked, Is.Null, "Parent must become indeterminate again.");
        Assert.That(vm.ActiveTagFiltersCountText, Is.EqualTo(" (1)"));

        // 4. Uncheck robin
        robNode.IsChecked = false;

        Assert.That(facesNode.IsChecked, Is.EqualTo(false),
            "Parent must become false when all children are unchecked.");
        Assert.That(vm.IsTagFilterActive, Is.False);
    }

    [Test]
    public void UpdateAvailableTags_BuildsHierarchicalTree_WithRootAndChildNodes() {
        var vm = new FilterToolbarViewModel();
        var pictures = CreateSamplePictures();

        vm.UpdateAvailableTags(pictures);

        Assert.That(vm.RootNodes.Count, Is.EqualTo(2));

        var facesNode = vm.RootNodes.FirstOrDefault(n => n.Name == "faces");
        Assert.That(facesNode, Is.Not.Null);
        Assert.That(facesNode!.HasChildren, Is.True);
        Assert.That(facesNode.Count, Is.EqualTo(36)); // 17 + 19 distinct pictures under faces
        Assert.That(facesNode.Children.Count, Is.EqualTo(2));

        var katNode = facesNode.Children.FirstOrDefault(c => c.Name == "katsiuska");
        Assert.That(katNode, Is.Not.Null);
        Assert.That(katNode!.Count, Is.EqualTo(17));
        Assert.That(katNode.HasChildren, Is.False);
        Assert.That(katNode.FullPath, Is.EqualTo("faces|katsiuska"));

        var robNode = facesNode.Children.FirstOrDefault(c => c.Name == "robin");
        Assert.That(robNode, Is.Not.Null);
        Assert.That(robNode!.Count, Is.EqualTo(19));
        Assert.That(robNode.HasChildren, Is.False);
        Assert.That(robNode.FullPath, Is.EqualTo("faces|robin"));

        var landNode = vm.RootNodes.FirstOrDefault(n => n.Name == "landscape");
        Assert.That(landNode, Is.Not.Null);
        Assert.That(landNode!.HasChildren, Is.False);
        Assert.That(landNode.Count, Is.EqualTo(5));
    }
}
