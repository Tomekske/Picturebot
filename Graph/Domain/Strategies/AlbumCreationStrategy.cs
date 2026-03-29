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

        if (parent is not Folder parentFolder)
            throw new InvalidOperationException("Branching: Only Folder nodes can have children.");

        // Type Homogeneity: A Folder can contain either Folder[] or Album[], but never a mix of both.
        if (parentFolder.Children != null && parentFolder.Children.Any()) {
            var firstChild = parentFolder.Children.First();
            if (firstChild.Type != NodeType.Album) {
                throw new InvalidOperationException("Type Homogeneity: This folder already contains non-album nodes.");
            }
        }

        return Task.CompletedTask;
    }

    public void Prepare(Node node) {
        if (node is Album album) {
            album.Type = NodeType.Album;
            album.Uuid = Guid.CreateVersion7().ToString();
        }
    }
}
