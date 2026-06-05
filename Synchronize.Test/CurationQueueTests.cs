using Database.Domain.Entities;
using Graph.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Synchronize.Test;

[TestFixture]
public class CurationQueueTests {
    private Mock<IPickedService> _mockPickedService;
    private Mock<INodeService> _mockNodeService;
    private Mock<IServiceScopeFactory> _mockScopeFactory;
    private Mock<IServiceScope> _mockScope;
    private Mock<IServiceProvider> _mockServiceProvider;
    private CurationQueue _curationQueue;

    [SetUp]
    public void Setup() {
        _mockPickedService = new Mock<IPickedService>();
        _mockNodeService = new Mock<INodeService>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        _mockScopeFactory.Setup(s => s.CreateScope()).Returns(_mockScope.Object);
        _mockScope.Setup(s => s.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceProvider.Setup(s => s.GetService(typeof(INodeService))).Returns(_mockNodeService.Object);
        _mockServiceProvider.Setup(s => s.GetService(typeof(IPickedService))).Returns(_mockPickedService.Object);

        _curationQueue = new CurationQueue(_mockScopeFactory.Object);
    }

    [TearDown]
    public void TearDown() {
        _curationQueue.Dispose();
    }

    [Test]
    public async Task Enqueue_ShouldTriggerUpdateAndSync() {
        // Arrange
        await _curationQueue.StartAsync(CancellationToken.None);
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
