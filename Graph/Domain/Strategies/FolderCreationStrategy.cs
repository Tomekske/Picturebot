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

        if (parent is not Folder parentFolder) {
            throw new InvalidOperationException("Branching: Only Folder nodes can have children.");
        }

        // Type Homogeneity: A Folder can contain either Folder[] or Album[], but never a mix of both.
        if (parentFolder.Children != null && parentFolder.Children.Any()) {
            var firstChild = parentFolder.Children.First();
            if (firstChild.Type != NodeType.Folder) {
                throw new InvalidOperationException("Type Homogeneity: This folder already contains non-folder nodes.");
            }
        }

        return Task.CompletedTask;
    }

    public void Prepare(Node node) {
        node.Type = NodeType.Folder;
    }
}
