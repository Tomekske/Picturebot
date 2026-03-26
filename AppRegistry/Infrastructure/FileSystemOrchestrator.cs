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

    public async Task<ErrorOr<string>> ExecuteAsync(object data)
    {
        var appDirectory = Path.Combine(Location, Name, Environment);
        var logsDirectory = Path.Combine(appDirectory, "Logs");

        try
        {
            // Ensure the main app directory exists
            if (!_fileSystem.Path.Exists(appDirectory))
            {
                _fileSystem.Directory.CreateDirectory(appDirectory);
            }

            // Ensure the Logs subdirectory exists
            if (!_fileSystem.Path.Exists(logsDirectory))
            {
                _fileSystem.Directory.CreateDirectory(logsDirectory);
            }

            return await Task.FromResult<ErrorOr<string>>(appDirectory);
        }
        catch (Exception e)
        {
            return await Task.FromResult<ErrorOr<string>>(Error.Failure(
                code: "FileSystem.Error",
                description: e.Message));
        }
    }

    public Task CompensateAsync(object data)
    {
        throw new NotImplementedException();
    }
}