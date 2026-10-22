using CommunityToolkit.Mvvm.Messaging;
using Database.Domain.Entities;
using Picturebot.Messages;
using Picturebot.Services;

namespace Tests;

/// <summary>
///     Pins the mouse Back/Forward navigation contract documented on INavigationService.
///     If one of these fails, a change broke the default navigation behaviour: fix the change, not the test.
/// </summary>
[TestFixture]
public class NavigationServiceTests {
    private NavigationService _nav = null!;
    private Folder _montreal = null!;
    private Folder _olympic = null!;
    private Album _biodome = null!;

    [SetUp]
    public void SetUp() {
        _nav = new NavigationService();
        _montreal = new Folder { Id = 1, Name = "Montreal" };
        _olympic = new Folder { Id = 2, Name = "Olympic Park", ParentId = 1, Parent = _montreal };
        _biodome = new Album { Id = 3, Name = "Biodome", ParentId = 2, Parent = _olympic };
    }

    [TearDown]
    public void TearDown() => WeakReferenceMessenger.Default.UnregisterAll(_nav);

    // The tree selects through the messenger, like clicking a node.
    private static void Select(Node? node) => WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(node));

    [Test]
    public void Back_GoesOneLevelUpToTheParent() {
        Select(_biodome);

        _nav.GoBack();
        Assert.That(_nav.CurrentNode, Is.SameAs(_olympic));
        _nav.GoBack();
        Assert.That(_nav.CurrentNode, Is.SameAs(_montreal));
        _nav.GoBack();
        Assert.That(_nav.CurrentNode, Is.Null);
        Assert.That(_nav.CanGoBack, Is.False);
    }

    [Test]
    public void Back_IgnoresWhatWasVisitedBefore_AndStillGoesToTheParent() {
        Select(_montreal);
        Select(_biodome); // jumped straight into a deep node

        _nav.GoBack();

        Assert.That(_nav.CurrentNode, Is.SameAs(_olympic));
    }

    [Test]
    public void Back_WorksForANodeWithOnlyAParentId() {
        Select(_olympic);
        var imported = new Album { Id = 9, Name = "Imported", ParentId = 2 }; // no Parent loaded
        Select(imported);

        _nav.GoBack();

        Assert.That(_nav.CurrentNode, Is.SameAs(_olympic));
    }

    [Test]
    public void Forward_GoesBackDownTheWayBackCameFrom() {
        Select(_biodome);
        _nav.GoBack();
        _nav.GoBack();
        Assert.That(_nav.CanGoForward, Is.True);

        _nav.GoForward();
        Assert.That(_nav.CurrentNode, Is.SameAs(_olympic));
        _nav.GoForward();
        Assert.That(_nav.CurrentNode, Is.SameAs(_biodome));
        Assert.That(_nav.CanGoForward, Is.False);
    }

    [Test]
    public void Forward_DoesNothingWithoutHistory() {
        Select(_montreal);

        _nav.GoForward();

        Assert.That(_nav.CurrentNode, Is.SameAs(_montreal));
        Assert.That(_nav.CanGoForward, Is.False);
    }

    [Test]
    public void PickingANodeStartsANewPath_ClearingForward() {
        Select(_biodome);
        _nav.GoBack();
        Assert.That(_nav.CanGoForward, Is.True);

        Select(_montreal);

        Assert.That(_nav.CanGoForward, Is.False);
    }

    [Test]
    public void BreadcrumbNavigation_ClearsForward() {
        Select(_biodome);
        _nav.GoBack();

        _nav.NavigateTo(_montreal);

        Assert.That(_nav.CanGoForward, Is.False);
        Assert.That(_nav.CurrentNode, Is.SameAs(_montreal));
    }

    [Test]
    public void DeletingTheCurrentNode_GoesToItsParent() {
        Select(_biodome);

        WeakReferenceMessenger.Default.Send(new NodeDeletedMessage(_biodome));

        Assert.That(_nav.CurrentNode, Is.SameAs(_olympic));
    }

    [Test]
    public void DeletingAFolder_RemovesItAndItsChildrenFromForward() {
        Select(_biodome);
        _nav.GoBack();
        _nav.GoBack(); // at Montreal, forward path: Olympic Park › Biodome

        WeakReferenceMessenger.Default.Send(new NodeDeletedMessage(_olympic));

        Assert.That(_nav.CanGoForward, Is.False);
    }
}
