using Database.Domain.Entities;
using Domain.Enums;

namespace Database.Domain.Interfaces;

/// <summary>
///     Defines the data access contract for managing nodes in the hierarchy.
/// </summary>
public interface INodeRepository {
    /// <summary>
    ///     Persists a new node to the data store.
    /// </summary>
    /// <param name="node">The node entity to create.</param>
    /// <returns>A task that represents the asynchronous creation operation.</returns>
    Task CreateAsync(Node node);

    /// <summary>
    ///     Retrieves all nodes from the data store.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation, containing the list of all nodes.</returns>
    Task<List<Node>> FindAllAsync();

    /// <summary>
    ///     Locates a specific node by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the node.</param>
    /// <returns>A task that represents the asynchronous operation, returning the node if found; otherwise, null.</returns>
    Task<Node?> FindByIdAsync(int id);

    /// <summary>
    ///     Checks if a node with the same name and type already exists under a specific parent.
    /// </summary>
    /// <param name="parentId">The identifier of the parent node, or null for root-level nodes.</param>
    /// <param name="name">The name to check for duplicates.</param>
    /// <param name="type">The type of the node.</param>
    /// <returns>A task that returns true if a duplicate exists; otherwise, false.</returns>
    Task<bool> FindDuplicateAsync(int? parentId, string name, NodeType type);

    /// <summary>
    ///     Checks if a picture with the specified perceptual hash already exists under a specific parent.
    /// </summary>
    /// <param name="parentId">The identifier of the parent album.</param>
    /// <param name="hash">The perceptual hash to check.</param>
    /// <returns>A task that returns true if a picture with the same hash exists in the album; otherwise, false.</returns>
    Task<bool> IsPictureHashDuplicateAsync(int parentId, ulong hash);

    /// <summary>
    ///     Retrieves all nodes of a specific type.
    /// </summary>
    /// <param name="type">The node type to filter by.</param>
    /// <returns>A task that represents the asynchronous operation, containing the list of matching nodes.</returns>
    Task<List<Node>> FindNodesByTypeAsync(NodeType type);

    /// <summary>
    ///     Retrieves the immediate children of a specified parent node.
    /// </summary>
    /// <param name="parentId">The identifier of the parent node.</param>
    /// <returns>A task that represents the asynchronous operation, containing the list of child nodes.</returns>
    Task<List<Node>> FindChildrenAsync(int parentId);

    /// <summary>
    ///     Updates an existing node's information in the data store.
    /// </summary>
    /// <param name="node">The node entity with updated values.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    Task UpdateAsync(Node node);

    /// <summary>
    ///     Removes a node and its associated data from the data store.
    /// </summary>
    /// <param name="node">The node entity to delete.</param>
    /// <returns>A task that represents the asynchronous deletion operation.</returns>
    Task DeleteAsync(Node node);
}
