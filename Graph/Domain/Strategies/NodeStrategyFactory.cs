using Domain.Enums;
using Graph.Domain.Interfaces;

namespace Graph.Domain.Strategies;

public class NodeStrategyFactory(
    FolderCreationStrategy folderStrategy,
    AlbumCreationStrategy albumStrategy,
    PictureCreationStrategy pictureStrategy) {
    public ICreationStrategy GetStrategy(NodeType type) {
        return type switch {
            NodeType.Folder => folderStrategy,
            NodeType.Album => albumStrategy,
            NodeType.Picture => pictureStrategy,
            _ => throw new NotSupportedException($"NodeType {type} is not supported for creation.")
        };
    }
}
