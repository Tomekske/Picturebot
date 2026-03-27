namespace Tests.Database;

[TestFixture]
public class HelloWorldTest
{
    private string _testString;

    [SetUp]
    public void Setup()
    {
        // This runs BEFORE every individual [Test]
        _testString = "Hello Picturebot";
    }

    [Test]
    public void Basic_Setup_ShouldWork()
    {
        // Arrange & Act (using the string from Setup)
        var length = _testString.Length;

        // Assert
        Assert.That(length, Is.EqualTo(16), "The string length should match 'Hello Picturebot'");
    }

    [Test]
    public void Math_ShouldStillWork()
    {
        // Simple assertion to check the runner
        Assert.That(1 + 1, Is.EqualTo(2));
    }
}