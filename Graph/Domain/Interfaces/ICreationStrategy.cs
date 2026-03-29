using Database.Domain.Entities;

namespace Graph.Domain.Interfaces;

/// <summary>
///     Encapsulates validation and preparation logic for specific node types.
/// </summary>
public interface ICreationStrategy {
    /// <summary>
    ///     Validates the proposed node against business rules before creation.
    /// </summary>
    /// <param name="node">The node to validate.</param>
    /// <param name="parent">The proposed parent node, or null if root.</param>
    /// <returns>A Task representing the asynchronous validation operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if validation fails.</exception>
    Task ValidateAsync(Node node, Node? parent);

    /// <summary>
    ///     Performs any necessary preparation or modification on the node before it is persisted.
    /// </summary>
    /// <param name="node">The node to prepare.</param>
    void Prepare(Node node);
}
