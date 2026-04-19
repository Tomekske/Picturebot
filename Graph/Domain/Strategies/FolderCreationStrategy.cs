using Database.Domain.Entities;
using Domain.Enums;
using Graph.Domain.Interfaces;

namespace Graph.Domain.Strategies;

public class FolderCreationStrategy : ICreationStrategy {
    public Task ValidateAsync(Node node, Node? parent) {
        if (node is not Folder) {
            throw new InvalidOperationException("Node must be of type Folder.");
        }

        if (parent == null) {
            // Root level: Must be a Folder. Spec says: "Root Level: The entry point of any tree must be a Folder."
            return Task.CompletedTask;
        }

        if (parent is not Folder && parent is not Album) {
            throw new InvalidOperationException("Branching: Only Folder or Album nodes can have children.");
        }

        return Task.CompletedTask;
    }

    public void Prepare(Node node) {
        node.Type = NodeType.Folder;
    }
}
