using Database.Domain.Entities;
using Graph.Domain.Interfaces;
using Moq;

namespace Synchronize.Test;

[TestFixture]
public class CurationQueueTests {
    private Mock<IPickedService> _mockPickedService;
    private Mock<INodeService> _mockNodeService;
    private CurationQueue _curationQueue;

    [SetUp]
    public void Setup() {
        _mockPickedService = new Mock<IPickedService>();
        _mockNodeService = new Mock<INodeService>();
        _curationQueue = new CurationQueue(_mockPickedService.Object, _mockNodeService.Object);
    }

    [TearDown]
    public void TearDown() {
        _curationQueue.Dispose();
    }

    [Test]
    public async Task Enqueue_ShouldTriggerUpdateAndSync() {
        // Arrange
        var picture = new Picture { Name = "TestPic" };
        var tcs = new TaskCompletionSource<bool>();

        _mockPickedService.Setup(s => s.SyncToPickedAsync(picture))
            .Callback(() => tcs.SetResult(true))
            .Returns(Task.CompletedTask);

        // Act
        _curationQueue.Enqueue(picture);

        // Assert
        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.That(completedTask, Is.EqualTo(tcs.Task), "Processing timed out");
        
        _mockNodeService.Verify(s => s.UpdateNodeAsync(picture), Times.Once);
        _mockPickedService.Verify(s => s.SyncToPickedAsync(picture), Times.Once);
    }
}
