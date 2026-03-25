using Domain.Interfaces;
using System.IO.Abstractions;
using ErrorOr;

namespace AppRegistry.Infrastructure;

public class FileSystemOrchestrator : IOrchestratorService
{
    private readonly IFileSystem _fileSystem;
    public required string Location { get; init; }
    public required string Name { get; init; }
    public required string Environment { get; init; }

    // The default constructor for production uses the real file system
    public FileSystemOrchestrator() : this(new FileSystem())
    {
    }

    // This constructor is used for Testing (Dependency Injection)
    public FileSystemOrchestrator(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public Task<ErrorOr<string>> ExecuteAsync(object data)
    {
        var appDirectory = Path.Combine(Location, Name, Environment, "Logs");
        try
        {
            // The service's only job is to ensure this directory exists
            if (!_fileSystem.Path.Exists(appDirectory))
            {
                _fileSystem.Directory.CreateDirectory(appDirectory);
            }

            return Task.FromResult<ErrorOr<string>>(appDirectory);
        }
        catch (Exception e)
        {
            return Task.FromResult<ErrorOr<string>>(Error.Failure(
                code: "FileSystem.Error",
                description: e.Message));
        }
    }

    public Task CompensateAsync(object data)
    {
        throw new NotImplementedException();
    }
}