using Microsoft.Extensions.Configuration;

namespace Database.Infrastructure.Data;

public static class DbPathProvider
{
    public static string GetConnectionString(IConfiguration configuration)
    {
        var myDocumentsLocation = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var rootFolderName = configuration["Picturebot:AppDataPath"] ?? "Picturebot-Fallback";
        var rootFolder = Path.Combine(myDocumentsLocation, rootFolderName);

        Directory.CreateDirectory(rootFolder);
        var dbPath = Path.Combine(rootFolder, "picturebot.db");

        return $"Data Source={dbPath}";
    }
}
