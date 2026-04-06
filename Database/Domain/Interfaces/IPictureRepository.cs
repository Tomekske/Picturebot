using Database.Domain.Entities;

namespace Database.Domain.Interfaces;

/// <summary>
///     Defines the data access contract for managing picture entities.
/// </summary>
public interface IPictureRepository : INodeRepository {
    /// <summary>
    ///     Retrieves all pictures belonging to a specific hierarchy (e.g., within a folder or album).
    /// </summary>
    /// <param name="hierarchyId">The unique identifier of the parent node.</param>
    /// <returns>A task that represents the asynchronous operation, containing the list of pictures.</returns>
    Task<List<Picture>> FindByHierarchyIdAsync(int hierarchyId);
}
