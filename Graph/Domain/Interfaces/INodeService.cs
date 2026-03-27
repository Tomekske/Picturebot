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
    ///     Retrieves the entire node hierarchy, fully hydrated.
    /// </summary>
    /// <returns>A Task that returns the root nodes of the hydrated tree.</returns>
    Task<List<Node>> LoadHydratedTreeAsync();
}
