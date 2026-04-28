using Database.Domain.Entities;

namespace Graph.Domain.Interfaces;

/// <summary>
///     Primary API for CRUD operations and tree hydration within the graph.
/// </summary>
public interface INodeService {
    /// <summary>
    ///     Creates a new node within the hierarchy, applying all business rules and strategies.
    /// </summary>
    /// <param name="node">The node entity to create.</param>
    /// <returns>A Task representing the asynchronous creation operation.</returns>
    Task CreateNodeAsync(Node node);

    /// <summary>
    ///     Checks if a picture with the specified perceptual hash already exists under a specific parent.
    /// </summary>
    /// <param name="parentId">The identifier of the parent album.</param>
    /// <param name="hash">The perceptual hash to check.</param>
    /// <returns>A task that returns true if a duplicate hash is found in the album; otherwise, false.</returns>
    Task<bool> IsPictureHashDuplicateAsync(int parentId, ulong hash);

    /// <summary>
    ///     Retrieves the entire node hierarchy, fully hydrated.
    /// </summary>
    /// <returns>A Task that returns the root nodes of the hydrated tree.</returns>
    Task<List<Node>> LoadHydratedTreeAsync();

    Task<List<Node>> GetAllNodesAsync();

    /// <summary>
    ///     Updates an existing node's information within the hierarchy.
    /// </summary>
    /// <param name="node">The node entity with updated values.</param>
    /// <returns>A Task representing the asynchronous update operation.</returns>
    Task UpdateNodeAsync(Node node);
}
