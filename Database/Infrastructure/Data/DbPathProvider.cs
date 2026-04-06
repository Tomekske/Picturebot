using Microsoft.Extensions.Configuration;

namespace Database.Infrastructure.Data;

public static class DbPathProvider {
    public static string GetConnectionString(IConfiguration configuration, string? environment = null) {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var rootFolderName = configuration["Picturebot:AppDataPath"] ?? "Picturebot";
        
        // Match the logic in Program.cs and FileSystemOrchestrator
        var rootFolder = Path.Combine(localAppData, rootFolderName, environment ?? "Production");

        Directory.CreateDirectory(rootFolder);
        var dbPath = Path.Combine(rootFolder, "picturebot.db");

        return $"Data Source={dbPath}";
    }
}
