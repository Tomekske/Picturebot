using Database.Domain.Entities;
using Domain.Enums;
using Graph.Domain.Interfaces;

namespace Graph.Domain.Strategies;

public class AlbumCreationStrategy : ICreationStrategy {
    public Task ValidateAsync(Node node, Node? parent) {
        if (node is not Album)
            throw new InvalidOperationException("Node must be of type Album.");

        if (parent == null) {
            throw new InvalidOperationException("Root Level: The entry point of any tree must be a Folder.");
        }

        if (parent is not Folder && parent is not Album)
            throw new InvalidOperationException("Branching: Only Folder or Album nodes can have children.");

        return Task.CompletedTask;
    }

    public void Prepare(Node node) {
        if (node is Album album) {
            album.Type = NodeType.Album;
            album.Uuid = Guid.CreateVersion7().ToString();
        }
    }
}
