using Database.Domain.Entities;
using Domain.Enums;
using Graph.Domain.Interfaces;

namespace Graph.Domain.Strategies;

public class PictureCreationStrategy : ICreationStrategy {
    public Task ValidateAsync(Node node, Node? parent) {
        if (node is not Picture) {
            throw new InvalidOperationException("Node must be of type Picture.");
        }

        if (parent == null) {
            throw new InvalidOperationException("Picture nodes must have a parent.");
        }

        if (parent is not Album) {
            throw new InvalidOperationException("Branching: Pictures can only be children of Album nodes.");
        }

        return Task.CompletedTask;
    }

    public void Prepare(Node node) {
        node.Type = NodeType.Picture;
    }
}
