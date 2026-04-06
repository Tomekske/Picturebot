namespace Graph.Domain.Models;

public class FileGroup {
    public required string BaseName { get; set; }
    public DateTime PrimaryDate { get; set; }
    public List<string> FilePaths { get; set; } = [];
}
